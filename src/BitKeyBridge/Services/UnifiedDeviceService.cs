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
        CancellationToken ct = default)
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
        var intuneTask = MetadataSourceIsolation.ExecuteAsync(
            () => graph.SearchManagedDevicesAsync(
                accessToken, query, maximumItems, ct),
            ct);
        var entraTask = MetadataSourceIsolation.ExecuteAsync(
            () => graph.SearchAsync(accessToken, query, ct),
            ct);

        var tasks = new List<Task> { intuneTask, entraTask };
        if (adTask is not null)
            tasks.Add(adTask);
        await Task.WhenAll(tasks);
        ct.ThrowIfCancellationRequested();

        var ad = adTask is null ? null : await adTask;
        var intune = await intuneTask;
        var entra = await entraTask;

        var report = UnifiedDeviceSearchReport.Combine(
            ad, intune, entra, maximumItems);

        // A failed recovery-metadata query must NOT fall back to fetching up
        // to 100 keys individually. This avoids 403/429 storms and falsely
        // treating inaccessible objects as devices with no recovery keys.
        if (entra.Succeeded)
        {
            var missing = report.Rows
                .Where(row =>
                    row.FoundInIntune &&
                    row.IdentityStatus != "Ambiguous device ID" &&
                    !string.IsNullOrWhiteSpace(row.EntraDeviceId) &&
                    row.RecoveryIds.Count == 0)
                .Take(100)
                .ToList();

            using var gate = new SemaphoreSlim(6);
            var tasksToEnrich = missing.Select(async row =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var keys = await graph.GetRecoveryMetadataForDeviceAsync(
                        accessToken, row.EntraDeviceId, row.ComputerName, ct);
                    lock (row.RecoveryIds)
                    {
                        foreach (var key in keys)
                            UnifiedDeviceMerge.AddRecoveryId(
                                row, key.RecoveryId);
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
}

/// <summary>
/// Non-secret per-source status accompanying a unified Devices search.
/// Source failure is never interpreted as a confirmed missing backup.
/// </summary>
public sealed class UnifiedDeviceSearchReport
{
    public List<UnifiedDeviceInfo> Rows { get; init; } = [];
    public bool AdAttempted { get; init; }
    public bool AdSucceeded { get; init; }
    public bool IntuneSucceeded { get; init; }
    public bool EntraSucceeded { get; init; }
    public string AdError { get; init; } = string.Empty;
    public string IntuneError { get; init; } = string.Empty;
    public string EntraError { get; init; } = string.Empty;
    public int RecoveryEnrichmentFailures;

    public bool IsPartial =>
        (AdAttempted && !AdSucceeded) ||
        !IntuneSucceeded ||
        !EntraSucceeded ||
        RecoveryEnrichmentFailures > 0;

    public string SourceSummary =>
        "AD: " + (!AdAttempted ? "not connected" : AdSucceeded ? "OK" : "failed") +
        " | Intune: " + (IntuneSucceeded ? "OK" : "failed") +
        " | Entra BitLocker: " + (EntraSucceeded ? "OK" : "failed") +
        (RecoveryEnrichmentFailures > 0
            ? $" | Entra key metadata errors: {RecoveryEnrichmentFailures}"
            : string.Empty);

    public string DiagnosticSummary =>
        string.Join(Environment.NewLine, new[]
        {
            AdAttempted && !AdSucceeded ? "AD: " + AdError : string.Empty,
            !IntuneSucceeded ? "Intune: " + IntuneError : string.Empty,
            !EntraSucceeded ? "Entra BitLocker metadata: " + EntraError : string.Empty,
            RecoveryEnrichmentFailures > 0
                ? $"Additional per-device recovery metadata lookups failed: {RecoveryEnrichmentFailures}."
                : string.Empty
        }.Where(text => !string.IsNullOrWhiteSpace(text)));

    public static UnifiedDeviceSearchReport Combine(
        MetadataLookupResult<AdComputerInfo>? ad,
        MetadataLookupResult<ManagedDeviceInfo> intune,
        MetadataLookupResult<CloudRecoveryMetadata> entra,
        int maximumItems = 250)
    {
        if (ad?.Succeeded != true && !intune.Succeeded && !entra.Succeeded)
            throw new InvalidOperationException(
                "All device inventory sources failed. " +
                string.Join("; ", new[]
                {
                    ad is { Succeeded: false } ? "AD: " + ad.Error : string.Empty,
                    "Intune: " + intune.Error,
                    "Entra BitLocker: " + entra.Error
                }.Where(text => !string.IsNullOrWhiteSpace(text))));

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
            if (!entra.Succeeded || (row.FoundInAd && !row.FoundInEntra))
                row.RecoveryMetadataIncomplete = true;
        }

        return new UnifiedDeviceSearchReport
        {
            Rows = rows,
            AdAttempted = ad is not null,
            AdSucceeded = ad?.Succeeded == true,
            IntuneSucceeded = intune.Succeeded,
            EntraSucceeded = entra.Succeeded,
            AdError = ad?.Error ?? string.Empty,
            IntuneError = intune.Error ?? string.Empty,
            EntraError = entra.Error ?? string.Empty
        };
    }
}
