using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Read-only protected audit journal capacity check. Does not move, rotate,
/// delete, copy or expose privileged events or change any NTFS permissions.
/// </summary>
internal static class ProtectedAuditCapacityService
{
    // AuditService defaults to a 20 MiB journal for the protected broker.
    internal const long RotationThresholdBytes = 20L * 1024L * 1024L;
    internal const long WarningBytes = RotationThresholdBytes * 80 / 100;

    internal sealed record CapacityState(
        string Status, long CurrentBytes, long ArchivedBytes,
        bool ExistingArchive, long ThresholdBytes, string Message);

    internal static CapacityState Evaluate(long current, long archived, bool hasArchive)
    {
        if (current < 0 || archived < 0)
            throw new ArgumentOutOfRangeException(nameof(current));

        if (hasArchive && current >= RotationThresholdBytes)
            return new CapacityState("Blocked", current, archived, true,
                RotationThresholdBytes,
                "Next protected rotation would overwrite existing evidence. " +
                "Strict recovery must fail closed until approved archival/checkpoint.");

        if (hasArchive || current >= WarningBytes)
            return new CapacityState("Warning", current, archived, hasArchive,
                RotationThresholdBytes,
                "Administrator archival planning required before future rotation. " +
                "Never delete or truncate the protected .old file.");

        return new CapacityState("Ready", current, archived, false,
            RotationThresholdBytes,
            "Below warning threshold; audit integrity and SIEM delivery are separate checks.");
    }

    private static long LengthOrMissing(string path, out bool present)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0)
                throw new IOException("Expected audit file, found directory.");
            present = true;
            return new FileInfo(path).Length;
        }
        catch (FileNotFoundException)
        {
            present = false;
            return 0;
        }
        catch (DirectoryNotFoundException)
        {
            present = false;
            return 0;
        }
        // Access denied and I/O errors must NOT be converted to missing files.
    }

    internal static int PrintStatus()
    {
        try
        {
            var current = LengthOrMissing(AppPaths.BrokerAuditLogFile,
                out var currentExists);
            var prior = LengthOrMissing(AppPaths.BrokerAuditLogFile + ".old",
                out var archiveExists);
            var report = !currentExists
                ? new CapacityState("NotProvisioned", 0, prior, archiveExists,
                    RotationThresholdBytes,
                    "No current protected journal was found. " +
                    "The existence of an archive is not proof that broker auditing is available.")
                : Evaluate(current, prior, archiveExists);

            var archiveDirectory = ProtectedAuditArchiveService.DirectoryFor(
                AppPaths.BrokerAuditLogFile);
            var archiveFiles = ProtectedAuditArchiveService.OrderedFiles(
                AppPaths.BrokerAuditLogFile)
                .Where(x => string.Equals(Path.GetDirectoryName(x),
                    archiveDirectory, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var archiveBytes = archiveFiles.Sum(x => new FileInfo(x).Length);

            // A full journal plus .old is recoverable without erasing any
            // entries ONLY when ACLs and all hash links can be validated.
            var canArchive = currentExists &&
                ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(
                    AppPaths.BrokerAuditLogFile) &&
                AuditIntegrityService.Verify(AppPaths.BrokerAuditLogFile).Valid;
            if (report.Status == "Blocked" && canArchive)
                report = report with
                {
                    Status = "Warning",
                    Message = "Journal rotation requires verified archiving. " +
                        "The protected broker can preserve .old as a new numbered " +
                        "segment before its next write; no retained entries are deleted."
                };

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Component = "ProtectedAuditJournalCapacity",
                ArchivedSegments = archiveFiles.Length,
                RetainedArchiveBytes = archiveBytes,
                VerifiedArchivingReady = canArchive,
                report.Status,
                report.CurrentBytes,
                report.ArchivedBytes,
                report.ExistingArchive,
                report.ThresholdBytes,
                report.Message,
                Limit = "Read-only metadata; not hash-chain verification, SIEM receipt, or permission to reveal secrets."
            }, new JsonSerializerOptions { WriteIndented = true }));
            return report.Status is "Blocked" or "NotProvisioned" ? 3 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Protected audit capacity check failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return 3;
        }
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        if (Evaluate(0, 0, false).Status != "Ready" ||
            Evaluate(WarningBytes, 0, false).Status != "Warning" ||
            Evaluate(100, 1024, true).Status != "Warning" ||
            Evaluate(RotationThresholdBytes, 1024, true).Status != "Blocked" ||
            Evaluate(RotationThresholdBytes, 0, false).Status != "Warning")
            errors.Add("Protected audit rotation-capacity states are not fail-closed.");
        return errors;
    }
}
