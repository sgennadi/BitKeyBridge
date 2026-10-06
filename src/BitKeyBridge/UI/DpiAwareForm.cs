namespace BitKeyBridge;

/// <summary>
/// Shared WinForms base for BitKeyBridge. UI coordinates are authored at the
/// standard 96-DPI logical baseline; this keeps forms and dialogs consistent
/// when Windows display scaling changes between monitors.
/// </summary>
public class DpiAwareForm : Form
{
    private readonly Dictionary<Label, Size> _labelMaximumSizes =
        new();

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

        SizeChanged +=
            (_, _) =>
            {
                if (IsHandleCreated)
                {
                    RefreshResponsiveLabelWidths();
                }
            };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // A scrollbar is preferable to clipped controls on high DPI, large-text
        // configurations, RDP sessions, or small displays.
        AutoScroll = true;
        RefreshResponsiveLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshResponsiveLayout();
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

    internal void PrepareResponsiveLayoutForTesting()
    {
        AutoScroll =
            true;
        ApplyResponsiveDefaults(
            this);
        PerformLayout();
        RefreshResponsiveLabelWidths();
        PerformLayout();
    }

    private void RefreshResponsiveLayout()
    {
        ApplyResponsiveDefaults(
            this);
        ConstrainToWorkingArea();
        PerformLayout();
        RefreshResponsiveLabelWidths();
        PerformLayout();
    }

    private void ApplyResponsiveDefaults(
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

        if (root is UiStatusLabel statusLabel)
        {
            UiStyle.RefreshStatusLabel(
                statusLabel);
        }

        if (root is Label label &&
            label.AutoSize &&
            label.MaximumSize.Width > 0)
        {
            if (!_labelMaximumSizes.ContainsKey(
                    label))
            {
                _labelMaximumSizes[label] =
                    label.MaximumSize;
            }

            RefreshResponsiveLabelWidth(
                label);
        }

        foreach (Control child in
                 root.Controls)
        {
            ApplyResponsiveDefaults(
                child);
        }
    }

    private void RefreshResponsiveLabelWidths()
    {
        foreach (var label in
                 _labelMaximumSizes.Keys
                     .Where(
                         x => !x.IsDisposed)
                     .ToArray())
        {
            RefreshResponsiveLabelWidth(
                label);
        }
    }

    private void RefreshResponsiveLabelWidth(
        Label label)
    {
        if (!_labelMaximumSizes.TryGetValue(
                label,
                out var designMaximum) ||
            designMaximum.Width <= 0 ||
            label.Parent is null)
        {
            return;
        }

        var availableWidth =
            GetAvailableWidth(
                label);

        if (availableWidth <= 0)
            return;

        var maximumWidth =
            Math.Max(
                1,
                Math.Min(
                    designMaximum.Width,
                    availableWidth));

        var target =
            new Size(
                maximumWidth,
                designMaximum.Height);

        if (label.MaximumSize !=
            target)
        {
            label.MaximumSize =
                target;
        }
    }

    private static int GetAvailableWidth(
        Control control)
    {
        var parent =
            control.Parent;

        if (parent is null)
            return 0;

        var available =
            parent.ClientSize.Width -
            control.Margin.Horizontal;

        if (parent is TableLayoutPanel table)
        {
            try
            {
                var position =
                    table.GetPositionFromControl(
                        control);
                var widths =
                    table.GetColumnWidths();

                if (position.Column >= 0 &&
                    position.Column <
                    widths.Length)
                {
                    var span =
                        Math.Max(
                            1,
                            table.GetColumnSpan(
                                control));
                    var end =
                        Math.Min(
                            widths.Length,
                            position.Column +
                            span);

                    available =
                        0;

                    for (var column =
                             position.Column;
                         column <
                         end;
                         column++)
                    {
                        available +=
                            widths[column];
                    }

                    available -=
                        control.Margin.Horizontal;
                }
            }
            catch
            {
                // Fall back to the parent client width.
            }
        }
        else
        {
            available -=
                parent.Padding.Horizontal;
        }

        var parentAvailable =
            Math.Max(
                0,
                parent.ClientSize.Width -
                parent.Padding.Horizontal -
                control.Margin.Horizontal);

        if (parentAvailable > 0)
        {
            available =
                Math.Min(
                    available,
                    parentAvailable);
        }

        return Math.Max(
            0,
            available);
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
