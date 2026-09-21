namespace BitKeyBridge;

public static class ConfigService
{
    public static AppConfig LoadAppConfig()
    {
        try
        {
            return new ConfigMigrationService()
                .LoadAndMigrate();
        }
        catch (FutureConfigurationSchemaException)
        {
            throw;
        }
        catch
        {
            return new AppConfig
            {
                SchemaVersion =
                    ConfigSchema.CurrentVersion
            };
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

        JsonStore.WriteAtomic(
            AppPaths.AppSettingsFile,
            config);
    }
}
