namespace BitKeyBridge;

public sealed class HealthSnapshot
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = string.Empty;
    public string MachineName { get; set; } = Environment.MachineName;
    public string OverallStatus { get; set; } = "Unknown";
    public string ServiceState { get; set; } = "NotInstalled";
    public string ServiceIdentity { get; set; } = string.Empty;
    public bool ServiceInstalled { get; set; }
    public bool ServiceCoverageEnabled { get; set; }
    public int ServiceCoverageIntervalMinutes { get; set; }
    public bool MachineCloudConfigured { get; set; }
    public string MachineCloudCertificateThumbprint { get; set; } = string.Empty;
    public string MachineCloudKeyAccessStatus { get; set; } = "Unknown";
    public string MachineCloudKeyAccessAccount { get; set; } = string.Empty;
    public string MachineCloudKeyProvider { get; set; } = string.Empty;
    public bool RbacEnabled { get; set; }
    public bool RbacAllowLocalAdministrators { get; set; }
    public int RbacRecoveryReaderPrincipals { get; set; }
    public int RbacRotationOperatorPrincipals { get; set; }
    public int RbacValidationErrors { get; set; }
    public string AuditIntegrityStatus { get; set; } = "NeverVerified";
    public DateTime? AuditIntegrityVerifiedAtUtc { get; set; }
    public DateTime? AuditLastWriteUtc { get; set; }
    public int AuditIntegrityFilesChecked { get; set; }
    public int AuditIntegrityEntries { get; set; }
    public int AuditIntegrityLegacyEntries { get; set; }
    public int AuditIntegrityChainedEntries { get; set; }
    public string AuditIntegrityError { get; set; } = string.Empty;
    public bool AuditSigningEnabled { get; set; }
    public string AuditSigningStatus { get; set; } = "NotConfigured";
    public string AuditSigningCertificateThumbprint { get; set; } = string.Empty;
    public DateTime? AuditSigningCertificateExpiresUtc { get; set; }
    public double? AuditSigningCertificateDaysRemaining { get; set; }
    public DateTime? AuditSigningCheckpointCreatedAtUtc { get; set; }
    public int AuditSigningCheckpointEntries { get; set; }
    public bool AuditSigningSignatureValid { get; set; }
    public bool AuditSigningCurrentHeadSigned { get; set; }
    public string AuditSigningTransitionHistoryStatus { get; set; } = "None";
    public int AuditSigningTransitionCount { get; set; }
    public DateTime? AuditSigningLastTransitionUtc { get; set; }
    public string AuditSigningTransitionError { get; set; } = string.Empty;
    public string HealthEndpoint { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;
    public bool OutputDirectoryExists { get; set; }
    public bool CsvExists { get; set; }
    public int CsvRows { get; set; }

    public bool? LastRunSuccess { get; set; }
    public DateTime? LastRunFinished { get; set; }
    public bool LastRunPublished { get; set; }
    public string LastRunError { get; set; } = string.Empty;
    public int LastRunRows { get; set; }
    public string LastRunDc { get; set; } = string.Empty;
    public bool? ReplicationHealthy { get; set; }
    public int ReplicationErrors { get; set; }
    public int ReplicationWarnings { get; set; }

    public DateTime? LastSuccessfulExport { get; set; }
    public double? LastSuccessfulExportAgeHours { get; set; }
    public bool LastSuccessfulExportStale { get; set; }

    public bool? LastCoverageSuccess { get; set; }
    public DateTime? LastCoverageFinishedUtc { get; set; }
    public double? LastCoverageAgeHours { get; set; }
    public string LastCoverageError { get; set; } = string.Empty;
    public int CoverageTotalDevices { get; set; }
    public int CoverageNoRecoveryKey { get; set; }
    public int CoverageIntuneNotEncrypted { get; set; }
    public int CoverageIntuneStale { get; set; }
    public int CoverageOldCloudKey { get; set; }
    public bool CoveragePolicyEnabled { get; set; }
    public bool CoveragePolicyCompliant { get; set; } = true;
    public int CoveragePolicyErrors { get; set; }
    public int CoveragePolicyWarnings { get; set; }

    public bool CloudConfigured { get; set; }
    public string CloudAuthMode { get; set; } = string.Empty;
    public bool CertificateConfigured { get; set; }
    public string CertificateThumbprint { get; set; } = string.Empty;
    public DateTime? CertificateExpires { get; set; }
    public double? CertificateDaysRemaining { get; set; }
    public string CertificateStatus { get; set; } = "NotConfigured";

    public bool RemoteApiEnabled { get; set; }
    public int RemoteApiPort { get; set; }
    public bool RemoteApiManagementEnabled { get; set; }
    public string RemoteApiCertificateThumbprint { get; set; } = string.Empty;
    public bool RemoteApiAdminTokenConfigured { get; set; }
    public bool RemoteApiReadTokenConfigured { get; set; }
    public bool RemoteApiCoverageRunTokenConfigured { get; set; }
    public bool RemoteApiExportTokenConfigured { get; set; }
    public DateTime? RemoteApiCertificateExpiresUtc { get; set; }
    public double? RemoteApiCertificateDaysRemaining { get; set; }
    public string RemoteApiCertificateStatus { get; set; } = "NotConfigured";
    public string RemoteApiKeyAccessStatus { get; set; } = "Unknown";

    public DateTime? UpdateCheckedAtUtc { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public bool UpdateAvailable { get; set; }
    public string UpdateError { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class ServiceInfo
{
    public bool Installed { get; set; }
    public string State { get; set; } = "NotInstalled";
    public string BinaryPath { get; set; } = string.Empty;
    public string Identity { get; set; } = string.Empty;
}

public sealed class SecureOutputResult
{
    public string DirectoryPath { get; set; } = string.Empty;
    public string ShareName { get; set; } = string.Empty;
    public List<string> ReaderPrincipals { get; set; } = [];
    public bool ShareCreated { get; set; }
}
