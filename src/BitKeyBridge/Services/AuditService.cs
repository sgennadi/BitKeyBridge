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
        bool requireProtectedStorage = false,
        bool preferBroker = false)
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

        return WriteEntry(entry, requireProtectedStorage, preferBroker);
    }

    // Only the privileged Windows Service calls this after RunAsClient
    // authenticated the real token against the explicit RBAC allowlist.
    // Caller identity, timestamp, action and result are SERVICE generated;
    // no user-supplied "authorized" claim is stored as a trusted decision.
    internal AuditEntry? WriteBrokerAuthorizedRequest(
        ProtectedAuditBrokerRequest request, string actorName, string actorSid)
    {
        if (!string.Equals(_path, AppPaths.BrokerAuditLogFile,
                StringComparison.OrdinalIgnoreCase) ||
            !ProtectedAuditBrokerProtocol.Validate(request) ||
            string.IsNullOrWhiteSpace(actorName) ||
            !actorSid.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
            return null;

        var entry = new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            User = Sanitize(actorName),
            Host = Environment.MachineName,
            Action = request.Operation == ProtectedAuditBrokerProtocol.ProbeOperation
                ? "AuditBrokerHealthProbe"
                : "RecoveryPreReadRequested",
            Result = "Committed",
            ComputerName = Sanitize(request.Computer),
            RecoveryId = Sanitize(request.RecoveryId),
            Source = Sanitize(request.Source),
            AuthMode = "WindowsNamedPipe",
            Reference = Sanitize(request.Reference),
            Reason = Sanitize(request.Reason),
            Details = Sanitize("Operation=" + request.Operation + "; SID=" + actorSid),
            CorrelationId = Sanitize(request.SessionId)
        };
        return WriteEntry(entry, requireProtectedStorage: true, preferBroker: false);
    }

    private AuditEntry? WriteEntry(
        AuditEntry entry,
        bool requireProtectedStorage,
        bool preferBroker)
    {
        lock (Sync)
        {
            LastWriteUsedUserFallback = false;

            if (requireProtectedStorage && preferBroker)
            {
                if (!string.Equals(_path, AppPaths.AuditLogFile,
                        StringComparison.OrdinalIgnoreCase))
                    return null;

                // An unavailable, spoofed or rejecting service must NEVER
                // downgrade strict audit to local or per-user storage.
                return ProtectedAuditBrokerClient.TryCommit(entry);
            }

            if (requireProtectedStorage)
            {
                var canonicalMachineJournal =
                    string.Equals(_path, AppPaths.AuditLogFile,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_path, AppPaths.BrokerAuditLogFile,
                        StringComparison.OrdinalIgnoreCase);
                var trustedJournal = string.Equals(
                        _path, AppPaths.BrokerAuditLogFile,
                        StringComparison.OrdinalIgnoreCase)
                    ? ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(_path)
                    : ProtectedAuditStoragePolicy.IsTrusted(_path);
                if (!canonicalMachineJournal || !trustedJournal)
                    return null;

                try
                {
                    if (!AuditIntegrityService.Verify(_path).Valid)
                        return null;
                }
                catch
                {
                    return null;
                }
            }

            if (TryAppend(_path, entry, durable: requireProtectedStorage))
                return entry;

            if (!requireProtectedStorage &&
                string.Equals(_path, AppPaths.AuditLogFile,
                    StringComparison.OrdinalIgnoreCase) &&
                TryAppend(AppPaths.UserAuditLogFile, entry))
            {
                LastWriteUsedUserFallback = true;
                WindowsEventLogService.TryWrite(
                    "Protected audit journal unavailable; event stored under " +
                    "current-user profile only. Action=" + Sanitize(entry.Action),
                    EventLogSeverity.Warning, 4681, "RecoveryAudit");
                return entry;
            }

            return null;
        }
    }

    private bool TryAppend(string path, AuditEntry entry, bool durable = false)
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

            var serialized = JsonSerializer.Serialize(entry);
            if (durable)
            {
                // Acknowledgement is issued only after an actual Windows
                // FlushFileBuffers on the SYSTEM-managed journal handle.
                using var stream = new FileStream(
                    path, FileMode.Append, FileAccess.Write,
                    FileShare.Read, 4096, FileOptions.WriteThrough);
                using (var writer = new StreamWriter(
                           stream, new System.Text.UTF8Encoding(false),
                           4096, leaveOpen: true))
                {
                    writer.WriteLine(serialized);
                    writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            else
            {
                File.AppendAllText(path, serialized + Environment.NewLine);
            }

            // The protected audit was flushed before optional SIEM queueing.
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
            {
                entries.AddRange(ReadRecentFromPath(AppPaths.UserAuditLogFile, maximum));
                entries.AddRange(ReadRecentFromPath(AppPaths.BrokerAuditLogFile, maximum));
            }

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

    // Never silently destroy already retained SYSTEM-owned protected audit
    // history. Such loss would invalidate incident evidence and cannot be
    // authorized by ordinary readers or an automatic rotation worker.
    internal static bool RejectProtectedArchiveOverwrite(
        string path, bool previousArchiveExists) =>
        previousArchiveExists &&
        string.Equals(Path.GetFullPath(path),
            Path.GetFullPath(AppPaths.BrokerAuditLogFile),
            StringComparison.OrdinalIgnoreCase);

    private void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < _maxBytes) return;
        var old = path + ".old";
        if (RejectProtectedArchiveOverwrite(path, File.Exists(old)))
            throw new IOException(
                "Protected audit capacity reached. Refusing to delete the existing " +
                "rotated archive. A security-approved archival/checkpoint policy " +
                "is required before more protected recovery operations.");

        // Existing legacy/non-protected audit rotation behavior is unchanged.
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
