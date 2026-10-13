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
    /// on either side of the computer list, controlled by a per-user setting.
    /// On narrow/RDP layouts the list stays above the cards for easy selection.
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
        bool recoveryLeft = _startConnectionPreferences.RecoveryCardsOnLeft;
        var key = sideBySide
            ? recoveryLeft ? "RecoveryLeft" : "ComputersLeft"
            : "Stacked";
        UpdateHomePaneDragUi(sideBySide);
        if (_homeResultsLayoutKey == key)
        {
            ArrangeHomeServiceCards();
            return;
        }

        // Reparenting the host panels can recreate the WinForms grid handle.
        // Preserve the computer selection by stable row identity so swapping
        // never replaces an already-selected recovery context.
        var selectedComputer = GetSelectedHomeSearchResult();
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
                // The recovery pane always owns the larger share. The user
                // can swap its side by dragging either labeled pane header.
                var recoveryWidth = UiStyle.HomeRecoveryColumnPercent;
                _homeResultsArea.ColumnStyles.Add(new ColumnStyle(
                    SizeType.Percent, recoveryLeft ? recoveryWidth : 100F - recoveryWidth));
                _homeResultsArea.ColumnStyles.Add(new ColumnStyle(
                    SizeType.Percent, recoveryLeft ? 100F - recoveryWidth : recoveryWidth));
                _homeResultsArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _homeResultsArea.Controls.Add(
                    recoveryLeft ? _homeRecoveryPaneHost : _homeComputersPaneHost, 0, 0);
                _homeResultsArea.Controls.Add(
                    recoveryLeft ? _homeComputersPaneHost : _homeRecoveryPaneHost, 1, 0);
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
                _homeResultsArea.Controls.Add(_homeComputersPaneHost, 0, 0);
                _homeResultsArea.Controls.Add(_homeRecoveryPaneHost, 0, 1);
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

        if (selectedComputer is not null &&
            GetSelectedHomeSearchResult() is null)
        {
            var selectedRow = _homeSearchResults.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(row => ReferenceEquals(row.Tag, selectedComputer));
            if (selectedRow is not null)
            {
                _homeSearchResults.ClearSelection();
                selectedRow.Selected = true;
                if (selectedRow.Cells.Count > 0)
                    _homeSearchResults.CurrentCell = selectedRow.Cells[0];
            }
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
