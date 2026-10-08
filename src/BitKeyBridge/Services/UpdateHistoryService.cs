namespace BitKeyBridge;

public sealed class UpdateHistoryService
{
    private static readonly object Sync =
        new();

    private readonly string _path;

    public UpdateHistoryService(
        string? path = null)
    {
        _path =
            path ??
            AppPaths.UpdateHistoryFile;
    }

    public List<UpdateHistoryEntry> Read(
        int maximum = 200)
    {
        maximum =
            Math.Clamp(
                maximum,
                1,
                1000);

        lock (Sync)
        {
            try
            {
                return (
                        JsonStore.Read<List<UpdateHistoryEntry>>(
                            _path) ??
                        [])
                    .OrderByDescending(
                        x =>
                            x.TimestampUtc)
                    .Take(
                        maximum)
                    .ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    public void Append(
        UpdateHistoryEntry entry)
    {
        lock (Sync)
        {
            try
            {
                var rows =
                    Read(
                        499);
                rows.Insert(
                    0,
                    entry);
                JsonStore.WriteAtomic(
                    _path,
                    rows
                        .OrderByDescending(
                            x =>
                                x.TimestampUtc)
                        .Take(
                            500)
                        .ToList());
            }
            catch (Exception ex)
            {
                WindowsEventLogService.TryWrite(
                    "Update history persistence failed: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4201,
                    "Update");
            }
        }
    }

    public static string GetAuthenticodePublisher(
        string path)
    {
        var publisher =
            AuthenticodeVerificationService.GetPublisher(
                path);

        return string.IsNullOrWhiteSpace(
                publisher)
            ? "Unsigned"
            : publisher;
    }}
