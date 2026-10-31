namespace BitKeyBridge;

public sealed partial class MainForm
{
    // The seven top-level Start statuses may be nested inside different
    // containers (Connections group, source chooser, Search, selection panel).
    // Align their *glyphs* visually, not their borders or whole panels.
    // Draggable recovery panes can move to another column, so positions are
    // grouped by current screen X rather than assuming a fixed parent.
    private UiStatusLabel[] HomeTopLevelStatusLabels() =>
    [
        _homeConnectionStatus,
        _homeAdAccountStatus,
        _homeCloudConnectionStatus,
        _startSetupStatus,
        _homeSearchStatus,
        _homeSelectionSummary,
        _secretLifetimeStatus
    ];

    private void InitializeHomeStatusGlyphAlignment()
    {
        foreach (var label in HomeTopLevelStatusLabels())
            label.VisibleChanged += (_, _) => QueueMainViewportReflow();
    }

    /// <summary>
    /// Run after the responsive workspace and font/DPI layout has settled.
    /// Nearby status icons share one x coordinate, even if some use a border
    /// and others are inline. Far-away draggable columns remain independent.
    /// </summary>
    private void AlignHomeStatusGlyphs()
    {
        var page = _homeWorkspaceTab;
        if (page is null || page.IsDisposed)
            return;

        var tolerance = (int)Math.Ceiling(
            UiStyle.HomeStatusGlyphGroupingDistance *
            EffectiveHomeDpiScale());

        var labels = HomeTopLevelStatusLabels()
            .Where(label => label.Visible && !label.IsDisposed &&
                label.Width > 0 && label.Image is not null)
            .Select(label => new
            {
                Label = label,
                Anchor = HomeGlyphBaseX(page, label)
            })
            .Where(item => item.Anchor.HasValue)
            .OrderBy(item => item.Anchor)
            .ToArray();

        for (var first = 0; first < labels.Length;)
        {
            var last = first + 1;
            var groupStartX = labels[first].Anchor!.Value;
            while (last < labels.Length &&
                   labels[last].Anchor!.Value - groupStartX <= tolerance)
                last++;

            // Only shift to the right. Status text remains inside its own
            // control; borders remain aligned to their original containers.
            var iconColumnX = labels[last - 1].Anchor!.Value;
            for (var i = first; i < last; i++)
            {
                var entry = labels[i];
                entry.Label.GlyphColumnInset =
                    iconColumnX - entry.Anchor!.Value;
            }
            first = last;
        }

        // Clear any stale offset when a label is hidden or changes columns.
        foreach (var label in HomeTopLevelStatusLabels())
        {
            if (!labels.Any(entry => ReferenceEquals(entry.Label, label)))
                label.GlyphColumnInset = 0;

            // FlowLayoutPanel retains an AutoSize Label's specified bounds
            // when only our custom glyph inset changes. Reconcile the actual
            // size after alignment so the inset cannot steal caption space.
            if (label.Visible && label.AutoSize &&
                label.Dock == DockStyle.None &&
                label.Parent is FlowLayoutPanel flow)
            {
                var available = Math.Max(1,
                    flow.ClientSize.Width - flow.Padding.Horizontal -
                    label.Margin.Horizontal);
                var preferred = label.GetPreferredSize(new Size(available, 0));
                if (label.Size != preferred)
                    label.Size = preferred;
            }
        }
    }

    /// <summary>Page-relative X of a label's glyph without visual alignment.</summary>
    private static int? HomeGlyphBaseX(Control page, UiStatusLabel label)
    {
        var x = label.GlyphBaseLeftInset;
        for (Control? current = label; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, page))
                return x;
            x += current.Left;
        }
        return null;
    }

    // Offline regression assertions use the actual painted column.
    internal static int? HomeGlyphPaintedX(Control page, UiStatusLabel label)
    {
        var x = HomeGlyphBaseX(page, label);
        return x + label.GlyphColumnInset;
    }
}
