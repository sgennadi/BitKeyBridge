namespace BitKeyBridge;

public sealed partial class MainForm
{
    private bool _mainViewportReflowQueued;
    private bool _mainViewportReflowRunning;
    private bool _homeResultsReflowRunning;
    private string _homeResultsLayoutKey = string.Empty;

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


    /// <summary>
    /// Keep selected recovery cards visible without scrolling through an
    /// oversized results grid. On a wide monitor the recovery cards are
    /// on the left and the computer list on the right. On narrow/RDP layouts
    /// the list stays above the cards so another computer remains easy to select.
    /// </summary>
    private void ReflowHomeResultsArea()
    {
        if (_homeResultsReflowRunning || _homeResultsArea.IsDisposed ||
            _homeResultsArea.Parent is null)
            return;

        var available = Math.Max(0, _homeResultsArea.Parent.ClientSize.Width -
            _homeResultsArea.Parent.Padding.Horizontal);
        var required = (int)Math.Ceiling(
            UiStyle.HomeResultsSideBySideMinimumWidth *
            DeviceDpi / (double)UiStyle.BaselineDpi);
        bool sideBySide = available >= required &&
            GetSelectedHomeSearchResult() is not null;
        var key = sideBySide ? "SideBySide" : "Stacked";
        if (_homeResultsLayoutKey == key)
        {
            ArrangeHomeServiceCards();
            return;
        }

        _homeResultsReflowRunning = true;
        _homeResultsArea.SuspendLayout();
        try
        {
            _homeResultsArea.Controls.Clear();
            _homeResultsArea.ColumnStyles.Clear();
            _homeResultsArea.RowStyles.Clear();
            if (sideBySide)
            {
                _homeResultsArea.ColumnCount = 2;
                _homeResultsArea.RowCount = 1;
                // Recovery is the primary helpdesk task; use the wider
                // left column for BitLocker and LAPS side-by-side cards.
                // The compact computer picker occupies the right column.
                _homeResultsArea.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent,
                        UiStyle.HomeRecoveryColumnPercent));
                _homeResultsArea.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent,
                        100F - UiStyle.HomeRecoveryColumnPercent));
                _homeResultsArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _homeResultsArea.Controls.Add(_homeSelectionPanel, 0, 0);
                _homeResultsArea.Controls.Add(_homeSearchResults, 1, 0);
            }
            else
            {
                _homeResultsArea.ColumnCount = 1;
                _homeResultsArea.RowCount = 2;
                _homeResultsArea.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100F));
                _homeResultsArea.RowStyles.Add(
                    new RowStyle(SizeType.Absolute,
                        UiStyle.HomeCompactSearchResultsHeight *
                        DeviceDpi / (float)UiStyle.BaselineDpi));
                _homeResultsArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _homeResultsArea.Controls.Add(_homeSearchResults, 0, 0);
                _homeResultsArea.Controls.Add(_homeSelectionPanel, 0, 1);
            }

            _homeSearchResults.Dock = DockStyle.Fill;
            _homeSelectionPanel.Dock = DockStyle.Top;
            _homeResultsLayoutKey = key;
        }
        finally
        {
            _homeResultsArea.ResumeLayout(true);
            _homeResultsReflowRunning = false;
        }

        _homeSelectionPanel.PerformLayout();
        _homeResultsArea.PerformLayout();
        ArrangeHomeServiceCards();
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

            ReflowHomeResultsArea();
            shell.PerformLayout();
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
