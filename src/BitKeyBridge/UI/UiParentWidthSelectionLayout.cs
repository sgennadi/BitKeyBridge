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
        if (Parent is { IsDisposed: false } parent &&
            parent.ClientSize.Width > 0)
        {
            var width = Math.Max(1,
                parent.ClientSize.Width - parent.Padding.Horizontal -
                Margin.Horizontal);
            // Use the live parent constraint for preferred-height
            // measurement. This is not a fixed control position or a
            // hard-coded height, and still wraps at narrow/RDP widths.
            proposedSize.Width = width;
        }

        return base.GetPreferredSize(proposedSize);
    }
}
