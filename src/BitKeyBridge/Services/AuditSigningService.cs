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
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "An audit-signing certificate is already configured but is unavailable or invalid. " +
                    "BitKeyBridge will not silently replace the audit trust anchor. " +
                    "Restore the configured certificate or perform an explicit trust-anchor migration.",
                    ex);
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

    public AuditSigningRolloverResult Rollover(
        int years = 5,
        string? auditPath = null)
    {
        if (!SecurityContext.IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator rights are required to roll over the audit-signing certificate.");
        }

        if (!_config.AuditSigningEnabled ||
            string.IsNullOrWhiteSpace(
                _config.AuditSigningCertificateThumbprint))
        {
            throw new InvalidOperationException(
                "Audit signing must be configured before certificate rollover.");
        }

        years = Math.Clamp(years, 1, 10);
        auditPath ??= AppPaths.AuditLogFile;

        var previousThumbprint =
            NormalizeThumbprint(
                _config.AuditSigningCertificateThumbprint);

        var previousCheckpoint =
            SignCheckpoint(auditPath);

        var previousCertificate =
            new CertificateService()
                .FindLocalMachineCertificate(
                    previousThumbprint,
                    requirePrivateKey: true,
                    requireCurrentValidity: true);

        var integrity =
            AuditIntegrityService.Verify(
                auditPath);

        if (!integrity.Valid ||
            string.IsNullOrWhiteSpace(
                integrity.LastHash))
        {
            throw new InvalidOperationException(
                "Audit integrity must be valid before signing-certificate rollover.");
        }

        var newCertificate =
            new CertificateService()
                .CreateAuditSigningCertificate(
                    "BitKeyBridge Audit Signing",
                    years);

        CertificateKeyAccessInfo? keyAccess = null;
        var windowsService =
            WindowsServiceHost.GetInfo();

        if (windowsService.Installed &&
            !string.IsNullOrWhiteSpace(
                windowsService.Identity))
        {
            keyAccess =
                new CertificatePrivateKeyAccessService()
                    .EnsureServiceAccess(
                        newCertificate.Thumbprint,
                        windowsService.Identity);
        }

        var transition =
            new AuditSigningTransition
            {
                CreatedAtUtc =
                    DateTime.UtcNow,
                MachineName =
                    Environment.MachineName,
                ChainVersion =
                    integrity.LastChainVersion > 0
                        ? integrity.LastChainVersion
                        : AuditIntegrityService.CurrentChainVersion,
                AuditHeadHash =
                    integrity.LastHash,
                PreviousCertificateThumbprint =
                    previousThumbprint,
                NewCertificateThumbprint =
                    NormalizeThumbprint(
                        newCertificate.Thumbprint),
                PreviousCertificateNotAfterUtc =
                    previousCertificate.NotAfter
                        .ToUniversalTime(),
                NewCertificateNotAfterUtc =
                    newCertificate.NotAfter
                        .ToUniversalTime()
            };

        var transitionPayload =
            BuildTransitionPayload(
                transition);

        using (var oldPrivate =
                   previousCertificate.GetRSAPrivateKey()
                   ?? throw new InvalidOperationException(
                       "Previous audit-signing private key is unavailable."))
        {
            transition.PreviousSignatureBase64 =
                Convert.ToBase64String(
                    oldPrivate.SignData(
                        transitionPayload,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1));
        }

        using (var newPrivate =
                   newCertificate.GetRSAPrivateKey()
                   ?? throw new InvalidOperationException(
                       "New audit-signing private key is unavailable."))
        {
            transition.NewSignatureBase64 =
                Convert.ToBase64String(
                    newPrivate.SignData(
                        transitionPayload,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1));
        }

        var previousSignatureValid =
            VerifyTransitionSignature(
                transition,
                previousCertificate,
                transition.PreviousSignatureBase64);

        var newSignatureValid =
            VerifyTransitionSignature(
                transition,
                newCertificate,
                transition.NewSignatureBase64);

        if (!previousSignatureValid ||
            !newSignatureValid)
        {
            throw new InvalidOperationException(
                "Audit-signing transition dual-signature verification failed.");
        }

        var newCheckpoint =
            CreateCheckpoint(
                integrity,
                newCertificate);

        if (!VerifyCheckpointSignature(
                newCheckpoint,
                newCertificate))
        {
            throw new InvalidOperationException(
                "New audit-signing checkpoint signature verification failed.");
        }

        var originalThumbprint =
            _config.AuditSigningCertificateThumbprint;

        try
        {
            _config.AuditSigningCertificateThumbprint =
                transition.NewCertificateThumbprint;

            ConfigService.SaveAppConfig(
                _config);

            JsonStore.WriteAtomic(
                AppPaths.AuditSigningCheckpointFile,
                newCheckpoint);

            var verification =
                VerifyCheckpoint(
                    auditPath);

            if (!verification.Valid ||
                !verification.CurrentHeadSigned)
            {
                throw new InvalidOperationException(
                    "New audit-signing checkpoint did not validate after rollover. Status=" +
                    verification.Status);
            }

            PersistTransition(
                transition);

            WindowsEventLogService.TryWrite(
                $"Audit-signing certificate rollover completed. Previous={transition.PreviousCertificateThumbprint}; New={transition.NewCertificateThumbprint}; AuditHead={transition.AuditHeadHash}.",
                EventLogSeverity.Warning,
                4526,
                "AuditSigning");

            return new AuditSigningRolloverResult
            {
                PreviousThumbprint =
                    transition.PreviousCertificateThumbprint,
                NewThumbprint =
                    transition.NewCertificateThumbprint,
                NewCertificateNotAfterUtc =
                    transition.NewCertificateNotAfterUtc,
                AuditHeadHash =
                    transition.AuditHeadHash,
                PreviousSignatureValid =
                    previousSignatureValid,
                NewSignatureValid =
                    newSignatureValid,
                NewCheckpointValid =
                    verification.Valid &&
                    verification.CurrentHeadSigned,
                ServiceKeyAccessStatus =
                    keyAccess?.Status ??
                    "NotApplicable"
            };
        }
        catch
        {
            _config.AuditSigningCertificateThumbprint =
                originalThumbprint;

            try
            {
                ConfigService.SaveAppConfig(
                    _config);
            }
            catch
            {
            }

            try
            {
                JsonStore.WriteAtomic(
                    AppPaths.AuditSigningCheckpointFile,
                    previousCheckpoint);
            }
            catch
            {
            }

            WindowsEventLogService.TryWrite(
                $"Audit-signing certificate rollover failed and configuration rollback was attempted. Previous={previousThumbprint}; Candidate={newCertificate.Thumbprint}.",
                EventLogSeverity.Error,
                4528,
                "AuditSigning");

            throw;
        }
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

        if (File.Exists(AppPaths.AuditSigningCheckpointFile))
        {
            var previous = VerifyCheckpoint(auditPath);
            if (!previous.Valid)
            {
                throw new InvalidOperationException(
                    "Existing signed audit checkpoint verification failed. " +
                    "Refusing to overwrite the trust anchor. Status=" +
                    previous.Status +
                    (string.IsNullOrWhiteSpace(previous.Error)
                        ? string.Empty
                        : "; Error=" + previous.Error));
            }
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
            ChainVersion =
                integrity.LastChainVersion > 0
                    ? integrity.LastChainVersion
                    : AuditIntegrityService.CurrentChainVersion,
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

    public static byte[] BuildTransitionPayload(
        AuditSigningTransition transition)
    {
        var builder =
            new StringBuilder(1024);

        Append(
            builder,
            transition.Version.ToString());
        Append(
            builder,
            transition.CreatedAtUtc
                .ToUniversalTime()
                .ToString("O"));
        Append(
            builder,
            transition.MachineName);
        Append(
            builder,
            transition.ChainVersion.ToString());
        Append(
            builder,
            transition.AuditHeadHash);
        Append(
            builder,
            NormalizeThumbprint(
                transition.PreviousCertificateThumbprint));
        Append(
            builder,
            NormalizeThumbprint(
                transition.NewCertificateThumbprint));
        Append(
            builder,
            transition.PreviousCertificateNotAfterUtc
                .ToUniversalTime()
                .ToString("O"));
        Append(
            builder,
            transition.NewCertificateNotAfterUtc
                .ToUniversalTime()
                .ToString("O"));
        Append(
            builder,
            transition.SignatureAlgorithm);

        return Encoding.UTF8.GetBytes(
            builder.ToString());
    }

    private static AuditSigningCheckpoint CreateCheckpoint(
        AuditIntegrityResult integrity,
        X509Certificate2 certificate)
    {
        using var rsa =
            certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException(
                "Audit-signing RSA private key is unavailable.");

        var checkpoint =
            new AuditSigningCheckpoint
            {
                Version = 1,
                CreatedAtUtc =
                    DateTime.UtcNow,
                MachineName =
                    Environment.MachineName,
                ChainVersion =
                    integrity.LastChainVersion > 0
                        ? integrity.LastChainVersion
                        : AuditIntegrityService.CurrentChainVersion,
                FilesChecked =
                    integrity.FilesChecked,
                TotalEntries =
                    integrity.TotalEntries,
                LegacyEntries =
                    integrity.LegacyEntries,
                ChainedEntries =
                    integrity.ChainedEntries,
                LastHash =
                    integrity.LastHash,
                CertificateThumbprint =
                    NormalizeThumbprint(
                        certificate.Thumbprint),
                SignatureAlgorithm =
                    "RSA-SHA256-PKCS1"
            };

        checkpoint.SignatureBase64 =
            Convert.ToBase64String(
                rsa.SignData(
                    BuildPayload(
                        checkpoint),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1));

        return checkpoint;
    }

    private static bool VerifyCheckpointSignature(
        AuditSigningCheckpoint checkpoint,
        X509Certificate2 certificate)
    {
        try
        {
            using var rsa =
                certificate.GetRSAPublicKey();
            if (rsa is null)
                return false;

            return rsa.VerifyData(
                BuildPayload(
                    checkpoint),
                Convert.FromBase64String(
                    checkpoint.SignatureBase64),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyTransitionSignature(
        AuditSigningTransition transition,
        X509Certificate2 certificate,
        string signatureBase64)
    {
        try
        {
            using var rsa =
                certificate.GetRSAPublicKey();
            if (rsa is null)
                return false;

            return rsa.VerifyData(
                BuildTransitionPayload(
                    transition),
                Convert.FromBase64String(
                    signatureBase64),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static void PersistTransition(
        AuditSigningTransition transition)
    {
        JsonStore.WriteAtomic(
            AppPaths.AuditSigningTransitionFile,
            transition);

        Directory.CreateDirectory(
            AppPaths.AuditSigningTransitionsDirectory);

        var oldShort =
            ShortThumbprint(
                transition.PreviousCertificateThumbprint);
        var newShort =
            ShortThumbprint(
                transition.NewCertificateThumbprint);

        var historyPath =
            Path.Combine(
                AppPaths.AuditSigningTransitionsDirectory,
                transition.CreatedAtUtc.ToString(
                    "yyyyMMdd-HHmmssfff") +
                "-" +
                oldShort +
                "-to-" +
                newShort +
                ".json");

        JsonStore.WriteAtomic(
            historyPath,
            transition);
    }

    private static string ShortThumbprint(
        string value)
    {
        var normalized =
            NormalizeThumbprint(value);

        return normalized[..Math.Min(
            12,
            normalized.Length)];
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
