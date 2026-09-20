namespace BitKeyBridge;

public sealed class AppConfig
{
    public string SysvolScriptsRoot { get; set; } = @"C:\Windows\SYSVOL\domain\scripts";
    public string OutputSubdirectory { get; set; } = "BL";
    public int StaleSuccessHours { get; set; } = 36;
    public int ReplicationStaleHours { get; set; } = 24;
    public bool BlockExportOnReplicationErrors { get; set; } = false;
    public bool AllowPartialExport { get; set; } = false;
    public bool AllowEmptyExport { get; set; } = false;
    public bool FailIfOutputAclIsBroad { get; set; } = false;
    public bool BlockSuspiciousRowDrop { get; set; } = true;
    public int MinimumRowsForDropGuard { get; set; } = 10;
    public double CriticalRowRatio { get; set; } = 0.50;
    public double WarningRowRatio { get; set; } = 0.80;
    public bool BlockPublishOnScopeChange { get; set; } = true;
    public bool TestBitLockerCountsOnReadOnlyDcs { get; set; } = false;
    public int MaxLogSizeMb { get; set; } = 10;

    public bool HealthEndpointEnabled { get; set; } = true;
    public int HealthEndpointPort { get; set; } = 8750;
    public int ServiceIntervalMinutes { get; set; } = 60;
    public bool ServiceRunExportOnStart { get; set; } = true;

    public List<BitLockerScope> DefaultScopes { get; set; } = [];

    public string OutputDirectory => Path.Combine(SysvolScriptsRoot, OutputSubdirectory);
    public string OutputCsv => Path.Combine(OutputDirectory, "bitlocker_recovery_keys.csv");
    public string ErrorLog => Path.Combine(OutputDirectory, "bitlocker_errors.log");
    public string LockFile => Path.Combine(OutputDirectory, "bitlocker_export.lock");
    public string StatusFile => Path.Combine(OutputDirectory, "bitlocker_status.json");
    public string LastSuccessFile => Path.Combine(OutputDirectory, "bitlocker_last_success.json");
    public string DcTestStatusFile => Path.Combine(OutputDirectory, "bitlocker_dc_test_status.json");
}
