namespace BitKeyBridge;

public static class UnifiedDeviceMerge
{
    public static Dictionary<string, UnifiedDeviceInfo> Merge(
        IEnumerable<AdComputerInfo>? adRows,
        IEnumerable<ManagedDeviceInfo>? intuneRows,
        IEnumerable<CloudRecoveryMetadata>? recoveryRows)
    {
        var result =
            new Dictionary<string, UnifiedDeviceInfo>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var ad in
                 adRows ??
                 [])
        {
            var key =
                NormalizeName(
                    ad.ComputerName);
            if (string.IsNullOrWhiteSpace(
                    key))
            {
                continue;
            }

            var row =
                GetOrCreate(
                    result,
                    key,
                    ad.ComputerName);
            row.FoundInAd =
                true;
            row.AdDistinguishedName =
                ad.DistinguishedName;
            row.AdLastLogonTimestamp =
                ad.LastLogonTimestamp;

            if (string.IsNullOrWhiteSpace(
                    row.OperatingSystem))
            {
                row.OperatingSystem =
                    ad.OperatingSystem;
            }

            if (string.IsNullOrWhiteSpace(
                    row.OsVersion))
            {
                row.OsVersion =
                    ad.OperatingSystemVersion;
            }
        }

        foreach (var md in
                 intuneRows ??
                 [])
        {
            var key =
                NormalizeName(
                    md.DeviceName);
            if (string.IsNullOrWhiteSpace(
                    key))
            {
                key =
                    md.EntraDeviceId;
            }

            if (string.IsNullOrWhiteSpace(
                    key))
            {
                key =
                    md.ManagedDeviceId;
            }

            if (string.IsNullOrWhiteSpace(
                    key))
            {
                continue;
            }

            var row =
                GetOrCreate(
                    result,
                    key,
                    md.DeviceName);

            row.FoundInIntune =
                true;
            row.FoundInEntra =
                !string.IsNullOrWhiteSpace(
                    md.EntraDeviceId);
            row.EntraDeviceId =
                md.EntraDeviceId;
            row.ManagedDeviceId =
                md.ManagedDeviceId;
            row.SerialNumber =
                md.SerialNumber;
            row.UserPrincipalName =
                md.UserPrincipalName;
            row.UserDisplayName =
                md.UserDisplayName;
            row.Manufacturer =
                md.Manufacturer;
            row.Model =
                md.Model;
            row.OperatingSystem =
                md.OperatingSystem;
            row.OsVersion =
                md.OsVersion;
            row.ComplianceState =
                md.ComplianceState;
            row.IsEncrypted =
                md.IsEncrypted;
            row.LastSyncDateTime =
                md.LastSyncDateTime;
        }

        foreach (var recovery in
                 recoveryRows ??
                 [])
        {
            var key =
                NormalizeName(
                    recovery.ComputerName);
            if (string.IsNullOrWhiteSpace(
                    key))
            {
                key =
                    recovery.DeviceId;
            }

            if (string.IsNullOrWhiteSpace(
                    key))
            {
                continue;
            }

            var row =
                GetOrCreate(
                    result,
                    key,
                    recovery.ComputerName);
            row.FoundInEntra =
                true;

            if (string.IsNullOrWhiteSpace(
                    row.EntraDeviceId))
            {
                row.EntraDeviceId =
                    recovery.DeviceId;
            }

            AddRecoveryId(
                row,
                recovery.RecoveryId);
        }

        return result;
    }

    public static void AddRecoveryId(
        UnifiedDeviceInfo row,
        string recoveryId)
    {
        if (string.IsNullOrWhiteSpace(
                recoveryId))
        {
            return;
        }

        if (!row.RecoveryIds.Contains(
                recoveryId,
                StringComparer.OrdinalIgnoreCase))
        {
            row.RecoveryIds.Add(
                recoveryId);
        }
    }

    public static string NormalizeName(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        var text =
            value.Trim();
        var dot =
            text.IndexOf('.');

        if (dot > 0)
            text =
                text[..dot];

        return text;
    }

    private static UnifiedDeviceInfo GetOrCreate(
        IDictionary<string, UnifiedDeviceInfo> rows,
        string key,
        string computerName)
    {
        if (rows.TryGetValue(
                key,
                out var existing))
        {
            return existing;
        }

        var created =
            new UnifiedDeviceInfo
            {
                ComputerName =
                    computerName
            };
        rows[key] =
            created;
        return created;
    }
}
