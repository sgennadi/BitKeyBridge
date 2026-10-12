namespace BitKeyBridge;

/// <summary>
/// Stores only non-secret LDAP metadata under the operator's LocalAppData.
/// Compares by AD objectGUID (or DN if GUID was unavailable), never by name.
/// </summary>
public static class LapsCoverageSnapshotService
{
    public static string CurrentSnapshotPath =>
        Path.Combine(AppPaths.LocalConfigDirectory, "laps_coverage_latest.json");

    public static LapsCoverageSnapshot? LoadPrevious() =>
        File.Exists(CurrentSnapshotPath)
            ? JsonStore.Read<LapsCoverageSnapshot>(CurrentSnapshotPath)
            : null;

    public static void SaveCurrent(LapsCoverageSnapshot result)
    {
        if (result.Truncated)
            throw new InvalidOperationException(
                "A truncated LAPS scan must not replace the full snapshot.");
        JsonStore.WriteAtomic(CurrentSnapshotPath, result);

        // Retain a bounded, non-secret per-user history for audit comparison.
        // Data contains GUIDs, DN and expiration metadata only.
        var folder = Path.Combine(
            AppPaths.LocalConfigDirectory, "LapsCoverageHistory");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder,
            $"laps-{result.GeneratedAtUtc:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
        JsonStore.WriteAtomic(file, result);

        // Bounded rolling retention; do not touch the latest baseline file.
        foreach (var obsolete in Directory.EnumerateFiles(folder, "laps-*.json")
                     .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                     .Skip(30))
        {
            try
            {
                File.Delete(obsolete);
            }
            catch (IOException)
            {
                // A locked history file is retained rather than forcing deletion.
            }
            catch (UnauthorizedAccessException)
            {
                // Never fail the active inventory because a history file is protected.
            }
        }
    }

    public static LapsCoverageComparison Compare(
        LapsCoverageSnapshot previous,
        LapsCoverageSnapshot current)
    {
        if (previous.Truncated || current.Truncated ||
            !string.Equals(previous.SearchBaseDn, current.SearchBaseDn,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cannot compare different AD scopes or truncated LAPS scans.");
        }

        var result = new LapsCoverageComparison
        {
            PreviousAtUtc = previous.GeneratedAtUtc,
            CurrentAtUtc = current.GeneratedAtUtc,
            SearchBaseDn = current.SearchBaseDn
        };

        var before = previous.Devices
            .ToDictionary(x => x.IdentityKey, StringComparer.OrdinalIgnoreCase);
        var now = current.Devices
            .ToDictionary(x => x.IdentityKey, StringComparer.OrdinalIgnoreCase);

        foreach (var key in before.Keys.Union(now.Keys,
                     StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            before.TryGetValue(key, out var oldDevice);
            now.TryGetValue(key, out var newDevice);
            if (oldDevice is null && newDevice is not null)
            {
                Add(result, newDevice, "New AD object", "-", newDevice.BackupEvidence);
                continue;
            }

            if (oldDevice is not null && newDevice is null)
            {
                // No longer visible is NOT proof of deletion or lost backup.
                Add(result, oldDevice, "No longer visible in scan",
                    oldDevice.BackupEvidence, "Unverified");
                continue;
            }

            if (oldDevice is null || newDevice is null)
                continue;

            if (!string.Equals(oldDevice.BackupEvidence,
                    newDevice.BackupEvidence, StringComparison.Ordinal))
            {
                var kind = newDevice.BackupEvidence == "Not verified"
                    ? "Backup metadata no longer visible"
                    : oldDevice.BackupEvidence == "Not verified"
                        ? "Backup metadata became visible"
                        : "Backup metadata type changed";
                Add(result, newDevice, kind, oldDevice.BackupEvidence,
                    newDevice.BackupEvidence);
            }

            if (oldDevice.EffectiveExpirationUtc !=
                newDevice.EffectiveExpirationUtc)
            {
                Add(result, newDevice, "Expiry changed",
                    oldDevice.EffectiveExpirationUtc?.ToString("O") ?? "Unknown",
                    newDevice.EffectiveExpirationUtc?.ToString("O") ?? "Unknown");
            }
        }

        return result;
    }

    private static void Add(LapsCoverageComparison report,
        LapsCoverageDevice row, string kind, string oldValue, string newValue)
    {
        report.Changes.Add(new LapsCoverageChange
        {
            IdentityKey = row.IdentityKey,
            ComputerName = row.ComputerName,
            ChangeType = kind,
            PreviousState = oldValue,
            CurrentState = newValue
        });
    }
}
