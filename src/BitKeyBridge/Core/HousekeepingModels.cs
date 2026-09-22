namespace BitKeyBridge;

public sealed class HousekeepingStatus
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public bool Success { get; set; }
    public bool DryRun { get; set; }

    public int IncidentFilesScanned { get; set; }
    public int IncidentCandidates { get; set; }
    public int IncidentDeleted { get; set; }
    public int IncidentPreserved { get; set; }
    public int IncidentPreservedNotFullyRetained { get; set; }
    public int IncidentErrors { get; set; }

    public int BackupFilesScanned { get; set; }
    public int BackupCandidates { get; set; }
    public int BackupDeleted { get; set; }
    public int BackupPreservedMinimum { get; set; }
    public int BackupErrors { get; set; }

    public int TemporaryFilesScanned { get; set; }
    public int TemporaryFilesDeleted { get; set; }
    public int TemporaryFileErrors { get; set; }

    public long BytesFreed { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class StorageAclDirectoryStatus
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public bool InheritanceProtected { get; set; }
    public bool SystemFullControl { get; set; }
    public bool AdministratorsFullControl { get; set; }
    public string ServiceIdentity { get; set; } = string.Empty;
    public bool ServiceAccessRequired { get; set; }
    public bool ServiceAccessPresent { get; set; }
    public string ServiceAccessMode { get; set; } = string.Empty;
    public int UnexpectedAllowRules { get; set; }
    public List<string> Problems { get; set; } = [];

    public bool Valid =>
        Exists &&
        InheritanceProtected &&
        SystemFullControl &&
        AdministratorsFullControl &&
        (!ServiceAccessRequired || ServiceAccessPresent) &&
        UnexpectedAllowRules == 0 &&
        Problems.Count == 0;
}

public sealed class StorageSecurityStatus
{
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public bool RepairAttempted { get; set; }
    public bool RepairSucceeded { get; set; }
    public string ServiceIdentity { get; set; } = string.Empty;
    public List<StorageAclDirectoryStatus> Directories { get; set; } = [];
    public List<string> Errors { get; set; } = [];

    public bool Valid =>
        Errors.Count == 0 &&
        Directories.Count > 0 &&
        Directories.All(x => x.Valid);
}
