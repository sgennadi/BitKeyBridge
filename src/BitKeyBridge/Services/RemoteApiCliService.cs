namespace BitKeyBridge;

public static class RemoteApiCliService
{
    public static int ShowTokenStatus(
        AppConfig config)
    {
        Console.WriteLine(
            $"Remote API enabled: {config.RemoteApiEnabled}");
        Console.WriteLine(
            $"Management enabled: {config.RemoteApiAllowManagement}");
        Console.WriteLine(
            $"Admin token: {State(config.RemoteApiTokenSha256)}");
        Console.WriteLine(
            $"Read token: {State(config.RemoteApiReadTokenSha256)}");
        Console.WriteLine(
            $"CoverageRun token: {State(config.RemoteApiCoverageRunTokenSha256)}");
        Console.WriteLine(
            $"Export token: {State(config.RemoteApiExportTokenSha256)}");
        return 0;
    }

    public static int GenerateScopedToken(
        AppConfig config,
        string scope)
    {
        try
        {
            var result =
                new RemoteApiSetupService()
                    .GenerateScopedToken(
                        config,
                        scope);

            RestartServiceIfRunning();

            Console.WriteLine(
                $"Remote API token scope: {result.Scope}");
            Console.WriteLine(
                $"Token: {result.Token}");
            Console.WriteLine(
                $"API base URL: https://{Environment.MachineName}:{result.Port}/api/v1/");
            Console.WriteLine(
                "Save the token now. BitKeyBridge stores only its SHA-256 hash.");
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

    public static int RevokeScopedToken(
        AppConfig config,
        string scope)
    {
        try
        {
            new RemoteApiSetupService()
                .RevokeScopedToken(
                    config,
                    scope);

            RestartServiceIfRunning();
            Console.WriteLine(
                $"Remote API scoped token revoked: {RemoteApiSetupService.NormalizeScope(scope)}");
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

    private static string State(
        string hash) =>
        string.IsNullOrWhiteSpace(hash)
            ? "NotConfigured"
            : "Configured";

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
