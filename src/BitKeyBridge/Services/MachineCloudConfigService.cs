namespace BitKeyBridge;

public static class MachineCloudConfigService
{
    public static int ShowStatus()
    {
        try
        {
            var config = ConfigService.LoadMachineCloudConfig();
            var exists = File.Exists(AppPaths.MachineCloudConfigFile);

            Console.WriteLine($"Machine cloud config: {(exists ? "Configured" : "Not configured")}");
            Console.WriteLine($"Path: {AppPaths.MachineCloudConfigFile}");
            if (!exists)
                return 3;

            Console.WriteLine($"Auth mode: {config.AuthMode}");
            Console.WriteLine($"Tenant ID: {config.TenantId}");
            Console.WriteLine($"Client ID: {config.ClientId}");
            Console.WriteLine($"Certificate thumbprint: {config.CertificateThumbprint}");

            if (!string.Equals(
                    config.AuthMode,
                    "Certificate",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "WARNING: machine cloud config should use Certificate authentication.");
                return 4;
            }

            try
            {
                var cert = new CertificateService()
                    .FindLocalMachineByThumbprint(config.CertificateThumbprint);
                Console.WriteLine($"Certificate expires: {cert.NotAfter:yyyy-MM-dd HH:mm:ss}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Certificate validation failed: " + ex.Message);
                return 4;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Save(string[] args)
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
                throw new InvalidOperationException(
                    "Administrator rights are required to save machine cloud configuration.");

            var source = ConfigService.LoadCloudConfig();

            var tenant = GetOptionValue(args, "--tenant-id");
            var client = GetOptionValue(args, "--client-id");
            var thumbprint = GetOptionValue(args, "--cert-thumbprint");

            if (!string.IsNullOrWhiteSpace(tenant))
                source.TenantId = tenant.Trim();
            if (!string.IsNullOrWhiteSpace(client))
                source.ClientId = client.Trim();
            if (!string.IsNullOrWhiteSpace(thumbprint))
                source.CertificateThumbprint =
                    thumbprint.Replace(" ", string.Empty).Trim();

            Require(source.TenantId, "Tenant ID");
            Require(source.ClientId, "Client ID");
            Require(source.CertificateThumbprint, "certificate thumbprint");

            var cert = new CertificateService()
                .FindLocalMachineByThumbprint(source.CertificateThumbprint);

            var machine = new CloudAuthConfig
            {
                TenantId = source.TenantId.Trim(),
                ClientId = source.ClientId.Trim(),
                CertificateThumbprint =
                    source.CertificateThumbprint.Replace(" ", string.Empty).Trim(),
                AuthMode = "Certificate"
            };

            ConfigService.SaveMachineCloudConfig(machine);

            Console.WriteLine("Machine cloud configuration saved.");
            Console.WriteLine($"Path: {AppPaths.MachineCloudConfigFile}");
            Console.WriteLine($"Tenant ID: {machine.TenantId}");
            Console.WriteLine($"Client ID: {machine.ClientId}");
            Console.WriteLine($"Certificate thumbprint: {machine.CertificateThumbprint}");
            Console.WriteLine($"Certificate expires: {cert.NotAfter:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine(
                "No password, access token, or certificate private key was written to the config file.");
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

    public static int Delete()
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
                throw new InvalidOperationException(
                    "Administrator rights are required to delete machine cloud configuration.");

            ConfigService.DeleteMachineCloudConfig();
            Console.WriteLine("Machine cloud configuration deleted.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? GetOptionValue(
        IReadOnlyList<string> args,
        string option)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Count)
                throw new ArgumentException($"{option} requires a value.");

            return args[i + 1];
        }

        return null;
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} is required.");
    }
}
