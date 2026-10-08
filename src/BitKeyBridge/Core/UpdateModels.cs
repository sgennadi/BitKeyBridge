namespace BitKeyBridge;

public sealed class UpdateInfo
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseTag { get; set; } = string.Empty;
    public bool UpdateAvailable { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public long AssetSizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string ExpectedSha256 { get; set; } = string.Empty;
    public string AssetDigestSha256 { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string ReleaseNotes { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PreparedUpdate
{
    public UpdateInfo Info { get; set; } = new();
    public string ZipPath { get; set; } = string.Empty;
    public string StagedExecutable { get; set; } = string.Empty;
    public string StagedExecutableSha256 { get; set; } = string.Empty;
    public string AuthenticodePublisher { get; set; } = string.Empty;
}

public sealed class UpdateApplyPlan
{
    public string StagedExecutable { get; set; } = string.Empty;
    public string StagedExecutableSha256 { get; set; } = string.Empty;
    public string ExpectedVersion { get; set; } = string.Empty;
    public string PreviousVersion { get; set; } = string.Empty;
    public bool Automatic { get; set; }
    public bool Rollback { get; set; }
    public string AuthenticodePublisher { get; set; } = string.Empty;
    public List<string> TargetExecutables { get; set; } = [];
    public int WaitForProcessId { get; set; }
    public bool RestartService { get; set; }
    public bool RestartGui { get; set; }
    public string GuiExecutable { get; set; } = string.Empty;
    public string CleanupDirectory { get; set; } = string.Empty;
}

public sealed class RemoteApiSetupResult
{
    public string Token { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public DateTime CertificateExpires { get; set; }
    public int Port { get; set; }
}


public sealed class RemoteApiScopedTokenResult
{
    public string Scope { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public int Port { get; set; }
}


public sealed class UpdateHistoryEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool Automatic { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
