using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Explicit, machine-scoped export of the existing non-secret fleet status.
/// File transport uses the caller's Windows token and an approved destination.
/// It never connects to AD/Graph or reads BitLocker/LAPS secrets.
/// </summary>
internal static class FleetSnapshotExportService
{
    internal static int Export(AppConfig config, string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            Console.Error.WriteLine("Usage: --fleet-export <existing-directory\\MACHINE.json>");
            return 2;
        }

        try
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Fleet export requires Windows.");

            var degraded = false;
            ServiceInfo service;
            try
            {
                service = WindowsServiceHost.GetInfo();
            }
            catch (Exception)
            {
                // A restricted SCM token means Unknown, never Running/healthy.
                service = new ServiceInfo { Installed = true, State = "Unknown" };
                degraded = true;
            }

            UpdateHistoryEntry? lastUpdate = null;
            try
            {
                lastUpdate = new UpdateHistoryService().Read(1).FirstOrDefault();
            }
            catch (Exception)
            {
                // Lack of update history visibility cannot imply success.
                degraded = true;
            }

            var report = FleetInventoryService.Build(
                config, Environment.MachineName, service,
                UpdateRollbackHoldService.IsActiveForCurrentBuild(), lastUpdate);
            WriteReportAtomic(destination, report);
            Console.WriteLine(degraded
                ? "Fleet metadata exported with unavailable local status fields."
                : "Fleet metadata exported.");
            return degraded ? 2 : 0;
        }
        catch (Exception ex)
        {
            // Never log destination paths, JSON content, accounts or token data.
            Console.Error.WriteLine("Fleet export failed: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
            return 2;
        }
    }

    internal static void WriteReportAtomic(
        string destination, FleetInventoryService.FleetReport report)
    {
        var machine = report.Machine;
        if (string.IsNullOrWhiteSpace(machine) || machine.Length > 63 ||
            machine.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
            throw new InvalidDataException("Invalid machine identity for fleet export.");

        var full = Path.GetFullPath(destination);
        var expected = machine + ".json";
        if (!string.Equals(Path.GetFileName(full), expected,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Fleet export must target its own MACHINE.json, not arbitrary files.");

        var parent = Path.GetDirectoryName(full) ??
            throw new InvalidDataException("Fleet export parent directory not found.");
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException("Fleet destination must already exist.");

        // Avoid known reparse targets including intermediate junctions.
        for (DirectoryInfo? current = new(parent);
             current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Fleet destination path contains a link.");
        }

        if (File.Exists(full) &&
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Fleet snapshot target is a link.");

        var payload = FleetInventoryService.Serialize(report);
        if (Encoding.UTF8.GetByteCount(payload) > 64 * 1024)
            throw new InvalidDataException("Fleet report exceeds its safe size limit.");

        var temp = Path.Combine(parent, "." + machine + "." +
            Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // Exclusive new temporary file, same directory for atomic replace.
            using (var stream = new FileStream(temp, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = new UTF8Encoding(false).GetBytes(payload);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    internal static IEnumerable<string> RunOfflineTests(string scratchDirectory)
    {
        var errors = new List<string>();
        var folder = Path.Combine(scratchDirectory, "fleet-export-tests");
        Directory.CreateDirectory(folder);

        try
        {
            var report = FleetInventoryService.Build(
                new AppConfig(), "PC-FLEET-01",
                new ServiceInfo(), rollbackHold: false);
            var path = Path.Combine(folder, "PC-FLEET-01.json");
            WriteReportAtomic(path, report);
            WriteReportAtomic(path, report with
            {
                Version = "0.32.0",
                CollectedUtc = DateTime.UtcNow
            });

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.GetProperty("version").GetString() != "0.32.0" ||
                root.GetProperty("dataClassification").GetString() !=
                    "NonSecretMetadataOnly" ||
                root.GetProperty("machine").GetString() != "PC-FLEET-01" ||
                root.EnumerateObject().Any(p =>
                    p.Name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("token", StringComparison.OrdinalIgnoreCase)))
                errors.Add("Fleet export did not atomically replace a safe report.");

            var blocked = false;
            try
            {
                WriteReportAtomic(Path.Combine(folder, "ANOTHER-PC.json"), report);
            }
            catch (InvalidDataException) { blocked = true; }
            if (!blocked)
                errors.Add("Fleet export allowed overwriting another machine filename.");

            blocked = false;
            try
            {
                WriteReportAtomic(Path.Combine(folder, "PC-FLEET-01.txt"), report);
            }
            catch (InvalidDataException) { blocked = true; }
            if (!blocked)
                errors.Add("Fleet export accepted a non-JSON target.");

            blocked = false;
            try
            {
                WriteReportAtomic(path, report with
                {
                    Machine = "=UNTRUSTED()"
                });
            }
            catch (InvalidDataException) { blocked = true; }
            if (!blocked)
                errors.Add("Fleet export accepted an unsafe machine identity.");
        }
        catch (Exception ex)
        {
            errors.Add("Fleet export regression: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
        }
        return errors;
    }
}
