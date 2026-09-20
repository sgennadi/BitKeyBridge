namespace BitKeyBridge;

public sealed class HealthSnapshot
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = string.Empty;
    public string MachineName { get; set; } = Environment.MachineName;
    public string OverallStatus { get; set; } = "Unknown";
    public string ServiceState { get; set; } = "NotInstalled";
    public bool ServiceInstalled { get; set; }
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
