using System.Security.Cryptography;

namespace BitKeyBridge;

public sealed class HousekeepingService
{
    private readonly string _incidentsDirectory;
    private readonly string _backupsDirectory;
    private readonly string _machineConfigDirectory;
    private readonly string _auditPath;
    private readonly string _statusPath;

    public HousekeepingService(
        string? incidentsDirectory = null,
        string? backupsDirectory = null,
        string? machineConfigDirectory = null,
        string? auditPath = null,
        string? statusPath = null)
    {
        _incidentsDirectory =
            Path.GetFullPath(
                incidentsDirectory ??
                AppPaths.IncidentsDirectory);
        _backupsDirectory =
            Path.GetFullPath(
                backupsDirectory ??
                AppPaths.BackupsDirectory);
        _machineConfigDirectory =
            Path.GetFullPath(
                machineConfigDirectory ??
                AppPaths.MachineConfigDirectory);
        _auditPath =
            Path.GetFullPath(
                auditPath ??
                AppPaths.AuditLogFile);
        _statusPath =
            Path.GetFullPath(
                statusPath ??
                AppPaths.HousekeepingStatusFile);
    }

    public HousekeepingStatus Run(
        AppConfig config,
        bool dryRun = false)
    {
        var result =
            new HousekeepingStatus
            {
                StartedAtUtc =
                    DateTime.UtcNow,
                DryRun =
                    dryRun
            };

        try
        {
            CleanupIncidents(
                config,
                result,
                dryRun);

            CleanupBackups(
                config,
                result,
                dryRun);

            CleanupTemporaryFiles(
                config,
                result,
                dryRun);

            result.Success =
                result.Errors.Count == 0 &&
                result.IncidentErrors == 0 &&
                result.BackupErrors == 0 &&
                result.TemporaryFileErrors == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(
                ex.Message);
        }
        finally
        {
            result.FinishedAtUtc =
                DateTime.UtcNow;

            TryPersist(result);

            WindowsEventLogService.TryWrite(
                $"Housekeeping completed. Success={result.Success}; DryRun={result.DryRun}; " +
                $"IncidentsDeleted={result.IncidentDeleted}; IncidentsPreserved={result.IncidentPreserved}; " +
                $"BackupsDeleted={result.BackupDeleted}; TempDeleted={result.TemporaryFilesDeleted}; " +
                $"BytesFreed={result.BytesFreed}; Errors={result.Errors.Count + result.IncidentErrors + result.BackupErrors + result.TemporaryFileErrors}.",
                result.Success
                    ? EventLogSeverity.Information
                    : EventLogSeverity.Warning,
                4630,
                "Housekeeping");
        }

        return result;
    }

    public HousekeepingStatus? ReadLastStatus()
    {
        try
        {
            return JsonStore.Read<HousekeepingStatus>(
                _statusPath);
        }
        catch
        {
            return null;
        }
    }

    private void CleanupIncidents(
        AppConfig config,
        HousekeepingStatus result,
        bool dryRun)
    {
        var files =
            EnumerateFilesOrThrow(
                _incidentsDirectory,
                "*.json")
                .ToArray();

        result.IncidentFilesScanned =
            files.Length;

        var retentionDays =
            Math.Clamp(
                config.IncidentRetentionDays,
                0,
                36500);

        if (retentionDays == 0)
            return;

        var cutoff =
            DateTime.UtcNow.AddDays(
                -retentionDays);

        var verifier =
            new RecoveryIncidentVerificationService(
                _incidentsDirectory,
                _auditPath);

        foreach (var path in files)
        {
            try
            {
                var bundle =
                    JsonStore.Read<RecoveryIncidentBundle>(
                        path);

                if (bundle is null)
                {
                    result.IncidentErrors++;
                    result.Errors.Add(
                        $"Incident bundle is unreadable: {Path.GetFileName(path)}");
                    continue;
                }

                var effectiveUtc =
                    bundle.UpdatedAtUtc == default
                        ? File.GetLastWriteTimeUtc(
                            path)
                        : bundle.UpdatedAtUtc
                            .ToUniversalTime();

                if (effectiveUtc >= cutoff)
                    continue;

                result.IncidentCandidates++;

                var sessionId =
                    string.IsNullOrWhiteSpace(
                        bundle.SessionId)
                        ? Path.GetFileNameWithoutExtension(
                            path)
                        : bundle.SessionId;

                var verification =
                    verifier.Verify(
                        sessionId);

                if (!verification.Valid)
                {
                    result.IncidentPreserved++;

                    if (string.Equals(
                            verification.Status,
                            "NotFullyRetained",
                            StringComparison.Ordinal))
                    {
                        result.IncidentPreservedNotFullyRetained++;
                        result.Warnings.Add(
                            $"Incident {sessionId} was preserved because its audit anchors are no longer fully retained.");
                    }
                    else
                    {
                        result.IncidentErrors++;
                        result.Warnings.Add(
                            $"Incident {sessionId} was preserved because verification status is {verification.Status}.");
                    }

                    continue;
                }

                if (dryRun)
                    continue;

                var fileInfo =
                    new FileInfo(path);
                var size =
                    fileInfo.Exists
                        ? fileInfo.Length
                        : 0;

                var bundleHash =
                    ComputeFileSha256(
                        path);

                var audit =
                    new AuditService(
                        _auditPath,
                        10);

                AuditEntry? deletionIntent;
                try
                {
                    deletionIntent =
                        audit.Write(
                            "HousekeepingDeleteIncidentPlan",
                            result:
                                "Authorized",
                            source:
                                "Local",
                            details:
                                $"BundleSha256={bundleHash}; Actions={bundle.Actions.Count}; UpdatedAtUtc={effectiveUtc:O}; Verification=Valid",
                            reference:
                                bundle.Reference,
                            reason:
                                "Retention policy",
                            correlationId:
                                sessionId);
                }
                catch (Exception ex)
                {
                    deletionIntent = null;
                    result.Warnings.Add(
                        $"Incident {sessionId} was preserved because the retention audit intent could not be written: {ex.Message}");
                }

                if (deletionIntent is null)
                {
                    result.IncidentPreserved++;
                    result.IncidentErrors++;
                    continue;
                }

                try
                {
                    File.Delete(path);

                    result.IncidentDeleted++;
                    result.BytesFreed +=
                        Math.Max(
                            0,
                            size);

                    var completion =
                        audit.Write(
                            "HousekeepingDeleteIncident",
                            result:
                                "Success",
                            source:
                                "Local",
                            details:
                                $"BundleSha256={bundleHash}; Actions={bundle.Actions.Count}; UpdatedAtUtc={effectiveUtc:O}; Verification=Valid; IntentHash={deletionIntent.EntryHash}",
                            reference:
                                bundle.Reference,
                            reason:
                                "Retention policy",
                            correlationId:
                                sessionId);

                    if (completion is null)
                    {
                        result.IncidentErrors++;
                        result.Warnings.Add(
                            $"Incident {sessionId} was deleted after an authorized retention intent, but the completion audit record could not be written. IntentHash={deletionIntent.EntryHash}.");
                    }
                }
                catch (Exception ex)
                {
                    result.IncidentErrors++;
                    result.Errors.Add(
                        $"Incident {sessionId} retention deletion failed after authorization: {ex.Message}");

                    try
                    {
                        _ = audit.Write(
                            "HousekeepingDeleteIncident",
                            result:
                                "Failed",
                            source:
                                "Local",
                            details:
                                $"BundleSha256={bundleHash}; IntentHash={deletionIntent.EntryHash}; Error={ex.Message}",
                            reference:
                                bundle.Reference,
                            reason:
                                "Retention policy",
                            correlationId:
                                sessionId);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                result.IncidentErrors++;
                result.Errors.Add(
                    $"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }

    private void CleanupBackups(
        AppConfig config,
        HousekeepingStatus result,
        bool dryRun)
    {
        var files =
            EnumerateFilesOrThrow(
                _backupsDirectory,
                "*.json")
                .Select(path =>
                {
                    try
                    {
                        return new FileInfo(
                            path);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x is not null)
                .Cast<FileInfo>()
                .OrderByDescending(
                    x => x.LastWriteTimeUtc)
                .ToArray();

        result.BackupFilesScanned =
            files.Length;

        var retentionDays =
            Math.Clamp(
                config.BackupRetentionDays,
                0,
                36500);

        if (retentionDays == 0)
            return;

        var minimumFiles =
            Math.Clamp(
                config.BackupMinimumFiles,
                0,
                1000);

        var cutoff =
            DateTime.UtcNow.AddDays(
                -retentionDays);

        for (var i = 0;
             i < files.Length;
             i++)
        {
            var file =
                files[i];

            if (i < minimumFiles)
            {
                if (file.LastWriteTimeUtc <
                    cutoff)
                {
                    result.BackupPreservedMinimum++;
                }

                continue;
            }

            if (file.LastWriteTimeUtc >=
                cutoff)
            {
                continue;
            }

            result.BackupCandidates++;

            if (dryRun)
                continue;

            try
            {
                var size =
                    file.Exists
                        ? file.Length
                        : 0;

                file.Delete();

                result.BackupDeleted++;
                result.BytesFreed +=
                    Math.Max(
                        0,
                        size);
            }
            catch (Exception ex)
            {
                result.BackupErrors++;
                result.Errors.Add(
                    $"{file.Name}: {ex.Message}");
            }
        }
    }

    private void CleanupTemporaryFiles(
        AppConfig config,
        HousekeepingStatus result,
        bool dryRun)
    {
        var retentionDays =
            Math.Clamp(
                config.TemporaryFileRetentionDays,
                0,
                3650);

        if (retentionDays == 0)
            return;

        var cutoff =
            DateTime.UtcNow.AddDays(
                -retentionDays);

        var roots =
            new[]
            {
                _machineConfigDirectory,
                _incidentsDirectory,
                _backupsDirectory
            }
            .Distinct(
                StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var path in
                     EnumerateFilesOrThrow(
                         root,
                         "*.tmp"))
            {
                result.TemporaryFilesScanned++;

                try
                {
                    var info =
                        new FileInfo(path);

                    if (info.LastWriteTimeUtc >=
                        cutoff)
                    {
                        continue;
                    }

                    if (dryRun)
                        continue;

                    var size =
                        info.Exists
                            ? info.Length
                            : 0;

                    info.Delete();

                    result.TemporaryFilesDeleted++;
                    result.BytesFreed +=
                        Math.Max(
                            0,
                            size);
                }
                catch (Exception ex)
                {
                    result.TemporaryFileErrors++;
                    result.Errors.Add(
                        $"{Path.GetFileName(path)}: {ex.Message}");
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesOrThrow(
        string directory,
        string pattern)
    {
        if (!Directory.Exists(
                directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(
                directory,
                pattern,
                SearchOption.TopDirectoryOnly)
            .ToArray();
    }

    private static string ComputeFileSha256(
        string path)
    {
        using var stream =
            File.OpenRead(path);

        return Convert.ToHexString(
            SHA256.HashData(stream));
    }

    private void TryPersist(
        HousekeepingStatus status)
    {
        try
        {
            JsonStore.WriteAtomic(
                _statusPath,
                status);
        }
        catch
        {
        }
    }
}
