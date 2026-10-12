namespace BitKeyBridge;

/// <summary>
/// Joins device metadata only by authoritative, source-specific IDs.
/// A matching hostname, FQDN prefix, serial number, or UPN is NOT identity proof.
/// No recovery key may be attached to an Intune device by name alone.
/// </summary>
public static class UnifiedDeviceMerge
{
    public static Dictionary<string, UnifiedDeviceInfo> Merge(
        IEnumerable<AdComputerInfo>? adRows,
        IEnumerable<ManagedDeviceInfo>? intuneRows,
        IEnumerable<CloudRecoveryMetadata>? recoveryRows)
    {
        var result = new Dictionary<string, UnifiedDeviceInfo>(
            StringComparer.OrdinalIgnoreCase);
        var nextUnidentified = 0;

        foreach (var ad in adRows ?? [])
        {
            var stableId = !string.IsNullOrWhiteSpace(ad.ObjectGuid)
                ? "GUID:" + ad.ObjectGuid.Trim()
                : !string.IsNullOrWhiteSpace(ad.DistinguishedName)
                    ? "DN:" + ad.DistinguishedName.Trim()
                    : "UNRESOLVED:" + (++nextUnidentified).ToString();

            var key = "AD:" + stableId;
            var row = GetOrCreate(result, key, ad.ComputerName);
            row.IdentityStatus = "AD object";
            row.IdentityKey = key;
            row.FoundInAd = true;
            row.AdObjectGuid = ad.ObjectGuid;
            row.AdDistinguishedName = ad.DistinguishedName;
            row.AdDnsHostName = ad.DnsHostName;
            row.AdLastLogonTimestamp = ad.LastLogonTimestamp;
            row.OperatingSystem = ad.OperatingSystem;
            row.OsVersion = ad.OperatingSystemVersion;
        }

        foreach (var device in intuneRows ?? [])
        {
            var stableId = !string.IsNullOrWhiteSpace(device.ManagedDeviceId)
                ? "MANAGED:" + device.ManagedDeviceId.Trim()
                : !string.IsNullOrWhiteSpace(device.EntraDeviceId)
                    ? "DEVICE:" + device.EntraDeviceId.Trim()
                    : "UNRESOLVED:" + (++nextUnidentified).ToString();

            var key = "INTUNE:" + stableId;
            var row = GetOrCreate(result, key, device.DeviceName);
            row.IdentityStatus = "Intune object";
            row.IdentityKey = key;
            row.FoundInIntune = true;
            row.FoundInEntra = !string.IsNullOrWhiteSpace(device.EntraDeviceId);
            row.EntraDeviceId = device.EntraDeviceId;
            row.ManagedDeviceId = device.ManagedDeviceId;
            row.SerialNumber = device.SerialNumber;
            row.UserPrincipalName = device.UserPrincipalName;
            row.UserDisplayName = device.UserDisplayName;
            row.Manufacturer = device.Manufacturer;
            row.Model = device.Model;
            row.OperatingSystem = device.OperatingSystem;
            row.OsVersion = device.OsVersion;
            row.ComplianceState = device.ComplianceState;
            row.IsEncrypted = device.IsEncrypted;
            row.LastSyncDateTime = device.LastSyncDateTime;
        }

        // Duplicate Entra IDs on distinct Intune objects are never safe
        // recovery targets, even if no key metadata was returned.
        foreach (var group in result.Values
                     .Where(x => x.FoundInIntune &&
                                 !string.IsNullOrWhiteSpace(x.EntraDeviceId))
                     .GroupBy(x => x.EntraDeviceId,
                         StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (var row in group)
            {
                row.IdentityStatus = "Ambiguous device ID";
                row.IdentityWarning =
                    "More than one Intune object has this Entra device ID. " +
                    "Recovery actions require identity verification.";
            }
        }

        foreach (var recovery in recoveryRows ?? [])
        {
            var deviceId = recovery.DeviceId.Trim();
            List<UnifiedDeviceInfo> matched = string.IsNullOrWhiteSpace(deviceId)
                ? []
                : result.Values
                    .Where(x => x.FoundInIntune &&
                                string.Equals(x.EntraDeviceId, deviceId,
                                    StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // Exactly one managed device linked by its Entra deviceId is safe.
            // Two managed records claiming the same Entra ID are ambiguous.
            if (matched.Count == 1)
            {
                var row = matched[0];
                row.FoundInEntra = true;
                row.IdentityStatus = "Matched by device ID";
                AddRecoveryId(row, recovery.RecoveryId);
                continue;
            }

            var stableId = !string.IsNullOrWhiteSpace(deviceId)
                ? "DEVICE:" + deviceId
                : !string.IsNullOrWhiteSpace(recovery.RecoveryId)
                    ? "KEY:" + recovery.RecoveryId.Trim()
                    : "UNRESOLVED:" + (++nextUnidentified).ToString();
            var key = "ENTRA:" + stableId;
            var cloud = GetOrCreate(result, key, recovery.ComputerName);
            cloud.FoundInEntra = true;
            cloud.IdentityKey = key;
            cloud.EntraDeviceId = deviceId;
            cloud.IdentityStatus = matched.Count > 1
                ? "Ambiguous device ID"
                : "Entra object";

            if (matched.Count > 1)
            {
                cloud.IdentityWarning =
                    "Multiple Intune devices claim the same Entra device ID. " +
                    "Recovery metadata is intentionally kept separate.";
                foreach (var row in matched)
                {
                    row.IdentityStatus = "Ambiguous device ID";
                    row.IdentityWarning = cloud.IdentityWarning;
                }
            }

            AddRecoveryId(cloud, recovery.RecoveryId);
        }

        // Same-name entries are separate physical identities until the operator
        // verifies an authoritative ID. Mark the ambiguity, never silently merge.
        foreach (var duplicate in result.Values
                     .Where(x => !string.IsNullOrWhiteSpace(x.ComputerName))
                     .GroupBy(x => NormalizeName(x.ComputerName),
                         StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (var row in duplicate)
            {
                if (row.IdentityStatus != "Ambiguous device ID")
                    row.IdentityStatus = "Name collision";
                var warning = "This name occurs in multiple AD/Entra/Intune records. " +
                              "The records have NOT been merged; confirm the device ID.";
                row.IdentityWarning = string.IsNullOrWhiteSpace(row.IdentityWarning)
                    ? warning
                    : row.IdentityWarning + " " + warning;
            }
        }

        return result;
    }

    public static void AddRecoveryId(
        UnifiedDeviceInfo row,
        string recoveryId)
    {
        if (string.IsNullOrWhiteSpace(recoveryId))
            return;

        if (!row.RecoveryIds.Contains(recoveryId,
                StringComparer.OrdinalIgnoreCase))
        {
            row.RecoveryIds.Add(recoveryId);
        }
    }

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var name = value.Trim();
        var dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static UnifiedDeviceInfo GetOrCreate(
        IDictionary<string, UnifiedDeviceInfo> rows,
        string key,
        string computerName)
    {
        if (rows.TryGetValue(key, out var existing))
            return existing;

        var created = new UnifiedDeviceInfo
        {
            ComputerName = string.IsNullOrWhiteSpace(computerName)
                ? key
                : computerName,
            IdentityKey = key
        };
        rows[key] = created;
        return created;
    }
}
