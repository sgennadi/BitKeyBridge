namespace BitKeyBridge;

public sealed class UnifiedDeviceService
{
    private readonly ActiveDirectoryService _ad;

    public UnifiedDeviceService(AppConfig? config = null)
    {
        _ad = new ActiveDirectoryService(config);
    }

    public async Task<List<UnifiedDeviceInfo>> SearchAdOnlyAsync(
        string query,
        int maximumItems = 250,
        CancellationToken ct = default)
    {
        query = query?.Trim() ?? string.Empty;
        var dc = _ad.GetPreferredWritableDc();

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
                        ComputerName = ad.ComputerName,
                        FoundInAd = true,
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
                x => x.ComputerName,
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
        CancellationToken ct = default)
    {
        query = query?.Trim() ?? string.Empty;
        var dc = _ad.GetPreferredWritableDc();

        var adTask = Task.Run(() => _ad.SearchComputers(dc, query, maximumItems), ct);
        using var graph = new CloudGraphService();
        var intuneTask = graph.SearchManagedDevicesAsync(accessToken, query, maximumItems, ct);
        var recoveryTask = graph.SearchAsync(accessToken, query, ct);

        await Task.WhenAll(adTask, intuneTask, recoveryTask);

        var result = new Dictionary<string, UnifiedDeviceInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var ad in await adTask)
        {
            var key = NormalizeName(ad.ComputerName);
            if (string.IsNullOrWhiteSpace(key)) continue;
            var row = GetOrCreate(result, key, ad.ComputerName);
            row.FoundInAd = true;
            row.AdDistinguishedName = ad.DistinguishedName;
            row.AdLastLogonTimestamp = ad.LastLogonTimestamp;
            if (string.IsNullOrWhiteSpace(row.OperatingSystem)) row.OperatingSystem = ad.OperatingSystem;
            if (string.IsNullOrWhiteSpace(row.OsVersion)) row.OsVersion = ad.OperatingSystemVersion;
        }

        foreach (var md in await intuneTask)
        {
            var key = NormalizeName(md.DeviceName);
            if (string.IsNullOrWhiteSpace(key)) key = md.EntraDeviceId;
            if (string.IsNullOrWhiteSpace(key)) key = md.ManagedDeviceId;
            if (string.IsNullOrWhiteSpace(key)) continue;

            var row = GetOrCreate(result, key, md.DeviceName);
            row.FoundInIntune = true;
            row.FoundInEntra = !string.IsNullOrWhiteSpace(md.EntraDeviceId);
            row.EntraDeviceId = md.EntraDeviceId;
            row.ManagedDeviceId = md.ManagedDeviceId;
            row.SerialNumber = md.SerialNumber;
            row.UserPrincipalName = md.UserPrincipalName;
            row.UserDisplayName = md.UserDisplayName;
            row.Manufacturer = md.Manufacturer;
            row.Model = md.Model;
            row.OperatingSystem = md.OperatingSystem;
            row.OsVersion = md.OsVersion;
            row.ComplianceState = md.ComplianceState;
            row.IsEncrypted = md.IsEncrypted;
            row.LastSyncDateTime = md.LastSyncDateTime;

            if (!row.FoundInAd && !string.IsNullOrWhiteSpace(md.DeviceName))
            {
                try
                {
                    var ad = await Task.Run(() => _ad.FindComputerByName(dc, md.DeviceName), ct);
                    if (ad is not null)
                    {
                        row.FoundInAd = true;
                        row.AdDistinguishedName = ad.DistinguishedName;
                        row.AdLastLogonTimestamp = ad.LastLogonTimestamp;
                    }
                }
                catch
                {
                    // Unified search remains useful even if this individual AD lookup fails.
                }
            }
        }

        foreach (var recovery in await recoveryTask)
        {
            var key = NormalizeName(recovery.ComputerName);
            if (string.IsNullOrWhiteSpace(key)) key = recovery.DeviceId;
            if (string.IsNullOrWhiteSpace(key)) continue;

            var row = GetOrCreate(result, key, recovery.ComputerName);
            row.FoundInEntra = true;
            if (string.IsNullOrWhiteSpace(row.EntraDeviceId)) row.EntraDeviceId = recovery.DeviceId;
            AddRecoveryId(row, recovery.RecoveryId);
        }

        // For Intune hits that did not appear in the initial recovery search (for example,
        // serial-number or UPN searches), fetch metadata by Entra device ID with bounded concurrency.
        var missing = result.Values
            .Where(x => x.FoundInIntune &&
                        !string.IsNullOrWhiteSpace(x.EntraDeviceId) &&
                        x.RecoveryIds.Count == 0)
            .Take(100)
            .ToList();

        using var gate = new SemaphoreSlim(6);
        var fillTasks = missing.Select(async row =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var keys = await graph.GetRecoveryMetadataForDeviceAsync(
                    accessToken,
                    row.EntraDeviceId,
                    row.ComputerName,
                    ct);
                lock (row.RecoveryIds)
                {
                    foreach (var key in keys) AddRecoveryId(row, key.RecoveryId);
                }
            }
            catch
            {
                // Missing BitLocker metadata should not hide the device from unified results.
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(fillTasks);

        return result.Values
            .OrderByDescending(x => x.FoundInIntune)
            .ThenByDescending(x => x.FoundInAd)
            .ThenBy(x => x.ComputerName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumItems, 1, 1000))
            .ToList();
    }

    private static UnifiedDeviceInfo GetOrCreate(
        IDictionary<string, UnifiedDeviceInfo> rows,
        string key,
        string computerName)
    {
        if (rows.TryGetValue(key, out var existing)) return existing;
        var created = new UnifiedDeviceInfo { ComputerName = computerName };
        rows[key] = created;
        return created;
    }

    private static void AddRecoveryId(UnifiedDeviceInfo row, string recoveryId)
    {
        if (string.IsNullOrWhiteSpace(recoveryId)) return;
        if (!row.RecoveryIds.Contains(recoveryId, StringComparer.OrdinalIgnoreCase))
            row.RecoveryIds.Add(recoveryId);
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim();
        var dot = text.IndexOf('.');
        if (dot > 0) text = text[..dot];
        return text;
    }
}
