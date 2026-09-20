using System.Text.Json.Serialization;

namespace BitKeyBridge;

public sealed record BitLockerScope(string Name, string SearchBase)
{
    public override string ToString() => Name;
}

public sealed record RecoveryRecord(
    string ComputerName,
    string BitLockerId,
    string RecoveryKey,
    DateTime LastChecked,
    string Source = "AD");

public sealed class ScopeResult
{
    public string Name { get; set; } = string.Empty;
    public string SearchBase { get; set; } = string.Empty;
    public string Status { get; set; } = "OK";
    public int ObjectsFound { get; set; }
    public int ValidRows { get; set; }
    public int Duplicates { get; set; }
    public int Invalid { get; set; }
    public string? Error { get; set; }
}

public sealed class ReplicationPartnerInfo
{
    public string SourceServer { get; set; } = string.Empty;
    public string Partition { get; set; } = string.Empty;
    public DateTime? LastAttemptedSync { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public int LastSyncResult { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public double? AgeHours { get; set; }
    public string Status { get; set; } = "OK";
    public string? Message { get; set; }
}

public sealed class ReplicationHealthResult
{
    public string Server { get; set; } = string.Empty;
    public DateTime Checked { get; set; }
    public bool Healthy { get; set; }
    public bool HasWarnings { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<ReplicationPartnerInfo> Partners { get; set; } = [];
}

public class DomainControllerInfo
{
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public string IPv4Address { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool IsGlobalCatalog { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public bool Reachable { get; set; }
}

public sealed class DomainControllerComparisonRow : DomainControllerInfo
{
    public string Status { get; set; } = "ERROR";
    public int ObjectsFound { get; set; }
    public int? DifferenceFromMax { get; set; }
    public bool? ReplicationHealthy { get; set; }
    public int ReplicationErrors { get; set; }
    public int ReplicationWarnings { get; set; }
    public double? MaxReplicationAgeHours { get; set; }
    public string? Error { get; set; }
    public List<ScopeResult> Scopes { get; set; } = [];
}

public sealed class ExportResult
{
    public bool Success { get; set; }
    public bool DryRun { get; set; }
    public DateTime Started { get; set; }
    public DateTime Finished { get; set; }
    public double DurationSeconds { get; set; }
    public string HostName { get; set; } = Environment.MachineName;
    public string? AdServer { get; set; }
    public string OutputFile { get; set; } = string.Empty;
    public int PreviousRows { get; set; }
    public int ObjectsFound { get; set; }
    public int ValidRows { get; set; }
    public int Duplicates { get; set; }
    public int InvalidObjects { get; set; }
    public int QueryFailures { get; set; }
    public List<string> AclWarnings { get; set; } = [];
    public string? RowCountWarning { get; set; }
    public string? ScopeWarning { get; set; }
    public bool Published { get; set; }
    public int ExitCode { get; set; } = 1;
    public string? ErrorMessage { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public List<ScopeResult> Containers { get; set; } = [];
    public bool LastSuccessFound { get; set; }
    public DateTime? LastSuccessTimestamp { get; set; }
    public double? LastSuccessAgeHours { get; set; }
    public bool LastSuccessWasStale { get; set; }
    public bool ReplicationHealthy { get; set; }
    public List<string> ReplicationWarnings { get; set; } = [];
    public List<string> ReplicationErrors { get; set; } = [];
    public List<ReplicationPartnerInfo> ReplicationPartners { get; set; } = [];
}

public sealed class LastSuccessInfo
{
    public DateTime Finished { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string AdServer { get; set; } = string.Empty;
    public int ValidRows { get; set; }
    public int ObjectsFound { get; set; }
    public double DurationSeconds { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public List<ScopeResult> Containers { get; set; } = [];
}

public sealed class CloudAuthConfig
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "DeviceCode";
    public string BootstrapClientId { get; set; } = string.Empty;
}

public sealed class GraphToken
{
    [JsonIgnore]
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string AuthMode { get; init; } = string.Empty;
    public string? Username { get; init; }
}

public sealed class CloudRecoveryMetadata
{
    public string RecoveryId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string VolumeType { get; set; } = string.Empty;
    public DateTime? CreatedDateTime { get; set; }
    public string Source { get; set; } = "Entra";
}

public sealed class DeviceCodeInfo
{
    public string UserCode { get; init; } = string.Empty;
    public string VerificationUri { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
}

public sealed class EntraSetupResult
{
    public string TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApplicationObjectId { get; set; } = string.Empty;
    public string ServicePrincipalId { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public DateTime CertificateNotAfter { get; set; }
}
