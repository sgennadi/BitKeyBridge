using System.Security.Cryptography;

namespace BitKeyBridge;

public sealed class RemoteApiSetupService
{
    public RemoteApiSetupResult Enable(AppConfig config, int port, bool allowManagement)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Remote API setup is only supported on Windows.");
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException("Administrator rights are required to configure the Remote API.");

        port = Math.Clamp(port, 1024, 65535);
        var certificateService = new CertificateService();
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate;

        if (!string.IsNullOrWhiteSpace(config.RemoteApiCertificateThumbprint))
        {
            try
            {
                certificate = certificateService.FindByThumbprint(config.RemoteApiCertificateThumbprint);
            }
            catch
            {
                certificate = certificateService.CreateRemoteApiCertificate(
                    $"BitKeyBridge Remote API - {Environment.MachineName}");
            }
        }
        else
        {
            certificate = certificateService.CreateRemoteApiCertificate(
                $"BitKeyBridge Remote API - {Environment.MachineName}");
        }

        var installedService =
            WindowsServiceHost.GetInfo();

        if (installedService.Installed &&
            !string.IsNullOrWhiteSpace(
                installedService.Identity))
        {
            new CertificatePrivateKeyAccessService()
                .EnsureServiceAccess(
                    certificate.Thumbprint,
                    installedService.Identity);
        }

        var previousEnabled = config.RemoteApiEnabled;
        var previousPort = config.RemoteApiPort;
        var previousManagement = config.RemoteApiAllowManagement;
        var previousThumbprint = config.RemoteApiCertificateThumbprint;
        var previousTokenHash = config.RemoteApiTokenSha256;

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        try
        {
            var tokenHash = SHA256.HashData(tokenBytes);

            WindowsFirewallService.EnsureRemoteApiRule(port);

            config.RemoteApiEnabled = true;
            config.RemoteApiPort = port;
            config.RemoteApiAllowManagement = allowManagement;
            config.RemoteApiCertificateThumbprint = certificate.Thumbprint;
            config.RemoteApiTokenSha256 = Convert.ToHexString(tokenHash).ToLowerInvariant();
            ConfigService.SaveAppConfig(config);

            WindowsEventLogService.TryWrite(
                $"Remote API enabled on TCP {port}. Management={allowManagement}.",
                EventLogSeverity.Warning,
                4300,
                "RemoteAPI");

            return new RemoteApiSetupResult
            {
                Token = token,
                CertificateThumbprint = certificate.Thumbprint,
                CertificateExpires = certificate.NotAfter,
                Port = port
            };
        }
        catch
        {
            config.RemoteApiEnabled = previousEnabled;
            config.RemoteApiPort = previousPort;
            config.RemoteApiAllowManagement = previousManagement;
            config.RemoteApiCertificateThumbprint = previousThumbprint;
            config.RemoteApiTokenSha256 = previousTokenHash;

            try
            {
                if (previousEnabled)
                    WindowsFirewallService.EnsureRemoteApiRule(previousPort);
                else
                    WindowsFirewallService.RemoveRemoteApiRule();
            }
            catch { }

            try { ConfigService.SaveAppConfig(config); } catch { }
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    public void Disable(AppConfig config)
    {
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException("Administrator rights are required to disable the Remote API.");

        config.RemoteApiEnabled = false;
        config.RemoteApiAllowManagement = false;
        config.RemoteApiTokenSha256 = string.Empty;
        config.RemoteApiReadTokenSha256 = string.Empty;
        config.RemoteApiCoverageRunTokenSha256 = string.Empty;
        config.RemoteApiExportTokenSha256 = string.Empty;
        ConfigService.SaveAppConfig(config);
        WindowsFirewallService.RemoveRemoteApiRule();

        WindowsEventLogService.TryWrite(
            "Remote API disabled.",
            EventLogSeverity.Information,
            4301,
            "RemoteAPI");
    }

    public RemoteApiScopedTokenResult GenerateScopedToken(
        AppConfig config,
        string scope)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Remote API setup is only supported on Windows.");
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException(
                "Administrator rights are required to configure Remote API tokens.");
        if (!config.RemoteApiEnabled)
            throw new InvalidOperationException(
                "Remote API is not enabled.");

        var normalized = NormalizeScope(scope);

        var tokenBytes =
            RandomNumberGenerator.GetBytes(32);
        try
        {
            var hash = Convert.ToHexString(
                    SHA256.HashData(tokenBytes))
                .ToLowerInvariant();

            SetScopeHash(
                config,
                normalized,
                hash);
            ConfigService.SaveAppConfig(config);

            WindowsEventLogService.TryWrite(
                $"Remote API scoped token generated. Scope={normalized}.",
                EventLogSeverity.Warning,
                4305,
                "RemoteAPI");

            return new RemoteApiScopedTokenResult
            {
                Scope = normalized,
                Token =
                    Convert.ToBase64String(
                        tokenBytes),
                Port = config.RemoteApiPort
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                tokenBytes);
        }
    }

    public void RevokeScopedToken(
        AppConfig config,
        string scope)
    {
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException(
                "Administrator rights are required to revoke Remote API tokens.");

        var normalized = NormalizeScope(scope);
        SetScopeHash(
            config,
            normalized,
            string.Empty);
        ConfigService.SaveAppConfig(config);

        WindowsEventLogService.TryWrite(
            $"Remote API scoped token revoked. Scope={normalized}.",
            EventLogSeverity.Warning,
            4306,
            "RemoteAPI");
    }

    public static string NormalizeScope(
        string scope)
    {
        var value =
            (scope ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        return value switch
        {
            "read" or "readonly" or "read-only" =>
                "read",
            "coverage" or "coverage-run" =>
                "coverage-run",
            "export" =>
                "export",
            _ => throw new ArgumentException(
                "Remote API scope must be read, coverage-run, or export.")
        };
    }

    private static void SetScopeHash(
        AppConfig config,
        string scope,
        string hash)
    {
        switch (scope)
        {
            case "read":
                config.RemoteApiReadTokenSha256 =
                    hash;
                break;
            case "coverage-run":
                config.RemoteApiCoverageRunTokenSha256 =
                    hash;
                break;
            case "export":
                config.RemoteApiExportTokenSha256 =
                    hash;
                break;
            default:
                throw new ArgumentException(
                    "Unsupported Remote API scope.");
        }
    }

    public RemoteApiSetupResult RegenerateToken(AppConfig config)
    {
        if (!config.RemoteApiEnabled)
            throw new InvalidOperationException("Remote API is not enabled.");
        return Enable(config, config.RemoteApiPort, config.RemoteApiAllowManagement);
    }
}
