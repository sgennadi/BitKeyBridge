using System.Security.AccessControl;
using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>
/// Checks whether a machine audit journal is protected against modification
/// by ordinary local users. A hash chain alone is not evidence of protection
/// when the caller can replace the file or delete its directory.
/// This is a conservative ACL check, not an installer or privilege broker.
/// </summary>
internal static class ProtectedAuditStoragePolicy
{
    private const string SystemSid = "S-1-5-18";
    private const string AdministratorsSid = "S-1-5-32-544";

    private const FileSystemRights MutationRights =
        FileSystemRights.WriteData |
        FileSystemRights.AppendData |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.WriteAttributes |
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.Delete |
        FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;

    internal static bool IsTrusted(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            var machineRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(AppPaths.MachineConfigDirectory));

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) ||
                (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                return false;

            // A protected file can be replaced when any of its parent
            // directories is writable by an untrusted principal.
            var normalizedDirectory = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(directory));
            if (!string.Equals(normalizedDirectory, machineRoot,
                    StringComparison.OrdinalIgnoreCase) &&
                !normalizedDirectory.StartsWith(
                    machineRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            var serviceSid = ResolveServiceSid();
            var reachedMachineRoot = false;
            for (DirectoryInfo? current = new(normalizedDirectory);
                 current is not null; current = current.Parent)
            {
                // Never follow a user-controllable junction or symbolic link,
                // even if the target has apparently trusted ACLs.
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;

                var security = current.GetAccessControl(
                    AccessControlSections.Owner | AccessControlSections.Access);
                if (!IsTrustedOwner(security, serviceSid) ||
                    !HasNoUntrustedWriter(security, serviceSid))
                    return false;

                if (string.Equals(
                        Path.TrimEndingDirectorySeparator(current.FullName),
                        machineRoot, StringComparison.OrdinalIgnoreCase))
                {
                    reachedMachineRoot = true;
                    break;
                }
            }

            if (!reachedMachineRoot)
                return false;

            var fileSecurity = new FileInfo(fullPath).GetAccessControl(
                AccessControlSections.Owner | AccessControlSections.Access);
            return IsTrustedOwner(fileSecurity, serviceSid) &&
                   HasNoUntrustedWriter(fileSecurity, serviceSid);
        }
        catch
        {
            // Missing ACL visibility or uncertain file identity fails closed.
            return false;
        }
    }

    private static SecurityIdentifier? ResolveServiceSid()
    {
        try
        {
            return (SecurityIdentifier)new NTAccount(
                "NT SERVICE\\" + WindowsServiceHost.ServiceName)
                .Translate(typeof(SecurityIdentifier));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTrustedOwner(
        FileSystemSecurity security,
        SecurityIdentifier? serviceSid)
    {
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier sid)
            return false;
        return IsAllowedWriter(sid, serviceSid);
    }

    private static bool HasNoUntrustedWriter(
        FileSystemSecurity security,
        SecurityIdentifier? serviceSid)
    {
        var rules = security.GetAccessRules(
            includeExplicit: true, includeInherited: true,
            targetType: typeof(SecurityIdentifier));

        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow ||
                rule.IdentityReference is not SecurityIdentifier sid)
                continue;

            if ((rule.FileSystemRights & MutationRights) != 0 &&
                !IsAllowedWriter(sid, serviceSid))
                return false;
        }

        return true;
    }

    private static bool IsAllowedWriter(
        SecurityIdentifier sid,
        SecurityIdentifier? serviceSid) =>
        sid.Value is SystemSid or AdministratorsSid ||
        (serviceSid is not null && sid.Equals(serviceSid));
}
