using System.Net;

namespace BitKeyBridge;

public sealed class CredentialVaultMetadata
{
    public bool Exists { get; set; }
    public string Storage { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime? CreatedAtUtc { get; set; }
    public string ProtectedBy { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

internal sealed class MachineCredentialPayload
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MachineCredentialAccessInfo
{
    public string Account { get; set; } = string.Empty;
    public string Sid { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool FileExists { get; set; }
    public bool AccessRequired { get; set; }
    public bool DirectoryReadAllowed { get; set; }
    public bool FileReadAllowed { get; set; }
    public string Status =>
        !FileExists
            ? "CredentialFileMissing"
            : !AccessRequired
                ? "NotRequired"
                : DirectoryReadAllowed && FileReadAllowed
                    ? "Allowed"
                    : "NotGranted";
}

public sealed class StoredAdCredential
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public NetworkCredential ToNetworkCredential()
    {
        if (Username.Contains('@'))
            return new NetworkCredential(Username, Password);

        var slash = Username.IndexOf('\\');
        if (slash > 0 && slash + 1 < Username.Length)
            return new NetworkCredential(Username[(slash + 1)..], Password, Username[..slash]);

        return string.IsNullOrWhiteSpace(Domain)
            ? new NetworkCredential(Username, Password)
            : new NetworkCredential(Username, Password, Domain);
    }
}
