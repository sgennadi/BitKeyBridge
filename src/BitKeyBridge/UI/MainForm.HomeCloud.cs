namespace BitKeyBridge;

public sealed partial class MainForm
{
    private sealed record HomeCloudLookupStatus(
        int RecordsAdded, bool IsPartial, string SourceDiagnostics,
        string? Failure,
        MetadataFailureKind FailureKind = MetadataFailureKind.None);

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
                0, false, string.Empty, "Cloud session unavailable.",
                MetadataFailureKind.SignInRequired);

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
                DiagnosticRedaction.Sanitize(ex.Message),
                MetadataFailureGuidance.Classify(ex));
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
                    (result.Failure ?? result.SourceDiagnostics) +
                    Environment.NewLine + "Next check: " +
                    MetadataFailureGuidance.NextAction(
                        result.Failure is null
                            ? MetadataFailureKind.Unknown
                            : result.FailureKind, "Cloud"));

            var resultStatus = result.Failure is not null
                ? UiLanguageService.Translate(_uiLanguageCode,
                    "MetadataKind" + result.FailureKind) + ". " +
                  UiLanguageService.Translate(_uiLanguageCode,
                    "MetadataAction" + result.FailureKind) + " " +
                  UiLanguageService.Translate(_uiLanguageCode,
                    "MetadataBackupUnverified")
                : result.RecordsAdded == 0 && !result.IsPartial
                    ? UiLanguageService.Translate(_uiLanguageCode, "MetadataNoCloudRecords")
                    : $"Cloud: {result.RecordsAdded} record(s). " +
                      "Select an exact row for Device 360." +
                      (result.IsPartial ? " Some metadata is unverified." : "");
            UiStyle.SetStatus(
                _homeSearchStatus, resultStatus,
                result.Failure is not null || result.IsPartial ||
                    result.RecordsAdded == 0
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
