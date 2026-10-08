namespace BitKeyBridge;

public sealed class HelpdeskProfile
{
    public string Name { get; set; } = "Default";
    public string AdConnectionMode { get; set; } = "Auto";
    public string AdServer { get; set; } = string.Empty;
    public string AdDomain { get; set; } = string.Empty;
    public int AdPort { get; set; } = 389;
    public bool AdUseLdaps { get; set; }
    public bool AdUseExplicitCredentials { get; set; }
    public string AdUsername { get; set; } = string.Empty;
    public string RecoverySearchSource { get; set; } = "LiveAD";
    public string CloudTenant { get; set; } = string.Empty;
    public string CloudClientId { get; set; } = string.Empty;
    public string CloudUsername { get; set; } = string.Empty;
    public string CloudAuthMode { get; set; } = "DeviceCode";
    public string CloudCertificateThumbprint { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
