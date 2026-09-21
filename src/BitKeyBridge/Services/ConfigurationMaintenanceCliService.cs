namespace BitKeyBridge;

public static class ConfigurationMaintenanceCliService
{
    public static int Backup(
        string path)
    {
        try
        {
            var result =
                new ConfigurationMaintenanceService()
                    .CreateBackup(path);

            Console.WriteLine(
                "Configuration backup created:");
            Console.WriteLine(result);
            Console.WriteLine(
                "Credential blobs, access tokens, private keys, and BitLocker recovery passwords are not included.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Restore(
        string path)
    {
        try
        {
            var serviceBefore =
                WindowsServiceHost.GetInfo();

            var result =
                new ConfigurationMaintenanceService()
                    .Restore(path);

            if (serviceBefore.Installed &&
                string.Equals(
                    serviceBefore.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            Console.WriteLine(
                "Configuration restore completed.");
            Console.WriteLine(
                "Rollback backup: " +
                result.RollbackBackupPath);

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine(
                    "WARNING: " + warning);
            }

            return result.Success ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Diagnostics(
        string path)
    {
        try
        {
            var result =
                new ConfigurationMaintenanceService()
                    .CreateDiagnosticsBundle(path);

            Console.WriteLine(
                "Diagnostics bundle created:");
            Console.WriteLine(
                result.ZipPath);
            Console.WriteLine(
                $"Included files: {result.IncludedFiles.Count}");
            Console.WriteLine(
                "Recovery CSV/passwords, audit contents, credential blobs, bearer tokens/hashes, Graph tokens, and private keys are excluded.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
