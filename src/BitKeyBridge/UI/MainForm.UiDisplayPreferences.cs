using System.Drawing;

namespace BitKeyBridge;

/// <summary>
/// Per-user appearance only. No AD/Graph request, privilege transition,
/// password/recovery access or protected audit mutation occurs here.
/// </summary>
public sealed partial class MainForm
{
    private readonly UiFontZoomService _fontZoomService = new();
    private string _uiDensity = "Auto";
    private int _uiScalePercent = 100;
    private ToolStripMenuItem? _uiDisplayMenu;
    private ToolStripMenuItem? _uiDensityMenu;
    private ToolStripMenuItem? _uiScaleMenu;
    private readonly Dictionary<string, ToolStripMenuItem> _densityMenuChoices =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, ToolStripMenuItem> _scaleMenuChoices = [];

    private ToolStripMenuItem BuildUiDisplayMenu()
    {
        var menu = new ToolStripMenuItem("View")
        {
            Name = "UiDisplayMenu",
            AutoSize = true,
            Padding = new Padding(10, 0, 10, 0)
        };
        _uiDisplayMenu = menu;
        var density = new ToolStripMenuItem("UI Density")
        {
            Name = "UiDensityMenu",
            ToolTipText = "Auto adapts to window height; Compact and Comfortable override spacing."
        };
        _uiDensityMenu = density;
        foreach (var value in UiDisplayPreferencePolicy.AvailableDensities)
        {
            var choice = new ToolStripMenuItem(value)
            {
                Name = "UiDensity-" + value,
                Tag = value,
                CheckOnClick = false
            };
            choice.Click += (_, _) =>
                SetUiDisplayPreferences(value, _uiScalePercent, persist: true);
            density.DropDownItems.Add(choice);
            _densityMenuChoices.Add(value, choice);
        }

        var scale = new ToolStripMenuItem("UI Scale")
        {
            Name = "UiScaleMenu",
            ToolTipText = "Zoom interface fonts without changing Windows display DPI."
        };
        _uiScaleMenu = scale;
        foreach (var value in UiDisplayPreferencePolicy.AvailableScales)
        {
            var choice = new ToolStripMenuItem(value + "%")
            {
                Name = "UiScale-" + value,
                Tag = value,
                CheckOnClick = false
            };
            choice.Click += (_, _) =>
                SetUiDisplayPreferences(_uiDensity, value, persist: true);
            scale.DropDownItems.Add(choice);
            _scaleMenuChoices.Add(value, choice);
        }

        menu.DropDownItems.Add(density);
        menu.DropDownItems.Add(scale);
        RefreshUiDisplayMenuLanguage();
        return menu;
    }

    private static string DisplayCaption(string language, string english)
    {
        return (UiLanguageService.Normalize(language), english) switch
        {
            ("ru", "View") => "Вид",
            ("ru", "UI Density") => "Плотность интерфейса",
            ("ru", "UI Scale") => "Масштаб интерфейса",
            ("ru", "Auto") => "Автоматически",
            ("ru", "Compact") => "Компактно",
            ("ru", "Comfortable") => "Просторно",
            ("he", "View") => "תצוגה",
            ("he", "UI Density") => "צפיפות ממשק",
            ("he", "UI Scale") => "קנה מידה של הממשק",
            ("he", "Auto") => "אוטומטי",
            ("he", "Compact") => "צפוף",
            ("he", "Comfortable") => "מרווח",
            _ => english
        };
    }

    private void RefreshUiDisplayMenuLanguage()
    {
        if (_uiDisplayMenu is null)
            return;
        static void SetText(ToolStripItem? item, string text)
        {
            if (item is not null && !string.Equals(item.Text, text,
                    StringComparison.Ordinal))
                item.Text = text;
        }

        SetText(_uiDisplayMenu, DisplayCaption(_uiLanguageCode, "View"));
        SetText(_uiDensityMenu, DisplayCaption(_uiLanguageCode, "UI Density"));
        SetText(_uiScaleMenu, DisplayCaption(_uiLanguageCode, "UI Scale"));
        foreach (var (value, item) in _densityMenuChoices)
        {
            SetText(item, DisplayCaption(_uiLanguageCode, value));
            var selected = _uiDensity == value;
            if (item.Checked != selected)
                item.Checked = selected;
        }
        foreach (var (value, item) in _scaleMenuChoices)
        {
            var selected = _uiScalePercent == value;
            if (item.Checked != selected)
                item.Checked = selected;
        }
    }

    private void ApplyUiDisplayPreferences(
        UiWorkspacePreferences preferences, bool persist)
    {
        SetUiDisplayPreferences(preferences.UiDensity,
            preferences.UiScalePercent, persist);
    }

    private void SetUiDisplayPreferences(
        string density, int scalePercent, bool persist)
    {
        var normalizedDensity = UiDisplayPreferencePolicy.NormalizeDensity(
            density);
        var normalizedScale = UiDisplayPreferencePolicy.NormalizeScale(
            scalePercent);
        var densityChanged = _uiDensity != normalizedDensity;
        var scaleChanged = _uiScalePercent != normalizedScale;

        _uiDensity = normalizedDensity;
        _uiScalePercent = normalizedScale;
        _workspacePreferences.UiDensity = normalizedDensity;
        _workspacePreferences.UiScalePercent = normalizedScale;

        if (scaleChanged)
        {
            // Immutable baseline fonts make the 100 -> 125 -> 90 -> 100
            // round-trip exact, independently of 100–200% Windows DPI.
            _mainTabs.SuspendLayout();
            try
            {
                _fontZoomService.Apply(this, normalizedScale,
                    _homeConnectionToolsMenu.Items.Cast<ToolStripItem>());
            }
            finally
            {
                _mainTabs.ResumeLayout(true);
            }
        }

        if (densityChanged || scaleChanged)
        {
            // Previous height-density cache and card breakpoints are stale.
            _startHeightDensity = -1;
            ReflowMainViewport();
            QueueMainViewportReflow();
        }
        RefreshUiDisplayMenuLanguage();

        if (persist && !_layoutSelfTest)
        {
            try
            {
                UiWorkspacePreferenceService.Save(_workspacePreferences);
            }
            catch (Exception ex) when (ex is IOException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
            {
                ShowAppMessage("UI settings apply for this session only.",
                    "Unable to save per-user appearance preferences (" +
                    DiagnosticRedaction.Sanitize(ex.GetType().Name) + ").");
            }
        }
    }

    internal void PreviewSetUiDisplayPreferencesForUiSelfTest(
        string density, int scalePercent)
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI tests only.");
        SetUiDisplayPreferences(density, scalePercent, persist: false);
    }

    internal (string Density, int Scale, int StartTier)
        PreviewUiDisplayStateForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI tests only.");
        return (_uiDensity, _uiScalePercent, _startHeightDensity);
    }

    private void DisposeUiFontZoom() => _fontZoomService.Dispose();
}
