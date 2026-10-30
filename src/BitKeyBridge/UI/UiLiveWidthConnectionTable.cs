namespace BitKeyBridge;

/// <summary>
/// Auto-sized one-column connection table measured at the live parent-cell
/// width rather than a provisional 200px WinForms preferred-size width.
/// The stock TableLayoutPanel can preserve phantom wrapped row heights after
/// expanding and collapsing optional Start connection controls.
/// Security, identity selection and secrets are not touched by this layout.
/// </summary>
internal sealed class UiLiveWidthConnectionTable : TableLayoutPanel
{
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Parent is not { IsDisposed: false } parent ||
            !Visible || ColumnCount != 1 || RowCount <= 0)
            return base.GetPreferredSize(proposedSize);

        // The direct parent of HomeAdConnectionLayout has two percentage
        // columns on wide screens and one column on compact/RDP screens.
        // Never measure the left connection pane at the *entire* split width.
        var parentWidth = parent.ClientSize.Width - parent.Padding.Horizontal;
        if (parent is TableLayoutPanel table && table.ColumnCount > 1)
        {
            var col = table.GetColumn(this);
            if (col < 0 || col >= table.ColumnStyles.Count)
                return base.GetPreferredSize(proposedSize);

            var percentTotal = table.ColumnStyles.Cast<ColumnStyle>()
                .Where(style => style.SizeType == SizeType.Percent)
                .Sum(style => style.Width);
            var current = table.ColumnStyles[col];
            if (current.SizeType == SizeType.Percent && percentTotal > 0)
                parentWidth = (int)Math.Floor(parentWidth *
                    current.Width / percentTotal);
            else
                return base.GetPreferredSize(proposedSize);
        }

        // Before native layout, do not synthesize a height from an unknown
        // width. Once visible, parent.ClientSize is authoritative.
        var availableWidth = parentWidth - Margin.Horizontal;
        // Our outer connection table passes its computed live cell width
        // directly to this nested chooser. During the same layout pass,
        // Parent.ClientSize can still be the previous provisional 200px:
        // never throw away the wider, explicit measurement proposal.
        if (parent is UiLiveWidthConnectionTable && proposedSize.Width > 0)
            availableWidth = Math.Max(availableWidth, proposedSize.Width);
        if (availableWidth <= 0)
            return base.GetPreferredSize(proposedSize);

        var innerWidth = Math.Max(1, availableWidth - Padding.Horizontal);
        var height = Padding.Vertical;
        for (var row = 0; row < RowCount; row++)
        {
            var rowStyle = row < RowStyles.Count ? RowStyles[row] : null;
            if (rowStyle?.SizeType == SizeType.Absolute)
            {
                height += (int)Math.Ceiling(rowStyle.Height);
                continue;
            }

            if (rowStyle?.SizeType == SizeType.Percent)
                return base.GetPreferredSize(proposedSize);

            var child = GetControlFromPosition(0, row);
            if (child is null || !child.Visible)
                continue;

            var childWidth = Math.Max(1, innerWidth - child.Margin.Horizontal);
            var preferred = child.GetPreferredSize(new Size(childWidth, 0));
            height += Math.Max(child.MinimumSize.Height, preferred.Height) +
                child.Margin.Vertical;
        }

        return new Size(
            Math.Max(1, availableWidth),
            Math.Max(1, height));
    }
}
