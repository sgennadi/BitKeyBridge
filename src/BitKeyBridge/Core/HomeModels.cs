namespace BitKeyBridge;

public sealed class HomeSearchResult
{
    public string ComputerName { get; set; } =
        string.Empty;

    public string ComputerId { get; set; } =
        string.Empty;

    public int BitLockerKeyCount { get; set; }

    public DateTime? LatestBitLockerKeyUtc { get; set; }

    public string LatestRecoveryId { get; set; } =
        string.Empty;

    public bool LapsDetected { get; set; }

    public DateTime? LapsKeyDateUtc { get; set; }

    public DateTime? LapsExpiresAtUtc { get; set; }

    public string BitLockerStatus { get; set; } =
        "No backup";

    public string LapsStatus { get; set; } =
        "No backup";
}

public sealed class RecentComputerEntry
{
    public string ComputerName { get; set; } =
        string.Empty;

    public string ComputerId { get; set; } =
        string.Empty;

    public string LastAction { get; set; } =
        string.Empty;

    public DateTime LastUsedUtc { get; set; } =
        DateTime.UtcNow;
}

public static class HelpdeskStatus
{
    public static string BitLocker(
        IReadOnlyCollection<RecoverySearchResult> rows,
        int oldKeyDays)
    {
        if (rows.Count == 0)
            return "No backup";

        var latest =
            rows
                .Where(
                    row =>
                        row.IsLatest == true)
                .OrderByDescending(
                    row =>
                        row.KeyDate)
                .FirstOrDefault() ??
            rows
                .OrderByDescending(
                    row =>
                        row.KeyDate)
                .First();

        if (latest.KeyDate is { } date &&
            date.ToUniversalTime() <
            DateTime.UtcNow.AddDays(
                -Math.Max(
                    1,
                    oldKeyDays)))
        {
            return rows.Count > 1
                ? "Multiple keys / Old key"
                : "Old key";
        }

        return rows.Count > 1
            ? "Multiple keys"
            : "Available";
    }

    public static string Laps(
        LapsSearchResult? row)
    {
        if (row is null)
            return "No backup";

        if (row.ExpiresAtUtc is { } expires &&
            expires.ToUniversalTime() <
            DateTime.UtcNow)
        {
            return "Expired";
        }

        if (row.IsLatest != true &&
            !row.ExpiresAtUtc.HasValue &&
            !row.KeyDateUtc.HasValue)
        {
            return "No backup";
        }

        return "Detected";
    }

    public static string LapsEntry(
        LapsPasswordEntry row)
    {
        if (row.Status ==
                LapsPasswordStatus.Available &&
            row.ExpiresAtUtc is { } expires &&
            !row.IsHistory &&
            expires.ToUniversalTime() <
            DateTime.UtcNow)
        {
            return "Available / Expired";
        }

        return row.Status switch
        {
            LapsPasswordStatus.Available =>
                "Available",
            LapsPasswordStatus.AccessDenied =>
                "Access denied",
            LapsPasswordStatus.DecryptionFailed =>
                "Decrypt denied",
            LapsPasswordStatus.InvalidData =>
                "Invalid data",
            LapsPasswordStatus.NotReturned =>
                "No backup",
            _ =>
                row.Status.ToString()
        };
    }
}
