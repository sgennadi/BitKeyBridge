namespace BitKeyBridge;

/// <summary>
/// Native metadata-only LAPS inventory for scheduled tasks and GUI parity.
/// No Graph token, RSAT or PowerShell module is required.
/// </summary>
public static class LapsCoverageCliService
{
    public static async Task<int> RunAsync(
        AppConfig config, string[] args, CancellationToken ct = default)
    {
        try
        {
            var query = Option(args, "--laps-coverage-dn");
            var maxText = Option(args, "--laps-coverage-max");
            var max = 25000;
            if (!string.IsNullOrWhiteSpace(maxText) &&
                (!int.TryParse(maxText, out max) || max is < 1 or > 50000))
            {
                Console.Error.WriteLine(
                    "--laps-coverage-max requires a number from 1 through 50000.");
                return 2;
            }

            var destination = Option(args, "--laps-coverage-output");
            if (string.IsNullOrWhiteSpace(destination))
                destination = Path.Combine(
                    AppPaths.LocalConfigDirectory, "LapsCoverage");
            destination = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(destination));
            Directory.CreateDirectory(destination);

            var progress = new Progress<string>(Console.WriteLine);
            var scanner = new LapsCoverageService(config);
            var snapshot = await scanner.ScanAsync(query, max, progress, ct);
            var previous = LapsCoverageSnapshotService.LoadPrevious();
            LapsCoverageComparison? comparison = null;

            if (!snapshot.Truncated)
            {
                if (previous is not null)
                {
                    try
                    {
                        comparison = LapsCoverageSnapshotService.Compare(
                            previous, snapshot);
                    }
                    catch (InvalidOperationException)
                    {
                        Console.WriteLine(
                            "Previous snapshot used a different scope; saving a new baseline.");
                    }
                }

                LapsCoverageSnapshotService.SaveCurrent(snapshot);
            }

            var csv = Path.Combine(destination, "laps_coverage_latest.csv");
            var json = Path.Combine(destination, "laps_coverage_latest.json");
            LapsCoverageCsvService.Export(csv, snapshot);
            JsonStore.WriteAtomic(json, snapshot);

            if (comparison is not null)
            {
                var changesFile = Path.Combine(
                    destination, "laps_coverage_changes.json");
                JsonStore.WriteAtomic(changesFile, comparison);
                Console.WriteLine(
                    "Metadata changes since previous snapshot: " +
                    comparison.Changes.Count);
                Console.WriteLine("Change report: " + changesFile);
            }

            var summary = snapshot.Summary;
            Console.WriteLine(
                $"AD LAPS Coverage: scanned={summary.Scanned}; " +
                $"Windows={summary.WindowsMetadata}; Legacy={summary.LegacyMetadata}; " +
                $"Both={summary.BothMetadata}; NotVerified={summary.Unverified}; " +
                $"Expired={summary.Expired}; ExpiringSoon={summary.ExpiringSoon}.");
            Console.WriteLine("DC: " + snapshot.DomainController);
            Console.WriteLine("CSV: " + csv);
            Console.WriteLine("JSON: " + json);
            Console.WriteLine(
                "Metadata only: hidden attributes and expiry indicators " +
                "cannot prove the presence or absence of a readable password.");

            if (snapshot.Truncated)
            {
                Console.Error.WriteLine(
                    "WARNING: Max-device limit reached. Partial snapshot not used as baseline.");
                return 3;
            }

            if (HasFlag(args, "--laps-coverage-fail-expired") &&
                summary.Expired > 0)
                return 24;

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("LAPS metadata scan canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "LAPS metadata scan failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return 1;
        }
    }

    private static bool HasFlag(IEnumerable<string> args, string flag) =>
        args.Any(x => x.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? Option(IReadOnlyList<string> args, string option)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase) &&
                !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
