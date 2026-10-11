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

    // A matching AD computer is not proof that LAPS was backed up.
    public bool LapsCandidateFound { get; set; }

    public DateTime? LapsKeyDateUtc { get; set; }

    public DateTime? LapsExpiresAtUtc { get; set; }

    public string BitLockerStatus { get; set; } =
        "No backup";

    public string LapsStatus { get; set; } =
        "Not verified";

    public string SourceSummary { get; set; } =
        "AD";
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
    // UI navigation only: the actual secret-read permission is enforced by
    // AuthorizeLapsAccess before any LAPS password attribute is requested.
    public static bool CanAttemptLapsRead(bool connected, HomeSearchResult? selected) =>
        connected && selected is not null;

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
        // An LDAP metadata-only lookup cannot prove a missing LAPS backup.
        if (row is null)
            return "Not verified";

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
            return "Not verified";
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
                row.Attribute.StartsWith("msLAPS-Encrypted", StringComparison.OrdinalIgnoreCase)
                    ? "Decrypt unauthorized"
                    : "Access denied",
            LapsPasswordStatus.DecryptionFailed =>
                "Decrypt failed",
            LapsPasswordStatus.InvalidData =>
                "Invalid data",
            LapsPasswordStatus.NotReturned =>
                "Not returned",
            _ =>
                row.Status.ToString()
        };
    }
}
