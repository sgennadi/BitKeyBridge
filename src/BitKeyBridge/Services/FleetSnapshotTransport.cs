using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitKeyBridge;

/// <summary>
/// No remote telemetry or credentials. Operators use existing Windows/Intune
/// permissions to export whitelisted endpoint snapshots to an approved share.
/// </summary>
internal static class FleetSnapshotTransport
{
    private const int MaximumFiles = 20000;
    private const long MaximumBytes = 16384;
    private static readonly Regex MachineName = new(
        "^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly HashSet<string> AllowedFields = new(
        [
            "schemaVersion", "machine", "collectedUtc", "version", "runtimeId",
            "osArchitecture", "serviceState", "serviceInstalled",
            "auditBrokerConfigured", "protectedAuditRequired", "rbacConfigured",
            "autoInstallConfigured", "autoInstallPermitted", "unsignedConsent",
            "rollbackHold", "dataClassification"
        ], StringComparer.OrdinalIgnoreCase);

    internal sealed record DeviceRow(
        string Machine, string Version, DateTime CollectedUtc,
        string Architecture, string ServiceState, string Status,
        bool ProtectedAuditRequired, bool AuditBrokerConfigured,
        bool UpdateAllowed, bool RollbackHold);

    internal sealed record FleetSummary(
        int SchemaVersion, DateTime GeneratedUtc, int Reports,
        int Devices, int Stale, int ServiceAttention, int RollbackHolds,
        int AuditConfigurationAttention, IReadOnlyList<DeviceRow> Rows,
        string DataClassification, string Warning);

    internal static int ArgumentError(string reason)
    {
        Console.Error.WriteLine(reason);
        return 2;
    }

    internal static int Export(AppConfig config, string destination)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Windows is required.");
            ServiceInfo service;
            var unknown = false;
            try { service = WindowsServiceHost.GetInfo(); }
            catch (Exception ex)
            {
                unknown = true;
                WindowsEventLogService.TryWrite(
                    "Fleet SCM status unavailable: " +
                    DiagnosticRedaction.Sanitize(ex.GetType().Name),
                    EventLogSeverity.Warning, 4561, "Fleet");
                service = new ServiceInfo { Installed = true, State = "Unknown" };
            }
            var report = FleetInventoryService.Build(
                config, Environment.MachineName, service,
                UpdateRollbackHoldService.IsActiveForCurrentBuild());
            if (!Validate(report, DateTime.UtcNow))
                throw new InvalidDataException("Local fleet snapshot contract failed.");
            WriteAtomic(destination, FleetInventoryService.Serialize(report));
            Console.WriteLine("Non-secret fleet metadata exported.");
            return unknown ? 2 : 0;
        }
        catch (Exception ex)
        {
            return ArgumentError("Fleet export failed: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
        }
    }

    internal static int PrintSummary(string directory)
    {
        try
        {
            Console.WriteLine(JsonSerializer.Serialize(
                Aggregate(directory, DateTime.UtcNow),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            return 0;
        }
        catch (Exception ex)
        {
            return ArgumentError("Fleet aggregation failed: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
        }
    }

    internal static FleetSummary Aggregate(string directory, DateTime now)
    {
        if (!Directory.Exists(directory) ||
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Fleet inventory directory is missing or a reparse point.");
        var snapshots = new List<FleetInventoryService.FleetReport>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (snapshots.Count >= MaximumFiles)
                throw new InvalidDataException("Fleet inventory limit exceeded.");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Fleet snapshot links are not allowed.");
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > MaximumBytes)
                throw new InvalidDataException("Fleet snapshot file length is invalid.");
            using var document = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().Any(
                    property => !AllowedFields.Contains(property.Name)))
                throw new InvalidDataException("Fleet snapshot contains unknown fields.");
            var item = document.RootElement.Deserialize<FleetInventoryService.FleetReport>(ReadOptions);
            if (item is null || !Validate(item, now))
                throw new InvalidDataException("Fleet snapshot version, identity or timestamp invalid.");
            snapshots.Add(item);
        }

        var latest = snapshots
            .GroupBy(x => x.Machine, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.CollectedUtc).First())
            .OrderBy(x => x.Machine, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var report = latest.Select(x =>
        {
            var stale = now.ToUniversalTime() - x.CollectedUtc.ToUniversalTime() >
                TimeSpan.FromHours(24);
            var serviceIssue = x.ServiceInstalled && x.ServiceState != "Running";
            var auditIssue = x.ProtectedAuditRequired &&
                (!x.AuditBrokerConfigured || !x.RbacConfigured);
            return new DeviceRow(
                x.Machine, x.Version, x.CollectedUtc.ToUniversalTime(),
                x.RuntimeId, x.ServiceState,
                stale ? "Stale" : serviceIssue || auditIssue || x.RollbackHold
                    ? "Attention" : "Reported",
                x.ProtectedAuditRequired, x.AuditBrokerConfigured,
                x.AutoInstallPermitted, x.RollbackHold);
        }).ToArray();
        return new FleetSummary(
            FleetInventoryService.SchemaVersion, now.ToUniversalTime(),
            snapshots.Count, report.Length,
            report.Count(x => x.Status == "Stale"),
            latest.Count(x => x.ServiceInstalled && x.ServiceState != "Running"),
            latest.Count(x => x.RollbackHold),
            latest.Count(x => x.ProtectedAuditRequired &&
                (!x.AuditBrokerConfigured || !x.RbacConfigured)),
            report, "NonSecretMetadataOnly",
            "A stale snapshot does NOT prove network, AD or Graph server unavailability. No secret access is implied.");
    }

    private static bool Validate(FleetInventoryService.FleetReport row, DateTime now) =>
        row.SchemaVersion == FleetInventoryService.SchemaVersion &&
        row.DataClassification == "NonSecretMetadataOnly" &&
        row.Machine is not null && MachineName.IsMatch(row.Machine) &&
        row.CollectedUtc.Kind != DateTimeKind.Unspecified &&
        row.CollectedUtc.ToUniversalTime() > new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
        row.CollectedUtc.ToUniversalTime() <= now.ToUniversalTime().AddMinutes(10) &&
        !string.IsNullOrEmpty(row.Version) && row.Version.Length <= 48 &&
        row.RuntimeId is "win-x64" or "win-x86" or "win-arm64" &&
        row.OsArchitecture is "X64" or "X86" or "Arm64" &&
        row.ServiceState is "Running" or "Stopped" or "StartPending" or
            "StopPending" or "Paused" or "Unknown" or "NotInstalled";

    private static void WriteAtomic(string destination, string json)
    {
        if (!destination.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Fleet output must be a .json file.");
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("Fleet JSON exceeds schema size limit.");
        var target = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(target) ??
            throw new InvalidDataException("Fleet destination directory not found.");
        if (!Directory.Exists(parent) ||
            (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Fleet destination directory must already exist and not be a link.");
        if (Directory.Exists(target) ||
            File.Exists(target) &&
            (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Fleet destination must not be a directory or link.");
        var temporary = Path.Combine(parent,
            "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            using (var stream = new FileStream(
                       temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(flushToDisk: true);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    internal static IEnumerable<string> RunOfflineTests(string scratchDirectory)
    {
        var errors = new List<string>();
        var folder = Path.Combine(scratchDirectory, "fleet-inventory-fixtures");
        try
        {
            Directory.CreateDirectory(folder);
            var first = FleetInventoryService.Build(new AppConfig(),
                "PC-01", new ServiceInfo { Installed = true, State = "Running" }, false);
            WriteAtomic(Path.Combine(folder, "PC-01.json"), FleetInventoryService.Serialize(first));
            var previous = first with { Version = "0.30.1", CollectedUtc = first.CollectedUtc.AddHours(-26) };
            WriteAtomic(Path.Combine(folder, "PC-01-previous.json"),
                FleetInventoryService.Serialize(previous));
            var second = first with
            {
                Machine = "PC-02", Version = "0.30.3", ServiceState = "Stopped",
                RollbackHold = true
            };
            WriteAtomic(Path.Combine(folder, "PC-02.json"), FleetInventoryService.Serialize(second));
            var result = Aggregate(folder, DateTime.UtcNow);
            if (result.Reports != 3 || result.Devices != 2 || result.Stale != 0 ||
                result.RollbackHolds != 1 || result.ServiceAttention != 1 ||
                result.Rows.First(x => x.Machine == "PC-01").Version != first.Version)
                errors.Add("Fleet aggregate failed to deduplicate, detect holds or identify latest versions.");
            File.WriteAllText(Path.Combine(folder, "unexpected.json"),
                "{\"schemaVersion\":1,\"recoveryKey\":\"fake\"}");
            var blocked = false;
            try { _ = Aggregate(folder, DateTime.UtcNow); }
            catch (InvalidDataException) { blocked = true; }
            if (!blocked)
                errors.Add("Fleet aggregator accepted a forbidden secret property.");
        }
        catch (Exception ex)
        {
            errors.Add("Fleet snapshot test: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
        }
        return errors;
    }
}
