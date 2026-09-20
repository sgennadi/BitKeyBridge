using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class CredentialVaultService
{
    public const string DefaultTarget = "BitKeyBridge:ActiveDirectory";

    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    private const uint CryptProtectUiForbidden = 0x1;
    private const uint CryptProtectLocalMachine = 0x4;

    private static readonly byte[] Entropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("BitKeyBridge|MachineCredentialVault|v1"));

    public void SaveUserCredential(string target, string username, string password)
    {
        EnsureWindows();
        target = NormalizeTarget(target);
        ValidateCredential(username, password);

        var secretBytes = Encoding.Unicode.GetBytes(password);
        var blob = IntPtr.Zero;
        try
        {
            blob = Marshal.AllocCoTaskMem(secretBytes.Length);
            Marshal.Copy(secretBytes, 0, blob, secretBytes.Length);

            var credential = new CREDENTIAL
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = username.Trim()
            };

            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows Credential Manager could not save the BitKeyBridge credential.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            if (blob != IntPtr.Zero)
            {
                for (var i = 0; i < secretBytes.Length; i++)
                    Marshal.WriteByte(blob, i, 0);
                Marshal.FreeCoTaskMem(blob);
            }
        }
    }

    public StoredAdCredential? ReadUserCredential(string target, string fallbackDomain = "")
    {
        EnsureWindows();
        target = NormalizeTarget(target);

        if (!CredRead(target, CredTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return null;
            throw new Win32Exception(
                error,
                "Windows Credential Manager could not read the BitKeyBridge credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(pointer);
            var password = credential.CredentialBlob == IntPtr.Zero ||
                           credential.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / 2)) ?? string.Empty;

            return new StoredAdCredential
            {
                Username = credential.UserName ?? string.Empty,
                Domain = fallbackDomain,
                Password = password
            };
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public void DeleteUserCredential(string target)
    {
        EnsureWindows();
        target = NormalizeTarget(target);
        if (CredDelete(target, CredTypeGeneric, 0)) return;

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
            throw new Win32Exception(
                error,
                "Windows Credential Manager could not delete the BitKeyBridge credential.");
    }

    public CredentialVaultMetadata GetUserMetadata(string target)
    {
        var credential = ReadUserCredential(target);
        return credential is null
            ? new CredentialVaultMetadata
            {
                Storage = "CurrentUser",
                Target = NormalizeTarget(target),
                ProtectedBy = "Windows Credential Manager",
                Location = "Current Windows user credential set"
            }
            : new CredentialVaultMetadata
            {
                Exists = true,
                Storage = "CurrentUser",
                Target = NormalizeTarget(target),
                Username = credential.Username,
                Domain = credential.Domain,
                ProtectedBy = "Windows Credential Manager",
                Location = "Current Windows user credential set"
            };
    }

    public void SaveMachineCredential(
        string username,
        string domain,
        string password,
        string? path = null)
    {
        EnsureWindows();
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException(
                "Administrator rights are required to write the Machine / Service credential vault.");
        ValidateCredential(username, password);

        path ??= AppPaths.MachineAdCredentialFile;
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Machine credential path has no parent directory.");

        Directory.CreateDirectory(directory);
        SecureMachineSecretsDirectory(directory);

        var payload = new MachineCredentialPayload
        {
            Username = username.Trim(),
            Domain = domain?.Trim() ?? string.Empty,
            Password = password,
            CreatedAtUtc = DateTime.UtcNow
        };

        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = ProtectMachineData(plain);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, protectedBytes);
            SecureMachineCredentialFile(temp);
            File.Move(temp, path, true);
            SecureMachineCredentialFile(path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (protectedBytes is not null)
                CryptographicOperations.ZeroMemory(protectedBytes);
            payload.Password = string.Empty;
        }
    }

    public StoredAdCredential? ReadMachineCredential(string? path = null)
    {
        EnsureWindows();
        path ??= AppPaths.MachineAdCredentialFile;
        if (!File.Exists(path)) return null;

        var protectedBytes = File.ReadAllBytes(path);
        byte[]? plain = null;
        try
        {
            plain = UnprotectMachineData(protectedBytes);
            var payload = JsonSerializer.Deserialize<MachineCredentialPayload>(plain)
                ?? throw new InvalidOperationException("Machine credential payload is invalid.");

            return new StoredAdCredential
            {
                Username = payload.Username,
                Domain = payload.Domain,
                Password = payload.Password
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (plain is not null)
                CryptographicOperations.ZeroMemory(plain);
        }
    }

    public void DeleteMachineCredential(string? path = null)
    {
        EnsureWindows();
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException(
                "Administrator rights are required to delete the Machine / Service credential vault.");
        path ??= AppPaths.MachineAdCredentialFile;
        if (File.Exists(path))
            File.Delete(path);
    }

    public CredentialVaultMetadata GetMachineMetadata(string? path = null)
    {
        path ??= AppPaths.MachineAdCredentialFile;
        var credential = ReadMachineCredential(path);
        DateTime? created = null;

        if (File.Exists(path))
            created = File.GetCreationTimeUtc(path);

        return credential is null
            ? new CredentialVaultMetadata
            {
                Storage = "LocalMachine",
                Target = DefaultTarget,
                ProtectedBy = "Windows DPAPI LocalMachine + NTFS ACL",
                Location = path
            }
            : new CredentialVaultMetadata
            {
                Exists = true,
                Storage = "LocalMachine",
                Target = DefaultTarget,
                Username = credential.Username,
                Domain = credential.Domain,
                CreatedAtUtc = created,
                ProtectedBy = "Windows DPAPI LocalMachine + NTFS ACL",
                Location = path
            };
    }

    public static byte[] ProtectMachineData(byte[] plain)
    {
        EnsureWindows();
        return ProtectOrUnprotect(plain, protect: true);
    }

    public static byte[] UnprotectMachineData(byte[] protectedBytes)
    {
        EnsureWindows();
        return ProtectOrUnprotect(protectedBytes, protect: false);
    }

    private static byte[] ProtectOrUnprotect(byte[] input, bool protect)
    {
        var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        var entropyHandle = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
        var output = default(DATA_BLOB);
        IntPtr description = IntPtr.Zero;

        try
        {
            var inputBlob = new DATA_BLOB
            {
                cbData = input.Length,
                pbData = inputHandle.AddrOfPinnedObject()
            };
            var entropyBlob = new DATA_BLOB
            {
                cbData = Entropy.Length,
                pbData = entropyHandle.AddrOfPinnedObject()
            };

            var ok = protect
                ? CryptProtectData(
                    ref inputBlob,
                    "BitKeyBridge machine AD credential v1",
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden | CryptProtectLocalMachine,
                    out output)
                : CryptUnprotectData(
                    ref inputBlob,
                    out description,
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out output);

            if (!ok)
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    protect
                        ? "Windows DPAPI could not protect the machine credential."
                        : "Windows DPAPI could not unprotect the machine credential.");

            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (output.pbData != IntPtr.Zero)
                LocalFree(output.pbData);
            if (description != IntPtr.Zero)
                LocalFree(description);
            if (inputHandle.IsAllocated) inputHandle.Free();
            if (entropyHandle.IsAllocated) entropyHandle.Free();
        }
    }

    private static void SecureMachineSecretsDirectory(string directory)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        security.SetOwner(admins);
        security.AddAccessRule(new FileSystemAccessRule(
            system,
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            admins,
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        new DirectoryInfo(directory).SetAccessControl(security);
    }

    private static void SecureMachineCredentialFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        security.SetOwner(admins);
        security.AddAccessRule(new FileSystemAccessRule(
            system,
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            admins,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        new FileInfo(path).SetAccessControl(security);
    }

    private static string NormalizeTarget(string? target) =>
        string.IsNullOrWhiteSpace(target) ? DefaultTarget : target.Trim();

    private static void ValidateCredential(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("AD username is required.");
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("AD password is required.");
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows credential protection is only available on Windows.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint reservedFlag,
        out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB dataIn,
        string description,
        ref DATA_BLOB optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DATA_BLOB dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB dataIn,
        out IntPtr description,
        ref DATA_BLOB optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DATA_BLOB dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
