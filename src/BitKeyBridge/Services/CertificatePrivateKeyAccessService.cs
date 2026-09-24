using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace BitKeyBridge;

public sealed class CertificateKeyAccessInfo
{
    public string Thumbprint { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Sid { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public bool KeyFileExists { get; set; }
    public bool AccessRequired { get; set; } = true;
    public bool ExplicitReadAllowed { get; set; }
    public bool ExplicitReadDenied { get; set; }
    public string Status =>
        !KeyFileExists
            ? "KeyFileMissing"
            : !AccessRequired
                ? "NotRequired"
                : ExplicitReadDenied
                    ? "Denied"
                    : ExplicitReadAllowed
                        ? "Allowed"
                        : "NotGranted";
}

public sealed class CertificatePrivateKeyAccessService
{
    public CertificateKeyAccessInfo GetStatus(
        string thumbprint,
        string account)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Certificate private-key ACL management is only available on Windows.");

        var cert = new CertificateService()
            .FindLocalMachineByThumbprint(thumbprint);
        var target = ResolveAccount(account);
        var key = ResolvePrivateKey(cert);

        var result = new CertificateKeyAccessInfo
        {
            Thumbprint = NormalizeThumbprint(cert.Thumbprint),
            Account = target.Account,
            Sid = target.Sid.Value,
            Provider = key.Provider,
            KeyPath = key.Path,
            KeyFileExists = File.Exists(key.Path),
            AccessRequired = !IsLocalSystem(target.Account)
        };

        if (!result.KeyFileExists || !result.AccessRequired)
            return result;

        var security = new FileInfo(key.Path)
            .GetAccessControl(AccessControlSections.Access);

        var rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                targetType: typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Where(rule =>
                rule.IdentityReference is SecurityIdentifier sid &&
                sid.Equals(target.Sid))
            .ToList();

        const FileSystemRights required =
            FileSystemRights.ReadData |
            FileSystemRights.ReadAttributes |
            FileSystemRights.ReadExtendedAttributes |
            FileSystemRights.ReadPermissions;

        result.ExplicitReadDenied = rules.Any(rule =>
            rule.AccessControlType == AccessControlType.Deny &&
            (rule.FileSystemRights & required) != 0);

        result.ExplicitReadAllowed = !result.ExplicitReadDenied &&
            rules.Any(rule =>
                rule.AccessControlType == AccessControlType.Allow &&
                (rule.FileSystemRights & required) == required);

        return result;
    }

    public CertificateKeyAccessInfo GrantRead(
        string thumbprint,
        string account)
    {
        RequireAdministrator();

        var current = GetStatus(thumbprint, account);
        if (!current.AccessRequired ||
            (current.ExplicitReadAllowed && !current.ExplicitReadDenied))
        {
            return current;
        }

        var cert = new CertificateService()
            .FindLocalMachineByThumbprint(thumbprint);
        var target = ResolveAccount(account);
        var key = ResolvePrivateKey(cert);

        if (!File.Exists(key.Path))
            throw new FileNotFoundException(
                "The certificate private-key file was not found.",
                key.Path);

        var file = new FileInfo(key.Path);
        var security = file.GetAccessControl(AccessControlSections.Access);
        security.AddAccessRule(
            new FileSystemAccessRule(
                target.Sid,
                FileSystemRights.Read,
                AccessControlType.Allow));
        file.SetAccessControl(security);

        var status = GetStatus(thumbprint, target.Account);
        if (!status.ExplicitReadAllowed || status.ExplicitReadDenied)
        {
            throw new InvalidOperationException(
                $"Read access could not be verified for {target.Account} on the certificate private key.");
        }

        WindowsEventLogService.TryWrite(
            $"Certificate private-key read access granted to {target.Account}; Thumbprint={status.Thumbprint}; Provider={status.Provider}.",
            EventLogSeverity.Warning,
            4050,
            "CertificateACL");

        return status;
    }

    public CertificateKeyAccessInfo RevokeRead(
        string thumbprint,
        string account)
    {
        RequireAdministrator();

        var cert = new CertificateService()
            .FindLocalMachineByThumbprint(thumbprint);
        var target = ResolveAccount(account);
        var key = ResolvePrivateKey(cert);

        if (!File.Exists(key.Path))
            throw new FileNotFoundException(
                "The certificate private-key file was not found.",
                key.Path);

        var file = new FileInfo(key.Path);
        var security = file.GetAccessControl(AccessControlSections.Access);

        security.RemoveAccessRuleSpecific(
            new FileSystemAccessRule(
                target.Sid,
                FileSystemRights.Read,
                AccessControlType.Allow));

        file.SetAccessControl(security);

        var status = GetStatus(thumbprint, target.Account);

        WindowsEventLogService.TryWrite(
            $"Certificate private-key read ACL removed for {target.Account}; Thumbprint={status.Thumbprint}; Provider={status.Provider}.",
            EventLogSeverity.Warning,
            4051,
            "CertificateACL");

        return status;
    }

    public CertificateKeyAccessInfo EnsureServiceAccess(
        string thumbprint,
        string serviceIdentity)
    {
        var normalized =
            NormalizeServiceIdentity(
                serviceIdentity);
        var status =
            GetStatus(
                thumbprint,
                normalized);

        if (!status.KeyFileExists)
        {
            throw new FileNotFoundException(
                "The certificate private-key file was not found.",
                status.KeyPath);
        }

        if (IsLocalSystem(
                normalized))
        {
            return status;
        }

        if (status.ExplicitReadAllowed &&
            !status.ExplicitReadDenied)
        {
            return status;
        }

        return GrantRead(
            thumbprint,
            normalized);
    }

    public static string NormalizeServiceIdentity(string identity)
    {
        var value = (identity ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Service identity is required.");

        return IsLocalSystem(value)
            ? @"NT AUTHORITY\SYSTEM"
            : value;
    }

    public static bool IsLocalSystem(string identity) =>
        identity.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase) ||
        identity.Equals(@"NT AUTHORITY\SYSTEM", StringComparison.OrdinalIgnoreCase) ||
        identity.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase);

    private static (string Account, SecurityIdentifier Sid) ResolveAccount(
        string account)
    {
        var normalized = NormalizeServiceIdentity(account);

        try
        {
            var nt = new NTAccount(normalized);
            var sid = (SecurityIdentifier)nt.Translate(
                typeof(SecurityIdentifier));
            return (normalized, sid);
        }
        catch (IdentityNotMappedException ex)
        {
            throw new InvalidOperationException(
                $"Windows could not resolve service identity '{normalized}' to a SID.",
                ex);
        }
    }

    private static (string Path, string Provider) ResolvePrivateKey(
        X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException(
                "The configured Entra certificate does not expose an RSA private key.");

        if (rsa is RSACng cng)
        {
            var uniqueName = cng.Key.UniqueName;
            if (string.IsNullOrWhiteSpace(uniqueName))
                throw new InvalidOperationException(
                    "The CNG private key has no persistent unique name.");

            var common = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            return (
                Path.Combine(
                    common,
                    "Microsoft",
                    "Crypto",
                    "Keys",
                    uniqueName),
                "CNG");
        }

        if (rsa is RSACryptoServiceProvider csp)
        {
            var uniqueName =
                csp.CspKeyContainerInfo.UniqueKeyContainerName;
            if (string.IsNullOrWhiteSpace(uniqueName))
                throw new InvalidOperationException(
                    "The CAPI private key has no persistent container name.");

            var common = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            return (
                Path.Combine(
                    common,
                    "Microsoft",
                    "Crypto",
                    "RSA",
                    "MachineKeys",
                    uniqueName),
                "CAPI");
        }

        throw new NotSupportedException(
            $"Unsupported RSA private-key provider: {rsa.GetType().FullName}");
    }

    private static void RequireAdministrator()
    {
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException(
                "Administrator rights are required to modify certificate private-key ACLs.");
    }

    private static string NormalizeThumbprint(string? value) =>
        new((value ?? string.Empty)
            .Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
