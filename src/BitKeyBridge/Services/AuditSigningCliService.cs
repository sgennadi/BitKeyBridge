namespace BitKeyBridge;

public static class AuditSigningCliService
{
    public static int Setup(
        AppConfig config,
        string[] args)
    {
        try
        {
            var years = 5;
            var value = GetOptionValue(
                args,
                "--audit-signing-years");

            if (!string.IsNullOrWhiteSpace(value) &&
                (!int.TryParse(value, out years) ||
                 years is < 1 or > 10))
            {
                throw new ArgumentException(
                    "--audit-signing-years must be between 1 and 10.");
            }

            var windowsServiceBefore =
                WindowsServiceHost.GetInfo();
            var service = new AuditSigningService(config);
            var cert = service.Setup(years);

            new AuditService().Write(
                "AuditSigningSetup",
                source: "Local",
                details:
                    $"Certificate={cert.Thumbprint}; Expires={cert.NotAfter:O}");

            AuditSigningCheckpoint? checkpoint = null;
            try
            {
                checkpoint = service.SignCheckpoint();
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains(
                    "no hash-chained audit entries",
                    StringComparison.OrdinalIgnoreCase))
            {
            }

            Console.WriteLine("Audit signing enabled.");
            Console.WriteLine(
                $"Certificate: {cert.Thumbprint}");
            Console.WriteLine(
                $"Expires: {cert.NotAfter:O}");
            Console.WriteLine(
                "Private key: LocalMachine, persisted, non-exportable.");
            Console.WriteLine(
                checkpoint is null
                    ? "Checkpoint: not created yet (no chained audit entries)."
                    : $"Checkpoint: {checkpoint.CreatedAtUtc:O}; Entries={checkpoint.TotalEntries}; Hash={checkpoint.LastHash}");

            if (windowsServiceBefore.Installed &&
                string.Equals(
                    windowsServiceBefore.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
                Console.WriteLine(
                    "Windows Service restarted to load audit-signing configuration.");
            }

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

    public static int ShowStatus(AppConfig config)
    {
        try
        {
            var result =
                new AuditSigningService(config)
                    .VerifyCheckpoint();

            Print(result);

            if (!config.AuditSigningEnabled)
                return 3;

            return result.Valid ? 0 : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Sign(AppConfig config)
    {
        try
        {
            var checkpoint =
                new AuditSigningService(config)
                    .SignCheckpoint();

            Console.WriteLine("Audit checkpoint signed.");
            Console.WriteLine(
                $"Created: {checkpoint.CreatedAtUtc:O}");
            Console.WriteLine(
                $"Entries: {checkpoint.TotalEntries}");
            Console.WriteLine(
                $"Last hash: {checkpoint.LastHash}");
            Console.WriteLine(
                $"Certificate: {checkpoint.CertificateThumbprint}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Verify(AppConfig config)
    {
        try
        {
            var result =
                new AuditSigningService(config)
                    .VerifyCheckpoint();

            Print(result);
            return result.Valid ? 0 : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Disable(AppConfig config)
    {
        try
        {
            if (!config.AuditSigningEnabled)
            {
                Console.WriteLine(
                    "Audit signing is already disabled.");
                return 0;
            }

            var service = new AuditSigningService(config);

            new AuditService().Write(
                "AuditSigningDisable",
                source: "Local",
                details:
                    "Audit signing disabled by administrator. Existing certificate/checkpoint retained.");

            try
            {
                _ = service.SignCheckpoint();
            }
            catch
            {
                // Disable remains possible even if the final checkpoint
                // cannot be refreshed; the previous checkpoint is retained.
            }

            service.Disable();
            Console.WriteLine(
                "Audit signing disabled. Certificate and checkpoint retained.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Print(
        AuditSigningVerification result)
    {
        Console.WriteLine(
            $"Configured: {result.Configured}");
        Console.WriteLine(
            $"Status: {result.Status}");
        Console.WriteLine(
            $"Certificate found: {result.CertificateFound}");
        Console.WriteLine(
            $"Certificate private key present: {result.CertificateHasPrivateKey}");
        Console.WriteLine(
            $"Certificate expires: {result.CertificateExpiresUtc:O}");
        Console.WriteLine(
            $"Certificate days remaining: {result.CertificateDaysRemaining:0.0}");
        Console.WriteLine(
            $"Checkpoint exists: {result.CheckpointExists}");
        Console.WriteLine(
            $"Signature valid: {result.SignatureValid}");
        Console.WriteLine(
            $"Audit chain valid: {result.AuditChainValid}");
        Console.WriteLine(
            $"Checkpoint hash present: {result.CheckpointHashPresent}");
        Console.WriteLine(
            $"Current audit head signed: {result.CurrentHeadSigned}");

        if (result.Checkpoint is not null)
        {
            Console.WriteLine(
                $"Checkpoint created: {result.Checkpoint.CreatedAtUtc:O}");
            Console.WriteLine(
                $"Checkpoint entries: {result.Checkpoint.TotalEntries}");
            Console.WriteLine(
                $"Checkpoint hash: {result.Checkpoint.LastHash}");
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            Console.WriteLine(
                "Error: " + result.Error);
        }
    }

    private static string? GetOptionValue(
        IReadOnlyList<string> args,
        string option)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals(
                    option,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Count)
                throw new ArgumentException(
                    $"{option} requires a value.");

            return args[i + 1];
        }

        return null;
    }
}
