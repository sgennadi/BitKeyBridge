using System.Diagnostics;
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
    // Sharing one renderer and ControlAdded delegate avoids creating a new
    // object/closure for every control in the ~700-control cold Start tree.
    private static readonly ToolStripProfessionalRenderer SharedMenuRenderer =
        new(new NipkgMenuColorTable());

    private static void ThemeChildAdded(object? sender, ControlEventArgs args)
    {
        if (args.Control is { } child)
            Apply(child);
    }

    private static UiLayoutTelemetry.ThemeInstallKind ThemeKind(Control control) =>
        control switch
        {
            Form => UiLayoutTelemetry.ThemeInstallKind.Form,
            TabControl or TabPage => UiLayoutTelemetry.ThemeInstallKind.Tabs,
            GroupBox => UiLayoutTelemetry.ThemeInstallKind.GroupBox,
            Button => UiLayoutTelemetry.ThemeInstallKind.Buttons,
            ComboBox or TextBoxBase or UpDownBase or DateTimePicker =>
                UiLayoutTelemetry.ThemeInstallKind.Inputs,
            DataGridView or ListView => UiLayoutTelemetry.ThemeInstallKind.Grids,
            ToolStrip => UiLayoutTelemetry.ThemeInstallKind.ToolStrips,
            Panel => UiLayoutTelemetry.ThemeInstallKind.Panels,
            _ => UiLayoutTelemetry.ThemeInstallKind.Other
        };

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
        root.ControlAdded += ThemeChildAdded;
        // Stopwatch ticks only. Classification is by framework control type
        // and never includes Name, Text, metadata or secret-bearing values.
        var installStarted = Stopwatch.GetTimestamp();
        Install(root, marker);
        UiLayoutTelemetry.RecordThemeInstall(ThemeKind(root), installStarted);

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
            case UiNipkgBrandBanner:
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
                // Avoid multiple walks up the same Button's ancestor chain.
                // Recovery buttons keep their existing glyph and text layout.
                var recoveryButton = InsideRecoveryCard(button);
                if (!recoveryButton)
                {
                    var primary = IsPrimaryAction(button);
                    var positive = IsPositiveAction(button);
                    button.FlatStyle = FlatStyle.Flat;
                    // GDI+ paints an AA outline after the native Button
                    // content. Native borders and Region clips leave broken
                    // corner segments when scaled to fractional DPI.
                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.BorderColor = UiStyle.ModernBorder;
                    button.FlatAppearance.MouseOverBackColor = primary
                        ? positive ? UiStyle.ModernPositiveAccentHover
                            : UiStyle.ModernAccentHover
                        : UiStyle.ModernHover;
                    button.FlatAppearance.MouseDownBackColor = primary
                        ? positive ? UiStyle.ModernPositiveAccentPressed
                            : UiStyle.ModernAccentPressed
                        : UiStyle.ModernPressed;
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = primary
                        ? positive ? UiStyle.ModernPositiveAccent
                            : UiStyle.ModernAccent
                        : UiStyle.ModernSurface;
                    button.ForeColor = primary
                        ? Color.White : UiStyle.ModernForeground;
                }
                UiRecoveryGraphics.ConfigureRoundedButton(
                    button,
                    recoveryButton ? 12f : UiStyle.ModernButtonRadius,
                    recoveryButton || IsPrimaryAction(button)
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
                statusBar.BackColor = UiStyle.BrandSoftBlue;
                statusBar.ForeColor = UiStyle.ModernForeground;
                break;

            case MenuStrip menu:
                menu.BackColor = UiStyle.BrandSoftBlue;
                menu.ForeColor = UiStyle.ModernForeground;
                menu.Renderer = SharedMenuRenderer;
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

    // Green is reserved for safe metadata-only Search (NI's positive
    // action convention). Never make Reveal/Copy Key or rotation a green
    // one-click action; authorization and confirmation remain unchanged.
    internal static bool IsPositiveAction(Button button) =>
        button.Name == "HomeUnifiedSearchButton";

    private static bool InsideRecoveryCard(Control control)
    {
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is UiRecoveryCard)
                return true;
        }
        return false;
    }

    private sealed class NipkgMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => UiStyle.BrandSoftBlue;
        public override Color MenuStripGradientEnd => UiStyle.BrandSoftBlue;
        public override Color MenuItemSelected => UiStyle.ModernHover;
        public override Color MenuItemSelectedGradientBegin => UiStyle.ModernHover;
        public override Color MenuItemSelectedGradientEnd => UiStyle.ModernHover;
        public override Color MenuItemPressedGradientBegin => UiStyle.BrandSoftBlue;
        public override Color MenuItemPressedGradientEnd => UiStyle.BrandSoftBlue;
        public override Color MenuItemBorder => UiStyle.ModernBorder;
        public override Color ToolStripDropDownBackground => UiStyle.ModernSurface;
        public override Color ImageMarginGradientBegin => UiStyle.ModernSurface;
        public override Color ImageMarginGradientMiddle => UiStyle.ModernSurface;
        public override Color ImageMarginGradientEnd => UiStyle.ModernSurface;
        public override Color SeparatorDark => UiStyle.ModernBorder;
        public override Color SeparatorLight => UiStyle.ModernSurface;
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
            // Match NI Package Creator's native Windows 11 navy caption.
            // DWM rejects these attributes on older builds harmlessly.
            var caption = ColorTranslator.ToWin32(UiStyle.BrandNavy);
            var captionText = ColorTranslator.ToWin32(Color.White);
            _ = DwmSetWindowAttribute(form.Handle, 35, ref caption,
                Marshal.SizeOf<int>());
            _ = DwmSetWindowAttribute(form.Handle, 36, ref captionText,
                Marshal.SizeOf<int>());
        }
        catch (DllNotFoundException) { /* Earlier Windows version. */ }
        catch (EntryPointNotFoundException) { /* DWM API unavailable. */ }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);
}
