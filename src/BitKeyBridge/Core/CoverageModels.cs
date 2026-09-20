namespace BitKeyBridge;

public sealed class AdRecoveryMetadata
{
    public string ComputerName { get; set; } = string.Empty;
    public string RecoveryId { get; set; } = string.Empty;
    public DateTime? CreatedDateTime { get; set; }
}

public sealed class CoverageDeviceRow
{
    public string ComputerName { get; set; } = string.Empty;
    public bool FoundInAd { get; set; }
    public bool FoundInIntune { get; set; }
    public string EntraDeviceId { get; set; } = string.Empty;
    public string ManagedDeviceId { get; set; } = string.Empty;

    public int AdRecoveryKeyCount { get; set; }
    public int EntraRecoveryKeyCount { get; set; }
    public DateTime? NewestAdRecoveryKey { get; set; }
    public DateTime? NewestEntraRecoveryKey { get; set; }

    public bool MultipleRecoveryKeys =>
        AdRecoveryKeyCount > 1 || EntraRecoveryKeyCount > 1;

    public bool HasRecoveryKey =>
        AdRecoveryKeyCount > 0 || EntraRecoveryKeyCount > 0;

    public string CoverageStatus { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ComplianceState { get; set; } = string.Empty;
    public bool? IsEncrypted { get; set; }
    public DateTime? IntuneLastSync { get; set; }
    public DateTime? AdLastLogon { get; set; }

    public bool IntuneStale { get; set; }
    public bool CloudKeyOld { get; set; }
}

public sealed class CoverageSummary
{
    public int TotalDevices { get; set; }
    public int BothSources { get; set; }
    public int AdOnly { get; set; }
    public int EntraOnly { get; set; }
    public int NoRecoveryKey { get; set; }
    public int MultipleKeys { get; set; }
    public int IntuneManaged { get; set; }
    public int IntuneEncrypted { get; set; }
    public int IntuneNotEncrypted { get; set; }
    public int IntuneStale { get; set; }
    public int OldCloudKey { get; set; }
}

public sealed class CoverageResult
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string DomainController { get; set; } = string.Empty;
    public CoverageSummary Summary { get; set; } = new();
    public List<CoverageDeviceRow> Rows { get; set; } = [];
}
