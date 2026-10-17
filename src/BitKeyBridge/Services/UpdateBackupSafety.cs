namespace BitKeyBridge;

/// <summary>
/// Preserves an operator's previously retained rollback image while a newer
/// update is staged and health checked. All paths are local to the install
/// target's directory; no secret material or policy is copied.
/// </summary>
internal static class UpdateBackupSafety
{
    internal sealed record BackupSnapshot(string ActivePath, string? SnapshotPath);

    internal static BackupSnapshot Snapshot(string backupPath)
    {
        var active = Path.GetFullPath(backupPath);
        if (!File.Exists(active))
            return new BackupSnapshot(active, null);

        if ((File.GetAttributes(active) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Updater rollback backup must not be a reparse point.");

        var snapshot = active + ".preinstall-" + Guid.NewGuid().ToString("N");
        File.Copy(active, snapshot, overwrite: false);
        return new BackupSnapshot(active, snapshot);
    }

    /// <summary>Restore only AFTER the installed EXE was restored successfully.</summary>
    internal static void Restore(BackupSnapshot state)
    {
        if (state.SnapshotPath is not null)
        {
            if (!File.Exists(state.SnapshotPath))
                throw new IOException("Previous rollback backup snapshot is missing.");
            File.Move(state.SnapshotPath, state.ActivePath, overwrite: true);
        }
        else if (File.Exists(state.ActivePath))
        {
            // There was no .bak before the failed install.
            File.Delete(state.ActivePath);
        }
    }

    internal static void Commit(BackupSnapshot state)
    {
        if (state.SnapshotPath is not null && File.Exists(state.SnapshotPath))
            File.Delete(state.SnapshotPath);
    }

    internal static IEnumerable<string> RunOfflineTests(string temporaryDirectory)
    {
        var errors = new List<string>();
        var root = Path.Combine(temporaryDirectory, "updater-backup-test");
        Directory.CreateDirectory(root);
        try
        {
            var backup = Path.Combine(root, "BitKeyBridge.exe.bak");
            File.WriteAllText(backup, "older-rollback");
            var snapshot = Snapshot(backup);
            File.WriteAllText(backup, "installed-before-failed-update");
            Restore(snapshot);
            if (File.ReadAllText(backup) != "older-rollback" ||
                snapshot.SnapshotPath is null || File.Exists(snapshot.SnapshotPath))
                errors.Add("Failed upgrade did not preserve an existing prior .bak image.");

            File.Delete(backup);
            var initiallyMissing = Snapshot(backup);
            File.WriteAllText(backup, "temporary-backup");
            Restore(initiallyMissing);
            if (File.Exists(backup))
                errors.Add("Failed upgrade retained a .bak which was absent beforehand.");

            File.WriteAllText(backup, "old-rollback");
            var successful = Snapshot(backup);
            File.WriteAllText(backup, "immediately-previous-version");
            Commit(successful);
            if (File.ReadAllText(backup) != "immediately-previous-version" ||
                successful.SnapshotPath is null || File.Exists(successful.SnapshotPath))
                errors.Add("Successful upgrade did not retain only its immediate .bak image.");
        }
        catch (Exception ex)
        {
            errors.Add("Transactional rollback backup test: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return errors;
    }
}
