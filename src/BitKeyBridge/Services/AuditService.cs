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

    /// <summary>
    /// True only when the last successful write landed in the per-user audit
    /// journal instead of the centrally protected machine journal.
    /// A per-user file can be modified by its owner and is not tamper-proof.
    /// </summary>
    public bool LastWriteUsedUserFallback { get; private set; }

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
        string? correlationId = null,
        bool requireProtectedStorage = false)
    {
        var entry = new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            User = GetCurrentUser(),
            Host = Environment.MachineName,
            Action = action,
            Result = result,
            ComputerName = Sanitize(computerName),
            RecoveryId = Sanitize(recoveryId),
            Source = Sanitize(source),
            AuthMode = Sanitize(authMode),
            Reference = Sanitize(reference),
            Reason = Sanitize(reason),
            Details = Sanitize(details),
            CorrelationId = Sanitize(correlationId)
        };

        lock (Sync)
        {
            LastWriteUsedUserFallback = false;

            // A writable file is not necessarily a protected audit store.
            // Strict enterprise recovery requires both a canonical machine
            // path and a trusted NTFS owner/write ACL for the directory/file.
            if (requireProtectedStorage)
            {
                if (!string.Equals(_path, AppPaths.AuditLogFile,
                        StringComparison.OrdinalIgnoreCase) ||
                    !ProtectedAuditStoragePolicy.IsTrusted(_path))
                    return null;

                try
                {
                    // Fail closed on an already damaged hash chain before
                    // recording authorization for a new recovery secret.
                    if (!AuditIntegrityService.Verify(_path).Valid)
                        return null;
                }
                catch
                {
                    return null;
                }
            }

            if (TryAppend(_path, entry))
                return entry;

            // Only the standard machine audit can use this portable fallback.
            // Never silently downgrade explicitly configured/custom audit paths.
            if (!requireProtectedStorage &&
                string.Equals(_path, AppPaths.AuditLogFile, StringComparison.OrdinalIgnoreCase) &&
                TryAppend(AppPaths.UserAuditLogFile, entry))
            {
                LastWriteUsedUserFallback = true;
                WindowsEventLogService.TryWrite(
                    "Protected audit journal unavailable; audit event was saved only " +
                    "to current-user storage. Action=" + Sanitize(action),
                    EventLogSeverity.Warning, 4681, "RecoveryAudit");
                return entry;
            }

            return null;
        }
    }

    private bool TryAppend(string path, AuditEntry entry)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);

            using var processLock = AcquireInterprocessLock(path);
            if (processLock is null)
                return false;

            entry.ChainVersion = AuditIntegrityService.CurrentChainVersion;
            entry.PreviousHash = AuditIntegrityService.GetLastHash(path);
            RotateIfNeeded(path);
            entry.EntryHash = AuditIntegrityService.ComputeHash(entry);

            File.AppendAllText(
                path,
                JsonSerializer.Serialize(entry) +
                Environment.NewLine);

            // The audit was durably appended before optional SIEM queueing.
            SiemForwardingService.TryQueueFromAudit(entry);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Audit journal unavailable: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return false;
        }
    }

    public List<AuditEntry> ReadRecent(int maximum = 500)
    {
        maximum = Math.Clamp(maximum, 1, 5000);
        lock (Sync)
        {
            var entries = ReadRecentFromPath(_path, maximum);
            if (string.Equals(_path, AppPaths.AuditLogFile, StringComparison.OrdinalIgnoreCase))
                entries.AddRange(ReadRecentFromPath(AppPaths.UserAuditLogFile, maximum));

            return entries
                .OrderByDescending(x => x.TimestampUtc)
                .Take(maximum)
                .ToList();
        }
    }

    private static List<AuditEntry> ReadRecentFromPath(string path, int maximum)
    {
        try
        {
            if (!File.Exists(path))
                return [];

            var result = new List<AuditEntry>();
            foreach (var line in File.ReadLines(path)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .TakeLast(maximum))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<AuditEntry>(line);
                    if (entry is not null)
                        result.Add(entry);
                }
                catch (Exception ex)
                {
                    WindowsEventLogService.TryWrite(
                        "Audit entry parse failed: " +
                        DiagnosticRedaction.Sanitize(ex.Message),
                        EventLogSeverity.Warning, 4559, "Audit");
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Unable to read audit journal: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return [];
        }
    }

    private static FileStream? AcquireInterprocessLock(string path)
    {
        var lockPath = path + ".lock";

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

    private void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < _maxBytes) return;
        var old = path + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(path, old);
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

    private static string Sanitize(string? value) =>
        // Share the centralized scrubbing of BitLocker keys, password fields,
        // token/JWT strings and LAPS JSON before anything reaches audit storage.
        DiagnosticRedaction.Sanitize(value);
}
