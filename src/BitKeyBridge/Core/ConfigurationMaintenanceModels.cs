namespace BitKeyBridge;

public sealed class ConfigurationBackup
{
    public int FormatVersion { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string SourceMachine { get; set; } = Environment.MachineName;
    public string BitKeyBridgeVersion { get; set; } = string.Empty;
    public AppConfig AppConfig { get; set; } = new();
    public CloudAuthConfig UserCloudConfig { get; set; } = new();
    public CloudAuthConfig MachineCloudConfig { get; set; } = new();
    public bool MachineCloudConfigPresent { get; set; }
    public List<string> ExcludedSecrets { get; set; } =
    [
        "Credential Manager stored AD password",
        "Machine DPAPI AD credential blob",
        "Microsoft Graph access/refresh tokens",
        "Certificate private keys",
        "BitLocker recovery passwords"
    ];
}

public sealed class ConfigurationRestoreResult
{
    public bool Success { get; set; }
    public string RollbackBackupPath { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
}

public sealed class DiagnosticsBundleResult
{
    public string ZipPath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> IncludedFiles { get; set; } = [];
    public List<string> ExcludedSecrets { get; set; } =
    [
        "BitLocker recovery CSV and passwords",
        "audit.jsonl contents",
        "Credential Manager / DPAPI credential blobs",
        "Remote API bearer tokens and token hashes",
        "Graph access/refresh tokens",
        "certificate private keys"
    ];
}
