using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BitKeyBridge;

public sealed class SecureOutputService
{
    private const uint StypeDiskTree = 0;
    private const uint ShareAccessRead = 0x00000001;
    private const uint ShareAccessAll = 0x0000007F;
    private const uint NerrSuccess = 0;
    private const uint NerrDuplicateShare = 2118;

    public SecureOutputResult Create(
        string directoryPath,
        IEnumerable<string> readerPrincipals,
        string? shareName = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Secure Windows ACL/share creation is only supported on Windows.");

        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Output directory is required.", nameof(directoryPath));

        var fullPath = Path.GetFullPath(directoryPath.Trim());
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidOperationException("Secure Output Wizard requires a local directory, not a UNC path.");
        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A drive root cannot be used as the protected BitLocker output directory.");

        var readers = readerPrincipals
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Directory.CreateDirectory(fullPath);
        ApplyNtfsAcl(fullPath, readers);

        var result = new SecureOutputResult
        {
            DirectoryPath = fullPath,
            ReaderPrincipals = readers
        };

        if (!string.IsNullOrWhiteSpace(shareName))
        {
            var normalizedShare = ValidateShareName(shareName.Trim());
            CreateShare(fullPath, normalizedShare, readers);
            result.ShareName = normalizedShare;
            result.ShareCreated = true;
        }

        return result;
    }

    private static void ApplyNtfsAcl(string path, IReadOnlyCollection<string> readers)
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var current = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows user SID is unavailable.");

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(administrators);

        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(
            system,
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            administrators,
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            current,
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        foreach (var principal in readers)
        {
            var sid = ResolveSid(principal);
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
                inherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void CreateShare(string path, string shareName, IReadOnlyCollection<string> readers)
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var current = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows user SID is unavailable.");

        var dacl = new DiscretionaryAcl(false, false, Math.Max(8, readers.Count + 3));
        dacl.AddAccess(AccessControlType.Allow, system, (int)ShareAccessAll, InheritanceFlags.None, PropagationFlags.None);
        dacl.AddAccess(AccessControlType.Allow, administrators, (int)ShareAccessAll, InheritanceFlags.None, PropagationFlags.None);
        dacl.AddAccess(AccessControlType.Allow, current, (int)ShareAccessAll, InheritanceFlags.None, PropagationFlags.None);
        foreach (var principal in readers)
            dacl.AddAccess(AccessControlType.Allow, ResolveSid(principal), (int)ShareAccessRead, InheritanceFlags.None, PropagationFlags.None);

        var descriptor = new CommonSecurityDescriptor(
            isContainer: false,
            isDS: false,
            ControlFlags.DiscretionaryAclPresent,
            administrators,
            administrators,
            null,
            dacl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);

        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var info = new SHARE_INFO_502
            {
                shi502_netname = shareName,
                shi502_type = StypeDiskTree,
                shi502_remark = "BitKeyBridge protected BitLocker recovery output",
                shi502_permissions = 0,
                shi502_max_uses = uint.MaxValue,
                shi502_current_uses = 0,
                shi502_path = path,
                shi502_passwd = null,
                shi502_reserved = 0,
                shi502_security_descriptor = pin.AddrOfPinnedObject()
            };

            var status = NetShareAdd(null, 502, ref info, out var parameterError);
            if (status == NerrDuplicateShare)
                throw new InvalidOperationException(
                    $"SMB share '{shareName}' already exists. BitKeyBridge did not modify the existing share.");
            if (status != NerrSuccess)
                throw new InvalidOperationException(
                    $"NetShareAdd failed with Windows network error {status} (parameter {parameterError}).");
        }
        finally
        {
            pin.Free();
        }
    }

    private static SecurityIdentifier ResolveSid(string principal)
    {
        try
        {
            if (principal.StartsWith("S-", StringComparison.OrdinalIgnoreCase))
                return new SecurityIdentifier(principal);
            return (SecurityIdentifier)new NTAccount(principal).Translate(typeof(SecurityIdentifier));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Windows principal '{principal}' could not be resolved.", ex);
        }
    }

    private static string ValidateShareName(string name)
    {
        if (name.Length is < 1 or > 80)
            throw new InvalidOperationException("SMB share name must be between 1 and 80 characters.");
        if (name.IndexOfAny(['\\', '/', '[', ']', ':', '|', '<', '>', '+', '=', ';', ',', '?', '*']) >= 0)
            throw new InvalidOperationException("SMB share name contains a character that Windows does not allow.");
        return name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHARE_INFO_502
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string shi502_netname;
        public uint shi502_type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? shi502_remark;
        public uint shi502_permissions;
        public uint shi502_max_uses;
        public uint shi502_current_uses;
        [MarshalAs(UnmanagedType.LPWStr)] public string shi502_path;
        [MarshalAs(UnmanagedType.LPWStr)] public string? shi502_passwd;
        public uint shi502_reserved;
        public IntPtr shi502_security_descriptor;
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint NetShareAdd(
        string? serverName,
        uint level,
        ref SHARE_INFO_502 buffer,
        out uint parameterError);
}
