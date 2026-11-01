using System.Drawing;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Per-user, non-security UI state only. No credentials, device records,
/// recovery passwords or machine policy ever belong in this file.
/// </summary>
internal sealed class UiWorkspacePreferences
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1220;
    public int Height { get; set; } = 820;
    public bool Maximized { get; set; } = true;
    public bool CompactUpdates { get; set; } = true;
    // Per-user display settings only; never authorization or audit policy.
    public string UiDensity { get; set; } = "Auto";
    public int UiScalePercent { get; set; } = 100;
}

/// <summary>
/// OS DPI remains owned by Windows (PerMonitorV2). This policy defines a
/// bounded independent user zoom and non-security density preference.
/// </summary>
internal static class UiDisplayPreferencePolicy
{
    internal static readonly int[] AvailableScales = [90, 100, 110, 125];
    internal static readonly string[] AvailableDensities =
        ["Auto", "Compact", "Comfortable"];

    internal static string NormalizeDensity(string? value) =>
        AvailableDensities.FirstOrDefault(candidate =>
            string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
        ?? "Auto";

    internal static int NormalizeScale(int percent) =>
        AvailableScales.Contains(percent) ? percent : 100;

    internal static double EffectiveScale(
        int deviceDpi, float formFontSize, float standardFontSize,
        int zoomPercent)
    {
        var zoom = NormalizeScale(zoomPercent) / 100d;
        var nativeDpi = Math.Max(1d, deviceDpi /
            (double)UiStyle.BaselineDpi);
        // Test/accessibility fonts can be enlarged independently of DPI.
        var externalFontRatio = standardFontSize <= 0f
            ? 1d
            : Math.Max(1d, formFontSize / standardFontSize / zoom);
        return Math.Max(nativeDpi, externalFontRatio) * zoom;
    }

    internal static int ResolveStartTier(
        string density, double availableLogicalHeight) =>
        NormalizeDensity(density) switch
        {
            "Compact" => 2,
            "Comfortable" => 0,
            _ => availableLogicalHeight < 700 ? 2 :
                 availableLogicalHeight < 800 ? 1 : 0
        };
}

internal static class UiWorkspacePreferenceService
{
    internal static string FilePath =>
        Path.Combine(AppPaths.LocalConfigDirectory, "ui-workspace.json");

    internal static UiWorkspacePreferences Load(string? path = null)
    {
        try
        {
            var loaded = JsonStore.Read<UiWorkspacePreferences>(path ?? FilePath)
                ?? new UiWorkspacePreferences();
            loaded.UiDensity = UiDisplayPreferencePolicy.NormalizeDensity(
                loaded.UiDensity);
            loaded.UiScalePercent = UiDisplayPreferencePolicy.NormalizeScale(
                loaded.UiScalePercent);
            return loaded;
        }
        catch (Exception ex) when (ex is IOException or
                                   UnauthorizedAccessException or
                                   JsonException or
                                   System.Security.SecurityException)
        {
            System.Diagnostics.Debug.WriteLine("UI workspace preference unavailable: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
            return new UiWorkspacePreferences();
        }
    }

    internal static void Save(UiWorkspacePreferences preferences, string? path = null)
    {
        // UI state is not a source of trusted settings; never write a
        // nonsensical or off-screen rectangle from a corrupted session.
        if (preferences.Width < 680 || preferences.Height < 460 ||
            preferences.Width > 16384 || preferences.Height > 16384)
            throw new ArgumentOutOfRangeException(nameof(preferences));

        preferences.UiDensity = UiDisplayPreferencePolicy.NormalizeDensity(
            preferences.UiDensity);
        preferences.UiScalePercent = UiDisplayPreferencePolicy.NormalizeScale(
            preferences.UiScalePercent);
        JsonStore.WriteAtomic(path ?? FilePath, preferences);
    }

    internal static bool FitsAnyScreen(
        Rectangle bounds, IEnumerable<Rectangle> workAreas)
    {
        if (bounds.Width < 680 || bounds.Height < 460 ||
            bounds.Width > 16384 || bounds.Height > 16384)
            return false;

        // Reconnecting through RDP or unplugging a monitor must not restore a
        // window entirely outside the visible desktop.
        return workAreas.Any(area =>
        {
            var intersection = Rectangle.Intersect(bounds, area);
            return intersection.Width >= 160 && intersection.Height >= 120;
        });
    }

    internal static Rectangle? RestoreToVisibleArea(
        Rectangle previousBounds, IEnumerable<Rectangle> workAreas,
        Size minimumSize)
    {
        if (minimumSize.Width < 1 || minimumSize.Height < 1 ||
            previousBounds.Width < 680 || previousBounds.Height < 460 ||
            previousBounds.Width > 16384 || previousBounds.Height > 16384)
            return null;

        // RDP reconnection, scaling changes and monitor removal can leave
        // most of the old window off-screen. Never reuse that rectangle as-is.
        // Choose the monitor with the largest overlap, then clamp all edges.
        var best = workAreas
            .Where(a => a.Width >= minimumSize.Width &&
                        a.Height >= minimumSize.Height)
            .Select(a => new
            {
                Work = a,
                Overlap = Rectangle.Intersect(previousBounds, a)
            })
            .Where(x => x.Overlap.Width >= 160 &&
                        x.Overlap.Height >= 120)
            .OrderByDescending(x => (long)x.Overlap.Width * x.Overlap.Height)
            .FirstOrDefault();
        if (best is null)
            return null;

        var area = best.Work;
        var width = Math.Clamp(previousBounds.Width, minimumSize.Width, area.Width);
        var height = Math.Clamp(previousBounds.Height, minimumSize.Height, area.Height);
        return new Rectangle(
            Math.Clamp(previousBounds.Left, area.Left, area.Right - width),
            Math.Clamp(previousBounds.Top, area.Top, area.Bottom - height),
            width, height);
    }

    internal static IEnumerable<string> RunOfflineTests(string directory)
    {
        var errors = new List<string>();
        try
        {
            var screen = new Rectangle(0, 0, 1920, 1080);
            if (!FitsAnyScreen(new Rectangle(100, 50, 1100, 750), [screen]) ||
                FitsAnyScreen(new Rectangle(5000, 5000, 1100, 750), [screen]) ||
                FitsAnyScreen(new Rectangle(0, 0, 90, 60), [screen]))
                errors.Add("Workspace restore accepted invalid/off-screen window bounds.");

            var moved = RestoreToVisibleArea(
                new Rectangle(1600, 740, 1800, 1200), [screen],
                new Size(780, 550));
            if (moved is null || moved.Value.Right > screen.Right ||
                moved.Value.Bottom > screen.Bottom || moved.Value.Left < 0 ||
                moved.Value.Top < 0 || moved.Value.Width != 1800 ||
                moved.Value.Height != 1080)
                errors.Add("Workspace restore failed to clamp a former 4K/RDP window.");

            if (RestoreToVisibleArea(new Rectangle(5000, 5000, 1200, 900),
                    [screen], new Size(780, 550)) is not null ||
                RestoreToVisibleArea(new Rectangle(100, 50, 1100, 750),
                    [new Rectangle(0, 0, 640, 480)], new Size(780, 550)) is not null)
                errors.Add("Workspace restore accepted a removed or too-small monitor.");

            var path = Path.Combine(directory, "ui-workspace-test.json");
            Save(new UiWorkspacePreferences
            {
                X = 75, Y = 84, Width = 1080, Height = 740,
                Maximized = false, CompactUpdates = false,
                UiDensity = "Compact", UiScalePercent = 125
            }, path);
            var loaded = Load(path);
            if (loaded.X != 75 || loaded.Y != 84 || loaded.Width != 1080 ||
                loaded.Height != 740 || loaded.Maximized || loaded.CompactUpdates ||
                loaded.UiDensity != "Compact" || loaded.UiScalePercent != 125)
                errors.Add("Workspace/density/zoom settings did not round-trip.");

            // Old preference JSON did not contain appearance fields.
            File.WriteAllText(path,
                "{\"Width\":1080,\"Height\":740,\"CompactUpdates\":true}");
            var legacy = Load(path);
            if (legacy.UiDensity != "Auto" || legacy.UiScalePercent != 100)
                errors.Add("Legacy visual settings lost default Auto/100%.");

            File.WriteAllText(path,
                "{\"UiDensity\":\"Invalid\",\"UiScalePercent\":999}");
            var invalid = Load(path);
            if (invalid.UiDensity != "Auto" || invalid.UiScalePercent != 100)
                errors.Add("Invalid visual preferences were not normalized.");

            foreach (var dpi in new[] { 96, 120, 144, 192 })
            foreach (var zoom in UiDisplayPreferencePolicy.AvailableScales)
            {
                var expected = dpi / 96d * zoom / 100d;
                var actual = UiDisplayPreferencePolicy.EffectiveScale(
                    dpi, 9f * zoom / 100f, 9f, zoom);
                if (Math.Abs(expected - actual) > 0.0001d)
                    errors.Add("OS DPI/user zoom scaling regression at " +
                        dpi + " DPI / " + zoom + "%.");
            }
            if (UiDisplayPreferencePolicy.ResolveStartTier("Auto", 680) != 2 ||
                UiDisplayPreferencePolicy.ResolveStartTier("Auto", 750) != 1 ||
                UiDisplayPreferencePolicy.ResolveStartTier("Auto", 900) != 0 ||
                UiDisplayPreferencePolicy.ResolveStartTier("Compact", 900) != 2 ||
                UiDisplayPreferencePolicy.ResolveStartTier("Comfortable", 550) != 0)
                errors.Add("UI density breakpoints/overrides are incorrect.");

            File.WriteAllText(path, "{ invalid");
            var reset = Load(path);
            if (!reset.Maximized || !reset.CompactUpdates ||
                reset.UiDensity != "Auto" || reset.UiScalePercent != 100)
                errors.Add("Corrupted visual preferences failed to use safe defaults.");
        }
        catch (Exception ex)
        {
            errors.Add("Workspace UI preferences: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return errors;
    }
}
