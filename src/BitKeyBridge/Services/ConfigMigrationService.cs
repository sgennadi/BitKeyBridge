using System.Text.Json;

namespace BitKeyBridge;

public sealed class FutureConfigurationSchemaException : InvalidOperationException
{
    public FutureConfigurationSchemaException(
        int storedVersion,
        int supportedVersion)
        : base(
            $"Configuration schema version {storedVersion} is newer than this BitKeyBridge build supports ({supportedVersion}). The file was not modified.")
    {
        StoredVersion = storedVersion;
        SupportedVersion = supportedVersion;
    }

    public int StoredVersion { get; }
    public int SupportedVersion { get; }
}

public sealed class ConfigMigrationService
{
    private readonly string _configPath;
    private readonly string _statusPath;
    private readonly string _backupsDirectory;

    public ConfigMigrationService(
        string? configPath = null,
        string? statusPath = null,
        string? backupsDirectory = null)
    {
        _configPath =
            configPath ?? AppPaths.AppSettingsFile;
        _statusPath =
            statusPath ?? AppPaths.ConfigMigrationStatusFile;
        _backupsDirectory =
            backupsDirectory ?? AppPaths.BackupsDirectory;
    }

    public AppConfig LoadAndMigrate()
    {
        // File.Exists returns false on ACCESS DENIED, not only on missing
        // paths. A standard-user token must never interpret an unreadable
        // machine security policy as permission to use new default settings.
        if (!ConfigurationPathExists(_configPath))
        {
            return new AppConfig
            {
                SchemaVersion =
                    ConfigSchema.CurrentVersion
            };
        }

        var storedVersion =
            DetectStoredSchemaVersion(
                _configPath);

        if (storedVersion >
            ConfigSchema.CurrentVersion)
        {
            var future =
                new FutureConfigurationSchemaException(
                    storedVersion,
                    ConfigSchema.CurrentVersion);

            TryPersistStatus(
                new ConfigMigrationStatus
                {
                    CheckedAtUtc =
                        DateTime.UtcNow,
                    StoredSchemaVersion =
                        storedVersion,
                    EffectiveSchemaVersion =
                        storedVersion,
                    Error = future.Message
                });

            throw future;
        }

        // Once detected, the policy must remain mandatory. A disappearing
        // file, denied read, malformed JSON or literal null cannot silently
        // reset RBAC/protected audit. All propagate to ConfigService.
        var config =
            JsonStore.ReadRequired<AppConfig>(
                _configPath);

        if (storedVersion ==
            ConfigSchema.CurrentVersion)
        {
            config.SchemaVersion =
                ConfigSchema.CurrentVersion;

            var existing =
                ReadStatus();

            if (existing is null ||
                existing.EffectiveSchemaVersion !=
                    ConfigSchema.CurrentVersion ||
                existing.StoredSchemaVersion !=
                    ConfigSchema.CurrentVersion ||
                existing.MigrationRequired ||
                !string.IsNullOrWhiteSpace(
                    existing.Error))
            {
                TryPersistStatus(
                    new ConfigMigrationStatus
                    {
                        CheckedAtUtc =
                            DateTime.UtcNow,
                        StoredSchemaVersion =
                            storedVersion,
                        EffectiveSchemaVersion =
                            config.SchemaVersion,
                        MigrationRequired = false,
                        Migrated = false
                    });
            }

            return config;
        }

        var status =
            new ConfigMigrationStatus
            {
                CheckedAtUtc =
                    DateTime.UtcNow,
                StoredSchemaVersion =
                    storedVersion,
                EffectiveSchemaVersion =
                    ConfigSchema.CurrentVersion,
                MigrationRequired = true
            };

        config.SchemaVersion =
            ConfigSchema.CurrentVersion;

        ApplyMigrationDefaults(
            config,
            storedVersion);

        try
        {
            ConfigurationMaintenanceService
                .ValidateAppConfig(
                    config);

            Directory.CreateDirectory(
                _backupsDirectory);

            var backupPath =
                BuildBackupPath(
                    storedVersion);

            File.Copy(
                _configPath,
                backupPath,
                overwrite: false);

            status.BackupPath =
                backupPath;

            JsonStore.WriteAtomic(
                _configPath,
                config);

            status.Migrated = true;

            WindowsEventLogService.TryWrite(
                $"BitKeyBridge appsettings migrated from schema {storedVersion} to {ConfigSchema.CurrentVersion}. Backup={backupPath}.",
                EventLogSeverity.Information,
                4610,
                "Configuration");
        }
        catch (Exception ex)
        {
            status.Error = ex.Message;

            WindowsEventLogService.TryWrite(
                $"BitKeyBridge appsettings migration could not be persisted. StoredSchema={storedVersion}; EffectiveSchema={ConfigSchema.CurrentVersion}; Error={ex.Message}",
                EventLogSeverity.Warning,
                4611,
                "Configuration");

            // The deserialized config already contains the current code defaults.
            // Returning it keeps read-only/non-admin clients functional. The
            // migration will be retried on the next load until it can be saved.
        }

        TryPersistStatus(status);
        return config;
    }

    private static void ApplyMigrationDefaults(
        AppConfig config,
        int storedVersion)
    {
        if (storedVersion >= 5)
            return;

        // 0.25 makes all enterprise/helpdesk policy gates explicit opt-ins.
        // Older personal/helpdesk configurations sometimes retained the
        // ticket/rotation prompts even though no privileged enterprise policy
        // was enabled. Reset only that non-enterprise case; real configured
        // RBAC/JIT/approval/SIEM deployments keep their existing choices.
        var enterprisePolicyConfigured =
            config.RbacEnabled ||
            config.JitRecoveryEnabled ||
            config.TwoPersonApprovalEnabled ||
            config.SiemEnabled;

        if (!enterprisePolicyConfigured)
        {
            config.RequireRecoveryAccessReference =
                false;
            config.SuggestRotationAfterCloudKeyRetrieval =
                false;
            config.CoveragePolicyEnabled =
                false;
        }
    }

    public ConfigMigrationStatus? ReadStatus()
    {
        try
        {
            return JsonStore.Read<ConfigMigrationStatus>(
                _statusPath);
        }
        catch
        {
            return null;
        }
    }

    public static int DetectStoredSchemaVersion(
        string path)
    {
        if (!ConfigurationPathExists(path))
            return ConfigSchema.CurrentVersion;

        using var document =
            JsonDocument.Parse(
                File.ReadAllText(path));

        if (document.RootElement.ValueKind !=
            JsonValueKind.Object)
        {
            throw new JsonException(
                "Application configuration root must be a JSON object.");
        }

        foreach (var property in
                 document.RootElement
                     .EnumerateObject())
        {
            if (!property.Name.Equals(
                    "SchemaVersion",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind !=
                    JsonValueKind.Number ||
                !property.Value.TryGetInt32(
                    out var value) ||
                value < 0)
            {
                throw new JsonException(
                    "SchemaVersion must be a non-negative integer.");
            }

            return value;
        }

        // Pre-versioned BitKeyBridge configuration.
        return 0;
    }

    private static bool ConfigurationPathExists(string path)
    {
        try
        {
            // Unlike File.Exists, GetAttributes throws on access-denied and
            // other failures. Only actual absence permits first-run defaults.
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private string BuildBackupPath(
        int storedVersion)
    {
        var stamp =
            DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmss");

        var candidate =
            Path.Combine(
                _backupsDirectory,
                $"appsettings.schema{storedVersion}.{stamp}.json");

        if (!File.Exists(candidate))
            return candidate;

        return Path.Combine(
            _backupsDirectory,
            $"appsettings.schema{storedVersion}.{stamp}.{Guid.NewGuid():N}.json");
    }

    private void TryPersistStatus(
        ConfigMigrationStatus status)
    {
        try
        {
            JsonStore.WriteAtomic(
                _statusPath,
                status);
        }
        catch
        {
            // Read-only clients must still be able to load the effective config.
        }
    }
}
