namespace BitKeyBridge;

// A deliberate rollback must not be undone by the next startup's default
// automatic update check. The hold is version-bound: a manually installed
// newer build does not inherit the previous version's hold.
public sealed class RollbackAutoUpdateHold
{
    public string InstalledVersion { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public static class UpdateRollbackHoldService
{
    // A marker from an older restored version can outlive a later manual
    // update performed by that older binary. Keep Resume available for it.
    public static bool HasRecordedHold() =>
        File.Exists(AppPaths.UpdateRollbackHoldFile);

    public static bool IsActiveForCurrentBuild() =>
        IsActiveForVersion(UpdateService.GetCurrentVersion().ToString());

    internal static bool IsActiveForVersion(
        string installedVersion,
        string? path = null)
    {
        var file = path ?? AppPaths.UpdateRollbackHoldFile;
        if (!File.Exists(file))
            return false;

        try
        {
            var hold = JsonStore.Read<RollbackAutoUpdateHold>(file);
            // An unreadable marker must fail closed: never silently undo a
            // user's downgrade because the persisted update hold was damaged.
            if (hold is null || string.IsNullOrWhiteSpace(hold.InstalledVersion))
                return true;

            return UpdateService.ReleaseVersionsMatch(
                hold.InstalledVersion,
                installedVersion);
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "BitKeyBridge rollback hold could not be read: " +
                DiagnosticRedaction.Sanitize(ex.Message),
                EventLogSeverity.Warning,
                4107,
                "Update");
            return true;
        }
    }

    public static void Activate(
        string installedVersion,
        string source,
        string? path = null)
    {
        _ = UpdateService.ParseVersion(installedVersion);
        if (source is not "Local" and not "Online")
            throw new InvalidDataException("Unknown rollback source.");

        JsonStore.WriteAtomic(
            path ?? AppPaths.UpdateRollbackHoldFile,
            new RollbackAutoUpdateHold
            {
                InstalledVersion = installedVersion,
                Source = source,
                CreatedUtc = DateTime.UtcNow
            });
    }

    public static void Clear(string? path = null)
    {
        var file = path ?? AppPaths.UpdateRollbackHoldFile;
        if (File.Exists(file))
            File.Delete(file);
    }
}
