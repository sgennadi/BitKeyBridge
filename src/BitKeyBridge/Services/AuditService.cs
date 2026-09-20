using System.Text.Json;

namespace BitKeyBridge;

public sealed class AuditService
{
    private static readonly object Sync = new();
    private readonly string _path;
    private readonly long _maxBytes;

    public AuditService(string? path = null, int maxSizeMb = 20)
    {
        _path = path ?? AppPaths.AuditLogFile;
        _maxBytes = Math.Max(1, maxSizeMb) * 1024L * 1024L;
    }

    public string Path => _path;

    public void Write(
        string action,
        string result = "Success",
        string? computerName = null,
        string? recoveryId = null,
        string? source = null,
        string? authMode = null,
        string? details = null)
    {
        var entry = new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            User = GetCurrentUser(),
            Host = Environment.MachineName,
            Action = action,
            Result = result,
            ComputerName = computerName ?? string.Empty,
            RecoveryId = recoveryId ?? string.Empty,
            Source = source ?? string.Empty,
            AuthMode = authMode ?? string.Empty,
            Details = Sanitize(details)
        };

        lock (Sync)
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_path)!;
                Directory.CreateDirectory(directory);
                RotateIfNeeded();
                File.AppendAllText(_path, JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
            catch
            {
                // Audit logging must not expose a recovery key or crash the recovery workflow.
            }
        }
    }

    public List<AuditEntry> ReadRecent(int maximum = 500)
    {
        maximum = Math.Clamp(maximum, 1, 5000);
        lock (Sync)
        {
            try
            {
                if (!File.Exists(_path)) return [];
                var lines = File.ReadLines(_path)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .TakeLast(maximum)
                    .ToArray();
                var result = new List<AuditEntry>(lines.Length);
                foreach (var line in lines)
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<AuditEntry>(line);
                        if (entry is not null) result.Add(entry);
                    }
                    catch { }
                }
                result.Reverse();
                return result;
            }
            catch
            {
                return [];
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path) || new FileInfo(_path).Length < _maxBytes) return;
        var old = _path + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(_path, old);
    }

    private static string GetCurrentUser()
    {
        try
        {
            var domain = Environment.UserDomainName;
            return string.IsNullOrWhiteSpace(domain)
                ? Environment.UserName
                : domain + "\\" + Environment.UserName;
        }
        catch
        {
            return Environment.UserName;
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        // Never accept a 48-digit BitLocker recovery password into the audit trail.
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\b\d{6}(?:-\d{6}){7}\b",
            "[REDACTED-BITLOCKER-KEY]");
    }
}
