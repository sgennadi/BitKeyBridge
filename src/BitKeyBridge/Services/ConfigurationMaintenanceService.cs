using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitKeyBridge;

public sealed class ConfigurationMaintenanceService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public string CreateBackup(string path)
    {
        path = NormalizeOutputFile(
            path,
            ".json");

        var backup = new ConfigurationBackup
        {
            CreatedAtUtc = DateTime.UtcNow,
            SourceMachine = Environment.MachineName,
            BitKeyBridgeVersion =
                typeof(ConfigurationMaintenanceService)
                    .Assembly
                    .GetName()
                    .Version?
                    .ToString() ?? "unknown",
            AppConfig =
                ConfigService.LoadAppConfig(),
            UserCloudConfig =
                ConfigService.LoadCloudConfig(),
            MachineCloudConfig =
                ConfigService.LoadMachineCloudConfig(),
            MachineCloudConfigPresent =
                File.Exists(
                    AppPaths.MachineCloudConfigFile)
        };

        JsonStore.WriteAtomic(
            path,
            backup);

        return path;
    }

    public ConfigurationRestoreResult Restore(
        string path)
    {
        if (!SecurityContext.IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator rights are required to restore BitKeyBridge configuration.");
        }

        path = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(
                path));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Configuration backup was not found.",
                path);
        }

        var backup =
            JsonStore.Read<ConfigurationBackup>(
                path)
            ?? throw new InvalidOperationException(
                "Configuration backup is empty or invalid.");

        if (backup.FormatVersion != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported configuration backup format version {backup.FormatVersion}.");
        }

        ValidateAppConfig(
            backup.AppConfig);

        var rollbackDirectory =
            AppPaths.BackupsDirectory;
        Directory.CreateDirectory(
            rollbackDirectory);

        var rollbackPath =
            Path.Combine(
                rollbackDirectory,
                "pre-restore-" +
                DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss") +
                ".json");

        CreateBackup(
            rollbackPath);

        var result =
            new ConfigurationRestoreResult
            {
                RollbackBackupPath =
                    rollbackPath
            };

        ConfigService.SaveAppConfig(
            backup.AppConfig);
        ConfigService.SaveCloudConfig(
            backup.UserCloudConfig);

        if (backup.MachineCloudConfigPresent)
        {
            ConfigService.SaveMachineCloudConfig(
                backup.MachineCloudConfig);
        }
        else
        {
            ConfigService.DeleteMachineCloudConfig();
        }

        AddCertificateWarnings(
            backup,
            result.Warnings);

        result.Success = true;

        WindowsEventLogService.TryWrite(
            $"BitKeyBridge configuration restored from {path}. Rollback={rollbackPath}. Warnings={result.Warnings.Count}.",
            EventLogSeverity.Warning,
            4600,
            "Configuration");

        return result;
    }

    public DiagnosticsBundleResult CreateDiagnosticsBundle(
        string zipPath)
    {
        zipPath = NormalizeOutputFile(
            zipPath,
            ".zip");

        var result =
            new DiagnosticsBundleResult
            {
                ZipPath = zipPath,
                CreatedAtUtc =
                    DateTime.UtcNow
            };

        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "BitKeyBridge-Diagnostics-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            tempDirectory);

        try
        {
            WriteJson(
                tempDirectory,
                "health.json",
                new HealthService(
                    ConfigService.LoadAppConfig())
                    .GetSnapshot(),
                result);

            ServiceInfo serviceInfo;
            try
            {
                serviceInfo =
                    WindowsServiceHost.GetInfo();
            }
            catch
            {
                serviceInfo =
                    new ServiceInfo();
            }

            WriteJson(
                tempDirectory,
                "service.json",
                serviceInfo,
                result);

            var appConfig =
                CloneAppConfig(
                    ConfigService.LoadAppConfig());

            appConfig.RemoteApiTokenSha256 =
                string.Empty;
            appConfig.RemoteApiReadTokenSha256 =
                string.Empty;
            appConfig.RemoteApiCoverageRunTokenSha256 =
                string.Empty;
            appConfig.RemoteApiExportTokenSha256 =
                string.Empty;

            WriteJson(
                tempDirectory,
                "appsettings.sanitized.json",
                appConfig,
                result);

            var userCloud =
                ConfigService.LoadCloudConfig();
            WriteJson(
                tempDirectory,
                "cloud-user-metadata.json",
                userCloud,
                result);

            if (File.Exists(
                    AppPaths.MachineCloudConfigFile))
            {
                WriteJson(
                    tempDirectory,
                    "cloud-machine-metadata.json",
                    ConfigService.LoadMachineCloudConfig(),
                    result);
            }

            WriteCertificateDiagnostics(
                tempDirectory,
                appConfig,
                serviceInfo,
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.AuditIntegrityStatusFile,
                "audit-integrity-status.json",
                result);

            if (File.Exists(
                    AppPaths.AuditSigningCheckpointFile))
            {
                try
                {
                    var signing =
                        new AuditSigningService(
                            appConfig)
                            .VerifyCheckpoint();

                    WriteJson(
                        tempDirectory,
                        "audit-signing-status.json",
                        signing,
                        result);
                }
                catch (Exception ex)
                {
                    WriteText(
                        tempDirectory,
                        "audit-signing-error.txt",
                        ex.Message,
                        result);
                }
            }

            WriteStatusFile(
                tempDirectory,
                AppPaths.CoverageStatusFile,
                "coverage-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                appConfig.StatusFile,
                "export-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                appConfig.DcTestStatusFile,
                "dc-test-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.ConfigMigrationStatusFile,
                "config-migration-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.HousekeepingStatusFile,
                "housekeeping-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.StorageSecurityStatusFile,
                "storage-security-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.PrivilegedAccessStatusFile,
                "privileged-access-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.SiemStatusFile,
                "siem-status.json",
                result);

            WriteStatusFile(
                tempDirectory,
                AppPaths.UpdateStatusFile,
                "update-status.json",
                result);

            if (File.Exists(
                    AppPaths.ServiceLogFile))
            {
                var text =
                    ReadTailText(
                        AppPaths.ServiceLogFile,
                        512 * 1024);

                WriteText(
                    tempDirectory,
                    "service-log.sanitized.txt",
                    SanitizeDiagnosticText(
                        text),
                    result);
            }

            WriteJson(
                tempDirectory,
                "diagnostics-manifest.json",
                result,
                result,
                addToIncludedFiles: false);

            var directory =
                Path.GetDirectoryName(
                    zipPath);
            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(
                tempDirectory,
                zipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            return result;
        }
        finally
        {
            try
            {
                if (Directory.Exists(
                        tempDirectory))
                {
                    Directory.Delete(
                        tempDirectory,
                        recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    public static void ValidateAppConfig(
        AppConfig config)
    {
        ValidatePort(
            config.AdPort,
            nameof(config.AdPort),
            minimum: 1);

        ValidatePort(
            config.HealthEndpointPort,
            nameof(config.HealthEndpointPort));

        ValidatePort(
            config.RemoteApiPort,
            nameof(config.RemoteApiPort));

        if (config.ServiceIntervalMinutes is < 1 or > 10080)
        {
            throw new InvalidOperationException(
                "ServiceIntervalMinutes must be between 1 and 10080.");
        }

        if (config.ServiceCoverageIntervalMinutes is < 15 or > 10080)
        {
            throw new InvalidOperationException(
                "ServiceCoverageIntervalMinutes must be between 15 and 10080.");
        }

        if (config.CriticalRowRatio is < 0 or > 1 ||
            config.WarningRowRatio is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Row-ratio guards must be between 0 and 1.");
        }

        if (config.MinimumRowsForDropGuard < 0)
        {
            throw new InvalidOperationException(
                "MinimumRowsForDropGuard cannot be negative.");
        }

        if (config.HousekeepingIntervalHours is < 1 or > 168)
        {
            throw new InvalidOperationException(
                "HousekeepingIntervalHours must be between 1 and 168.");
        }

        if (config.IncidentRetentionDays is < 0 or > 36500)
        {
            throw new InvalidOperationException(
                "IncidentRetentionDays must be between 0 and 36500.");
        }

        if (config.BackupRetentionDays is < 0 or > 36500)
        {
            throw new InvalidOperationException(
                "BackupRetentionDays must be between 0 and 36500.");
        }

        if (config.BackupMinimumFiles is < 0 or > 1000)
        {
            throw new InvalidOperationException(
                "BackupMinimumFiles must be between 0 and 1000.");
        }

        if (config.TemporaryFileRetentionDays is < 0 or > 3650)
        {
            throw new InvalidOperationException(
                "TemporaryFileRetentionDays must be between 0 and 3650.");
        }

        if (config.JitRecoveryGrantMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "JitRecoveryGrantMinutes must be between 1 and 1440.");
        }

        if (config.TwoPersonApprovalMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "TwoPersonApprovalMinutes must be between 1 and 1440.");
        }

        if (config.SiemWebhookTimeoutSeconds is < 2 or > 120)
        {
            throw new InvalidOperationException(
                "SiemWebhookTimeoutSeconds must be between 2 and 120.");
        }

        if (config.SiemFlushIntervalMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "SiemFlushIntervalMinutes must be between 1 and 1440.");
        }

        if (config.SiemMaxOutboxEvents is < 100 or > 100000)
        {
            throw new InvalidOperationException(
                "SiemMaxOutboxEvents must be between 100 and 100000.");
        }

        if (config.SiemEnabled)
        {
            var mode =
                SiemForwardingService.NormalizeMode(
                    config.SiemMode);

            if (mode is not "FileJsonl" and not "Webhook")
            {
                throw new InvalidOperationException(
                    "SiemMode must be FileJsonl or Webhook.");
            }

            if (mode == "Webhook")
            {
                if (!Uri.TryCreate(
                        config.SiemWebhookUrl,
                        UriKind.Absolute,
                        out var uri) ||
                    !string.Equals(
                        uri.Scheme,
                        Uri.UriSchemeHttps,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Enabled SIEM webhook mode requires a valid HTTPS SiemWebhookUrl.");
                }
            }
        }

        if (config.DefaultScopes.Any(x =>
                string.IsNullOrWhiteSpace(
                    x.SearchBase)))
        {
            throw new InvalidOperationException(
                "Default OU scopes contain an empty SearchBase.");
        }
    }

    private static void AddCertificateWarnings(
        ConfigurationBackup backup,
        ICollection<string> warnings)
    {
        CheckCertificate(
            backup.AppConfig
                .AuditSigningCertificateThumbprint,
            "Audit-signing certificate",
            localMachineOnly: true,
            warnings);

        CheckCertificate(
            backup.AppConfig
                .RemoteApiCertificateThumbprint,
            "Remote API certificate",
            localMachineOnly: false,
            warnings);

        if (backup.MachineCloudConfigPresent)
        {
            CheckCertificate(
                backup.MachineCloudConfig
                    .CertificateThumbprint,
                "Machine cloud certificate",
                localMachineOnly: true,
                warnings);
        }
    }

    private static void CheckCertificate(
        string thumbprint,
        string label,
        bool localMachineOnly,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(
                thumbprint))
        {
            return;
        }

        try
        {
            var service =
                new CertificateService();

            _ = localMachineOnly
                ? service.FindLocalMachineCertificate(
                    thumbprint,
                    requirePrivateKey: true,
                    requireCurrentValidity: false)
                : service.FindByThumbprint(
                    thumbprint);
        }
        catch (Exception ex)
        {
            warnings.Add(
                $"{label}: {ex.Message}");
        }
    }

    private static void WriteCertificateDiagnostics(
        string directory,
        AppConfig config,
        ServiceInfo serviceInfo,
        DiagnosticsBundleResult result)
    {
        var items =
            new List<object>();

        AddCertificateDiagnostic(
            items,
            "AuditSigning",
            config.AuditSigningCertificateThumbprint,
            serviceInfo.Identity,
            localMachineOnly: true);

        AddCertificateDiagnostic(
            items,
            "RemoteApi",
            config.RemoteApiCertificateThumbprint,
            serviceInfo.Identity,
            localMachineOnly: true);

        if (File.Exists(
                AppPaths.MachineCloudConfigFile))
        {
            var cloud =
                ConfigService.LoadMachineCloudConfig();

            AddCertificateDiagnostic(
                items,
                "MachineCloud",
                cloud.CertificateThumbprint,
                serviceInfo.Identity,
                localMachineOnly: true);
        }

        WriteJson(
            directory,
            "certificates.json",
            items,
            result);
    }

    private static void AddCertificateDiagnostic(
        ICollection<object> items,
        string purpose,
        string thumbprint,
        string serviceIdentity,
        bool localMachineOnly)
    {
        if (string.IsNullOrWhiteSpace(
                thumbprint))
        {
            return;
        }

        try
        {
            var certificateService =
                new CertificateService();

            X509Certificate2 cert =
                localMachineOnly
                    ? certificateService
                        .FindLocalMachineCertificate(
                            thumbprint,
                            requirePrivateKey: false,
                            requireCurrentValidity: false)
                    : certificateService
                        .FindByThumbprint(
                            thumbprint);

            object? keyAccess = null;

            if (!string.IsNullOrWhiteSpace(
                    serviceIdentity) &&
                cert.HasPrivateKey)
            {
                try
                {
                    var access =
                        new CertificatePrivateKeyAccessService()
                            .GetStatus(
                                thumbprint,
                                serviceIdentity);

                    keyAccess = new
                    {
                        access.Account,
                        access.Sid,
                        access.Provider,
                        access.KeyFileExists,
                        access.AccessRequired,
                        access.ExplicitReadAllowed,
                        access.ExplicitReadDenied,
                        access.Status
                    };
                }
                catch (Exception ex)
                {
                    keyAccess =
                        new
                        {
                            error =
                                ex.Message
                        };
                }
            }

            items.Add(
                new
                {
                    purpose,
                    thumbprint =
                        cert.Thumbprint,
                    cert.Subject,
                    cert.Issuer,
                    notBeforeUtc =
                        cert.NotBefore.ToUniversalTime(),
                    notAfterUtc =
                        cert.NotAfter.ToUniversalTime(),
                    cert.HasPrivateKey,
                    keyAccess
                });
        }
        catch (Exception ex)
        {
            items.Add(
                new
                {
                    purpose,
                    thumbprint,
                    error =
                        ex.Message
                });
        }
    }

    private static AppConfig CloneAppConfig(
        AppConfig config)
    {
        var json =
            JsonSerializer.Serialize(
                config,
                JsonOptions);

        return JsonSerializer.Deserialize<AppConfig>(
                   json,
                   JsonOptions)
               ?? new AppConfig();
    }

    private static void WriteStatusFile(
        string directory,
        string sourcePath,
        string targetName,
        DiagnosticsBundleResult result)
    {
        try
        {
            if (!File.Exists(sourcePath))
                return;

            var text =
                File.ReadAllText(
                    sourcePath);

            WriteText(
                directory,
                targetName,
                SanitizeDiagnosticText(text),
                result);
        }
        catch (Exception ex)
        {
            WriteText(
                directory,
                targetName + ".error.txt",
                ex.Message,
                result);
        }
    }

    private static void WriteJson(
        string directory,
        string name,
        object value,
        DiagnosticsBundleResult result,
        bool addToIncludedFiles = true)
    {
        var path =
            Path.Combine(
                directory,
                name);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                value,
                JsonOptions));

        if (addToIncludedFiles)
            result.IncludedFiles.Add(name);
    }

    private static void WriteText(
        string directory,
        string name,
        string value,
        DiagnosticsBundleResult result)
    {
        var path =
            Path.Combine(
                directory,
                name);

        File.WriteAllText(
            path,
            value);

        result.IncludedFiles.Add(name);
    }

    private static string ReadTailText(
        string path,
        int maximumBytes)
    {
        var bytes =
            File.ReadAllBytes(path);

        if (bytes.Length <= maximumBytes)
        {
            return System.Text.Encoding.UTF8
                .GetString(bytes);
        }

        return System.Text.Encoding.UTF8
            .GetString(
                bytes,
                bytes.Length - maximumBytes,
                maximumBytes);
    }

    private static string SanitizeDiagnosticText(
        string value)
    {
        var redacted =
            Regex.Replace(
                value ?? string.Empty,
                @"\b\d{6}(?:-\d{6}){7}\b",
                "[REDACTED-BITLOCKER-KEY]");

        redacted =
            Regex.Replace(
                redacted,
                @"(?i)(Authorization\s*:\s*Bearer\s+)[A-Za-z0-9+/=_-]+",
                "$1[REDACTED]");

        redacted =
            Regex.Replace(
                redacted,
                @"(?i)(password\s*[=:]\s*)[^\s;,\r\n]+",
                "$1[REDACTED]");

        return redacted;
    }

    private static string NormalizeOutputFile(
        string path,
        string extension)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            throw new ArgumentException(
                "Output path is required.");
        }

        var expanded =
            Environment.ExpandEnvironmentVariables(
                path.Trim());

        if (string.IsNullOrWhiteSpace(
                Path.GetExtension(expanded)))
        {
            expanded += extension;
        }

        return Path.GetFullPath(
            expanded);
    }

    private static void ValidatePort(
        int value,
        string name,
        int minimum = 1024)
    {
        if (value < minimum ||
            value > 65535)
        {
            throw new InvalidOperationException(
                $"{name} must be between {minimum} and 65535.");
        }
    }
}
