using System.Diagnostics.Eventing.Reader;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace BitKeyBridge;

public sealed class EndpointDiagnosticBundle
{
    public int FormatVersion { get; set; } = 1;
    public string Product { get; set; } = "BitKeyBridge endpoint metadata diagnostics";
    public string ComputerName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> WhitelistedPolicyMetadata { get; set; } = [];
    public List<EndpointEventSummary> EventSummaries { get; set; } = [];
    public List<EndpointBitLockerVolumeState> VolumeStates { get; set; } = [];
    public EndpointTpmState? Tpm { get; set; }
    public List<string> Diagnostics { get; set; } = [];
}
public sealed record EndpointEventSummary(string Log, int Id, string Level, DateTimeOffset? TimeUtc);

public static class EndpointDiagnosticCollector
{
    private const string EntryName = "endpoint-summary.json";
    private static readonly string[] BitLockerAllowedValues =
    [
        "OSActiveDirectoryBackup", "OSRequireActiveDirectoryBackup",
        "OSRecovery", "UseTPM", "UseTPMPIN", "UseAdvancedStartup"
    ];
    private static readonly string[] LapsAllowedValues =
    [
        "BackupDirectory", "ADPasswordEncryptionEnabled",
        "ADPasswordEncryptionPrincipal", "ADEncryptedPasswordHistorySize",
        "PasswordAgeDays"
    ];

    public static EndpointDiagnosticBundle CollectLocal()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Endpoint collection needs Windows.");
        var bundle = new EndpointDiagnosticBundle
        {
            ComputerName = Environment.MachineName,
            OperatingSystem = Environment.OSVersion.VersionString,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        };

        ReadPolicy(bundle, @"SOFTWARE\Policies\Microsoft\FVE", BitLockerAllowedValues,
            "BitLocker GPO");
        ReadPolicy(bundle, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\LAPS",
            LapsAllowedValues, "Windows LAPS GPO");
        ReadPolicy(bundle, @"SOFTWARE\Microsoft\Policies\LAPS",
            LapsAllowedValues, "LAPS CSP");

        EndpointDeviceStateInspector.Append(bundle);
        ReadEvents(bundle, "Microsoft-Windows-LAPS/Operational");
        ReadEvents(bundle, "Microsoft-Windows-BitLocker-API/Management");
        bundle.Diagnostics.Add("Events contain only Event ID, level, time and provider log name. Event message payloads are NEVER collected.");
        bundle.Diagnostics.Add("BitLocker/TPM status is reported only when non-secret local WMI queries succeed; otherwise NotVerified.");
        bundle.Diagnostics.Add("The tool does not read BitLocker protectors, 48-digit recovery passwords, LAPS passwords, tokens, private keys, or registry secret values.");
        bundle.Diagnostics.Add("For actual protection state on this computer, use Get-BitLockerVolume -MountPoint $env:SystemDrive and inspect non-secret summary properties.");
        return bundle;
    }

    public static string Write(string filePath)
    {
        var bundle = CollectLocal();
        var destination = Path.GetFullPath(filePath);
        if (!destination.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The endpoint report must be a .zip file.", nameof(filePath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var file = new FileStream(destination, FileMode.CreateNew,
            FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        var entry = zip.CreateEntry(EntryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, bundle, new JsonSerializerOptions { WriteIndented = true });
        return destination;
    }

    public static EndpointDiagnosticBundle Read(string zipFile)
    {
        var info = new FileInfo(zipFile);
        if (!info.Exists || info.Length > 8 * 1024 * 1024)
            throw new InvalidDataException("Endpoint report missing or larger than 8 MB.");
        using var archive = ZipFile.OpenRead(zipFile);
        if (archive.Entries.Count != 1 || archive.Entries[0].FullName != EntryName)
            throw new InvalidDataException("Unexpected archive contents. Only a BitKeyBridge endpoint-summary.json is accepted.");
        var entry = archive.Entries[0];
        if (entry.Length > 2 * 1024 * 1024)
            throw new InvalidDataException("Uncompressed endpoint report is too large.");
        using var stream = entry.Open();
        using var bounded = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer)) != 0)
        {
            if (bounded.Length + read > 2 * 1024 * 1024)
                throw new InvalidDataException("Endpoint report exceeded its uncompressed size limit.");
            bounded.Write(buffer, 0, read);
        }
        bounded.Position = 0;
        var bundle = JsonSerializer.Deserialize<EndpointDiagnosticBundle>(bounded)
                     ?? throw new InvalidDataException("Empty endpoint report.");
        if (bundle.FormatVersion != 1 ||
            bundle.Product != "BitKeyBridge endpoint metadata diagnostics")
            throw new InvalidDataException("Unsupported endpoint report schema.");
        return bundle;
    }

    public static string SafeSummary(EndpointDiagnosticBundle value)
    {
        var lines = new List<string>
        {
            "BitKeyBridge endpoint metadata diagnosis",
            "Computer: " + DiagnosticRedaction.Sanitize(value.ComputerName),
            "Captured UTC: " + value.CapturedAt.ToUniversalTime().ToString("O"),
            "OS: " + DiagnosticRedaction.Sanitize(value.OperatingSystem),
            "",
            "WHITELISTED POLICY METADATA"
        };
        lines.AddRange(value.WhitelistedPolicyMetadata.OrderBy(x => x.Key)
            .Select(x => DiagnosticRedaction.Sanitize(x.Key + " = " + x.Value)));
        lines.Add("");
        lines.Add("BITLOCKER VOLUME STATUS (LOCAL WMI; NO PROTECTOR SECRETS)");
        lines.AddRange((value.VolumeStates ?? []).Take(30).Select(x =>
            DiagnosticRedaction.Sanitize(x.MountPoint + " | Protection=" +
                x.ProtectionStatus + " | Conversion=" + x.ConversionStatus +
                " | Method=" + x.EncryptionMethod)));
        lines.Add("TPM: " + (value.Tpm is null ? "NotVerified" :
            DiagnosticRedaction.Sanitize("State=" + value.Tpm.Status +
                ", Enabled=" + value.Tpm.Enabled + ", Activated=" +
                value.Tpm.Activated + ", Owned=" + value.Tpm.Owned +
                ", Spec=" + value.Tpm.SpecificationVersion)));
        lines.Add("");
        lines.Add("EVENT IDS / NO MESSAGE PAYLOADS");
        lines.AddRange(value.EventSummaries.Take(200).Select(x =>
            DiagnosticRedaction.Sanitize(x.Log + " | " + x.Id + " | " + x.Level +
                " | " + x.TimeUtc?.ToString("O"))));
        lines.Add("");
        lines.AddRange(value.Diagnostics.Take(50).Select(DiagnosticRedaction.Sanitize));
        return string.Join(Environment.NewLine, lines);
    }

    private static void ReadPolicy(EndpointDiagnosticBundle bundle,
        string subkey, IEnumerable<string> allowlist, string label)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subkey, writable: false);
            if (key is null)
            {
                bundle.Diagnostics.Add(label + ": registry policy not found / not readable.");
                return;
            }

            foreach (var name in allowlist)
            {
                var value = key.GetValue(name, null);
                if (value is int n)
                    bundle.WhitelistedPolicyMetadata[label + "/" + name] = n.ToString();
                else if (value is string text && text.Length <= 256 &&
                    !name.Contains("Password", StringComparison.OrdinalIgnoreCase))
                    bundle.WhitelistedPolicyMetadata[label + "/" + name] = text;
                else if (value is string group && name == "ADPasswordEncryptionPrincipal")
                    bundle.WhitelistedPolicyMetadata[label + "/" + name] = group[..Math.Min(group.Length, 256)];
            }
        }
        catch (Exception ex)
        {
            bundle.Diagnostics.Add(label + ": " + DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void ReadEvents(EndpointDiagnosticBundle bundle, string log)
    {
        try
        {
            var query = new EventLogQuery(log, PathType.LogName) { ReverseDirection = true };
            using var reader = new EventLogReader(query);
            for (var i = 0; i < 60; i++)
            {
                using var evt = reader.ReadEvent();
                if (evt is null) break;
                bundle.EventSummaries.Add(new EndpointEventSummary(log, evt.Id,
                    evt.LevelDisplayName ?? "(unknown)",
                    evt.TimeCreated is DateTime moment
                        ? new DateTimeOffset(moment).ToUniversalTime() : null));
            }
        }
        catch (Exception ex)
        {
            bundle.Diagnostics.Add(log + ": not readable (" +
                DiagnosticRedaction.Sanitize(ex.GetType().Name) + ").");
        }
    }
}
