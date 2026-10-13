namespace BitKeyBridge;

public sealed partial class MainForm
{
    private bool _mainViewportReflowQueued;
    private bool _mainViewportReflowRunning;
    private bool _homeResultsReflowRunning;
    private string _homeResultsLayoutKey = string.Empty;
    private string _homeConnectionsRecentLayoutKey = string.Empty;
    private bool _homeConnectionsRecentReflowRunning;

    private void InitializeMainViewport()
    {
        AutoScroll = false;
        ClientSizeChanged += (_, _) => QueueMainViewportReflow();
        Shown += (_, _) => QueueMainViewportReflow();
    }

    private void QueueMainViewportReflow()
    {
        if (_mainViewportReflowQueued || !IsHandleCreated || IsDisposed)
            return;

        _mainViewportReflowQueued = true;
        BeginInvoke(new MethodInvoker(() =>
        {
            _mainViewportReflowQueued = false;
            if (!IsDisposed)
                ReflowMainViewport();
        }));
    }


    private double EffectiveHomeDpiScale() =>
        Math.Max(DeviceDpi / (double)UiStyle.BaselineDpi,
            Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));

    private void ReflowConnectionsRecent()
    {
        var layout = _homeConnectionsRecentLayout;
        var connection = _homeAdConnectionLayout;
        var recent = _homeRecentQuickPanel;
        if (_homeConnectionsRecentReflowRunning || layout is null ||
            connection is null || recent is null || layout.IsDisposed ||
            layout.Parent is null)
            return;

        var width = Math.Max(0,
            layout.Parent.ClientSize.Width - layout.Parent.Padding.Horizontal);
        bool horizontal = width >= (int)Math.Ceiling(
            UiStyle.HomeConnectionRecentPairMinimumWidth * EffectiveHomeDpiScale());
        var key = horizontal ? "ConnectionsRecentWide" : "ConnectionsRecentStacked";
        if (_homeConnectionsRecentLayoutKey == key)
            return;

        _homeConnectionsRecentReflowRunning = true;
        layout.SuspendLayout();
        try
        {
            layout.Controls.Clear();
            layout.ColumnStyles.Clear();
            layout.RowStyles.Clear();
            if (horizontal)
            {
                layout.ColumnCount = 2;
                layout.RowCount = 1;
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(connection, 0, 0);
                layout.Controls.Add(recent, 1, 0);
            }
            else
            {
                layout.ColumnCount = 1;
                layout.RowCount = 2;
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(connection, 0, 0);
                layout.Controls.Add(recent, 0, 1);
            }
            _homeConnectionsRecentLayoutKey = key;
        }
        finally
        {
            layout.ResumeLayout(true);
            _homeConnectionsRecentReflowRunning = false;
        }
    }

    /// <summary>
    /// Three equal-size draggable panes. The main form owns the height,
    /// not a data grid with a percentage row that hides recovery actions.
    /// At high DPI or small RDP widths the panes stack at equal heights.
    /// </summary>
    private void ReflowHomeResultsArea()
    {
        if (_homeResultsReflowRunning || _homeServiceCards.IsDisposed ||
            _homeServiceCards.Parent is null)
            return;

        var width = _homeResultsArea.Parent?.ClientSize.Width ?? 0;
        if (width <= 0)
            width = _homeResultsArea.ClientSize.Width;
        width -= _homeResultsArea.Parent?.Padding.Horizontal ?? 0;
        var effectiveScale = EffectiveHomeDpiScale();
        var scaledWidth = (int)Math.Ceiling(
            UiStyle.HomeThreePaneMinimumWidth * effectiveScale);
        var scaledHeight = (int)Math.Ceiling(
            UiStyle.HomeThreePaneMinimumHeight * effectiveScale);
        bool threeColumns = width >= scaledWidth;
        UpdateHomePaneDragUi(threeColumns);

        var desiredKey = (threeColumns ? "3cols:" : "3rows:") +
            string.Join(",", _homePaneOrder) + ":" + DeviceDpi;
        if (_homeResultsLayoutKey == desiredKey)
            return;

        _homeResultsReflowRunning = true;
        _homeServiceCards.SuspendLayout();
        try
        {
            // On a simple drag/reorder, move only cell positions. Existing
            // card and DataGridView handles (including secret/history state)
            // stay intact. No AD or Graph call occurs.
            if (threeColumns && _homeServiceCards.ColumnCount == 3 &&
                _homeServiceCards.RowCount == 1 &&
                _homePaneOrder.All(pane =>
                    _homeServiceCards.Controls.Contains(HomePaneHost(pane))))
            {
                for (var i = 0; i < _homePaneOrder.Length; i++)
                    _homeServiceCards.SetCellPosition(
                        HomePaneHost(_homePaneOrder[i]),
                        new TableLayoutPanelCellPosition(i, 0));
                _homeServiceCards.MinimumSize =
                    new Size(0, scaledHeight);
            }
            else
            {
                var selectedComputer = GetSelectedHomeSearchResult();
                _homeServiceCards.Controls.Clear();
                _homeServiceCards.ColumnStyles.Clear();
                _homeServiceCards.RowStyles.Clear();

                if (threeColumns)
                {
                    _homeServiceCards.ColumnCount = 3;
                    _homeServiceCards.RowCount = 1;
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.ColumnStyles.Add(
                            new ColumnStyle(SizeType.Percent, 100F / 3F));
                    _homeServiceCards.RowStyles.Add(
                        new RowStyle(SizeType.Percent, 100F));
                    _homeServiceCards.MinimumSize =
                        new Size(0, scaledHeight);
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.Controls.Add(
                            HomePaneHost(_homePaneOrder[i]), i, 0);
                }
                else
                {
                    _homeServiceCards.ColumnCount = 1;
                    _homeServiceCards.RowCount = 3;
                    _homeServiceCards.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100F));
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.RowStyles.Add(
                            new RowStyle(SizeType.Absolute, scaledHeight));
                    _homeServiceCards.MinimumSize =
                        new Size(0, scaledHeight * 3);
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.Controls.Add(
                            HomePaneHost(_homePaneOrder[i]), 0, i);
                }

                if (selectedComputer is not null &&
                    GetSelectedHomeSearchResult() is null)
                {
                    var row = _homeSearchResults.Rows
                        .Cast<DataGridViewRow>()
                        .FirstOrDefault(item =>
                            ReferenceEquals(item.Tag, selectedComputer));
                    if (row is not null)
                    {
                        _homeSearchResults.ClearSelection();
                        row.Selected = true;
                        if (row.Cells.Count > 0)
                            _homeSearchResults.CurrentCell = row.Cells[0];
                    }
                }
            }
            _homeResultsLayoutKey = desiredKey;
        }
        finally
        {
            _homeServiceCards.ResumeLayout(true);
            _homeResultsReflowRunning = false;
        }
        _homeServiceCards.PerformLayout();
    }

    internal void ReflowMainViewport()
    {
        if (_mainViewportReflowRunning || IsDisposed || ClientSize.Width == 0)
            return;

        _mainViewportReflowRunning = true;
        try
        {
            AutoScroll = false;
            var shell = Controls.OfType<TableLayoutPanel>()
                .FirstOrDefault(x => x.Name == "MainLayoutRoot");
            if (shell is null)
                return;

            // Do not let a pre-maximize preferred width constrain the entire
            // application to the top-left of a 4K / Remote Desktop screen.
            shell.Dock = DockStyle.Fill;
            if (shell.Bounds != ClientRectangle)
                shell.Bounds = ClientRectangle;

            shell.PerformLayout();
            ReflowConnectionsRecent();
            shell.PerformLayout();
            ReflowHomeResultsArea();
            _mainTabs.Dock = DockStyle.Fill;
            _mainTabs.PerformLayout();

            if (_mainTabs.SelectedTab is TabPage page)
            {
                page.PerformLayout();
                if (page.Controls.Count > 0 &&
                    page.Controls[0].Dock == DockStyle.Fill)
                {
                    var content = page.Controls[0];
                    if (content.Bounds != page.DisplayRectangle)
                        content.Bounds = page.DisplayRectangle;
                    content.PerformLayout();
                }
            }

            UiModernTheme.Refresh(shell);
        }
        finally
        {
            _mainViewportReflowRunning = false;
        }
    }
}
