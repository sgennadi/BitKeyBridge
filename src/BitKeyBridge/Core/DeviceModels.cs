namespace BitKeyBridge;

public sealed class AdComputerInfo
{
    public string ComputerName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string DnsHostName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;
    public DateTime? LastLogonTimestamp { get; set; }
}

public sealed class ManagedDeviceInfo
{
    public string ManagedDeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string EntraDeviceId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ComplianceState { get; set; } = string.Empty;
    public bool? IsEncrypted { get; set; }
    public DateTime? LastSyncDateTime { get; set; }
}

public sealed class UnifiedDeviceInfo
{
    public string ComputerName { get; set; } = string.Empty;
    public bool FoundInAd { get; set; }
    public bool FoundInEntra { get; set; }
    public bool FoundInIntune { get; set; }
    public string AdDistinguishedName { get; set; } = string.Empty;
    public string EntraDeviceId { get; set; } = string.Empty;
    public string ManagedDeviceId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ComplianceState { get; set; } = string.Empty;
    public bool? IsEncrypted { get; set; }
    public DateTime? LastSyncDateTime { get; set; }
    public DateTime? AdLastLogonTimestamp { get; set; }
    public List<string> RecoveryIds { get; set; } = [];
    public int RecoveryKeyCount => RecoveryIds.Count;
}

public sealed class RecoveryAccessContext
{
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool RemindRotation { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditEntry
{
    public DateTime TimestampUtc { get; set; }
    public string User { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string RecoveryId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string AuthMode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int ChainVersion { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string EntryHash { get; set; } = string.Empty;
}

public sealed class AuditIntegrityResult
{
    public bool Valid { get; set; } = true;
    public int FilesChecked { get; set; }
    public int TotalEntries { get; set; }
    public int LegacyEntries { get; set; }
    public int ChainedEntries { get; set; }
    public string LastHash { get; set; } = string.Empty;
    public string FirstError { get; set; } = string.Empty;
}
