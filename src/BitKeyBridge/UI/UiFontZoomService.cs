using System.Drawing;

namespace BitKeyBridge;

/// <summary>
/// Bounded visual font zoom, independent of Windows PerMonitorV2 DPI.
/// Preserves immutable font baselines so toggling 100/125/90/100 is exact,
/// rather than multiplying already-zoomed values or resizing fixed bounds.
/// No network, identity, recovery or audit data is read by this service.
/// </summary>
internal sealed class UiFontZoomService : IDisposable
{
    private readonly Dictionary<Control, Font> _baseFonts = new(
        ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ToolStripItem, Font> _baseItems = new(
        ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DataGridView, (Font? Body, Font? Header)>
        _gridBase = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Family, float Size, FontStyle Style,
        int Percent), Font> _scaledFonts = [];
    private int _lastPercent = 100;

    internal void Apply(Control root, int percent,
        IEnumerable<ToolStripItem> extraItems)
    {
        percent = UiDisplayPreferencePolicy.NormalizeScale(percent);
        if (percent == _lastPercent || root.IsDisposed)
            return;

        var controls = ControlsIn(root).ToList();

        // Snapshot every original font before mutating inherited parents.
        foreach (var control in controls)
        {
            if (!_baseFonts.ContainsKey(control))
                _baseFonts[control] = OriginalForNewControl(control);
            if (control is DataGridView grid && !_gridBase.ContainsKey(grid))
                _gridBase[grid] = (grid.DefaultCellStyle.Font,
                    grid.ColumnHeadersDefaultCellStyle.Font);
        }

        var items = controls.OfType<ToolStrip>()
            .SelectMany(strip => MenuItems(strip.Items.Cast<ToolStripItem>()))
            .Concat(MenuItems(extraItems)).Distinct().ToList();
        foreach (var item in items)
            if (!_baseItems.ContainsKey(item))
                _baseItems[item] = item.Font;

        // Descendants first: inherited font changes on parents otherwise
        // contaminate the original sizes of nested labels, grids and tabs.
        for (var index = controls.Count - 1; index >= 0; index--)
        {
            var control = controls[index];
            if (control.IsDisposed)
                continue;
            var next = Scaled(_baseFonts[control], percent);
            if (!ReferenceEquals(control.Font, next))
                control.Font = next;

            if (control is DataGridView grid &&
                _gridBase.TryGetValue(grid, out var baseline))
            {
                if (baseline.Body is not null)
                    grid.DefaultCellStyle.Font = Scaled(baseline.Body, percent);
                if (baseline.Header is not null)
                    grid.ColumnHeadersDefaultCellStyle.Font =
                        Scaled(baseline.Header, percent);
            }
        }

        foreach (var item in items)
        {
            if (item.IsDisposed)
                continue;
            var next = Scaled(_baseItems[item], percent);
            if (!ReferenceEquals(item.Font, next))
                item.Font = next;
        }
        _lastPercent = percent;
    }

    private Font OriginalForNewControl(Control control)
    {
        if (_lastPercent != 100 && control.Parent is { } parent &&
            _baseFonts.TryGetValue(parent, out var baseline) &&
            control.Font.Equals(parent.Font))
        {
            // A late-added child inherits its parent's already-zoomed font.
            return baseline;
        }
        return control.Font;
    }

    private Font Scaled(Font baseline, int percent)
    {
        if (percent == 100)
            return baseline;

        var key = (baseline.FontFamily.Name, baseline.SizeInPoints,
            baseline.Style, percent);
        if (_scaledFonts.TryGetValue(key, out var font))
            return font;

        font = new Font(baseline.FontFamily,
            Math.Clamp(baseline.SizeInPoints * percent / 100f, 7f, 48f),
            baseline.Style, GraphicsUnit.Point);
        _scaledFonts.Add(key, font);
        return font;
    }

    private static IEnumerable<Control> ControlsIn(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        foreach (var descendant in ControlsIn(child))
            yield return descendant;
    }

    private static IEnumerable<ToolStripItem> MenuItems(
        IEnumerable<ToolStripItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            if (item is ToolStripDropDownItem nested)
            foreach (var descendant in MenuItems(
                         nested.DropDownItems.Cast<ToolStripItem>()))
                yield return descendant;
        }
    }

    public void Dispose()
    {
        // Scaled fonts are owned here; original fonts belong to WinForms.
        foreach (var font in _scaledFonts.Values)
            font.Dispose();
        _scaledFonts.Clear();
        _baseFonts.Clear();
        _baseItems.Clear();
        _gridBase.Clear();
    }
}
