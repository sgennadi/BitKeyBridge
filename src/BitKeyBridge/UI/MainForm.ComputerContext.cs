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
                // Supersede running work before changing a text field.
                // The old task's finally cannot reset UI after generation advances.
                _homeSearchGeneration++;
                CancelHomeSearch();
                _homeSearchButton.Enabled = _directoryConnected;
                _homeSearchCancelButton.Enabled = false;
                _homeSearchProgress.Visible = false;

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
                _startCancel.Enabled = false;
                _startProgress.Visible = false;
                _recoverySource.Enabled = true;
                _startSelectOu.Enabled =
                    _recoverySource.SelectedIndex == 0 &&
                    !string.IsNullOrWhiteSpace(_startDomainDn);
                _startSearch.Enabled = _recoverySource.SelectedIndex switch
                {
                    1 => File.Exists(_config.OutputCsv),
                    2 => File.Exists(ProtectedRecoveryCacheService.DefaultFile),
                    _ => _startScope is not null
                };
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
