namespace BitKeyBridge;

public sealed class LapsCoverageDevice
{
    public string ComputerName { get; set; } = string.Empty;
    public string IdentityKey { get; set; } = string.Empty;
    public string ObjectGuid { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;
    public string DirectoryServer { get; set; } = string.Empty;
    public bool WindowsMetadataVisible { get; set; }
    public bool LegacyMetadataVisible { get; set; }
    public DateTime? WindowsExpirationUtc { get; set; }
    public DateTime? LegacyExpirationUtc { get; set; }
    public DateTime? AdLastLogonUtc { get; set; }

    public string BackupEvidence =>
        (WindowsMetadataVisible, LegacyMetadataVisible) switch
        {
            (true, true) => "Windows + Legacy",
            (true, false) => "Windows",
            (false, true) => "Legacy",
            _ => "Not verified"
        };

    public DateTime? EffectiveExpirationUtc =>
        WindowsExpirationUtc ?? LegacyExpirationUtc;

    public string ExpirationState(DateTime nowUtc, int soonDays = 7)
    {
        if (EffectiveExpirationUtc is not DateTime value)
            return "Not verified";

        if (value <= nowUtc)
            return "Expired";
        if (value <= nowUtc.AddDays(Math.Max(1, soonDays)))
            return "Expiring soon";
        return "Scheduled";
    }

    public bool CanRequestAdExpiration =>
        !string.IsNullOrWhiteSpace(ObjectGuid) &&
        !string.IsNullOrWhiteSpace(DistinguishedName) &&
        (BackupEvidence == "Windows" || BackupEvidence == "Legacy");
}

public sealed class LapsCoverageSummary
{
    public int Scanned { get; set; }
    public int WindowsMetadata { get; set; }
    public int LegacyMetadata { get; set; }
    public int BothMetadata { get; set; }
    public int Unverified { get; set; }
    public int Expired { get; set; }
    public int ExpiringSoon { get; set; }
    public int Active { get; set; }
}

public sealed class LapsCoverageSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string DomainController { get; set; } = string.Empty;
    public string SearchBaseDn { get; set; } = string.Empty;
    public bool Truncated { get; set; }
    public LapsCoverageSummary Summary { get; set; } = new();
    public List<LapsCoverageDevice> Devices { get; set; } = [];
}

public sealed class LapsCoverageChange
{
    public string IdentityKey { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string PreviousState { get; set; } = string.Empty;
    public string CurrentState { get; set; } = string.Empty;
}

public sealed class LapsCoverageComparison
{
    public DateTime PreviousAtUtc { get; set; }
    public DateTime CurrentAtUtc { get; set; }
    public string SearchBaseDn { get; set; } = string.Empty;
    public List<LapsCoverageChange> Changes { get; set; } = [];
}
