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


public sealed class CoverageRunStatus
{
    public bool Success { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime FinishedUtc { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string DomainController { get; set; } = string.Empty;
    public string CsvPath { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public CoverageSummary Summary { get; set; } = new();
    public CoveragePolicyResult Policy { get; set; } = new();
}


public sealed class CoveragePolicyViolation
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Metric { get; set; } = string.Empty;
    public int Actual { get; set; }
    public int Maximum { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CoveragePolicyResult
{
    public bool Enabled { get; set; }
    public bool Compliant { get; set; } = true;
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<CoveragePolicyViolation> Violations { get; set; } = [];
}
