using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BitKeyBridge;

public sealed class AuditSigningService
{
    private readonly AppConfig _config;

    public AuditSigningService(AppConfig config) => _config = config;

    public X509Certificate2 Setup(int years = 5)
    {
        if (!SecurityContext.IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator rights are required to configure audit signing.");
        }

        X509Certificate2 cert;
        if (!string.IsNullOrWhiteSpace(
                _config.AuditSigningCertificateThumbprint))
        {
            try
            {
                cert = new CertificateService()
                    .FindLocalMachineCertificate(
                        _config.AuditSigningCertificateThumbprint,
                        requirePrivateKey: true,
                        requireCurrentValidity: true);
            }
            catch
            {
                cert = new CertificateService()
                    .CreateAuditSigningCertificate(
                        "BitKeyBridge Audit Signing",
                        years);
            }
        }
        else
        {
            cert = new CertificateService()
                .CreateAuditSigningCertificate(
                    "BitKeyBridge Audit Signing",
                    years);
        }

        _config.AuditSigningEnabled = true;
        _config.AuditSigningCertificateThumbprint =
            NormalizeThumbprint(cert.Thumbprint);
        ConfigService.SaveAppConfig(_config);

        var service = WindowsServiceHost.GetInfo();
        if (service.Installed &&
            !string.IsNullOrWhiteSpace(service.Identity))
        {
            new CertificatePrivateKeyAccessService()
                .EnsureServiceAccess(
                    _config.AuditSigningCertificateThumbprint,
                    service.Identity);
        }

        WindowsEventLogService.TryWrite(
            $"Audit signing configured. Certificate={_config.AuditSigningCertificateThumbprint}; Expires={cert.NotAfter:O}.",
            EventLogSeverity.Warning,
            4520,
            "AuditSigning");

        return cert;
    }

    public void Disable()
    {
        if (!SecurityContext.IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator rights are required to disable audit signing.");
        }

        _config.AuditSigningEnabled = false;
        ConfigService.SaveAppConfig(_config);

        WindowsEventLogService.TryWrite(
            "Audit signing disabled. Existing certificate and checkpoint were retained.",
            EventLogSeverity.Warning,
            4521,
            "AuditSigning");
    }

    public AuditSigningCheckpoint SignCheckpoint(
        string? auditPath = null)
    {
        auditPath ??= AppPaths.AuditLogFile;

        if (!_config.AuditSigningEnabled)
        {
            throw new InvalidOperationException(
                "Audit signing is disabled.");
        }

        if (string.IsNullOrWhiteSpace(
                _config.AuditSigningCertificateThumbprint))
        {
            throw new InvalidOperationException(
                "Audit signing certificate is not configured.");
        }

        var integrity = AuditIntegrityService.Verify(auditPath);
        if (!integrity.Valid)
        {
            throw new InvalidOperationException(
                "The audit chain is not valid and cannot be signed: " +
                integrity.FirstError);
        }

        if (integrity.ChainedEntries <= 0 ||
            string.IsNullOrWhiteSpace(integrity.LastHash))
        {
            throw new InvalidOperationException(
                "There are no hash-chained audit entries to sign.");
        }

        var cert = new CertificateService()
            .FindLocalMachineCertificate(
                _config.AuditSigningCertificateThumbprint,
                requirePrivateKey: true,
                requireCurrentValidity: true);

        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException(
                "The audit-signing certificate does not expose an RSA private key.");

        var checkpoint = new AuditSigningCheckpoint
        {
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            ChainVersion = AuditIntegrityService.CurrentChainVersion,
            FilesChecked = integrity.FilesChecked,
            TotalEntries = integrity.TotalEntries,
            LegacyEntries = integrity.LegacyEntries,
            ChainedEntries = integrity.ChainedEntries,
            LastHash = integrity.LastHash,
            CertificateThumbprint =
                NormalizeThumbprint(cert.Thumbprint),
            SignatureAlgorithm = "RSA-SHA256-PKCS1"
        };

        checkpoint.SignatureBase64 =
            Convert.ToBase64String(
                rsa.SignData(
                    BuildPayload(checkpoint),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1));

        JsonStore.WriteAtomic(
            AppPaths.AuditSigningCheckpointFile,
            checkpoint);

        return checkpoint;
    }

    public AuditSigningVerification VerifyCheckpoint(
        string? auditPath = null)
    {
        auditPath ??= AppPaths.AuditLogFile;

        var result = new AuditSigningVerification
        {
            Configured =
                _config.AuditSigningEnabled &&
                !string.IsNullOrWhiteSpace(
                    _config.AuditSigningCertificateThumbprint)
        };

        if (!result.Configured)
        {
            result.Status = "NotConfigured";
            return result;
        }

        AuditSigningCheckpoint? checkpoint;
        try
        {
            checkpoint =
                JsonStore.Read<AuditSigningCheckpoint>(
                    AppPaths.AuditSigningCheckpointFile);
        }
        catch (Exception ex)
        {
            result.Status = "CheckpointUnreadable";
            result.Error = ex.Message;
            return result;
        }

        if (checkpoint is null)
        {
            result.Status = "NoCheckpoint";
            return result;
        }

        result.CheckpointExists = true;
        result.Checkpoint = checkpoint;

        var configuredThumbprint = NormalizeThumbprint(
            _config.AuditSigningCertificateThumbprint);
        var checkpointThumbprint = NormalizeThumbprint(
            checkpoint.CertificateThumbprint);

        if (!string.Equals(
                configuredThumbprint,
                checkpointThumbprint,
                StringComparison.OrdinalIgnoreCase))
        {
            result.Status = "CertificateMismatch";
            result.Error =
                "The checkpoint signer does not match the configured audit-signing certificate.";
            return result;
        }

        X509Certificate2 cert;
        try
        {
            cert = new CertificateService()
                .FindLocalMachineCertificate(
                    configuredThumbprint,
                    requirePrivateKey: false,
                    requireCurrentValidity: false);

            result.CertificateFound = true;
            result.CertificateHasPrivateKey = cert.HasPrivateKey;
            result.CertificateExpiresUtc =
                cert.NotAfter.ToUniversalTime();
            result.CertificateDaysRemaining =
                (cert.NotAfter.ToUniversalTime() - DateTime.UtcNow)
                .TotalDays;
            result.CertificateExpired =
                cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Status = "CertificateMissing";
            result.Error = ex.Message;
            return result;
        }

        try
        {
            using var rsa = cert.GetRSAPublicKey()
                ?? throw new InvalidOperationException(
                    "The audit-signing certificate does not expose an RSA public key.");

            var signature =
                Convert.FromBase64String(
                    checkpoint.SignatureBase64);

            result.SignatureValid =
                rsa.VerifyData(
                    BuildPayload(checkpoint),
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            result.Status = "SignatureUnreadable";
            result.Error = ex.Message;
            return result;
        }

        var integrity =
            AuditIntegrityService.Verify(auditPath);
        result.AuditChainValid = integrity.Valid;

        if (!integrity.Valid)
        {
            result.Status = "AuditInvalid";
            result.Error = integrity.FirstError;
            return result;
        }

        result.CheckpointHashPresent =
            AuditIntegrityService.ContainsEntryHash(
                auditPath,
                checkpoint.LastHash);

        result.CurrentHeadSigned =
            string.Equals(
                integrity.LastHash,
                checkpoint.LastHash,
                StringComparison.OrdinalIgnoreCase);

        if (!result.SignatureValid)
        {
            result.Status = "InvalidSignature";
            return result;
        }

        if (!result.CheckpointHashPresent)
        {
            result.Status = "CheckpointHashMissing";
            return result;
        }

        if (result.CertificateExpired)
        {
            result.Status = "CertificateExpired";
            return result;
        }

        result.Status = result.CurrentHeadSigned
            ? "Valid"
            : "ValidCheckpointStale";

        return result;
    }

    public CertificateKeyAccessInfo? EnsureServiceAccess(
        string? identity = null,
        bool required = false)
    {
        if (!_config.AuditSigningEnabled ||
            string.IsNullOrWhiteSpace(
                _config.AuditSigningCertificateThumbprint))
        {
            if (required)
            {
                throw new InvalidOperationException(
                    "Audit signing is enabled but no signing certificate is configured.");
            }

            return null;
        }

        var serviceIdentity =
            string.IsNullOrWhiteSpace(identity)
                ? WindowsServiceHost.GetInfo().Identity
                : identity.Trim();

        if (string.IsNullOrWhiteSpace(serviceIdentity))
        {
            if (required)
            {
                throw new InvalidOperationException(
                    "The Windows Service identity could not be determined.");
            }

            return null;
        }

        return new CertificatePrivateKeyAccessService()
            .EnsureServiceAccess(
                _config.AuditSigningCertificateThumbprint,
                serviceIdentity);
    }

    public static byte[] BuildPayload(
        AuditSigningCheckpoint checkpoint)
    {
        var builder = new StringBuilder(1024);

        Append(builder, checkpoint.Version.ToString());
        Append(
            builder,
            checkpoint.CreatedAtUtc
                .ToUniversalTime()
                .ToString("O"));
        Append(builder, checkpoint.MachineName);
        Append(builder, checkpoint.ChainVersion.ToString());
        Append(builder, checkpoint.FilesChecked.ToString());
        Append(builder, checkpoint.TotalEntries.ToString());
        Append(builder, checkpoint.LegacyEntries.ToString());
        Append(builder, checkpoint.ChainedEntries.ToString());
        Append(builder, checkpoint.LastHash);
        Append(
            builder,
            NormalizeThumbprint(
                checkpoint.CertificateThumbprint));
        Append(builder, checkpoint.SignatureAlgorithm);

        return Encoding.UTF8.GetBytes(
            builder.ToString());
    }

    private static void Append(
        StringBuilder builder,
        string? value)
    {
        value ??= string.Empty;
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }

    private static string NormalizeThumbprint(
        string? value) =>
        new((value ?? string.Empty)
            .Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
