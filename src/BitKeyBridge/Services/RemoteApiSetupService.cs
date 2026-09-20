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
        ConfigService.SaveAppConfig(config);
        WindowsFirewallService.RemoveRemoteApiRule();

        WindowsEventLogService.TryWrite(
            "Remote API disabled.",
            EventLogSeverity.Information,
            4301,
            "RemoteAPI");
    }

    public RemoteApiSetupResult RegenerateToken(AppConfig config)
    {
        if (!config.RemoteApiEnabled)
            throw new InvalidOperationException("Remote API is not enabled.");
        return Enable(config, config.RemoteApiPort, config.RemoteApiAllowManagement);
    }
}
