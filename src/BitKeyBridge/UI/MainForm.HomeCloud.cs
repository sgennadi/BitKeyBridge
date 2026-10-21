namespace BitKeyBridge;

public sealed partial class MainForm
{
    private sealed record HomeCloudLookupStatus(
        int RecordsAdded, bool IsPartial, string SourceDiagnostics,
        string? Failure);

    private bool HasCloudSearchSession() =>
        _cloudToken is not null &&
        _cloudTokenHasRecovery &&
        _cloudToken.ExpiresAt > DateTime.Now.AddMinutes(1) &&
        string.Equals(_cloudTokenContext, BuildCloudTokenContext(),
            StringComparison.Ordinal);

    private async Task<HomeCloudLookupStatus> AppendHomeCloudRowsAsync(
        string query, int generation, CancellationToken ct)
    {
        if (!HasCloudSearchSession())
            return new HomeCloudLookupStatus(
                0, false, string.Empty, "Cloud session unavailable.");

        try
        {
            var service = new UnifiedDeviceService(_config);
            var report = await service.SearchWithStatusAsync(
                _cloudToken!.AccessToken, query, maximumItems: 100,
                includeActiveDirectory: false, ct: ct);
            ct.ThrowIfCancellationRequested();
            if (generation != _homeSearchGeneration || IsDisposed)
                return new HomeCloudLookupStatus(
                    0, report.IsPartial, report.DiagnosticSummary, null);

            var mapped = HomeCloudSearchMapper.Map(report.Rows);
            _updatingHomeSearchResults = true;
            try
            {
                foreach (var result in mapped)
                {
                    var index = _homeSearchResults.Rows.Add(
                        result.ComputerName, result.SourceSummary,
                        result.BitLockerStatus, (object?)result.LatestBitLockerKeyUtc ?? DBNull.Value,
                        result.LapsStatus, (object?)result.LapsKeyDateUtc ?? DBNull.Value);
                    _homeSearchResults.Rows[index].Tag = result;
                }

                _homeSearchResults.ClearSelection();
                _homeSearchResults.CurrentCell = null;
                if (_homeSearchResults.Rows.Count == 1)
                {
                    _homeSearchResults.Rows[0].Selected = true;
                    _homeSearchResults.CurrentCell =
                        _homeSearchResults.Rows[0].Cells[0];
                }
            }
            finally
            {
                _updatingHomeSearchResults = false;
            }

            return new HomeCloudLookupStatus(
                mapped.Count, report.IsPartial, report.DiagnosticSummary, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            return new HomeCloudLookupStatus(
                0, false, string.Empty,
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private async Task SearchHomeCloudOnlyAsync(string query)
    {
        using var cancellation = new CancellationTokenSource();
        var generation = ++_homeSearchGeneration;
        _homeSearchCancellation = cancellation;
        _homeSearchButton.Enabled = false;
        _homeSearchCancelButton.Enabled = true;
        _homeSearchProgress.Visible = true;
        _homeSearchResults.Rows.Clear();
        UiStyle.SetStatus(
            _homeSearchStatus,
            "Searching Entra/Intune metadata (no AD session)...",
            UiStatusKind.Busy);

        try
        {
            var result = await AppendHomeCloudRowsAsync(
                query, generation, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _homeSearchGeneration || IsDisposed)
                return;

            UpdateHomeSearchSelection();
            if (result.Failure is not null || result.IsPartial)
                _appDiagnostics.ShowMessage(
                    "Cloud metadata search was incomplete.",
                    result.Failure ?? result.SourceDiagnostics);

            UiStyle.SetStatus(
                _homeSearchStatus,
                result.Failure is not null
                    ? "Cloud lookup failed. See diagnostics; no backup absence is proven."
                    : $"Cloud: {result.RecordsAdded} record(s). " +
                      "Select a source-qualified row for Device 360." +
                      (result.IsPartial ? " Some metadata is unverified." : ""),
                result.Failure is not null || result.IsPartial
                    ? UiStatusKind.Warning
                    : UiStatusKind.Success);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && generation == _homeSearchGeneration)
                UiStyle.SetStatus(
                    _homeSearchStatus, "Search canceled.", UiStatusKind.Warning);
        }
        finally
        {
            if (ReferenceEquals(_homeSearchCancellation, cancellation))
                _homeSearchCancellation = null;
            if (!IsDisposed && generation == _homeSearchGeneration)
            {
                _homeSearchButton.Enabled =
                    _directoryConnected || HasCloudSearchSession();
                _homeSearchCancelButton.Enabled = false;
                _homeSearchProgress.Visible = false;
            }
        }
    }
}
