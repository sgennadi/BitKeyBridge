namespace BitKeyBridge;

// Pure on-screen geometry. No control Text, AD/Graph data, credentials or
// selected device metadata is captured in this diagnostic snapshot.
public sealed partial class MainForm
{
    internal readonly record struct StartViewportGeometry(
        int ViewportWidth,
        int ViewportHeight,
        int ConnectionHeight,
        int SearchHeight,
        int SelectionHeight,
        int CardsTop,
        int CardsHeight,
        int CardsVisibleHeight,
        int ScrollOffset,
        int CardColumns,
        int CardRows);

    internal StartViewportGeometry? CaptureStartViewportGeometry()
    {
        if (_homeWorkspaceTab is null ||
            !ReferenceEquals(_mainTabs.SelectedTab, _homeWorkspaceTab) ||
            _homeServiceCards.IsDisposed ||
            _homeServiceCards.Parent is null)
            return null;

        var root = _homeWorkspaceTab.Controls
            .OfType<TableLayoutPanel>()
            .FirstOrDefault(x => x.Name == "HomeLayoutRoot");
        if (root is null || root.IsDisposed ||
            !root.Visible || !root.IsHandleCreated ||
            !_homeServiceCards.Visible || !_homeServiceCards.IsHandleCreated)
            return null;

        var connections = root.Controls.OfType<GroupBox>()
            .FirstOrDefault(x => x.Name == "HomeConnectionsGroup");
        var search = root.Controls.OfType<TableLayoutPanel>()
            .FirstOrDefault(x => x.Name == "HomeSearchControls");

        // Coordinates are relative to the scrolling Start root, not the
        // screen. Therefore they also work after RDP resize and page scrolling.
        var top = root.PointToClient(
            _homeServiceCards.PointToScreen(Point.Empty)).Y;
        var bottom = top + _homeServiceCards.Height;
        var visible = Math.Max(0,
            Math.Min(root.ClientRectangle.Bottom, bottom) -
            Math.Max(root.ClientRectangle.Top, top));

        return new StartViewportGeometry(
            root.ClientSize.Width,
            root.ClientSize.Height,
            connections?.Height ?? 0,
            search?.Height ?? 0,
            _homeSelectionPanel.Visible ? _homeSelectionPanel.Height : 0,
            top,
            _homeServiceCards.Height,
            visible,
            root.VerticalScroll.Value,
            _homeServiceCards.ColumnCount,
            _homeServiceCards.RowCount);
    }

    internal string StartViewportGeometryReport()
    {
        var snapshot = CaptureStartViewportGeometry();
        if (snapshot is not { } value)
            return "Start viewport (pixels only): not currently visible.";

        return "Start viewport (pixels only): " +
            $"viewport={value.ViewportWidth}x{value.ViewportHeight}; " +
            $"connections={value.ConnectionHeight}; " +
            $"search={value.SearchHeight}; " +
            $"selection={value.SelectionHeight}; " +
            $"cardsTop={value.CardsTop}; " +
            $"cardsHeight={value.CardsHeight}; " +
            $"cardsVisible={value.CardsVisibleHeight}; " +
            $"scrollY={value.ScrollOffset}; " +
            $"grid={value.CardColumns}x{value.CardRows}.";
    }
}
