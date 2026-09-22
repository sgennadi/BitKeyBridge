namespace BitKeyBridge;

public static class AppPaths
{
    public static string LocalConfigDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();
            return Path.Combine(root, "BitKeyBridge");
        }
    }

    public static string MachineConfigDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(root, "BitKeyBridge");
        }
    }

    public static string CloudConfigFile => Path.Combine(LocalConfigDirectory, "cloud_auth_config.json");
    public static string MachineCloudConfigFile => Path.Combine(MachineConfigDirectory, "cloud_auth_machine.json");
    public static string CoverageStatusFile => Path.Combine(MachineConfigDirectory, "coverage_status.json");
    public static string AuditLogFile => Path.Combine(MachineConfigDirectory, "audit.jsonl");
    public static string AuditIntegrityStatusFile => Path.Combine(MachineConfigDirectory, "audit_integrity_status.json");
    public static string AuditSigningCheckpointFile => Path.Combine(MachineConfigDirectory, "audit_signing_checkpoint.json");
    public static string AuditSigningTransitionFile => Path.Combine(MachineConfigDirectory, "audit_signing_transition.json");
    public static string AuditSigningTransitionsDirectory => Path.Combine(MachineConfigDirectory, "AuditSigningTransitions");
    public static string IncidentsDirectory => Path.Combine(MachineConfigDirectory, "Incidents");
    public static string ServiceLogFile => Path.Combine(MachineConfigDirectory, "service.log");
    public static string UpdateStatusFile => Path.Combine(MachineConfigDirectory, "update_status.json");
    public static string UpdatesDirectory => Path.Combine(MachineConfigDirectory, "Updates");
    public static string SecretsDirectory => Path.Combine(MachineConfigDirectory, "Secrets");
    public static string MachineAdCredentialFile => Path.Combine(SecretsDirectory, "ad-machine.cred");
    public static string AppSettingsFile => Path.Combine(MachineConfigDirectory, "appsettings.json");
    public static string ConfigMigrationStatusFile => Path.Combine(MachineConfigDirectory, "config_migration_status.json");
    public static string BackupsDirectory => Path.Combine(MachineConfigDirectory, "Backups");
    public static string HousekeepingStatusFile => Path.Combine(MachineConfigDirectory, "housekeeping_status.json");
    public static string StorageSecurityStatusFile => Path.Combine(MachineConfigDirectory, "storage_security_status.json");
    public static string AccessControlDirectory => Path.Combine(MachineConfigDirectory, "AccessControl");
    public static string JitGrantsDirectory => Path.Combine(AccessControlDirectory, "JitGrants");
    public static string ApprovalRequestsDirectory => Path.Combine(AccessControlDirectory, "ApprovalRequests");
    public static string ApprovalDecisionsDirectory => Path.Combine(AccessControlDirectory, "ApprovalDecisions");
    public static string PrivilegedAccessStatusFile => Path.Combine(AccessControlDirectory, "privileged_access_status.json");
    public static string SiemDirectory => Path.Combine(MachineConfigDirectory, "SIEM");
    public static string SiemOutboxDirectory => Path.Combine(SiemDirectory, "Outbox");
    public static string SiemStatusFile => Path.Combine(SiemDirectory, "siem_status.json");
    public static string DefaultSiemJsonlFile => Path.Combine(SiemDirectory, "bitkeybridge-siem.jsonl");
    public static string ServiceInstallDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BitKeyBridge");
    public static string ServiceExecutable => Path.Combine(ServiceInstallDirectory, "BitKeyBridge.exe");
}
