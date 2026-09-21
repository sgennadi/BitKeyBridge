namespace BitKeyBridge;

public static class EntraCertificateLifecycleCliService
{
    public static int Rollover(
        CancellationToken ct = default)
    {
        try
        {
            var current =
                ConfigService.LoadCloudConfig();

            using var lifecycle =
                new EntraCertificateLifecycleService();

            var progress =
                new Progress<string>(
                    message =>
                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss}] {message}"));

            var result =
                lifecycle.RolloverAsync(
                        current,
                        info =>
                        {
                            Console.WriteLine(
                                info.Message);
                            Console.WriteLine(
                                $"Verification URL: {info.VerificationUri}");
                            Console.WriteLine(
                                $"Code: {info.UserCode}");
                            return Task.CompletedTask;
                        },
                        progress,
                        ct)
                    .GetAwaiter()
                    .GetResult();

            Console.WriteLine();
            Console.WriteLine(
                "Entra certificate rollover completed.");
            Console.WriteLine(
                $"Previous: {result.PreviousThumbprint}");
            Console.WriteLine(
                $"New: {result.NewThumbprint}");
            Console.WriteLine(
                $"Expires: {result.NewCertificateNotAfter:O}");
            Console.WriteLine(
                $"Certificate auth verified: {result.CertificateAuthenticationVerified}");
            Console.WriteLine(
                $"Graph metadata objects read during test: {result.MetadataObjectsReadDuringTest}");
            Console.WriteLine(
                $"Machine cloud config updated: {result.MachineCloudConfigUpdated}");
            Console.WriteLine(
                $"Service key access: {result.ServiceKeyAccessStatus}");

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine(
                    "WARNING: " + warning);
            }

            Console.WriteLine(
                "Previous Graph credential/local certificate retained for rollback/grace.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }
}
