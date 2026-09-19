namespace BitKeyBridge;

public static class AppPaths
{
    public static string LocalConfigDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();
            return Path.Combine(root, "BitKeyBridge");
        }
    }

    public static string MachineConfigDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(root, "BitKeyBridge");
        }
    }

    public static string CloudConfigFile => Path.Combine(LocalConfigDirectory, "cloud_auth_config.json");
    public static string AppSettingsFile => Path.Combine(MachineConfigDirectory, "appsettings.json");
}
