namespace BitKeyBridge;

/// <summary>
/// Shared WinForms base for BitKeyBridge. UI coordinates are authored at the
/// standard 96-DPI logical baseline; this keeps forms and dialogs consistent
/// when Windows display scaling changes between monitors.
/// </summary>
public class DpiAwareForm : Form
{
    public DpiAwareForm()
    {
        AutoScaleMode =
            AutoScaleMode.Dpi;
        AutoScaleDimensions =
            new SizeF(
                UiStyle.BaselineDpi,
                UiStyle.BaselineDpi);
        Font =
            UiStyle.BodyFont;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // A scrollbar is preferable to clipped controls on high DPI, large-text
        // configurations, RDP sessions, or small displays.
        AutoScroll = true;
        ApplyResponsiveDefaults(
            this);
        ConstrainToWorkingArea();
        PerformLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyResponsiveDefaults(
            this);
        ConstrainToWorkingArea();
        PerformLayout();
    }

    protected override void OnDpiChanged(
        DpiChangedEventArgs e)
    {
        base.OnDpiChanged(
            e);

        BeginInvoke(
            new MethodInvoker(
                RefreshResponsiveLayout));
    }

    protected override void OnFontChanged(
        EventArgs e)
    {
        base.OnFontChanged(
            e);

        if (IsHandleCreated)
        {
            BeginInvoke(
                new MethodInvoker(
                    RefreshResponsiveLayout));
        }
    }

    private void RefreshResponsiveLayout()
    {
        ApplyResponsiveDefaults(
            this);
        ConstrainToWorkingArea();
        PerformLayout();
    }

    private static void ApplyResponsiveDefaults(
        Control root)
    {
        if (root is FlowLayoutPanel flow &&
            flow.FlowDirection is
                FlowDirection.LeftToRight or
                FlowDirection.RightToLeft)
        {
            // Horizontal action/status rows must be allowed to wrap when
            // DPI, text size, RDP width, or localization increases.
            flow.WrapContents =
                true;
        }

        if (root is Button button &&
            button.AutoSize)
        {
            button.MinimumSize =
                new Size(
                    Math.Max(
                        button.MinimumSize.Width,
                        UiStyle.MinimumButtonWidth),
                    Math.Max(
                        button.MinimumSize.Height,
                        UiStyle.MinimumButtonHeight));
        }

        foreach (Control child in
                 root.Controls)
        {
            ApplyResponsiveDefaults(
                child);
        }
    }

    private int ScaleLogical(
        int value) =>
        Math.Max(
            1,
            (int)Math.Round(
                value *
                DeviceDpi /
                (double)UiStyle.BaselineDpi));

    private void ConstrainToWorkingArea()
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        var margin =
            ScaleLogical(
                UiStyle.WindowMargin);

        var maxWidth =
            Math.Max(
                ScaleLogical(
                    320),
                workingArea.Width -
                margin *
                2);
        var maxHeight =
            Math.Max(
                ScaleLogical(
                    240),
                workingArea.Height -
                margin *
                2);
        var targetWidth = Math.Min(Width, maxWidth);
        var targetHeight = Math.Min(Height, maxHeight);

        if (MinimumSize.Width > targetWidth ||
            MinimumSize.Height > targetHeight)
        {
            MinimumSize = new Size(
                Math.Min(MinimumSize.Width, targetWidth),
                Math.Min(MinimumSize.Height, targetHeight));
        }

        if (targetWidth != Width || targetHeight != Height)
            Size = new Size(targetWidth, targetHeight);

        var maxLeft = Math.Max(
            workingArea.Left + margin,
            workingArea.Right - Width - margin);
        var maxTop = Math.Max(
            workingArea.Top + margin,
            workingArea.Bottom - Height - margin);

        Location = new Point(
            Math.Clamp(Left, workingArea.Left + margin, maxLeft),
            Math.Clamp(Top, workingArea.Top + margin, maxTop));
    }
}
