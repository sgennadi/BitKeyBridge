using System.Text.Json;

namespace BitKeyBridge;

public static class HousekeepingCliService
{
    public static int ShowStatus(
        AppConfig config)
    {
        try
        {
            Console.WriteLine(
                $"Enabled: {config.HousekeepingEnabled}");
            Console.WriteLine(
                $"Run on service start: {config.HousekeepingRunOnStart}");
            Console.WriteLine(
                $"Interval hours: {config.HousekeepingIntervalHours}");
            Console.WriteLine(
                $"Incident retention days: {config.IncidentRetentionDays} (0 = keep forever)");
            Console.WriteLine(
                $"Backup retention days: {config.BackupRetentionDays} (0 = keep forever)");
            Console.WriteLine(
                $"Minimum backup files: {config.BackupMinimumFiles}");
            Console.WriteLine(
                $"Temporary-file retention days: {config.TemporaryFileRetentionDays} (0 = disabled)");
            Console.WriteLine(
                $"Storage ACL enforcement: {config.StorageAclHardeningEnabled}");

            var status =
                new HousekeepingService()
                    .ReadLastStatus();

            if (status is null)
            {
                Console.WriteLine(
                    "Last housekeeping run: Never");
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }));

            return status.Success ? 0 : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int Run(
        AppConfig config,
        bool dryRun)
    {
        try
        {
            var status =
                new HousekeepingService()
                    .Run(
                        config,
                        dryRun);

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }));

            return status.Success ? 0 : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int ApplySettings(
        AppConfig config,
        string[] args)
    {
        try
        {
            if (Has(
                    args,
                    "--housekeeping-enable"))
            {
                config.HousekeepingEnabled = true;
            }

            if (Has(
                    args,
                    "--housekeeping-disable"))
            {
                config.HousekeepingEnabled = false;
            }

            if (Has(
                    args,
                    "--housekeeping-run-on-start"))
            {
                config.HousekeepingRunOnStart = true;
            }

            if (Has(
                    args,
                    "--housekeeping-no-run-on-start"))
            {
                config.HousekeepingRunOnStart = false;
            }

            ApplyInt(
                args,
                "--housekeeping-interval-hours",
                1,
                168,
                value =>
                    config.HousekeepingIntervalHours =
                        value);

            ApplyInt(
                args,
                "--incident-retention-days",
                0,
                36500,
                value =>
                    config.IncidentRetentionDays =
                        value);

            ApplyInt(
                args,
                "--backup-retention-days",
                0,
                36500,
                value =>
                    config.BackupRetentionDays =
                        value);

            ApplyInt(
                args,
                "--backup-minimum-files",
                0,
                1000,
                value =>
                    config.BackupMinimumFiles =
                        value);

            ApplyInt(
                args,
                "--temp-retention-days",
                0,
                3650,
                value =>
                    config.TemporaryFileRetentionDays =
                        value);

            ConfigurationMaintenanceService
                .ValidateAppConfig(
                    config);

            ConfigService.SaveAppConfig(
                config);

            RestartServiceIfRunning();

            Console.WriteLine(
                "Housekeeping settings saved.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int ShowStorageAclStatus(
        AppConfig config)
    {
        try
        {
            var status =
                new StorageSecurityService()
                    .Check();

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }));

            if (!config.StorageAclHardeningEnabled)
            {
                Console.WriteLine(
                    "Storage ACL health enforcement is disabled.");
            }

            return status.Valid ? 0 : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int RepairStorageAcl(
        AppConfig config)
    {
        try
        {
            var status =
                new StorageSecurityService()
                    .Repair();

            if (status.RepairSucceeded)
            {
                config.StorageAclHardeningEnabled =
                    true;
                ConfigService.SaveAppConfig(
                    config);
                RestartServiceIfRunning();
            }

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }));

            return status.RepairSucceeded
                ? 0
                : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    private static bool Has(
        IReadOnlyList<string> args,
        string option) =>
        args.Any(x =>
            x.Equals(
                option,
                StringComparison.OrdinalIgnoreCase));

    private static void ApplyInt(
        IReadOnlyList<string> args,
        string option,
        int minimum,
        int maximum,
        Action<int> apply)
    {
        var index =
            -1;

        for (var i = 0;
             i < args.Count;
             i++)
        {
            if (args[i].Equals(
                    option,
                    StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return;

        if (index + 1 >= args.Count ||
            !int.TryParse(
                args[index + 1],
                out var value) ||
            value < minimum ||
            value > maximum)
        {
            throw new ArgumentException(
                $"{option} requires an integer between {minimum} and {maximum}.");
        }

        apply(value);
    }

    private static void RestartServiceIfRunning()
    {
        var service =
            WindowsServiceHost.GetInfo();

        if (!service.Installed ||
            !string.Equals(
                service.State,
                "Running",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WindowsServiceHost.Stop();
        WindowsServiceHost.Start();
    }
}
