namespace BitKeyBridge;

/// <summary>
/// A selected-device status bar has to calculate its AutoSize height at the
/// actual row width. WinForms can otherwise ask nested AutoSize tables for a
/// provisional 200px-wide preferred size even after a 1600px viewport is
/// available, leaving multiple phantom wrapped rows above recovery cards.
/// </summary>
internal sealed class UiParentWidthSelectionLayout : TableLayoutPanel
{
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Parent is not { IsDisposed: false } parent ||
            parent.ClientSize.Width <= 0 || Controls.Count == 0)
            return base.GetPreferredSize(proposedSize);

        var availableWidth = Math.Max(1,
            parent.ClientSize.Width - parent.Padding.Horizontal -
            Margin.Horizontal);
        var rowWidth = Math.Max(1, availableWidth - Padding.Horizontal);

        // TableLayoutPanel's default AutoSize path can measure its nested
        // AutoSize FlowLayoutPanel at the original 200px designer width.
        // That produces a false 200+px selection row on a wide Start page.
        // Measure each visible child directly at the current viewport width
        // so the status and action rows return to their intrinsic heights.
        var height = Padding.Vertical;
        for (var index = 0; index < RowCount; index++)
        {
            var child = GetControlFromPosition(0, index);
            if (child is null || !child.Visible)
                continue;

            var childWidth = Math.Max(1, rowWidth - child.Margin.Horizontal);
            var preferred = child.GetPreferredSize(new Size(childWidth, 0));
            height += Math.Max(child.MinimumSize.Height, preferred.Height) +
                child.Margin.Vertical;
        }

        return new Size(availableWidth, Math.Max(1, height));
    }
}
