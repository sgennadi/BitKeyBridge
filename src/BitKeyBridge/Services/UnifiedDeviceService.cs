namespace BitKeyBridge;

public sealed class UnifiedDeviceService
{
    private readonly ActiveDirectoryService _ad;

    public UnifiedDeviceService(
        AppConfig? config = null)
    {
        _ad =
            new ActiveDirectoryService(
                config);
    }

    public async Task<List<UnifiedDeviceInfo>> SearchAdOnlyAsync(
        string query,
        int maximumItems = 250,
        CancellationToken ct = default)
    {
        query =
            query?.Trim() ??
            string.Empty;

        var dc =
            _ad.GetPreferredWritableDc();

        var rows =
            await Task.Run(
                () =>
                    _ad.SearchComputers(
                        dc,
                        query,
                        maximumItems),
                ct);

        // The same source-qualified identity rules also apply when
        // Microsoft Graph is disabled or not authenticated.
        var devices = UnifiedDeviceMerge.Merge(rows, [], [])
            .Values
            .OrderBy(x => x.ComputerName,
                StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumItems, 1, 1000))
            .ToList();

        // This inventory search does not inspect AD BitLocker key objects.
        // Zero cloud RecoveryIds cannot mean the computer has no key.
        foreach (var device in devices)
            device.RecoveryMetadataIncomplete = true;
        return devices;
    }

    // Return typed status for AD-only search, including the case where AD
    // is unreachable. Cloud is explicitly NOT attempted in this search.
    public async Task<UnifiedDeviceSearchReport> SearchAdOnlyWithStatusAsync(
        string query, int maximumItems = 250, CancellationToken ct = default)
    {
        query = query?.Trim() ?? string.Empty;
        maximumItems = Math.Clamp(maximumItems, 1, 1000);
        var ad = await MetadataSourceIsolation.ExecuteAsync(
            () => Task.Run(() =>
            {
                var dc = _ad.GetPreferredWritableDc();
                return _ad.SearchComputers(dc, query, maximumItems);
            }, ct), ct);
        ct.ThrowIfCancellationRequested();
        var intune = new MetadataLookupResult<ManagedDeviceInfo>([], null);
        var entra = new MetadataLookupResult<CloudRecoveryMetadata>([], null);
        var result = UnifiedDeviceSearchReport.Combine(
            ad, intune, entra, maximumItems, cloudAttempted: false);
        result.SourceState = new UnifiedSourceRetryState(
            query, maximumItems, false, ad, intune, entra);
        return result;
    }

    public async Task<List<UnifiedDeviceInfo>> SearchAsync(
        string accessToken,
        string query,
        int maximumItems = 250,
        bool includeActiveDirectory = true,
        CancellationToken ct = default)
    {
        var report = await SearchWithStatusAsync(
            accessToken, query, maximumItems, includeActiveDirectory, ct);
        return report.Rows;
    }

    /// <summary>
    /// AD, managed-device and Entra BitLocker metadata queries run
    /// independently. One failed source never masks another source's results.
    /// A result with 0 keys and an incomplete lookup is explicitly unverified.
    /// </summary>
    public async Task<UnifiedDeviceSearchReport> SearchWithStatusAsync(
        string accessToken,
        string query,
        int maximumItems = 250,
        bool includeActiveDirectory = true,
        CancellationToken ct = default,
        IProgress<UnifiedDeviceSearchProgress>? progress = null)
    {
        query = query?.Trim() ?? string.Empty;
        maximumItems = Math.Clamp(maximumItems, 1, 1000);

        Task<MetadataLookupResult<AdComputerInfo>>? adTask = null;
        if (includeActiveDirectory)
        {
            adTask = MetadataSourceIsolation.ExecuteAsync(
                () => Task.Run(() =>
                {
                    var dc = _ad.GetPreferredWritableDc();
                    return _ad.SearchComputers(dc, query, maximumItems);
                }, ct),
                ct);
        }

        using var graph = new CloudGraphService();
        var intuneTask = MetadataSourceIsolation.ExecuteWithTransientRetryAsync(
            () => graph.SearchManagedDevicesAsync(
                accessToken, query, maximumItems, ct),
            ct);
        var entraTask = MetadataSourceIsolation.ExecuteWithTransientRetryAsync(
            () => graph.SearchAsync(accessToken, query, ct),
            ct);

        var pending = new List<Task> { intuneTask, entraTask };
        if (adTask is not null)
            pending.Add(adTask);
        // Report completed source results immediately instead of hiding AD
        // rows until a large Intune tenant / Entra metadata scan finishes.
        // These are provisional, non-secret, read-only rows until ALL sources
        // complete. The WinForms caller controls rendering and cancellation.
        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            await done;
            pending.Remove(done);
            ct.ThrowIfCancellationRequested();
            if (progress is not null)
            {
                var adRows = adTask?.IsCompletedSuccessfully == true
                    ? adTask.Result.Rows : null;
                var intuneRows = intuneTask.IsCompletedSuccessfully
                    ? intuneTask.Result.Rows : [];
                var entraRows = entraTask.IsCompletedSuccessfully
                    ? entraTask.Result.Rows : [];
                var preview = UnifiedDeviceMerge.Merge(adRows, intuneRows, entraRows)
                    .Values
                    .OrderBy(x => x.ComputerName,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(maximumItems).ToList();
                static string State<T>(Task<MetadataLookupResult<T>>? task) =>
                    task is null ? "not connected" :
                    !task.IsCompleted ? "searching" :
                    task.Result.Succeeded ? "OK" : "failed";
                var stage = "AD: " + State(adTask) +
                    " | Intune: " + State(intuneTask) +
                    " | Entra: " + State(entraTask) +
                    ". " + preview.Count + " provisional record(s); " +
                    "waiting for remaining metadata.";
                progress.Report(new UnifiedDeviceSearchProgress(stage, preview));
            }
        }
        ct.ThrowIfCancellationRequested();

        var ad = adTask is null ? null : await adTask;
        var intune = await intuneTask;
        var entra = await entraTask;

        var report = UnifiedDeviceSearchReport.Combine(
            ad, intune, entra, maximumItems);
        // Original responses live only as long as this in-memory result.
        // Never persist source rows or recovery IDs into a new cache.
        report.SourceState = new UnifiedSourceRetryState(
            query, maximumItems, true, ad, intune, entra);

        if (progress is not null)
            progress.Report(new UnifiedDeviceSearchProgress(
                "Source metadata returned: " + report.Rows.Count +
                " device(s). Checking source-qualified BitLocker IDs; " +
                "no passwords are being read.", report.Rows));

        // A failed recovery-metadata query must NOT fall back to fetching up
        // to 100 keys individually. This avoids 403/429 storms and falsely
        // treating inaccessible objects as devices with no recovery keys.
        if (entra.Succeeded)
        {
            var candidates = report.Rows
                .Where(row =>
                    row.FoundInIntune &&
                    row.IdentityStatus is not ("Name collision" or "Ambiguous device ID") &&
                    !string.IsNullOrWhiteSpace(row.EntraDeviceId) &&
                    row.RecoveryIds.Count == 0)
                .ToList();
            // Avoid up to 100 per-device Graph requests on broad searches.
            // A key not checked is UNKNOWN, never "not backed up".
            const int maxEnrichment = 32;
            var missing = candidates.Take(maxEnrichment).ToList();
            foreach (var deferred in candidates.Skip(maxEnrichment))
                deferred.RecoveryMetadataIncomplete = true;
            report.RecoveryEnrichmentDeferred =
                Math.Max(0, candidates.Count - maxEnrichment);
            if (report.RecoveryEnrichmentDeferred > 0)
                progress?.Report(new UnifiedDeviceSearchProgress(
                    "Checking first " + maxEnrichment +
                    " exact Entra device IDs. " +
                    report.RecoveryEnrichmentDeferred +
                    " additional devices need a narrower search; " +
                    "unqueried backup state remains unverified.",
                    report.Rows));

            using var gate = new SemaphoreSlim(6);
            var tasksToEnrich = missing.Select(async row =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var keys = await graph.GetRecoveryMetadataForDeviceAsync(
                        accessToken, row.EntraDeviceId, row.ComputerName, ct);
                    var inconsistentDeviceId = false;
                    lock (row.RecoveryIds)
                    {
                        foreach (var key in keys)
                            if (!UnifiedDeviceMerge.TryAttachExactEntraRecoveryMetadata(
                                    row, key))
                                inconsistentDeviceId = true;
                    }
                    if (inconsistentDeviceId)
                    {
                        row.RecoveryMetadataIncomplete = true;
                        Interlocked.Increment(
                            ref report.RecoveryEnrichmentFailures);
                        WindowsEventLogService.TryWrite(
                            "Unified Devices Entra enrichment returned an " +
                            "unmatched or invalid device identity.",
                            EventLogSeverity.Warning, 4578, "Devices");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    row.RecoveryMetadataIncomplete = true;
                    Interlocked.Increment(ref report.RecoveryEnrichmentFailures);
                    WindowsEventLogService.TryWrite(
                        "Unified Devices Entra metadata lookup failed: " +
                        DiagnosticRedaction.Sanitize(ex.Message),
                        EventLogSeverity.Warning, 4578, "Devices");
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(tasksToEnrich);
        }

        if (ad is { Succeeded: false })
            WindowsEventLogService.TryWrite(
                "Unified Devices AD lookup incomplete: " + ad.Error,
                EventLogSeverity.Warning, 4576, "Devices");
        if (!intune.Succeeded)
            WindowsEventLogService.TryWrite(
                "Unified Devices Intune lookup incomplete: " + intune.Error,
                EventLogSeverity.Warning, 4576, "Devices");
        if (!entra.Succeeded)
            WindowsEventLogService.TryWrite(
                "Unified Devices Entra BitLocker metadata incomplete: " + entra.Error,
                EventLogSeverity.Warning, 4576, "Devices");

        return report;
    }

    // Explicit operator action: retry ONLY a failed metadata source using
    // the existing source session and original query. No MFA, key reveal,
    // password read, rotation or fallback to another source.
    public async Task<UnifiedDeviceSearchReport> RetryFailedSourceAsync(
        UnifiedDeviceSearchReport previous, UnifiedMetadataSource source,
        string accessToken, CancellationToken ct = default)
    {
        if (!UnifiedSourceRetry.CanRetry(previous, source))
            throw new InvalidOperationException(
                "This source is not eligible for a targeted metadata retry.");
        var state = previous.SourceState!;
        ct.ThrowIfCancellationRequested();
        if (source == UnifiedMetadataSource.Ad)
        {
            var ad = await MetadataSourceIsolation.ExecuteAsync(
                () => Task.Run(() =>
                {
                    var dc = _ad.GetPreferredWritableDc();
                    return _ad.SearchComputers(dc, state.Query, state.MaximumItems);
                }, ct), ct);
            ct.ThrowIfCancellationRequested();
            return UnifiedSourceRetry.Apply(previous, source, ad: ad);
        }

        if (!state.CloudAttempted || string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException(
                "An active authenticated Microsoft 365 session is required.");
        using var graph = new CloudGraphService();
        if (source == UnifiedMetadataSource.Entra)
        {
            var entra = await MetadataSourceIsolation.ExecuteWithTransientRetryAsync(
                () => graph.SearchAsync(accessToken, state.Query, ct), ct);
            ct.ThrowIfCancellationRequested();
            return UnifiedSourceRetry.Apply(previous, source, entra: entra);
        }
        var intune = await MetadataSourceIsolation.ExecuteWithTransientRetryAsync(
            () => graph.SearchManagedDevicesAsync(
                accessToken, state.Query, state.MaximumItems, ct), ct);
        ct.ThrowIfCancellationRequested();
        return UnifiedSourceRetry.Apply(previous, source, intune: intune);
    }
}

/// <summary>
/// A provisional inventory preview from independently completed metadata
/// sources. It never contains a BitLocker/LAPS secret or an authorization.
/// </summary>
public sealed record UnifiedDeviceSearchProgress(
    string Message, List<UnifiedDeviceInfo> Rows);

/// <summary>
/// Non-secret per-source status accompanying a unified Devices search.
/// Source failure is never interpreted as a confirmed missing backup.
/// </summary>
public sealed class UnifiedDeviceSearchReport
{
    public List<UnifiedDeviceInfo> Rows { get; init; } = [];
    public bool AdAttempted { get; init; }
    public bool AdSucceeded { get; init; }
    public bool IntuneAttempted { get; init; } = true;
    public bool EntraAttempted { get; init; } = true;
    public bool IntuneSucceeded { get; init; }
    public bool EntraSucceeded { get; init; }
    // Metadata results and retry eligibility; this must never be serialized.
    internal UnifiedSourceRetryState? SourceState { get; set; }
    public string AdError { get; init; } = string.Empty;
    public string IntuneError { get; init; } = string.Empty;
    public string EntraError { get; init; } = string.Empty;
    // Typed, secret-free failure reasons. Never interpret arbitrary Graph
    // response text or a missing value as an authorization decision.
    public MetadataFailureKind AdFailureKind { get; init; }
    public MetadataFailureKind EntraFailureKind { get; init; }
    public MetadataFailureKind IntuneFailureKind { get; init; }
    public int RecoveryEnrichmentFailures;
    public int RecoveryEnrichmentDeferred;

    public bool IsPartial =>
        (AdAttempted && !AdSucceeded) ||
        (IntuneAttempted && !IntuneSucceeded) ||
        (EntraAttempted && !EntraSucceeded) ||
        RecoveryEnrichmentFailures > 0 ||
        RecoveryEnrichmentDeferred > 0;

    public string SourceSummary =>
        "AD: " + (!AdAttempted ? "not connected" : AdSucceeded ? "OK" : "failed") +
        " | Intune: " + (!IntuneAttempted ? "not connected" : IntuneSucceeded ? "OK" : "failed") +
        " | Entra BitLocker: " + (!EntraAttempted ? "not connected" : EntraSucceeded ? "OK" : "failed") +
        (RecoveryEnrichmentFailures > 0
            ? $" | Entra key metadata errors: {RecoveryEnrichmentFailures}"
            : string.Empty) +
        (RecoveryEnrichmentDeferred > 0
            ? $" | Entra key metadata deferred: {RecoveryEnrichmentDeferred}"
            : string.Empty);

    public string DiagnosticSummary =>
        string.Join(Environment.NewLine, new[]
        {
            AdAttempted && !AdSucceeded ? "AD: " + AdError : string.Empty,
            IntuneAttempted && !IntuneSucceeded ? "Intune: " + IntuneError : string.Empty,
            EntraAttempted && !EntraSucceeded ? "Entra BitLocker metadata: " + EntraError : string.Empty,
            RecoveryEnrichmentFailures > 0
                ? $"Additional per-device recovery metadata lookups failed: {RecoveryEnrichmentFailures}."
                : string.Empty,
            RecoveryEnrichmentDeferred > 0
                ? $"Entra recovery metadata not checked for {RecoveryEnrichmentDeferred} device(s). " +
                  "Search by exact device name or ID to verify them."
                : string.Empty
        }.Where(text => !string.IsNullOrWhiteSpace(text)));

    public static UnifiedDeviceSearchReport Combine(
        MetadataLookupResult<AdComputerInfo>? ad,
        MetadataLookupResult<ManagedDeviceInfo> intune,
        MetadataLookupResult<CloudRecoveryMetadata> entra,
        int maximumItems = 250, bool cloudAttempted = true)
    {
        // Return typed source failures even when every lookup fails.
        // Empty results are not proof that a device or backup is absent.
        var rows = UnifiedDeviceMerge.Merge(
                ad?.Rows, intune.Rows, entra.Rows)
            .Values
            .OrderByDescending(row => row.FoundInIntune)
            .ThenByDescending(row => row.FoundInAd)
            .ThenBy(row => row.ComputerName,
                StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumItems, 1, 1000))
            .ToList();

        foreach (var row in rows)
        {
            // AD-only rows have no Entra identity and were not queried for
            // recovery IDs; an empty collection is not a key-absence proof.
            if (!cloudAttempted || !entra.Succeeded ||
                (row.FoundInAd && !row.FoundInEntra))
                row.RecoveryMetadataIncomplete = true;
        }

        return new UnifiedDeviceSearchReport
        {
            Rows = rows,
            AdAttempted = ad is not null,
            AdSucceeded = ad?.Succeeded == true,
            IntuneAttempted = cloudAttempted,
            EntraAttempted = cloudAttempted,
            IntuneSucceeded = cloudAttempted && intune.Succeeded,
            EntraSucceeded = cloudAttempted && entra.Succeeded,
            AdError = ad?.Error ?? string.Empty,
            IntuneError = cloudAttempted ? intune.Error ?? string.Empty : string.Empty,
            EntraError = cloudAttempted ? entra.Error ?? string.Empty : string.Empty,
            AdFailureKind = ad?.FailureKind ?? MetadataFailureKind.None,
            IntuneFailureKind = intune.FailureKind,
            EntraFailureKind = entra.FailureKind
        };
    }
}
