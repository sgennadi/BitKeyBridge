using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public static class CoverageCliService
{
    public const int ExitNoRecoveryKey = 20;
    public const int ExitIntuneNotEncrypted = 21;
    public const int ExitIntuneStale = 22;
    public const int ExitPolicyViolation = 23;

    public static async Task<int> RunAsync(
        AppConfig config,
        string[] args,
        CancellationToken ct = default)
    {
        var startedUtc = DateTime.UtcNow;
        var csvPath = string.Empty;
        var jsonPath = string.Empty;

        try
        {
            var useMachineCloudConfig = HasFlag(args, "--coverage-machine-config");
            if (useMachineCloudConfig &&
                !File.Exists(AppPaths.MachineCloudConfigFile))
            {
                throw new InvalidOperationException(
                    "Machine cloud configuration is not configured. " +
                    "Run --cloud-machine-save first or omit --coverage-machine-config.");
            }

            var cloud = useMachineCloudConfig
                ? ConfigService.LoadMachineCloudConfig()
                : ConfigService.LoadCloudConfig();
            ApplyCloudOverrides(cloud, args);

            if (useMachineCloudConfig &&
                !string.Equals(
                    cloud.AuthMode,
                    "Certificate",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Machine cloud configuration must use Certificate authentication.");
            }

            var scopes = ParseScopes(args);
            var outputDirectory = GetOptionValue(args, "--coverage-output");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = config.OutputDirectory;

            outputDirectory = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(outputDirectory));

            csvPath = ResolveOutputPath(
                GetOptionValue(args, "--coverage-csv"),
                outputDirectory,
                "bitlocker_coverage.csv");

            jsonPath = ResolveOutputPath(
                GetOptionValue(args, "--coverage-json"),
                outputDirectory,
                "bitlocker_coverage.json");

            using var graph = new CloudGraphService();
            var token = await AcquireTokenAsync(graph, cloud, args, ct);

            var progress = new Progress<string>(
                message => Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] {message}"));

            var coverage = new CoverageService(config);
            var result = await coverage.RunAsync(
                token.AccessToken,
                scopes,
                progress,
                ct);

            CoverageReportService.WriteResult(
                result,
                csvPath,
                jsonPath,
                startedUtc,
                config);

            var policy =
                new CoveragePolicyService(config)
                    .Evaluate(result.Summary);

            Console.WriteLine();
            Console.WriteLine(
                "Coverage summary: " +
                $"Devices={result.Summary.TotalDevices}; " +
                $"AD+Entra={result.Summary.BothSources}; " +
                $"ADOnly={result.Summary.AdOnly}; " +
                $"EntraOnly={result.Summary.EntraOnly}; " +
                $"NoKey={result.Summary.NoRecoveryKey}; " +
                $"IntuneNotEncrypted={result.Summary.IntuneNotEncrypted}; " +
                $"IntuneStale={result.Summary.IntuneStale}; " +
                $"OldCloudKey={result.Summary.OldCloudKey}");
            Console.WriteLine(
                $"Coverage policy: Enabled={policy.Enabled}; Compliant={policy.Compliant}; " +
                $"Errors={policy.ErrorCount}; Warnings={policy.WarningCount}");
            foreach (var violation in policy.Violations)
            {
                Console.WriteLine(
                    $"  [{violation.Severity}] {violation.Code}: " +
                    $"{violation.Actual} > {violation.Maximum} - {violation.Message}");
            }

            Console.WriteLine("Coverage CSV: " + csvPath);
            Console.WriteLine("Coverage JSON: " + jsonPath);
            Console.WriteLine(
                "Coverage output contains metadata only; BitLocker recovery passwords are not requested.");

            if (HasFlag(args, "--coverage-json-stdout"))
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
            }

            return EvaluateExitCode(
                result.Summary,
                policy,
                args);
        }
        catch (ArgumentException ex)
        {
            CoverageReportService.TryWriteFailure(
                startedUtc,
                ex,
                csvPath,
                jsonPath);
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            CoverageReportService.TryWriteFailure(
                startedUtc,
                ex,
                csvPath,
                jsonPath);
            Console.Error.WriteLine("Coverage failed: " + ex.Message);
            return 1;
        }
        finally
        {
            AdSessionCredentials.Clear();
        }
    }

    public static int EvaluateExitCode(
        CoverageSummary summary,
        IReadOnlyCollection<string> args) =>
        EvaluateExitCode(
            summary,
            new CoveragePolicyResult
            {
                Enabled = false,
                Compliant = true
            },
            args);

    public static int EvaluateExitCode(
        CoverageSummary summary,
        CoveragePolicyResult policy,
        IReadOnlyCollection<string> args)
    {
        if (HasFlag(args, "--coverage-fail-policy") &&
            policy.Enabled &&
            !policy.Compliant)
        {
            return ExitPolicyViolation;
        }

        if (HasFlag(args, "--coverage-fail-no-key") &&
            summary.NoRecoveryKey > 0)
        {
            return ExitNoRecoveryKey;
        }

        if (HasFlag(args, "--coverage-fail-unencrypted") &&
            summary.IntuneNotEncrypted > 0)
        {
            return ExitIntuneNotEncrypted;
        }

        if (HasFlag(args, "--coverage-fail-stale") &&
            summary.IntuneStale > 0)
        {
            return ExitIntuneStale;
        }

        return 0;
    }

    private static async Task<GraphToken> AcquireTokenAsync(
        CloudGraphService graph,
        CloudAuthConfig cloud,
        string[] args,
        CancellationToken ct)
    {
        var requestedMode = GetOptionValue(args, "--cloud-auth");
        var mode = string.IsNullOrWhiteSpace(requestedMode)
            ? cloud.AuthMode
            : requestedMode;

        mode = string.IsNullOrWhiteSpace(mode)
            ? "DeviceCode"
            : mode.Trim();

        if (mode.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
        {
            RequireCloudValue(cloud.TenantId, "--tenant-id / saved Tenant ID");
            RequireCloudValue(cloud.ClientId, "--client-id / saved Client ID");
            RequireCloudValue(
                cloud.CertificateThumbprint,
                "--cert-thumbprint / saved certificate thumbprint");

            Console.WriteLine(
                "Authenticating to Microsoft Graph with the configured certificate...");

            return await graph.AcquireCertificateTokenAsync(
                cloud.TenantId,
                cloud.ClientId,
                cloud.CertificateThumbprint,
                ct);
        }

        if (mode.Equals("DeviceCode", StringComparison.OrdinalIgnoreCase))
        {
            RequireCloudValue(cloud.TenantId, "--tenant-id / saved Tenant ID");
            RequireCloudValue(cloud.ClientId, "--client-id / saved Client ID");

            if (!Environment.UserInteractive || Console.IsInputRedirected)
            {
                throw new InvalidOperationException(
                    "Device Code authentication requires an interactive session. " +
                    "Use certificate authentication for unattended coverage jobs.");
            }

            return await graph.AcquireDeviceCodeTokenAsync(
                cloud.TenantId,
                cloud.ClientId,
                info =>
                {
                    Console.WriteLine(info.Message);
                    Console.WriteLine(
                        $"Verification URL: {info.VerificationUri}");
                    Console.WriteLine($"Code: {info.UserCode}");
                    return Task.CompletedTask;
                },
                ct);
        }

        if (mode.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("ROPC", StringComparison.OrdinalIgnoreCase))
        {
            RequireCloudValue(cloud.TenantId, "--tenant-id / saved Tenant ID");
            RequireCloudValue(cloud.ClientId, "--client-id / saved Client ID");
            RequireCloudValue(cloud.Username, "--cloud-user / saved cloud user");

            if (!Environment.UserInteractive || Console.IsInputRedirected)
            {
                throw new InvalidOperationException(
                    "Password authentication requires an interactive password prompt. " +
                    "Use certificate authentication for unattended coverage jobs.");
            }

            var password = ReadSecretFromConsole(
                "Microsoft Entra password (session only): ");
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException(
                    "Microsoft Entra password is required.");

            try
            {
                return await graph.AcquirePasswordTokenAsync(
                    cloud.TenantId,
                    cloud.ClientId,
                    cloud.Username,
                    password,
                    ct);
            }
            finally
            {
                password = string.Empty;
            }
        }

        throw new ArgumentException(
            "--cloud-auth must be DeviceCode, Certificate, or Password.");
    }

    private static void ApplyCloudOverrides(
        CloudAuthConfig cloud,
        string[] args)
    {
        var tenantId = GetOptionValue(args, "--tenant-id");
        var clientId = GetOptionValue(args, "--client-id");
        var thumbprint = GetOptionValue(args, "--cert-thumbprint");
        var username = GetOptionValue(args, "--cloud-user");
        var authMode = GetOptionValue(args, "--cloud-auth");

        if (!string.IsNullOrWhiteSpace(tenantId))
            cloud.TenantId = tenantId.Trim();
        if (!string.IsNullOrWhiteSpace(clientId))
            cloud.ClientId = clientId.Trim();
        if (!string.IsNullOrWhiteSpace(thumbprint))
            cloud.CertificateThumbprint =
                thumbprint.Replace(" ", string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(username))
            cloud.Username = username.Trim();
        if (!string.IsNullOrWhiteSpace(authMode))
            cloud.AuthMode = authMode.Trim();
    }

    private static List<BitLockerScope>? ParseScopes(string[] args)
    {
        var result = new List<BitLockerScope>();

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals(
                    "--search-base",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
                throw new ArgumentException(
                    "--search-base requires a distinguished name.");

            result.Add(new BitLockerScope(
                $"Custom OU {result.Count + 1}",
                args[++i]));
        }

        return result.Count == 0 ? null : result;
    }

    private static string ResolveOutputPath(
        string? requestedPath,
        string defaultDirectory,
        string defaultFileName)
    {
        var path = string.IsNullOrWhiteSpace(requestedPath)
            ? Path.Combine(defaultDirectory, defaultFileName)
            : Environment.ExpandEnvironmentVariables(requestedPath.Trim());

        return Path.GetFullPath(path);
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
                throw new ArgumentException(
                    $"{option} requires a value.");

            return args[i + 1];
        }

        return null;
    }

    private static bool HasFlag(
        IReadOnlyCollection<string> args,
        string flag) =>
        args.Any(x => x.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static void RequireCloudValue(
        string value,
        string setting)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Cloud configuration is incomplete: {setting} is required.");
        }
    }

    private static string ReadSecretFromConsole(string prompt)
    {
        Console.Write(prompt);
        var secret = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secret.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                    secret.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                secret.Append(key.KeyChar);
        }
    }
}
