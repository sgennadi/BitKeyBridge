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
    public static string ServiceInstallDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BitKeyBridge");
    public static string ServiceExecutable => Path.Combine(ServiceInstallDirectory, "BitKeyBridge.exe");
}
