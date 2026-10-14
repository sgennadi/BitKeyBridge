using System.Security.AccessControl;
using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>
/// Explicit administrator-only provisioning. Harden the machine config (read
/// access for Helpdesk, no write) and a SEPARATE journal directory writable
/// only by SYSTEM/Administrators. No elevation is requested by ordinary GUI.
/// </summary>
internal static class ProtectedAuditBrokerProvisioning
{
    private static readonly SecurityIdentifier SystemSid =
        new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier AdministratorsSid =
        new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier AuthenticatedUsersSid =
        new(WellKnownSidType.AuthenticatedUserSid, null);

    internal static string Provision()
    {
        if (!OperatingSystem.IsWindows() || !SecurityContext.IsAdministrator())
            throw new UnauthorizedAccessException(
                "Run broker provisioning once from an elevated administrator console.");

        var service = WindowsServiceHost.GetInfo();
        if (!service.Installed ||
            !string.Equals(service.Identity, "LocalSystem",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Install the BitKeyBridge Windows Service with LocalSystem identity first.");

        var config = ConfigService.LoadAppConfig();
        if (!config.RbacEnabled ||
            config.RbacRecoveryReaders.Count == 0)
            throw new InvalidOperationException(
                "Configure RBAC RecoveryReaders before provisioning the audit broker.");

        _ = ProtectedAuditBrokerServer.ResolveExplicitReaders(config.RbacRecoveryReaders);

        Directory.CreateDirectory(AppPaths.MachineConfigDirectory);
        Directory.CreateDirectory(AppPaths.BrokerAuditDirectory);
        RejectReparsePoint(AppPaths.MachineConfigDirectory);
        RejectReparsePoint(AppPaths.BrokerAuditDirectory);
        RejectReparsePoint(AppPaths.AppSettingsFile);

        // Persist the original ACLs in an administrator-only archive BEFORE
        // modifying existing machine policy permissions. A recovery engineer
        // can restore the previous SDDL without guessing at historical ACLs.
        var rootSddl = new DirectoryInfo(AppPaths.MachineConfigDirectory)
            .GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access)
            .GetSecurityDescriptorSddlForm(
                AccessControlSections.Owner | AccessControlSections.Access);
        var configSddl = new FileInfo(AppPaths.AppSettingsFile)
            .GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access)
            .GetSecurityDescriptorSddlForm(
                AccessControlSections.Owner | AccessControlSections.Access);

        // Do not touch existing legacy audit.jsonl (retained for rollback).
        // We protect the parent machine policy folder to prevent a local user
        // from replacing machine RBAC/config files under the service's feet.
        // Secure the NEW isolated folder first; preserve the original root
        // SDDL before touching any existing machine configuration ACLs.
        SecureDirectory(AppPaths.BrokerAuditDirectory, allowUsersRead: false);

        var backupFile = Path.Combine(
            AppPaths.BrokerAuditDirectory,
            "pre-provision-acl-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") +
            "-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        var backup = System.Text.Json.JsonSerializer.Serialize(new
        {
            SavedAtUtc = DateTime.UtcNow,
            OriginalMachineDirectory = AppPaths.MachineConfigDirectory,
            OriginalMachineDirectorySddl = rootSddl,
            OriginalAppConfig = AppPaths.AppSettingsFile,
            OriginalAppConfigSddl = configSddl
        });
        File.WriteAllText(backupFile, backup);
        SecureFile(backupFile, allowUsersRead: false);

        // Only now change the existing machine configuration folder.
        SecureDirectory(AppPaths.MachineConfigDirectory, allowUsersRead: true);

        if (!File.Exists(AppPaths.BrokerAuditLogFile))
            File.WriteAllText(AppPaths.BrokerAuditLogFile, string.Empty);
        RejectReparsePoint(AppPaths.BrokerAuditLogFile);
        SecureFile(AppPaths.BrokerAuditLogFile, allowUsersRead: false);

        if (!AuditIntegrityService.Verify(AppPaths.BrokerAuditLogFile).Valid)
            throw new InvalidOperationException(
                "Existing protected journal has an invalid hash chain. " +
                "Restore it from a trusted backup before proceeding.");

        config.AuditBrokerEnabled = true;
        config.RequireProtectedAuditForRecovery = true;
        ConfigService.SaveAppConfig(config);

        RejectReparsePoint(AppPaths.AppSettingsFile);
        SecureFile(AppPaths.AppSettingsFile, allowUsersRead: true);

        if (!ProtectedAuditStoragePolicy.IsTrusted(AppPaths.AppSettingsFile) ||
            !ProtectedAuditStoragePolicy.IsTrusted(AppPaths.BrokerAuditLogFile))
            throw new InvalidOperationException(
                "Cannot verify machine configuration and protected journal owner/ACL.");

        return "Protected audit broker provisioned. Restart the Windows Service. " +
               "Recovery readers can use strict audit without granting user journal write ACLs.";
    }

    private static void SecureDirectory(string path, bool allowUsersRead)
    {
        var directory = new DirectoryInfo(path);
        var security = directory.GetAccessControl();
        security.SetOwner(AdministratorsSid);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true, includeInherited: true,
                     targetType: typeof(SecurityIdentifier)))
            security.RemoveAccessRuleSpecific(rule);

        security.AddAccessRule(new FileSystemAccessRule(
            SystemSid, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            AdministratorsSid, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        if (allowUsersRead)
            security.AddAccessRule(new FileSystemAccessRule(
                AuthenticatedUsersSid,
                FileSystemRights.Traverse |
                FileSystemRights.ReadAttributes |
                FileSystemRights.ReadPermissions,
                InheritanceFlags.None, PropagationFlags.None,
                AccessControlType.Allow));
        directory.SetAccessControl(security);
    }

    private static void SecureFile(string path, bool allowUsersRead)
    {
        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        security.SetOwner(AdministratorsSid);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true, includeInherited: true,
                     targetType: typeof(SecurityIdentifier)))
            security.RemoveAccessRuleSpecific(rule);

        security.AddAccessRule(new FileSystemAccessRule(
            SystemSid, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            AdministratorsSid, FileSystemRights.FullControl, AccessControlType.Allow));
        if (allowUsersRead)
            security.AddAccessRule(new FileSystemAccessRule(
                AuthenticatedUsersSid, FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(security);
    }

    private static void RejectReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException("Required provisioning path is missing.", path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException(
                "Refusing to provision an audit/config path through a reparse point.");
    }
}
