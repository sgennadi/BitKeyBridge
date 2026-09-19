namespace BitKeyBridge;

public static class ConfigService
{
    public static AppConfig LoadAppConfig()
    {
        try
        {
            return JsonStore.Read<AppConfig>(AppPaths.AppSettingsFile) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
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

    public static void SaveCloudConfig(CloudAuthConfig config) => JsonStore.WriteAtomic(AppPaths.CloudConfigFile, config);
    public static void SaveAppConfig(AppConfig config) => JsonStore.WriteAtomic(AppPaths.AppSettingsFile, config);
}
