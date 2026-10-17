using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>
/// Append-only, numbered protected journal archives. A previously rotated
/// .old journal is MOVED, never overwritten or deleted. Verification, archive
/// discovery and rotation run under the existing audit.jsonl.lock handle.
/// </summary>
internal static class ProtectedAuditArchiveService
{
    private static readonly Regex SegmentName = new(
        @"^audit-(\d{8})\.jsonl$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static bool IsBrokerJournal(string path) =>
        string.Equals(Path.GetFullPath(path),
            Path.GetFullPath(AppPaths.BrokerAuditLogFile),
            StringComparison.OrdinalIgnoreCase);

    internal static string DirectoryFor(string path) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,
            "Archive");

    /// <summary>
    /// Chronological order is mandatory for chain verification. Unknown
    /// entries, reparse points and numbered gaps are evidence of an unsafe
    /// journal; they must never be silently ignored.
    /// </summary>
    internal static IReadOnlyList<string> OrderedFiles(string path)
    {
        var ordered = new List<string>();
        var directory = DirectoryFor(path);
        var includeArchive = IsBrokerJournal(path) ||
                             (Path.GetFileName(path).Equals("audit.jsonl",
                                  StringComparison.OrdinalIgnoreCase) &&
                              Directory.Exists(directory));

        if (includeArchive)
        {
            if (File.Exists(directory) || Directory.Exists(directory) &&
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Invalid protected archive directory.");

            if (Directory.Exists(directory))
            {
                var expected = 1;
                foreach (var candidate in Directory.EnumerateFileSystemEntries(directory)
                             .OrderBy(Path.GetFileName, StringComparer.Ordinal))
                {
                    var match = SegmentName.Match(Path.GetFileName(candidate));
                    if (!match.Success ||
                        !int.TryParse(match.Groups[1].Value, out var number) ||
                        number != expected)
                        throw new InvalidDataException(
                            "Protected journal archive has an unexpected entry or segment gap.");

                    var attrs = File.GetAttributes(candidate);
                    if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                        throw new InvalidDataException(
                            "Protected journal archive contains an unsafe file.");
                    ordered.Add(candidate);
                    expected++;
                }
            }
        }

        var previous = path + ".old";
        if (Directory.Exists(previous))
            throw new InvalidDataException("Rotated audit file is a directory.");
        if (File.Exists(previous))
            ordered.Add(previous);
        if (Directory.Exists(path))
            throw new InvalidDataException("Current audit journal is a directory.");
        if (File.Exists(path))
            ordered.Add(path);
        return ordered;
    }

    /// <summary>
    /// Called ONLY by the protected broker append path while its exclusive
    /// interprocess lock is held. Leave the evidence intact on ANY failure.
    /// </summary>
    internal static void ArchivePreviousRotationUnderLock(string path)
    {
        if (!IsBrokerJournal(path))
            throw new InvalidOperationException(
                "Automatic retention archival is restricted to the protected broker journal.");

        var old = path + ".old";
        if (!File.Exists(old))
            return;

        if (!ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(path))
            throw new IOException("Protected archive ACL verification failed.");

        var before = AuditIntegrityService.VerifyUnderLock(path);
        if (!before.Valid)
            throw new IOException("Protected journal chain is invalid before archival.");

        var directory = DirectoryFor(path);
        Directory.CreateDirectory(directory);
        if (!ProtectedAuditStoragePolicy.IsTrustedBrokerArchiveDirectory(directory))
            throw new IOException("Archive directory does not have protected machine ACLs.");

        var count = OrderedFiles(path).Count(file =>
            string.Equals(Path.GetDirectoryName(file), directory,
                StringComparison.OrdinalIgnoreCase));
        if (count >= 99999999)
            throw new IOException("Protected audit archive segment number exhausted.");

        var next = Path.Combine(directory, $"audit-{count + 1:00000000}.jsonl");
        if (File.Exists(next) || Directory.Exists(next))
            throw new IOException("Protected journal archive destination exists.");

        File.Move(old, next); // atomic rename on the same volume; never overwrite
        try
        {
            if (!ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(path))
                throw new IOException("Protected archive ACL changed after rotation.");
            var after = AuditIntegrityService.VerifyUnderLock(path);
            if (!after.Valid)
                throw new IOException("Protected journal chain changed during archival.");
        }
        catch
        {
            // Best effort to restore the original name if the move was valid
            // but a subsequent check failed. Preserve whichever copy remains.
            try
            {
                if (File.Exists(next) && !File.Exists(old))
                    File.Move(next, old);
            }
            catch (IOException restoreException)
            {
                WindowsEventLogService.TryWrite(
                    "Protected audit archive rename rollback failed: " +
                    DiagnosticRedaction.Sanitize(restoreException.Message),
                    EventLogSeverity.Error, 4684, "Audit");
            }
            catch (UnauthorizedAccessException restoreException)
            {
                WindowsEventLogService.TryWrite(
                    "Protected audit archive rename rollback denied: " +
                    DiagnosticRedaction.Sanitize(restoreException.Message),
                    EventLogSeverity.Error, 4684, "Audit");
            }
            throw;
        }
    }

    /// <summary>
    /// Recover ONLY the narrowly defined interrupted-rotation state: an
    /// absent current file AND a verified, ACL-protected .old file, running
    /// as SYSTEM. An unexpectedly missing journal with no .old remains a
    /// hard failure, not an invitation to create a fresh empty audit log.
    /// </summary>
    internal static bool RecoverInterruptedRotation()
    {
        var path = AppPaths.BrokerAuditLogFile;
        if (File.Exists(path) || !File.Exists(path + ".old"))
            return false;
        if (!OperatingSystem.IsWindows() ||
            !WindowsIdentity.GetCurrent().IsSystem)
            throw new UnauthorizedAccessException(
                "Only LocalSystem may recover interrupted protected audit rotation.");

        using var auditLock = new FileStream(
            path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path))
            return false;

        if (!ProtectedAuditStoragePolicy.IsTrusted(path + ".old") ||
            !ProtectedAuditStoragePolicy.IsTrusted(path + ".lock"))
            throw new IOException("Interrupted journal rotation has unsafe file ACLs.");

        var archiveDir = DirectoryFor(path);
        if (Directory.Exists(archiveDir))
        {
            if (!ProtectedAuditStoragePolicy.IsTrustedBrokerArchiveDirectory(archiveDir))
                throw new IOException("Interrupted journal rotation has unsafe archive ACLs.");
            foreach (var file in OrderedFiles(path))
            {
                if (string.Equals(Path.GetDirectoryName(file), archiveDir,
                        StringComparison.OrdinalIgnoreCase) &&
                    !ProtectedAuditStoragePolicy.IsTrusted(file))
                    throw new IOException("An archived protected audit segment has unsafe ACLs.");
            }
        }
        var verified = AuditIntegrityService.VerifyUnderLock(path);
        if (!verified.Valid)
            throw new IOException("Interrupted protected journal rotation has invalid hash continuity.");

        using (var file = new FileStream(
                   path, FileMode.CreateNew, FileAccess.Write,
                   FileShare.None, 4096, FileOptions.WriteThrough))
            file.Flush(flushToDisk: true);

        if (!ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(path))
            throw new IOException("Recovered audit journal failed protected storage verification.");

        WindowsEventLogService.TryWrite(
            "Recovered an interrupted protected audit rotation without discarding evidence.",
            EventLogSeverity.Information, 4683, "Audit");
        return true;
    }

    internal static IEnumerable<string> RunOfflineTests(string scratchDirectory)
    {
        var errors = new List<string>();
        var root = Path.Combine(scratchDirectory, "archive-regression");
        var journal = Path.Combine(root, "audit.jsonl");
        var archiveDirectory = DirectoryFor(journal);
        try
        {
            Directory.CreateDirectory(archiveDirectory);
            string previous = string.Empty;
            var rows = new List<AuditEntry>();
            for (var i = 0; i < 4; i++)
            {
                var entry = new AuditEntry
                {
                    TimestampUtc = new DateTime(2026, 1, 1, 0, i, 0, DateTimeKind.Utc),
                    Action = "ArchiveOfflineFixture",
                    Result = "Committed",
                    User = "LOCAL-TEST",
                    Host = "FIXTURE",
                    ChainVersion = AuditIntegrityService.CurrentChainVersion,
                    PreviousHash = previous
                };
                entry.EntryHash = AuditIntegrityService.ComputeHash(entry);
                previous = entry.EntryHash;
                rows.Add(entry);
            }
            File.WriteAllText(Path.Combine(archiveDirectory, "audit-00000001.jsonl"),
                JsonSerializer.Serialize(rows[0]) + Environment.NewLine);
            File.WriteAllText(Path.Combine(archiveDirectory, "audit-00000002.jsonl"),
                JsonSerializer.Serialize(rows[1]) + Environment.NewLine);
            File.WriteAllText(journal + ".old",
                JsonSerializer.Serialize(rows[2]) + Environment.NewLine);
            File.WriteAllText(journal,
                JsonSerializer.Serialize(rows[3]) + Environment.NewLine);

            if (!AuditIntegrityService.Verify(journal).Valid ||
                !string.Equals(AuditIntegrityService.GetLastHash(journal),
                    rows[3].EntryHash, StringComparison.OrdinalIgnoreCase) ||
                !AuditIntegrityService.ContainsEntryHash(journal, rows[0].EntryHash))
                errors.Add("Numbered protected journal archive did not preserve hash chain/history.");

            // Power loss after File.Move(current, .old), before writing the
            // next entry: the chain head must remain available from .old.
            File.Delete(journal);
            if (!string.Equals(AuditIntegrityService.GetLastHash(journal),
                    rows[2].EntryHash, StringComparison.OrdinalIgnoreCase) ||
                !AuditIntegrityService.Verify(journal).Valid)
                errors.Add("Interrupted protected rotation lost the final .old hash.");
            File.WriteAllText(journal,
                JsonSerializer.Serialize(rows[3]) + Environment.NewLine);

            var missing = Path.Combine(archiveDirectory, "audit-00000001.jsonl");
            File.Delete(missing);
            if (AuditIntegrityService.Verify(journal).Valid)
                errors.Add("A missing archive segment was not rejected.");
        }
        catch (Exception ex)
        {
            errors.Add("Protected journal archival regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return errors;
    }
}
