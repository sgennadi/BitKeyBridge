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

        return rows
            .Select(
                ad =>
                    new UnifiedDeviceInfo
                    {
                        ComputerName =
                            ad.ComputerName,
                        IdentityKey =
                            !string.IsNullOrWhiteSpace(ad.ObjectGuid)
                                ? "AD:GUID:" + ad.ObjectGuid
                                : "AD:DN:" + ad.DistinguishedName,
                        IdentityStatus =
                            "AD object",
                        AdObjectGuid =
                            ad.ObjectGuid,
                        AdDnsHostName =
                            ad.DnsHostName,
                        FoundInAd =
                            true,
                        AdDistinguishedName =
                            ad.DistinguishedName,
                        OperatingSystem =
                            ad.OperatingSystem,
                        OsVersion =
                            ad.OperatingSystemVersion,
                        AdLastLogonTimestamp =
                            ad.LastLogonTimestamp
                    })
            .OrderBy(
                x =>
                    x.ComputerName,
                StringComparer.OrdinalIgnoreCase)
            .Take(
                Math.Clamp(
                    maximumItems,
                    1,
                    1000))
            .ToList();
    }

    public async Task<List<UnifiedDeviceInfo>> SearchAsync(
        string accessToken,
        string query,
        int maximumItems = 250,
        bool includeActiveDirectory = true,
        CancellationToken ct = default)
    {
        query =
            query?.Trim() ??
            string.Empty;

        // Cloud inventory remains usable when the operator has no live AD
        // session. Session-only AD credentials are intentionally memory-only.
        string? dc =
            null;
        Task<List<AdComputerInfo>>? adTask =
            null;

        if (includeActiveDirectory)
        {
            try
            {
                dc =
                    _ad.GetPreferredWritableDc();

                adTask =
                    Task.Run(
                        () =>
                            _ad.SearchComputers(
                                dc,
                                query,
                                maximumItems),
                        ct);
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException)
            {
                WindowsEventLogService.TryWrite(
                    "Unified device AD search was skipped: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4576,
                    "Devices");
            }
        }

        using var graph =
            new CloudGraphService();

        var intuneTask =
            graph.SearchManagedDevicesAsync(
                accessToken,
                query,
                maximumItems,
                ct);

        var recoveryTask =
            graph.SearchAsync(
                accessToken,
                query,
                ct);

        await Task.WhenAll(
            intuneTask,
            recoveryTask);

        var intuneRows =
            await intuneTask;
        var recoveryRows =
            await recoveryTask;
        var adRows =
            new List<AdComputerInfo>();

        if (adTask is not null)
        {
            try
            {
                adRows =
                    await adTask;
            }
            catch (OperationCanceledException) when (
                ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WindowsEventLogService.TryWrite(
                    "Unified device AD search failed; continuing with Entra/Intune: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4576,
                    "Devices");
            }
        }

        var result =
            UnifiedDeviceMerge.Merge(
                adRows,
                intuneRows,
                recoveryRows);

        // A matching computer name cannot prove that an AD object and an
        // Intune device are the same physical machine. Keep independent
        // records until an authoritative link is available.

        // For Intune hits that did not appear in the initial recovery search
        // (for example serial-number or UPN searches), fetch metadata by Entra
        // device ID with bounded concurrency.
        var missing =
            result.Values
                .Where(
                    x =>
                        x.FoundInIntune &&
                        x.IdentityStatus != "Ambiguous device ID" &&
                        !string.IsNullOrWhiteSpace(
                            x.EntraDeviceId) &&
                        x.RecoveryIds.Count ==
                            0)
                .Take(
                    100)
                .ToList();

        using var gate =
            new SemaphoreSlim(
                6);

        var fillTasks =
            missing.Select(
                async row =>
                {
                    await gate.WaitAsync(
                        ct);

                    try
                    {
                        var keys =
                            await graph.GetRecoveryMetadataForDeviceAsync(
                                accessToken,
                                row.EntraDeviceId,
                                row.ComputerName,
                                ct);

                        lock (row.RecoveryIds)
                        {
                            foreach (var key in
                                     keys)
                            {
                                UnifiedDeviceMerge.AddRecoveryId(
                                    row,
                                    key.RecoveryId);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (
                        ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        WindowsEventLogService.TryWrite(
                            "Unified device recovery metadata enrichment failed: " +
                            ex.Message,
                            EventLogSeverity.Warning,
                            4578,
                            "Devices");
                    }
                    finally
                    {
                        gate.Release();
                    }
                });

        await Task.WhenAll(
            fillTasks);

        return result.Values
            .OrderByDescending(
                x =>
                    x.FoundInIntune)
            .ThenByDescending(
                x =>
                    x.FoundInAd)
            .ThenBy(
                x =>
                    x.ComputerName,
                StringComparer.OrdinalIgnoreCase)
            .Take(
                Math.Clamp(
                    maximumItems,
                    1,
                    1000))
            .ToList();
    }
}
