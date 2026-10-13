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

        if (!Markers.TryGetValue(root, out _))
        {
            var marker = new ThemeMarker();
            Markers.Add(root, marker);
            root.ControlAdded += (_, args) => Apply(args.Control);
            Install(root, marker);
        }

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
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.Padding = new Point(
                    UiStyle.ModernTabHorizontalPadding,
                    UiStyle.ModernTabVerticalPadding);
                tabs.DrawItem += DrawTab;
                tabs.FontChanged += (_, _) => tabs.Invalidate();
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
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = UiStyle.ModernBorder;
                    button.FlatAppearance.MouseOverBackColor = UiStyle.ModernHover;
                    button.FlatAppearance.MouseDownBackColor = UiStyle.ModernPressed;
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = UiStyle.ModernSurface;
                    button.ForeColor = UiStyle.ModernForeground;
                }
                SetupRoundedRegion(button, marker, UiStyle.ModernButtonRadius);
                break;

            case ComboBox combo:
                combo.BackColor = UiStyle.ModernSurface;
                combo.ForeColor = UiStyle.ModernForeground;
                SetupRoundedRegion(combo, marker, UiStyle.ModernInputRadius);
                break;

            case TextBoxBase edit:
                edit.BackColor = UiStyle.ModernSurface;
                edit.ForeColor = UiStyle.ModernForeground;
                SetupRoundedRegion(edit, marker, UiStyle.ModernInputRadius);
                break;

            case UpDownBase numeric:
                numeric.BackColor = UiStyle.ModernSurface;
                SetupRoundedRegion(numeric, marker, UiStyle.ModernInputRadius);
                break;

            case DateTimePicker date:
                SetupRoundedRegion(date, marker, UiStyle.ModernInputRadius);
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
                SetupRoundedRegion(grid, marker, UiStyle.ModernSurfaceRadius);
                break;

            case ListView list:
                list.BackColor = UiStyle.ModernSurface;
                list.ForeColor = UiStyle.ModernForeground;
                list.BorderStyle = BorderStyle.FixedSingle;
                SetupRoundedRegion(list, marker, UiStyle.ModernSurfaceRadius);
                break;

            case UiStatusLabel status:
                if (status.BorderStyle != BorderStyle.None)
                    SetupRoundedRegion(status, marker, UiStyle.ModernSmallRadius);
                break;

            case ProgressBar progress:
                SetupRoundedRegion(progress, marker, UiStyle.ModernSmallRadius);
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
        control.HandleCreated += (_, _) => UpdateCorners(control, marker);
        UpdateCorners(control, marker, radius);
    }

    private static void UpdateCorners(Control control, ThemeMarker marker, int? radius = null)
    {
        if (control.IsDisposed || SystemInformation.HighContrast)
            return;

        var logicalRadius = radius ?? control switch
        {
            Button => UiStyle.ModernButtonRadius,
            ComboBox or TextBoxBase or UpDownBase or DateTimePicker =>
                UiStyle.ModernInputRadius,
            UiStatusLabel or ProgressBar => UiStyle.ModernSmallRadius,
            DataGridView or ListView or Panel => UiStyle.ModernSurfaceRadius,
            _ => 0
        };
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
            new RectangleF(0, 0, control.Width, control.Height), scaled);
        var old = control.Region;
        control.Region = new Region(path);
        old?.Dispose();
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
        using var path = UiRecoveryGraphics.RoundRect(rect,
            UiStyle.ModernTabRadius * tabs.DeviceDpi / (float)UiStyle.BaselineDpi);
        using var fill = new SolidBrush(
            active ? UiStyle.ModernSurface : UiStyle.ModernCanvas);
        args.Graphics.FillPath(fill, path);
        if (active)
        {
            using var border = new Pen(UiStyle.ModernBorder, 1f);
            args.Graphics.DrawPath(border, path);
        }

        TextRenderer.DrawText(args.Graphics,
            tabs.TabPages[args.Index].Text, tabs.Font, rect,
            active ? UiStyle.ModernForeground : UiStyle.ModernMuted,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis);
    }

    private static void PaintGroupBox(GroupBox group, PaintEventArgs args)
    {
        if (group.Width < 3 || group.Height < 3)
            return;

        var graphics = args.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var textHeight = TextRenderer.MeasureText(
            string.IsNullOrWhiteSpace(group.Text) ? " " : group.Text,
            group.Font).Height;
        var top = Math.Max(1, textHeight / 2f);
        graphics.Clear(group.Parent?.BackColor ?? UiStyle.ModernCanvas);
        using var outline = UiRecoveryGraphics.RoundRect(
            new RectangleF(0.5f, top, group.Width - 1.5f,
                Math.Max(2f, group.Height - top - 1.5f)),
            UiStyle.ModernSurfaceRadius * group.DeviceDpi / (float)UiStyle.BaselineDpi);
        using var fill = new SolidBrush(UiStyle.ModernSurface);
        graphics.FillPath(fill, outline);
        using var border = new Pen(UiStyle.ModernBorder,
            Math.Max(1f, group.DeviceDpi / (float)UiStyle.BaselineDpi));
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
