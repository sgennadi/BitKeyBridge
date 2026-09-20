using System.Net;

namespace BitKeyBridge;

public static class AdSessionCredentials
{
    private static readonly object Sync = new();
    private static string _password = string.Empty;

    public static bool HasPassword
    {
        get
        {
            lock (Sync) return !string.IsNullOrEmpty(_password);
        }
    }

    public static void SetPassword(string? password)
    {
        lock (Sync) _password = password ?? string.Empty;
    }

    public static string GetPasswordCopy()
    {
        lock (Sync) return _password;
    }

    public static void Clear()
    {
        lock (Sync) _password = string.Empty;
    }

    public static NetworkCredential? CreateNetworkCredential(AppConfig config)
    {
        if (!config.AdUseExplicitCredentials)
            return null;

        var mode = string.IsNullOrWhiteSpace(config.AdCredentialStorageMode)
            ? "Session"
            : config.AdCredentialStorageMode.Trim();

        var vault = new CredentialVaultService();

        if (mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
        {
            var credential = vault.ReadUserCredential(
                config.AdCredentialTarget,
                config.AdDomain);
            return credential?.ToNetworkCredential()
                ?? throw new InvalidOperationException(
                    "No Current User AD credential is stored for BitKeyBridge. " +
                    "Save it in Directory Connection or switch credential storage mode.");
        }

        if (mode.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase))
        {
            var credential = vault.ReadMachineCredential();
            return credential?.ToNetworkCredential()
                ?? throw new InvalidOperationException(
                    "No Machine / Service AD credential is stored for BitKeyBridge. " +
                    "Save it from an elevated GUI or switch credential storage mode.");
        }

        var username = config.AdUsername?.Trim() ?? string.Empty;
        string password;
        lock (Sync) password = _password;

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException(
                "Explicit AD credentials are enabled, but no AD username is configured.");
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException(
                "Session credential mode is selected, but no session password is loaded. " +
                "Enter the password in Directory Connection or use --ad-password-prompt.");

        if (username.Contains('@'))
            return new NetworkCredential(username, password);

        var slash = username.IndexOf('\\');
        if (slash > 0 && slash + 1 < username.Length)
            return new NetworkCredential(
                username[(slash + 1)..],
                password,
                username[..slash]);

        var domain = config.AdDomain?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(domain)
            ? new NetworkCredential(username, password)
            : new NetworkCredential(username, password, domain);
    }
}
