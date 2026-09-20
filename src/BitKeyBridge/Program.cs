using System.Text.Json;

namespace BitKeyBridge;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(x => x.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                          x.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                          x.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.EnsureConsole();
            PrintHelp();
            return 0;
        }

        if (args.Any(x => x.Equals("--version", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.EnsureConsole();
            Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
            return 0;
        }

        if (args.Any(x => x.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.EnsureConsole();
            return RunSelfTest();
        }

        var isCli = args.Any(x =>
            x.Equals("--cli", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--dry-run", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--dc-test", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--install-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--uninstall-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--start-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--stop-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--health", StringComparison.OrdinalIgnoreCase));
        if (isCli) ConsoleHelper.EnsureConsole();

        var config = ConfigService.LoadAppConfig();

        if (args.Any(x => x.Equals("--service", StringComparison.OrdinalIgnoreCase)))
            return WindowsServiceHost.RunService(config);

        var noElevation = args.Any(x => x.Equals("--no-elevation", StringComparison.OrdinalIgnoreCase));
        if (!noElevation && !SecurityContext.IsAdministrator())
        {
            if (!Environment.UserInteractive)
            {
                if (isCli) Console.Error.WriteLine("Administrative rights are required. Configure the scheduled task to run with highest privileges.");
                return 5;
            }

            if (SecurityContext.RelaunchElevated(args.Concat(["--no-elevation"]).ToArray())) return 0;
            if (isCli)
            {
                Console.Error.WriteLine("Administrative rights are required.");
                return 5;
            }
        }

        if (args.Any(x => x.Equals("--install-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.InstallOrUpdate();
                WindowsServiceHost.Start();
                Console.WriteLine($"Installed and started {WindowsServiceHost.DisplayName}.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--uninstall-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.Uninstall();
                Console.WriteLine($"Uninstalled {WindowsServiceHost.DisplayName}.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--start-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.Start();
                Console.WriteLine("BitKeyBridge service state: " + WindowsServiceHost.GetInfo().State);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--stop-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.Stop();
                Console.WriteLine("BitKeyBridge service state: " + WindowsServiceHost.GetInfo().State);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--service-status", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var service = WindowsServiceHost.GetInfo();
                Console.WriteLine(service.Installed
                    ? $"Installed; State={service.State}; Binary={service.BinaryPath}"
                    : "Not installed");
                return service.Installed ? 0 : 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--health", StringComparison.OrdinalIgnoreCase)))
        {
            var health = new HealthService(config).GetSnapshot();
            Console.WriteLine(JsonSerializer.Serialize(health, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return health.OverallStatus == "Error" ? 2 : 0;
        }

        if (args.Any(x => x.Equals("--dc-test", StringComparison.OrdinalIgnoreCase)))
            return RunDcTest(config, args).GetAwaiter().GetResult();

        if (isCli)
            return RunExport(config, args).GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(config));
        return 0;
    }

    private static async Task<int> RunExport(AppConfig config, string[] args)
    {
        var dryRun = args.Any(x => x.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        var force = args.Any(x => x.Equals("--force-publish", StringComparison.OrdinalIgnoreCase));
        var scopes = ParseScopes(args);
        var progress = new Progress<string>(m => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {m}"));
        var service = new ExportService(config);
        var result = await service.RunAsync(dryRun, force, scopes, progress);
        Console.WriteLine(result.Success
            ? $"Completed. Rows={result.ValidRows}; DC={result.AdServer}; Published={result.Published}"
            : "FAILED: " + result.ErrorMessage);
        return result.ExitCode;
    }

    private static async Task<int> RunDcTest(AppConfig config, string[] args)
    {
        var scopes = ParseScopes(args);
        var progress = new Progress<string>(m => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {m}"));
        var service = new DomainControllerComparisonService(config);
        try
        {
            var rows = await service.RunAsync(scopes, progress);
            foreach (var row in rows)
            {
                Console.WriteLine($"{row.Name,-18} Site={row.Site,-18} RODC={row.IsReadOnly,-5} Status={row.Status,-8} Objects={row.ObjectsFound,-8} ReplErrors={row.ReplicationErrors}");
            }
            return rows.Any(x => x.Status == "ERROR") ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int RunSelfTest()
    {
        var failures = new List<string>();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "BitKeyBridge-SelfTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var parsed = ActiveDirectoryService.GetParentComputerName(@"CN={00000000-0000-0000-0000-000000000000},CN=PC\,LAB,OU=Computers,DC=example,DC=com");
                if (!string.Equals(parsed, "PC,LAB", StringComparison.Ordinal))
                    failures.Add("LDAP DN parent parsing returned an unexpected computer name.");
            }
            catch (Exception ex) { failures.Add("LDAP DN parsing: " + ex.Message); }

            try
            {
                var csv = Path.Combine(tempDirectory, "recovery.csv");
                var rows = new List<RecoveryRecord>
                {
                    new("PC-01", "11111111-2222-3333-4444-555555555555", "111111-222222-333333-444444-555555-666666-777777-888888", new DateTime(2026, 1, 2, 3, 4, 5)),
                    new("PC,02", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "999999-888888-777777-666666-555555-444444-333333-222222", new DateTime(2026, 6, 7, 8, 9, 10))
                };
                CsvUtility.WriteRecoveryCsvAtomic(csv, rows);
                var read = CsvUtility.ReadRecoveryCsv(csv);
                if (read.Count != 2 || read[1].ComputerName != "PC,02" || read[0].RecoveryKey != rows[0].RecoveryKey)
                    failures.Add("CSV atomic write/read round-trip failed.");
            }
            catch (Exception ex) { failures.Add("CSV round-trip: " + ex.Message); }

            try
            {
                var a = new[]
                {
                    new BitLockerScope("One", "OU=One,DC=example,DC=com"),
                    new BitLockerScope("Two", "OU=Two,DC=example,DC=com")
                };
                var b = a.Reverse();
                if (!string.Equals(ExportService.GetScopeFingerprint(a), ExportService.GetScopeFingerprint(b), StringComparison.Ordinal))
                    failures.Add("Scope fingerprint changes when scope order changes.");
            }
            catch (Exception ex) { failures.Add("Scope fingerprint: " + ex.Message); }

            try
            {
                if (!Guid.TryParse(EntraSetupService.DefaultBootstrapClientId, out var bootstrapId) ||
                    bootstrapId == Guid.Empty)
                    failures.Add("Default Microsoft bootstrap Client ID is invalid.");
                if (!string.Equals(
                        EntraSetupService.DefaultBootstrapDisplayName,
                        "Microsoft Graph Command Line Tools",
                        StringComparison.Ordinal))
                    failures.Add("Default Microsoft bootstrap display name is unexpected.");
            }
            catch (Exception ex) { failures.Add("Entra bootstrap defaults: " + ex.Message); }

            try
            {
                var auditPath = Path.Combine(tempDirectory, "audit.jsonl");
                var audit = new AuditService(auditPath, 1);
                var fakeKey = "111111-222222-333333-444444-555555-666666-777777-888888";
                audit.Write("SelfTest", details: "Sensitive=" + fakeKey);
                var entries = audit.ReadRecent(10);
                if (entries.Count != 1 ||
                    entries[0].Details.Contains(fakeKey, StringComparison.Ordinal) ||
                    !entries[0].Details.Contains("[REDACTED-BITLOCKER-KEY]", StringComparison.Ordinal))
                    failures.Add("Audit redaction failed.");
            }
            catch (Exception ex) { failures.Add("Audit redaction: " + ex.Message); }
        }
        finally
        {
            try { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true); } catch { }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("SELF-TEST OK");
            return 0;
        }

        foreach (var failure in failures) Console.Error.WriteLine("SELF-TEST FAILED: " + failure);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("BitKeyBridge");
        Console.WriteLine();
        Console.WriteLine("GUI:");
        Console.WriteLine("  BitKeyBridge.exe");
        Console.WriteLine();
        Console.WriteLine("CLI:");
        Console.WriteLine("  --cli                 Export using saved scopes");
        Console.WriteLine("  --dry-run             Read and validate without publishing CSV");
        Console.WriteLine("  --force-publish       Override row-drop/scope-change publish guards");
        Console.WriteLine("  --dc-test             Discover and compare current domain controllers");
        Console.WriteLine("  --health              Print the local health snapshot as JSON");
        Console.WriteLine("  --install-service     Install/update and start the native Windows Service");
        Console.WriteLine("  --uninstall-service   Stop and remove the native Windows Service");
        Console.WriteLine("  --start-service       Start the installed BitKeyBridge service");
        Console.WriteLine("  --stop-service        Stop the installed BitKeyBridge service");
        Console.WriteLine("  --service-status      Show installed service state");
        Console.WriteLine("  --search-base <DN>    Override scopes for this run; may be repeated");
        Console.WriteLine("  --no-elevation        Do not relaunch through UAC");
        Console.WriteLine("  --self-test           Run offline smoke tests and exit");
        Console.WriteLine("  --version             Show application version");
        Console.WriteLine("  --help                Show this help");
    }

    private static List<BitLockerScope>? ParseScopes(string[] args)
    {
        var result = new List<BitLockerScope>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--search-base", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length) continue;
            var dn = args[++i];
            result.Add(new BitLockerScope($"Custom OU {result.Count + 1}", dn));
        }
        return result.Count == 0 ? null : result;
    }
}
