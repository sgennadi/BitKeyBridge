using System.Security.AccessControl;
using System.Security.Principal;

namespace BitKeyBridge;

public sealed class AclService
{
    public List<string> GetBroadReadWarnings(string directoryPath, string domainSid)
    {
        var broadSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "S-1-1-0",
            "S-1-5-11",
            "S-1-5-32-545",
            domainSid + "-513"
        };

        var warnings = new List<string>();
        var security = new DirectoryInfo(directoryPath).GetAccessControl(AccessControlSections.Access);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        var readMask = FileSystemRights.Read;

        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow) continue;
            if (rule.IdentityReference is not SecurityIdentifier sid) continue;
            if (!broadSids.Contains(sid.Value)) continue;
            if ((rule.FileSystemRights & readMask) == 0) continue;
            warnings.Add($"{sid.Value} [{rule.FileSystemRights}] Inherited={rule.IsInherited}");
        }
        return warnings;
    }
}
