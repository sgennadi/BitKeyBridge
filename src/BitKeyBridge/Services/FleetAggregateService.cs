using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Offline, schema-bound aggregation of approved secret-free endpoint status
/// JSON. This does not connect to endpoints, authenticate to a domain,
/// upload data, read the BitLocker/LAPS store or execute remote actions.
/// </summary>
internal static class FleetAggregateService
{
    private const int MaximumFiles = 5000;
    private const long MaximumReportBytes = 64L * 1024L;
    private const int StaleAfterDays = 7;

    internal sealed record Snapshot(
        IReadOnlyList<FleetInventoryService.FleetReport> Rows,
        int InputFiles, int RejectedFiles, int DuplicateFiles,
        DateTime CollectedAtUtc);

    internal static int Run(string[] args)
    {
        var index = Array.FindIndex(args, a =>
            a.Equals("--fleet-aggregate", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 2 >= args.Length ||
            string.IsNullOrWhiteSpace(args[index + 1]) ||
            string.IsNullOrWhiteSpace(args[index + 2]) ||
            args[index + 1].StartsWith("--", StringComparison.Ordinal) ||
            args[index + 2].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "Usage: BitKeyBridge.exe --fleet-aggregate <input-folder> <output.csv>");
            return 2;
        }

        try
        {
            var snapshot = ReadDirectory(args[index + 1]);
            WriteCsvAtomic(args[index + 2], snapshot);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Component = "BitKeyBridgeFleetAggregation",
                snapshot.InputFiles,
                AcceptedMachines = snapshot.Rows.Count,
                snapshot.RejectedFiles,
                snapshot.DuplicateFiles,
                Output = Path.GetFullPath(args[index + 2]),
                Contract = "NonSecretMetadataOnly",
                Note = "Offline aggregation only. A running service does not prove a committed audit ACK."
            }));
            return snapshot.Rows.Count == 0 || snapshot.RejectedFiles != 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Offline fleet aggregation failed: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
            return 2;
        }
    }

    internal static Snapshot ReadDirectory(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root) ||
            (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Inventory input must be an existing real directory.");

        var selected = new Dictionary<string, FleetInventoryService.FleetReport>(
            StringComparer.OrdinalIgnoreCase);
        var scanned = 0;
        var invalid = 0;
        var duplicates = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            scanned++;
            if (scanned > MaximumFiles)
                throw new InvalidDataException("Fleet inventory file count exceeds the configured safe limit.");

            try
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    info.Length == 0 || info.Length > MaximumReportBytes)
                    throw new InvalidDataException("Invalid inventory JSON file size or identity.");

                var content = File.ReadAllText(file, Encoding.UTF8);
                var report = ParseValidatedReport(content);
                if (selected.TryGetValue(report.Machine, out var existing))
                {
                    duplicates++;
                    if (existing.CollectedUtc < report.CollectedUtc)
                        selected[report.Machine] = report;
                }
                else
                    selected.Add(report.Machine, report);
            }
            catch (Exception ex) when (ex is IOException or
                                       UnauthorizedAccessException or
                                       JsonException or
                                       InvalidDataException or
                                       ArgumentException)
            {
                // Do not log source JSON, untrusted host names or file text.
                invalid++;
            }
        }

        return new Snapshot(
            selected.Values.OrderBy(x => x.Machine,
                StringComparer.OrdinalIgnoreCase).ToArray(),
            scanned, invalid, duplicates, DateTime.UtcNow);
    }

    private static FleetInventoryService.FleetReport ParseValidatedReport(
        string source)
    {
        using var json = JsonDocument.Parse(source, new JsonDocumentOptions
        {
            MaxDepth = 8, AllowTrailingCommas = false
        });
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Fleet report must be an object.");

        var permitted = typeof(FleetInventoryService.FleetReport)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (!permitted.Contains(property.Name) ||
                !present.Add(property.Name))
                throw new InvalidDataException(
                    "Unknown or duplicate inventory property.");
        }
        if (present.Count != permitted.Count)
            throw new InvalidDataException(
                "Inventory report has missing required fields.");

        var report = JsonSerializer.Deserialize<FleetInventoryService.FleetReport>(
            source, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                MaxDepth = 8
            }) ?? throw new InvalidDataException("Null inventory report.");

        if (report.SchemaVersion != FleetInventoryService.SchemaVersion ||
            report.DataClassification != "NonSecretMetadataOnly" ||
            string.IsNullOrWhiteSpace(report.Machine) ||
            report.Machine.Length > 255 ||
            report.Machine.Any(c =>
                !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')) ||
            string.IsNullOrWhiteSpace(report.Version) ||
            report.Version.Length > 80 ||
            report.Version.Any(c =>
                !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+')) ||
            report.RuntimeId is not ("win-x64" or "win-x86" or "win-arm64") ||
            report.ServiceState is not
                ("Running" or "Stopped" or "StartPending" or "StopPending" or
                 "Paused" or "Unknown" or "NotInstalled") ||
            report.BrokerServiceHealth is not
                ("NotConfigured" or "PolicyIncomplete" or "ServiceNotInstalled" or
                 "ServiceNotRunningOrUnknown" or "ServiceIdentityUnexpected" or
                 "ServiceRunningNotVerified") ||
            report.LastUpdateOutcome is not
                ("Success" or "Failed" or "Unclassified" or "UnknownOrNotRecorded") ||
            report.CollectedUtc.Kind != DateTimeKind.Utc ||
            report.CollectedUtc > DateTime.UtcNow.AddDays(1) ||
            report.CollectedUtc.Year < 2020)
        {
            throw new InvalidDataException("Invalid inventory schema or value.");
        }

        return report;
    }

    internal static void WriteCsvAtomic(string destination, Snapshot snapshot)
    {
        var full = Path.GetFullPath(destination);
        if (!string.Equals(Path.GetExtension(full), ".csv",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Fleet aggregation output must have a .csv extension.");

        var directory = Path.GetDirectoryName(full) ??
                        throw new InvalidDataException("Fleet aggregation output directory is missing.");
        Directory.CreateDirectory(directory);

        var temporary = Path.Combine(directory,
            ".fleet-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var writer = new StreamWriter(temporary, false,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                writer.WriteLine(
                    "Machine,CollectedUtc,Age,Version,RuntimeId,OsArchitecture," +
                    "ServiceState,BrokerServiceHealth,LastUpdateOutcome," +
                    "LastUpdateUtc,AutoInstallPermitted,RollbackHold");
                foreach (var row in snapshot.Rows)
                {
                    var age = snapshot.CollectedAtUtc - row.CollectedUtc;
                    var state = age.TotalDays > StaleAfterDays ? "Stale" : "Current";
                    writer.WriteLine(string.Join(",",
                        Field(row.Machine),
                        Field(row.CollectedUtc.ToString("O")),
                        Field(state),
                        Field(row.Version),
                        Field(row.RuntimeId),
                        Field(row.OsArchitecture),
                        Field(row.ServiceState),
                        Field(row.BrokerServiceHealth),
                        Field(row.LastUpdateOutcome),
                        Field(row.LastUpdateUtc?.ToUniversalTime().ToString("O")),
                        Field(row.AutoInstallPermitted ? "Yes" : "No"),
                        Field(row.RollbackHold ? "Yes" : "No")));
                }
            }

            File.Move(temporary, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static string Field(string? input)
    {
        var value = input ?? string.Empty;
        // Escaping quotes alone is insufficient for Excel/CSV formula injection.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    internal static IEnumerable<string> RunOfflineTests(string scratchDirectory)
    {
        var failures = new List<string>();
        var root = Path.Combine(scratchDirectory, "fleet-aggregation-tests");
        Directory.CreateDirectory(root);

        try
        {
            var baseline = FleetInventoryService.Build(new AppConfig(),
                "PC-DEMO", new ServiceInfo(), rollbackHold: false);
            var previous = baseline with
            {
                CollectedUtc = DateTime.UtcNow.AddHours(-2),
                Version = "0.30.3"
            };
            var current = baseline with
            {
                CollectedUtc = DateTime.UtcNow,
                Version = "0.32.0"
            };
            File.WriteAllText(Path.Combine(root, "older.json"),
                FleetInventoryService.Serialize(previous));
            File.WriteAllText(Path.Combine(root, "newer.json"),
                FleetInventoryService.Serialize(current));

            var invalid = FleetInventoryService.Serialize(current);
            var closingBrace = invalid.LastIndexOf('}');
            File.WriteAllText(Path.Combine(root, "extra.json"),
                invalid.Insert(closingBrace,
                    ", \"accessToken\": \"SECRET_DO_NOT_INCLUDE\""));
            File.WriteAllText(Path.Combine(root, "formula.json"),
                FleetInventoryService.Serialize(current with
                {
                    Machine = "=HYPERLINK(secret)"
                }));

            var aggregate = ReadDirectory(root);
            if (aggregate.Rows.Count != 1 || aggregate.InputFiles != 4 ||
                aggregate.RejectedFiles != 2 ||
                aggregate.DuplicateFiles != 1 ||
                aggregate.Rows[0].Version != "0.32.0")
                failures.Add("Fleet aggregation dedupe/schema rejection failed.");

            var csv = Path.Combine(scratchDirectory, "fleet-summary.csv");
            WriteCsvAtomic(csv, aggregate);
            var exported = File.ReadAllText(csv);
            if (!exported.Contains("PC-DEMO", StringComparison.Ordinal) ||
                !exported.Contains("0.32.0", StringComparison.Ordinal) ||
                exported.Contains("SECRET_DO_NOT_INCLUDE", StringComparison.Ordinal) ||
                exported.Contains("=HYPERLINK", StringComparison.Ordinal))
                failures.Add("Fleet CSV leaked invalid JSON values or lost valid records.");
        }
        catch (Exception ex)
        {
            failures.Add("Fleet aggregation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return failures;
    }
}
