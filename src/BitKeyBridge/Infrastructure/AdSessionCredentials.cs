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

    public static void Clear()
    {
        lock (Sync) _password = string.Empty;
    }

    public static NetworkCredential? CreateNetworkCredential(AppConfig config)
    {
        if (!config.AdUseExplicitCredentials)
            return null;

        var username = config.AdUsername?.Trim() ?? string.Empty;
        string password;
        lock (Sync) password = _password;

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException(
                "Explicit AD credentials are enabled, but no AD username is configured.");
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException(
                "Explicit AD credentials are enabled, but no session password is loaded. " +
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
