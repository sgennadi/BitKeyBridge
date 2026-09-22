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

    public AuditEntry? Write(
        string action,
        string result = "Success",
        string? computerName = null,
        string? recoveryId = null,
        string? source = null,
        string? authMode = null,
        string? details = null,
        string? reference = null,
        string? reason = null,
        string? correlationId = null)
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
            Reference = Sanitize(reference),
            Reason = Sanitize(reason),
            Details = Sanitize(details),
            CorrelationId = Sanitize(correlationId)
        };

        lock (Sync)
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_path)!;
                Directory.CreateDirectory(directory);

                using var processLock = AcquireInterprocessLock();
                if (processLock is null)
                    return null;

                var previousHash =
                    AuditIntegrityService.GetLastHash(_path);

                RotateIfNeeded();

                entry.ChainVersion =
                    AuditIntegrityService.CurrentChainVersion;
                entry.PreviousHash = previousHash;
                entry.EntryHash =
                    AuditIntegrityService.ComputeHash(entry);

                File.AppendAllText(
                    _path,
                    JsonSerializer.Serialize(entry) +
                    Environment.NewLine);

                SiemForwardingService
                    .TryQueueFromAudit(
                        entry);

                return entry;
            }
            catch
            {
                // Audit logging must not expose a recovery key or crash the recovery workflow.
                return null;
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

    private FileStream? AcquireInterprocessLock()
    {
        var lockPath = _path + ".lock";

        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
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
