using System.Text;

namespace BitKeyBridge;

public sealed class CoverageService
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;

    public CoverageService(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService(config);
    }

    public async Task<CoverageResult> RunAsync(
        string accessToken,
        IReadOnlyCollection<BitLockerScope>? scopes = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var selectedScopes = (scopes is { Count: > 0 }
            ? scopes
            : _config.DefaultScopes).ToList();

        if (selectedScopes.Count == 0)
        {
            throw new InvalidOperationException(
                "Configure at least one AD scope before running coverage analysis.");
        }

        var dc = _ad.GetPreferredWritableDc();
        progress?.Report(
            $"Reading AD computer and BitLocker metadata from {dc}...");

        var adTask = Task.Run(() =>
        {
            var computers = new Dictionary<string, AdComputerInfo>(
                StringComparer.OrdinalIgnoreCase);
            var recovery = new Dictionary<string, AdRecoveryMetadata>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var scope in selectedScopes)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var computer in _ad.GetComputersInScope(dc, scope))
                {
                    var key = NormalizeName(computer.ComputerName);
                    if (!string.IsNullOrWhiteSpace(key))
                        computers[key] = computer;
                }

                foreach (var item in _ad.GetRecoveryMetadata(dc, scope))
                {
                    if (!string.IsNullOrWhiteSpace(item.RecoveryId))
                        recovery[item.RecoveryId] = item;
                }
            }

            return (
                Computers: computers.Values.ToList(),
                Recovery: recovery.Values.ToList());
        }, ct);

        progress?.Report(
            "Reading Intune device inventory and Entra BitLocker metadata...");

        using var graph = new CloudGraphService();
        var intuneTask = graph.SearchManagedDevicesAsync(
            accessToken,
            string.Empty,
            50000,
            ct);
        var entraKeysTask = graph.GetAllRecoveryMetadataAsync(
            accessToken,
            100000,
            ct);

        await Task.WhenAll(adTask, intuneTask, entraKeysTask);

        var adData = await adTask;
        var intune = await intuneTask;
        var entraKeys = await entraKeysTask;

        progress?.Report("Joining AD, Entra and Intune metadata...");

        var rows = new Dictionary<string, CoverageDeviceRow>(
            StringComparer.OrdinalIgnoreCase);
        var entraIdToRowKey = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var computer in adData.Computers)
        {
            var key = NormalizeName(computer.ComputerName);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            rows[key] = new CoverageDeviceRow
            {
                ComputerName = computer.ComputerName,
                FoundInAd = true,
                OperatingSystem = computer.OperatingSystem,
                OsVersion = computer.OperatingSystemVersion,
                AdLastLogon = computer.LastLogonTimestamp
            };
        }

        foreach (var recovery in adData.Recovery)
        {
            var key = NormalizeName(recovery.ComputerName);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var row = GetOrCreate(rows, key, recovery.ComputerName);
            row.FoundInAd = true;
            row.AdRecoveryKeyCount++;

            if (recovery.CreatedDateTime is not null &&
                (row.NewestAdRecoveryKey is null ||
                 recovery.CreatedDateTime > row.NewestAdRecoveryKey))
            {
                row.NewestAdRecoveryKey = recovery.CreatedDateTime;
            }
        }

        foreach (var device in intune)
        {
            var key = NormalizeName(device.DeviceName);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = !string.IsNullOrWhiteSpace(device.EntraDeviceId)
                    ? "entra:" + device.EntraDeviceId
                    : "intune:" + device.ManagedDeviceId;
            }

            var row = GetOrCreate(rows, key, device.DeviceName);
            row.FoundInIntune = true;
            row.EntraDeviceId = device.EntraDeviceId;
            row.ManagedDeviceId = device.ManagedDeviceId;
            row.SerialNumber = device.SerialNumber;
            row.UserPrincipalName = device.UserPrincipalName;
            row.Manufacturer = device.Manufacturer;
            row.Model = device.Model;
            row.OperatingSystem = device.OperatingSystem;
            row.OsVersion = device.OsVersion;
            row.ComplianceState = device.ComplianceState;
            row.IsEncrypted = device.IsEncrypted;
            row.IntuneLastSync = device.LastSyncDateTime;

            if (!string.IsNullOrWhiteSpace(device.EntraDeviceId))
                entraIdToRowKey[device.EntraDeviceId] = key;
        }

        foreach (var recovery in entraKeys
                     .Where(x => !string.IsNullOrWhiteSpace(x.RecoveryId))
                     .GroupBy(x => x.RecoveryId, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First()))
        {
            string key;

            if (!string.IsNullOrWhiteSpace(recovery.DeviceId) &&
                entraIdToRowKey.TryGetValue(
                    recovery.DeviceId,
                    out var mapped))
            {
                key = mapped;
            }
            else
            {
                key = NormalizeName(recovery.ComputerName);
                if (string.IsNullOrWhiteSpace(key))
                    key = "entra:" + recovery.DeviceId;
            }

            if (string.IsNullOrWhiteSpace(key))
                continue;

            var row = GetOrCreate(
                rows,
                key,
                string.IsNullOrWhiteSpace(recovery.ComputerName)
                    ? recovery.DeviceId
                    : recovery.ComputerName);

            if (string.IsNullOrWhiteSpace(row.EntraDeviceId))
                row.EntraDeviceId = recovery.DeviceId;

            row.EntraRecoveryKeyCount++;

            if (recovery.CreatedDateTime is not null &&
                (row.NewestEntraRecoveryKey is null ||
                 recovery.CreatedDateTime > row.NewestEntraRecoveryKey))
            {
                row.NewestEntraRecoveryKey = recovery.CreatedDateTime;
            }
        }

        var staleCutoff = DateTime.Now.AddDays(
            -Math.Max(1, _config.CoverageStaleIntuneDays));
        var oldCloudKeyCutoff = DateTime.Now.AddDays(
            -Math.Max(1, _config.CoverageOldCloudKeyDays));

        foreach (var row in rows.Values)
        {
            row.CoverageStatus =
                (row.AdRecoveryKeyCount > 0, row.EntraRecoveryKeyCount > 0)
                switch
                {
                    (true, true) => "AD + Entra",
                    (true, false) => "AD only",
                    (false, true) => "Entra only",
                    _ => "No recovery key"
                };

            row.IntuneStale =
                row.FoundInIntune &&
                (row.IntuneLastSync is null ||
                 row.IntuneLastSync < staleCutoff);

            row.CloudKeyOld =
                row.EntraRecoveryKeyCount > 0 &&
                row.NewestEntraRecoveryKey is not null &&
                row.NewestEntraRecoveryKey < oldCloudKeyCutoff;
        }

        var ordered = rows.Values
            .OrderBy(x => x.ComputerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = new CoverageSummary
        {
            TotalDevices = ordered.Count,
            BothSources = ordered.Count(x => x.CoverageStatus == "AD + Entra"),
            AdOnly = ordered.Count(x => x.CoverageStatus == "AD only"),
            EntraOnly = ordered.Count(x => x.CoverageStatus == "Entra only"),
            NoRecoveryKey =
                ordered.Count(x => x.CoverageStatus == "No recovery key"),
            MultipleKeys = ordered.Count(x => x.MultipleRecoveryKeys),
            IntuneManaged = ordered.Count(x => x.FoundInIntune),
            IntuneEncrypted = ordered.Count(x => x.IsEncrypted == true),
            IntuneNotEncrypted =
                ordered.Count(x => x.FoundInIntune && x.IsEncrypted == false),
            IntuneStale = ordered.Count(x => x.IntuneStale),
            OldCloudKey = ordered.Count(x => x.CloudKeyOld)
        };

        progress?.Report(
            $"Coverage complete. Devices={summary.TotalDevices}; " +
            $"NoKey={summary.NoRecoveryKey}; AD+Entra={summary.BothSources}.");

        return new CoverageResult
        {
            GeneratedAt = DateTime.Now,
            DomainController = dc,
            Summary = summary,
            Rows = ordered
        };
    }

    public static void ExportCsv(
        string path,
        IEnumerable<CoverageDeviceRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "ComputerName,CoverageStatus,FoundInAD,FoundInIntune," +
            "ADRecoveryKeys,EntraRecoveryKeys,MultipleKeys,Encrypted," +
            "ComplianceState,IntuneLastSync,ADLastLogon,NewestADKey," +
            "NewestEntraKey,IntuneStale,CloudKeyOld,SerialNumber," +
            "UserPrincipalName,Manufacturer,Model,OperatingSystem,OSVersion," +
            "EntraDeviceId,ManagedDeviceId");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(row.ComputerName),
                Csv(row.CoverageStatus),
                Bool(row.FoundInAd),
                Bool(row.FoundInIntune),
                row.AdRecoveryKeyCount,
                row.EntraRecoveryKeyCount,
                Bool(row.MultipleRecoveryKeys),
                row.IsEncrypted is null
                    ? string.Empty
                    : Bool(row.IsEncrypted.Value),
                Csv(row.ComplianceState),
                Date(row.IntuneLastSync),
                Date(row.AdLastLogon),
                Date(row.NewestAdRecoveryKey),
                Date(row.NewestEntraRecoveryKey),
                Bool(row.IntuneStale),
                Bool(row.CloudKeyOld),
                Csv(row.SerialNumber),
                Csv(row.UserPrincipalName),
                Csv(row.Manufacturer),
                Csv(row.Model),
                Csv(row.OperatingSystem),
                Csv(row.OsVersion),
                Csv(row.EntraDeviceId),
                Csv(row.ManagedDeviceId)));
        }

        File.WriteAllText(
            path,
            sb.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static CoverageDeviceRow GetOrCreate(
        IDictionary<string, CoverageDeviceRow> rows,
        string key,
        string displayName)
    {
        if (rows.TryGetValue(key, out var existing))
        {
            if (string.IsNullOrWhiteSpace(existing.ComputerName) &&
                !string.IsNullOrWhiteSpace(displayName))
            {
                existing.ComputerName = displayName;
            }

            return existing;
        }

        var created = new CoverageDeviceRow
        {
            ComputerName = displayName
        };

        rows[key] = created;
        return created;
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        var dot = text.IndexOf('.');
        if (dot > 0)
            text = text[..dot];

        return text;
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string Bool(bool value) => value ? "True" : "False";

    private static string Date(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
}
