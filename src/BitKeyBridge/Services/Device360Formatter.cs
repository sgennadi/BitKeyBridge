namespace BitKeyBridge;

/// <summary>
/// A read-only device overview. The portable summary omits recovery IDs,
/// recovery passwords, LAPS secrets and access tokens.
/// </summary>
public static class Device360Formatter
{
    public static string Format(UnifiedDeviceInfo row)
    {
        var lines = new List<string>
        {
            "DEVICE 360 — NON-SECRET SUMMARY",
            "Computer: " + row.ComputerName,
            "Identity: " + row.IdentityStatus,
            "Qualified source ID: " + row.IdentityKey,
            "AD object GUID: " + Value(row.AdObjectGuid),
            "AD distinguished name: " + Value(row.AdDistinguishedName),
            "AD DNS hostname: " + Value(row.AdDnsHostName),
            "AD last logon: " + Date(row.AdLastLogonTimestamp),
            "Entra device ID: " + Value(row.EntraDeviceId),
            "Intune managed device ID: " + Value(row.ManagedDeviceId),
            "Directory sources: AD=" + row.FoundInAd +
                ", Entra=" + row.FoundInEntra +
                ", Intune=" + row.FoundInIntune,
            "Serial number: " + Value(row.SerialNumber),
            "Assigned user: " + Value(row.UserPrincipalName),
            "Hardware: " + Value((row.Manufacturer + " " + row.Model).Trim()),
            "Operating system: " + Value((row.OperatingSystem + " " + row.OsVersion).Trim()),
            "Intune compliance: " + Value(row.ComplianceState),
            "Encrypted: " + (row.IsEncrypted is null ? "not verified" :
                row.IsEncrypted.Value ? "Yes" : "No"),
            "Intune last sync: " + Date(row.LastSyncDateTime),
            "Entra BitLocker recovery IDs: " + row.RecoveryKeyCount +
                (row.RecoveryMetadataIncomplete ? " (unverified / incomplete)" : " (metadata only)"),
            "AD BitLocker backup: not evaluated in this source-qualified record",
            "LAPS backup: not evaluated in Device 360; use LAPS workspace",
            "Secrets: never retrieved by this view"
        };

        if (!string.IsNullOrWhiteSpace(row.IdentityWarning))
            lines.Add("IDENTITY WARNING: " + row.IdentityWarning);

        return string.Join(Environment.NewLine, lines);
    }

    private static string Value(string? v) =>
        string.IsNullOrWhiteSpace(v) ? "not available" : v;

    private static string Date(DateTime? v) =>
        v?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ??
        "not available";
}
