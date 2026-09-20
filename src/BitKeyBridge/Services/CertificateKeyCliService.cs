namespace BitKeyBridge;

public static class CertificateKeyCliService
{
    public static int ShowStatus(string[] args)
    {
        try
        {
            var thumbprint = ResolveThumbprint(args);
            var account = ResolveAccount(args);

            var status = new CertificatePrivateKeyAccessService()
                .GetStatus(thumbprint, account);

            Print(status);
            return status.Status is "Allowed" or "NotRequired" ? 0 : 4;
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

    public static int Grant(string[] args)
    {
        try
        {
            var thumbprint = ResolveThumbprint(args);
            var account = ResolveAccount(args);

            var status = new CertificatePrivateKeyAccessService()
                .GrantRead(thumbprint, account);

            Print(status);
            return status.Status is "Allowed" or "NotRequired" ? 0 : 4;
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

    public static int Revoke(string[] args)
    {
        try
        {
            var thumbprint = ResolveThumbprint(args);
            var account = ResolveAccount(args);

            var status = new CertificatePrivateKeyAccessService()
                .RevokeRead(thumbprint, account);

            Print(status);
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

    private static string ResolveThumbprint(IReadOnlyList<string> args)
    {
        var value = GetOptionValue(args, "--cert-thumbprint");
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        if (!File.Exists(AppPaths.MachineCloudConfigFile))
        {
            throw new InvalidOperationException(
                "No machine cloud config was found. Specify --cert-thumbprint <thumbprint>.");
        }

        var cloud = ConfigService.LoadMachineCloudConfig();
        if (string.IsNullOrWhiteSpace(cloud.CertificateThumbprint))
        {
            throw new InvalidOperationException(
                "Machine cloud config has no certificate thumbprint.");
        }

        return cloud.CertificateThumbprint;
    }

    private static string ResolveAccount(IReadOnlyList<string> args)
    {
        var value = GetOptionValue(args, "--cert-account");
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        var service = WindowsServiceHost.GetInfo();
        if (!service.Installed ||
            string.IsNullOrWhiteSpace(service.Identity))
        {
            throw new InvalidOperationException(
                "BitKeyBridge Windows Service is not installed. Specify --cert-account <DOMAIN\\account>.");
        }

        return service.Identity;
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

    private static void Print(CertificateKeyAccessInfo status)
    {
        Console.WriteLine($"Certificate: {status.Thumbprint}");
        Console.WriteLine($"Account: {status.Account}");
        Console.WriteLine($"SID: {status.Sid}");
        Console.WriteLine($"Provider: {status.Provider}");
        Console.WriteLine($"Key file: {status.KeyPath}");
        Console.WriteLine($"Key file exists: {status.KeyFileExists}");
        Console.WriteLine($"Access required: {status.AccessRequired}");
        Console.WriteLine($"Explicit read allowed: {status.ExplicitReadAllowed}");
        Console.WriteLine($"Explicit read denied: {status.ExplicitReadDenied}");
        Console.WriteLine($"Status: {status.Status}");
    }
}
