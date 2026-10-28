using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BitKeyBridge;

/// <summary>
/// One centrally managed Windows 11-inspired visual treatment for every
/// DpiAwareForm: main tabs, subpages, dialogs, buttons, inputs, tables and
/// status surfaces. Normal WinForms controls retain their keyboard behavior.
/// No passwords, permissions or event handlers are changed by the theme.
/// </summary>
internal static class UiModernTheme
{
    private sealed class ThemeMarker
    {
        public Size LastRoundedSize;
        public int LastRoundedDpi;
    }

    private static readonly ConditionalWeakTable<Control, ThemeMarker> Markers = new();

    internal static void Apply(Control root)
    {
        if (root.IsDisposed)
            return;

        // Once themed, new children are installed through ControlAdded.
        // Repeated full-tree Apply() traversals were O(all WinForms controls)
        // on every DPI/layout refresh even when nothing had changed.
        if (Markers.TryGetValue(root, out _))
            return;

        var marker = new ThemeMarker();
        Markers.Add(root, marker);
        UiLayoutTelemetry.RecordThemedControl();
        root.ControlAdded += (_, args) =>
        {
            if (args.Control is { } child) Apply(child);
        };
        Install(root, marker);

        foreach (Control child in root.Controls)
            Apply(child);
    }

    internal static void Refresh(Control root)
    {
        Apply(root);
        RefreshRounding(root);
    }

    private static void RefreshRounding(Control root)
    {
        if (root.IsDisposed)
            return;

        if (Markers.TryGetValue(root, out var marker))
            UpdateCorners(root, marker);

        if (root is TabControl tabs && tabs.DrawMode == TabDrawMode.OwnerDrawFixed)
            tabs.Invalidate();

        foreach (Control child in root.Controls)
            RefreshRounding(child);
    }

    private static void Install(Control control, ThemeMarker marker)
    {
        if (control is Form form)
        {
            if (!SystemInformation.HighContrast)
                form.BackColor = UiStyle.ModernCanvas;
            form.HandleCreated += (_, _) => RequestRoundedWindow(form);
            if (form.IsHandleCreated)
                RequestRoundedWindow(form);
        }

        if (SystemInformation.HighContrast)
            return;

        switch (control)
        {
            case UiRecoveryCard:
            case UiRecoveryBadge:
            case UiRecoveryGlyph:
                // These already draw their own rounded geometry and palette.
                return;

            case TabPage page:
                page.BackColor = UiStyle.ModernCanvas;
                break;

            case TabControl tabs when
                tabs.Alignment == TabAlignment.Top &&
                tabs.Appearance == TabAppearance.Normal:
                tabs.BackColor = UiStyle.ModernCanvas;
                // Keep the primary seven workspace tabs native in *all*
                // languages. Toggling DrawMode on every EN/RU <-> Hebrew
                // language switch forces an additional native TabControl
                // handle recreation and extra UI-thread work. The ordinary
                // Windows tab strip is also simpler, stable at 200% DPI and
                // does not leave mirrored owner-draw phantom headers.
                // Retain rounded owner-draw for nested non-mirrored tabs.
                var mainNavigation = tabs.Name == "MainWorkspaceTabs";
                tabs.DrawMode = mainNavigation || tabs.RightToLeftLayout
                    ? TabDrawMode.Normal : TabDrawMode.OwnerDrawFixed;
                tabs.Padding = new Point(
                    UiStyle.ModernTabHorizontalPadding,
                    UiStyle.ModernTabVerticalPadding);
                if (!mainNavigation)
                    tabs.DrawItem += DrawTab;
                tabs.FontChanged += (_, _) => tabs.Invalidate();
                tabs.SelectedIndexChanged += (_, _) => tabs.Invalidate();
                break;

            case GroupBox group:
                group.BackColor = UiStyle.ModernSurface;
                group.ForeColor = UiStyle.ModernForeground;
                group.FlatStyle = FlatStyle.Flat;
                group.Paint += (_, args) => PaintGroupBox(group, args);
                break;

            case Button button:
                // Recovery-card buttons already own a distinct visual style
                // with glyphs and measured, full-width captions.
                if (!InsideRecoveryCard(button))
                {
                    var primary = IsPrimaryAction(button);
                    button.FlatStyle = FlatStyle.Flat;
                    // GDI+ paints an AA outline after the native Button
                    // content. Native borders and Region clips leave broken
                    // corner segments when scaled to fractional DPI.
                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.BorderColor = UiStyle.ModernBorder;
                    button.FlatAppearance.MouseOverBackColor = primary
                        ? UiStyle.ModernAccentHover : UiStyle.ModernHover;
                    button.FlatAppearance.MouseDownBackColor = primary
                        ? UiStyle.ModernAccentPressed : UiStyle.ModernPressed;
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = primary
                        ? UiStyle.ModernAccent : UiStyle.ModernSurface;
                    button.ForeColor = primary
                        ? Color.White : UiStyle.ModernForeground;
                }
                UiRecoveryGraphics.ConfigureRoundedButton(
                    button,
                    InsideRecoveryCard(button) ? 12f : UiStyle.ModernButtonRadius,
                    InsideRecoveryCard(button) || IsPrimaryAction(button)
                        ? Color.Empty : UiStyle.ModernBorder);
                break;

            case ComboBox combo:
                combo.BackColor = UiStyle.ModernSurface;
                combo.ForeColor = UiStyle.ModernForeground;
                // Editing controls are native child HWNDs. Setting Regions
                // on every resize recreates native GDI handles and clips
                // caret/borders at mixed DPI; use clean native outlines.
                break;

            case TextBoxBase edit:
                edit.BackColor = UiStyle.ModernSurface;
                edit.ForeColor = UiStyle.ModernForeground;
                break;

            case UpDownBase numeric:
                numeric.BackColor = UiStyle.ModernSurface;
                break;

            case DateTimePicker:
                // Preserve Windows native hit-testing, calendar border and caret.
                break;

            case DataGridView grid:
                grid.BorderStyle = BorderStyle.None;
                grid.BackgroundColor = UiStyle.ModernSurface;
                grid.GridColor = UiStyle.ModernBorder;
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = UiStyle.ModernHeader;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = UiStyle.ModernForeground;
                grid.DefaultCellStyle.BackColor = UiStyle.ModernSurface;
                grid.AlternatingRowsDefaultCellStyle.BackColor = UiStyle.ModernAlternateRow;
                // Grid scrolling and virtualized rows do not benefit from a
                // clipped Window Region; preserve inexpensive rectangular HWND.
                break;

            case ListView list:
                list.BackColor = UiStyle.ModernSurface;
                list.ForeColor = UiStyle.ModernForeground;
                list.BorderStyle = BorderStyle.FixedSingle;
                break;

            case UiStatusLabel:
                // UiStatusLabel paints its own antialiased border. A native
                // Region + rectangular ControlPaint border cuts away the
                // rounded edge, especially at fractional monitor DPI.
                break;

            case ProgressBar:
                break;

            case StatusStrip statusBar:
                statusBar.BackColor = UiStyle.ModernSurface;
                statusBar.ForeColor = UiStyle.ModernForeground;
                break;

            case MenuStrip menu:
                menu.BackColor = UiStyle.ModernSurface;
                menu.ForeColor = UiStyle.ModernForeground;
                menu.RenderMode = ToolStripRenderMode.Professional;
                break;

            case ToolStrip strip:
                strip.BackColor = UiStyle.ModernSurface;
                strip.RenderMode = ToolStripRenderMode.Professional;
                break;

            case Panel panel when panel is not UiDiagnosticPanel:
                // Preserve explicitly colored panels, warnings and owner-drawn
                // views. Make generic layout panels inherit their card/canvas.
                if (panel.BackColor == SystemColors.Control &&
                    panel.BorderStyle == BorderStyle.None)
                    panel.BackColor = Color.Transparent;
                else if (panel.BorderStyle != BorderStyle.None)
                    SetupRoundedRegion(panel, marker, UiStyle.ModernSurfaceRadius);
                break;
        }

        // Rounded regions and tab chrome must be recalculated when moving an
        // application between monitors with different DPI settings.
        control.DpiChangedAfterParent += (_, _) =>
        {
            UpdateCorners(control, marker);
            if (control is GroupBox group)
                group.Invalidate();
        };
    }

    /// <summary>
    /// Only the most prominent non-secret action in a workflow receives
    /// accent treatment; it remains a normal accessible WinForms button.
    /// Avoid rewriting buttons that disclose/copy recovery passwords.
    /// </summary>
    internal static bool IsPrimaryAction(Button button) =>
        button.Name is
            "HomeConnectAdButton" or
            "HomeUnifiedSearchButton" or
            "SaveUpdateSettingsButton" or
            "UpdateNowButton" or
            "UpdateCenterRetryCheckButton";

    private static bool InsideRecoveryCard(Control control)
    {
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is UiRecoveryCard)
                return true;
        }
        return false;
    }

    private static void SetupRoundedRegion(
        Control control, ThemeMarker marker, int radius)
    {
        marker.LastRoundedSize = Size.Empty;
        marker.LastRoundedDpi = -1;
        control.SizeChanged += (_, _) => UpdateCorners(control, marker);
        control.HandleCreated += (_, _) =>
        {
            // A recreated HWND needs its rounded region on the NEW handle,
            // even when size/DPI still match the previous native window.
            marker.LastRoundedSize = Size.Empty;
            marker.LastRoundedDpi = -1;
            UpdateCorners(control, marker);
        };
        // Do not create hundreds of Region/GDI objects for hidden tabs at
        // startup. The HandleCreated callback installs identical geometry
        // before a newly visible control can draw.
        if (control.IsHandleCreated)
            UpdateCorners(control, marker, radius);
        else
            UiLayoutTelemetry.RecordDeferredRoundedRegion();
    }

    private static void UpdateCorners(Control control, ThemeMarker marker, int? radius = null)
    {
        if (control.IsDisposed || SystemInformation.HighContrast ||
            !control.IsHandleCreated)
            return;

        var logicalRadius = radius ?? (control switch
        {
            Button => UiStyle.ModernButtonRadius,
            ComboBox or TextBoxBase or UpDownBase or DateTimePicker =>
                UiStyle.ModernInputRadius,
            UiStatusLabel or ProgressBar => UiStyle.ModernSmallRadius,
            DataGridView or ListView or Panel => UiStyle.ModernSurfaceRadius,
            _ => 0
        });
        if (logicalRadius <= 0 || control.Width < 2 || control.Height < 2)
            return;

        var dpi = Math.Max(UiStyle.BaselineDpi, control.DeviceDpi);
        if (marker.LastRoundedSize == control.Size &&
            marker.LastRoundedDpi == dpi)
            return;

        marker.LastRoundedSize = control.Size;
        marker.LastRoundedDpi = dpi;
        var scaled = logicalRadius * dpi / (float)UiStyle.BaselineDpi;
        using var path = UiRecoveryGraphics.RoundRect(
            new RectangleF(0, 0, Math.Max(1, control.Width - 1),
                Math.Max(1, control.Height - 1)), scaled);
        var old = control.Region;
        control.Region = new Region(path);
        old?.Dispose();
        UiLayoutTelemetry.RecordRoundedRegionApplied();
    }

    private static void DrawTab(object? sender, DrawItemEventArgs args)
    {
        if (sender is not TabControl tabs ||
            args.Index < 0 || args.Index >= tabs.TabPages.Count)
            return;

        var active = args.Index == tabs.SelectedIndex;
        var rect = Rectangle.Inflate(args.Bounds, -2, -1);
        if (rect.Width <= 2 || rect.Height <= 2)
            return;

        args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        args.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var stroke = Math.Max(1f, tabs.DeviceDpi / (float)UiStyle.BaselineDpi);
        var bounds = UiRecoveryGraphics.InsideStrokeBounds(rect.Size, stroke);
        bounds.Offset(rect.Location);
        using var path = UiRecoveryGraphics.RoundRect(bounds,
            UiStyle.ModernTabRadius * tabs.DeviceDpi / (float)UiStyle.BaselineDpi);
        using var fill = new SolidBrush(
            active ? UiStyle.ModernSurface : UiStyle.ModernCanvas);
        args.Graphics.FillPath(fill, path);
        if (active)
        {
            using var border = new Pen(UiStyle.ModernBorder, stroke);
            args.Graphics.DrawPath(border, path);
            // Fluent-style slim accent indicator helps recognition without
            // painting an entire new tab control or animating the workspace.
            using var accent = new SolidBrush(UiStyle.ModernAccent);
            var markerHeight = Math.Max(2,
                UiStyle.ScaleLogicalPixels(2, tabs.DeviceDpi));
            var markerWidth = Math.Max(12, rect.Width / 3);
            args.Graphics.FillRectangle(accent,
                rect.Left + (rect.Width - markerWidth) / 2,
                rect.Bottom - markerHeight - 1,
                markerWidth, markerHeight);
        }

        // Owner-drawn tabs must explicitly pass the bidirectional text flag.
        // RTL tab positioning does not make TextRenderer detect Hebrew.
        var textFlags = TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis;
        if (tabs.RightToLeft == RightToLeft.Yes)
            textFlags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(args.Graphics,
            tabs.TabPages[args.Index].Text, tabs.Font, rect,
            active ? UiStyle.ModernForeground : UiStyle.ModernMuted,
            textFlags);
    }

    private static void PaintGroupBox(GroupBox group, PaintEventArgs args)
    {
        if (group.Width < 3 || group.Height < 3)
            return;

        var graphics = args.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var textHeight = TextRenderer.MeasureText(
            string.IsNullOrWhiteSpace(group.Text) ? " " : group.Text,
            group.Font).Height;
        var outlineTop = Math.Max(1, textHeight / 2f);
        graphics.Clear(group.Parent?.BackColor ?? UiStyle.ModernCanvas);
        var stroke = Math.Max(1f, group.DeviceDpi / (float)UiStyle.BaselineDpi);
        using var outline = UiRecoveryGraphics.RoundRect(
            UiRecoveryGraphics.InsideStrokeBounds(group.Size, stroke, outlineTop),
            UiStyle.ModernSurfaceRadius * group.DeviceDpi / (float)UiStyle.BaselineDpi);
        using var fill = new SolidBrush(UiStyle.ModernSurface);
        graphics.FillPath(fill, outline);
        using var border = new Pen(UiStyle.ModernBorder, stroke);
        graphics.DrawPath(border, outline);

        if (!string.IsNullOrEmpty(group.Text))
        {
            var gap = UiStyle.ControlGap;
            var width = Math.Max(1, group.Width - gap * 4);
            var size = TextRenderer.MeasureText(group.Text, group.Font,
                new Size(width, textHeight), TextFormatFlags.SingleLine);
            var title = new Rectangle(gap * 2, 0,
                Math.Min(width, size.Width + gap), textHeight);
            graphics.FillRectangle(fill, title);
            TextRenderer.DrawText(graphics, group.Text, group.Font, title,
                UiStyle.ModernForeground,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }
    }

    private static void RequestRoundedWindow(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ||
            SystemInformation.HighContrast || form.IsDisposed)
            return;

        try
        {
            const int roundedCorners = 2; // DWMWCP_ROUND
            var preference = roundedCorners;
            _ = DwmSetWindowAttribute(form.Handle, 33, ref preference,
                Marshal.SizeOf<int>());
        }
        catch (DllNotFoundException) { /* Earlier Windows version. */ }
        catch (EntryPointNotFoundException) { /* DWM API unavailable. */ }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);
}
