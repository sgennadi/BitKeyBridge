namespace BitKeyBridge;

public sealed class AppConfig
{
    public string SysvolScriptsRoot { get; set; } = @"C:\Windows\SYSVOL\domain\scripts";
    public string OutputRoot { get; set; } = string.Empty;
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

    public string AdConnectionMode { get; set; } = "Auto";
    public string AdServer { get; set; } = string.Empty;
    public string AdDomain { get; set; } = string.Empty;
    public string AdUsername { get; set; } = string.Empty;
    public bool AdUseExplicitCredentials { get; set; } = false;
    public string AdCredentialStorageMode { get; set; } = "Session";
    public string AdCredentialTarget { get; set; } = "BitKeyBridge:ActiveDirectory";
    public bool AdUseLdaps { get; set; } = false;
    public int AdPort { get; set; } = 389;

    public bool HealthEndpointEnabled { get; set; } = true;
    public int HealthEndpointPort { get; set; } = 8750;
    public int ServiceIntervalMinutes { get; set; } = 60;
    public bool ServiceRunExportOnStart { get; set; } = true;
    public bool ServiceCoverageEnabled { get; set; } = false;
    public int ServiceCoverageIntervalMinutes { get; set; } = 1440;
    public bool ServiceRunCoverageOnStart { get; set; } = true;
    public string ServiceIdentityMode { get; set; } = "LocalSystem";
    public string ServiceIdentityAccount { get; set; } = string.Empty;

    public string UpdateRepository { get; set; } = "sgennadi/BitKeyBridge";
    public bool CheckForUpdatesOnStart { get; set; } = true;
    public bool AllowPrereleaseUpdates { get; set; } = false;

    public bool RemoteApiEnabled { get; set; } = false;
    public int RemoteApiPort { get; set; } = 8751;
    public bool RemoteApiAllowManagement { get; set; } = false;
    public string RemoteApiCertificateThumbprint { get; set; } = string.Empty;
    public string RemoteApiTokenSha256 { get; set; } = string.Empty;

    public bool RequireRecoveryAccessReference { get; set; } = false;
    public bool SuggestRotationAfterCloudKeyRetrieval { get; set; } = true;

    public bool RbacEnabled { get; set; } = false;
    public bool RbacAllowLocalAdministrators { get; set; } = true;
    public List<string> RbacRecoveryReaders { get; set; } = [];
    public List<string> RbacRotationOperators { get; set; } = [];

    public bool AuditSigningEnabled { get; set; } = false;
    public string AuditSigningCertificateThumbprint { get; set; } = string.Empty;
    public int AuditSigningCertificateWarningDays { get; set; } = 90;

    public int CoverageStaleIntuneDays { get; set; } = 30;
    public int CoverageOldCloudKeyDays { get; set; } = 365;

    public bool CoveragePolicyEnabled { get; set; } = true;
    public int CoveragePolicyMaxNoRecoveryKey { get; set; } = 0;
    public int CoveragePolicyMaxIntuneNotEncrypted { get; set; } = 0;
    public int CoveragePolicyMaxIntuneStale { get; set; } = 0;
    public int CoveragePolicyMaxOldCloudKey { get; set; } = 0;
    public string CoveragePolicyNoRecoveryKeySeverity { get; set; } = "Warning";
    public string CoveragePolicyIntuneNotEncryptedSeverity { get; set; } = "Warning";
    public string CoveragePolicyIntuneStaleSeverity { get; set; } = "Warning";
    public string CoveragePolicyOldCloudKeySeverity { get; set; } = "Warning";

    public List<BitLockerScope> DefaultScopes { get; set; } = [];

    public string EffectiveOutputRoot =>
        Environment.ExpandEnvironmentVariables(
            string.IsNullOrWhiteSpace(OutputRoot)
                ? SysvolScriptsRoot
                : OutputRoot);

    public string OutputDirectory => Path.Combine(EffectiveOutputRoot, OutputSubdirectory);
    public string OutputCsv => Path.Combine(OutputDirectory, "bitlocker_recovery_keys.csv");
    public string ErrorLog => Path.Combine(OutputDirectory, "bitlocker_errors.log");
    public string LockFile => Path.Combine(OutputDirectory, "bitlocker_export.lock");
    public string StatusFile => Path.Combine(OutputDirectory, "bitlocker_status.json");
    public string LastSuccessFile => Path.Combine(OutputDirectory, "bitlocker_last_success.json");
    public string DcTestStatusFile => Path.Combine(OutputDirectory, "bitlocker_dc_test_status.json");
    public string CoverageOutputDirectory => Path.Combine(OutputDirectory, "Coverage");
    public string CoverageCsv => Path.Combine(CoverageOutputDirectory, "bitlocker_coverage.csv");
    public string CoverageJson => Path.Combine(CoverageOutputDirectory, "bitlocker_coverage.json");
}
