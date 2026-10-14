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
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            var dirSecurity = new DirectoryInfo(directory)
                .GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
            var fileSecurity = new FileInfo(path)
                .GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);

            var serviceSid = ResolveServiceSid();
            return IsTrustedOwner(dirSecurity, serviceSid) &&
                   IsTrustedOwner(fileSecurity, serviceSid) &&
                   HasNoUntrustedWriter(dirSecurity, serviceSid) &&
                   HasNoUntrustedWriter(fileSecurity, serviceSid);
        }
        catch
        {
            // An uninspectable ACL cannot prove machine audit protection.
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
