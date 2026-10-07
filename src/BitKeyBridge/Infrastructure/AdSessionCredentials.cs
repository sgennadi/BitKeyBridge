using System.Net;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace BitKeyBridge;

public static class AdSessionCredentials
{
    private static readonly object Sync = new();
    private static char[] _password = [];

    public static bool HasPassword
    {
        get
        {
            lock (Sync)
            {
                return _password.Length > 0;
            }
        }
    }

    public static void SetPassword(string? password)
    {
        lock (Sync)
        {
            ZeroPasswordBuffer();

            _password =
                string.IsNullOrEmpty(password)
                    ? []
                    : password.ToCharArray();
        }
    }

    public static string GetPasswordCopy()
    {
        lock (Sync)
        {
            return _password.Length == 0
                ? string.Empty
                : new string(_password);
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            ZeroPasswordBuffer();
            _password = [];
        }
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
                    "Save it in Start > Advanced or switch credential storage mode.");
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

        lock (Sync)
        {
            password =
                _password.Length == 0
                    ? string.Empty
                    : new string(_password);
        }

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException(
                "Explicit AD credentials are enabled, but no AD username is configured.");
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException(
                "Session credential mode is selected, but no session password is loaded. " +
                "Enter the password from Start > Connect to AD / Advanced or use --ad-password-prompt.");

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

    public static T RunWithNetworkIdentity<T>(NetworkCredential? credential, Func<T> action)
    {
        if (credential is null)
            return action();
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Explicit Windows LAPS decryption requires Windows.");

        var username = credential.UserName;
        string? domain = username.Contains('@') ? null : credential.Domain;
        // NEW_CREDENTIALS supports remote AD accounts on standalone workstations.
        // LDAP bind validates the credentials before any LAPS read. Do not fall back
        // to the process identity when impersonation or decryption fails.
        if (!LogonUserW(username, domain, credential.Password, 9, 3, out var token))
        {
            var error = Marshal.GetLastWin32Error();
            token?.Dispose();
            throw new Win32Exception(error, "Could not use the explicit AD account for Windows LAPS decryption.");
        }
        using (token)
            return WindowsIdentity.RunImpersonated(token, action);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUserW(string username, string? domain, string password,
        int logonType, int provider, out SafeAccessTokenHandle token);

    private static void ZeroPasswordBuffer()
    {
        if (_password.Length == 0)
            return;

        Array.Clear(
            _password,
            0,
            _password.Length);
    }
}
