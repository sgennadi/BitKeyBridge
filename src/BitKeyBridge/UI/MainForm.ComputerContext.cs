namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Share only non-secret computer names and search targets.
    private string _currentComputerQuery = string.Empty;
    private bool _applyingComputerQuery;
    private bool _updatingHomeSearchResults;

    private void RememberComputerQuery(string? query)
    {
        if (_applyingComputerQuery)
            return;

        _currentComputerQuery = (query ?? string.Empty).Trim();
    }

    private void ApplyComputerQueryToSelectedWorkspace()
    {
        TextBox? target =
            ReferenceEquals(_mainTabs.SelectedTab, _homeWorkspaceTab)
                ? _homeQuery
                : ReferenceEquals(_mainTabs.SelectedTab, _recoveryWorkspaceTab)
                    ? _startQuery
                    : ReferenceEquals(_mainTabs.SelectedTab, _lapsWorkspaceTab)
                        ? _lapsQuery
                        : null;

        if (target is null || target.IsDisposed ||
            string.Equals(target.Text, _currentComputerQuery,
                StringComparison.Ordinal))
        {
            return;
        }

        _applyingComputerQuery = true;
        try
        {
            if (ReferenceEquals(target, _homeQuery))
            {
                _homeSearchGeneration++;
                CancelHomeSearch();
                _updatingHomeSearchResults = true;
                try
                {
                    _homeSearchResults.Rows.Clear();
                }
                finally
                {
                    _updatingHomeSearchResults = false;
                }

                UpdateHomeSearchSelection();
                UiStyle.SetStatus(
                    _homeSearchStatus,
                    string.IsNullOrWhiteSpace(_currentComputerQuery)
                        ? "Enter a computer name to search."
                        : "Computer name carried over. Press Search to refresh metadata.",
                    UiStatusKind.Neutral);
            }
            else if (ReferenceEquals(target, _startQuery))
            {
                _startSearchGeneration++;
                _startSearchDebounceTimer?.Stop();
                _startSearchCancellation?.Cancel();
                ResetRecoverySearchState();
            }
            else if (ReferenceEquals(target, _lapsQuery))
            {
                CancelLapsSearch();
                _lapsSearchRows.Rows.Clear();
                // TextChanged clears displayed LAPS secrets and cancels reads.
            }

            target.Text = _currentComputerQuery;
            target.SelectionStart = target.TextLength;
        }
        finally
        {
            _applyingComputerQuery = false;
        }
    }
}
