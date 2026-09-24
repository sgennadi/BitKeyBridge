namespace BitKeyBridge;

public sealed class ConfigurationLoadException : InvalidOperationException
{
    public ConfigurationLoadException(
        string configPath,
        Exception innerException)
        : base(
            $"BitKeyBridge configuration '{configPath}' could not be loaded safely. " +
            "The application will not continue with default security settings. " +
            innerException.Message,
            innerException)
    {
        ConfigPath = configPath;
    }

    public string ConfigPath { get; }
}

public static class ConfigService
{
    public static AppConfig LoadAppConfig() =>
        LoadAppConfig(
            AppPaths.AppSettingsFile,
            AppPaths.ConfigMigrationStatusFile,
            AppPaths.BackupsDirectory);

    internal static AppConfig LoadAppConfig(
        string configPath,
        string statusPath,
        string backupsDirectory)
    {
        try
        {
            var config =
                new ConfigMigrationService(
                    configPath,
                    statusPath,
                    backupsDirectory)
                    .LoadAndMigrate();

            ConfigurationMaintenanceService
                .ValidateAppConfig(
                    config);

            return config;
        }
        catch (FutureConfigurationSchemaException)
        {
            throw;
        }
        catch (ConfigurationLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                $"BitKeyBridge configuration load failed closed. Path={configPath}; Error={ex.Message}",
                EventLogSeverity.Error,
                4612,
                "Configuration");

            throw new ConfigurationLoadException(
                configPath,
                ex);
        }
    }

    public static CloudAuthConfig LoadCloudConfig()
    {
        try
        {
            return JsonStore.Read<CloudAuthConfig>(AppPaths.CloudConfigFile) ?? new CloudAuthConfig();
        }
        catch
        {
            return new CloudAuthConfig();
        }
    }

    public static CloudAuthConfig LoadMachineCloudConfig()
    {
        try
        {
            return JsonStore.Read<CloudAuthConfig>(AppPaths.MachineCloudConfigFile) ?? new CloudAuthConfig
            {
                AuthMode = "Certificate"
            };
        }
        catch
        {
            return new CloudAuthConfig
            {
                AuthMode = "Certificate"
            };
        }
    }

    public static void SaveCloudConfig(CloudAuthConfig config) =>
        JsonStore.WriteAtomic(AppPaths.CloudConfigFile, config);

    public static void SaveMachineCloudConfig(CloudAuthConfig config) =>
        JsonStore.WriteAtomic(AppPaths.MachineCloudConfigFile, config);

    public static void DeleteMachineCloudConfig()
    {
        if (File.Exists(AppPaths.MachineCloudConfigFile))
            File.Delete(AppPaths.MachineCloudConfigFile);
    }

    public static void SaveAppConfig(AppConfig config)
    {
        if (config.SchemaVersion >
            ConfigSchema.CurrentVersion)
        {
            throw new FutureConfigurationSchemaException(
                config.SchemaVersion,
                ConfigSchema.CurrentVersion);
        }

        config.SchemaVersion =
            ConfigSchema.CurrentVersion;

        ConfigurationMaintenanceService
            .ValidateAppConfig(
                config);

        JsonStore.WriteAtomic(
            AppPaths.AppSettingsFile,
            config);
    }
}
