namespace BitKeyBridge;

public sealed class UpdateInfo
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool UpdateAvailable { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
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
}

public sealed class UpdateApplyPlan
{
    public string StagedExecutable { get; set; } = string.Empty;
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
