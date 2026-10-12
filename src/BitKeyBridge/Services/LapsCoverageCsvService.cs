using System.Text;

namespace BitKeyBridge;

/// <summary>
/// CSV contains only AD metadata and protects against spreadsheet formula
/// injection through untrusted computer names and OU strings.
/// </summary>
public static class LapsCoverageCsvService
{
    public static void Export(string path, LapsCoverageSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "ComputerName,ObjectGuid,DistinguishedName,BackupEvidence," +
            "WindowsExpirationUtc,LegacyExpirationUtc,ExpirationState," +
            "ADLastLogonUtc,DirectoryServer");
        foreach (var row in snapshot.Devices)
        {
            var data = new[]
            {
                row.ComputerName,
                row.ObjectGuid,
                row.DistinguishedName,
                row.BackupEvidence,
                row.WindowsExpirationUtc?.ToString("O") ?? "",
                row.LegacyExpirationUtc?.ToString("O") ?? "",
                row.ExpirationState(snapshot.GeneratedAtUtc),
                row.AdLastLogonUtc?.ToString("O") ?? "",
                row.DirectoryServer
            };
            sb.AppendLine(string.Join(",", data.Select(Quote)));
        }
        var destination = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, sb.ToString(), new UTF8Encoding(true));
    }

    public static string Quote(string? value)
    {
        var safe = (value ?? "").Replace("\r", " ").Replace("\n", " ");
        if (safe.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')
            safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
