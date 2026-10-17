namespace BitKeyBridge;

public sealed class HomeSearchResult
{
    public string ComputerName { get; set; } =
        string.Empty;

    public string ComputerId { get; set; } =
        string.Empty;

    // Exact AD computer DN captured during metadata search, when present.
    // Never use a matching hostname alone to authorize a secret read.
    public string AdComputerDistinguishedName { get; set; } = string.Empty;

    public int BitLockerKeyCount { get; set; }

    public bool BitLockerLookupFailed { get; set; }
    public bool LapsLookupFailed { get; set; }
    public bool IdentityAmbiguous { get; set; }
    public bool IsCloudRecord { get; set; }
    public string SourceIdentity { get; set; } = string.Empty;

    // The exact-computer BitLocker metadata was only found after
    // retrying outside the operator's currently selected OU.
    public bool BitLockerOutsideSelectedScope { get; set; }

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

public enum HomeDefaultAction
{
    None,
    BitLocker,
    Laps,
    Devices,
    ChooseRecoveryType
}

public static class HelpdeskStatus
{
    // Metadata-only search must not treat a matching AD computer object
    // as a confirmed LAPS password backup. Failed lookups remain visible
    // as explicitly unverified; they are not proof that a secret is absent.
    public static (bool BitLocker, bool Laps) VisibleHomeServices(
        HomeSearchResult? selected)
    {
        if (selected is null || selected.IsCloudRecord)
            return (false, false);

        var bitLocker = selected.BitLockerKeyCount > 0 ||
                        selected.BitLockerLookupFailed;
        var laps = selected.LapsLookupFailed ||
                   (selected.LapsCandidateFound &&
                    selected.LapsStatus is ("Detected" or "Expired"));

        if (!bitLocker && !laps)
            return (true, true); // Both unknown: allow manual diagnostics.

        return (bitLocker, laps);
    }

    // An LDAP metadata miss cannot prove that no recovery key exists.
    public static HomeDefaultAction ResolveHomeDefaultAction(
        HomeSearchResult? selected)
    {
        if (selected is null)
            return HomeDefaultAction.None;

        if (selected.IdentityAmbiguous)
            return HomeDefaultAction.ChooseRecoveryType;

        // A cloud result must not be routed into AD-only recovery.
        if (selected.IsCloudRecord)
            return HomeDefaultAction.Devices;

        if (selected.BitLockerKeyCount > 0)
            return HomeDefaultAction.BitLocker;

        // A failed BitLocker lookup does not establish the absence of a key.
        if (selected.BitLockerLookupFailed)
            return HomeDefaultAction.ChooseRecoveryType;

        // An AD computer match by itself is not proof of a LAPS backup.
        // Only positive non-secret metadata may route directly to LAPS.
        if (selected.LapsCandidateFound &&
            selected.LapsStatus is ("Detected" or "Expired"))
        {
            return HomeDefaultAction.Laps;
        }

        return HomeDefaultAction.ChooseRecoveryType;
    }

    public static bool CanAttemptBitLockerSearch(
        bool connected,
        HomeSearchResult? selected) =>
        connected && selected is not null && !selected.IdentityAmbiguous;

    public static bool HasIdentityCollision(
        IEnumerable<RecoverySearchResult> recovery,
        IEnumerable<LapsSearchResult> laps,
        string computerName)
    {
        var matchingRecovery = recovery
            .Where(row => row.ComputerName.Equals(
                computerName, StringComparison.OrdinalIgnoreCase));
        var matchingLaps = laps
            .Where(row => row.ComputerName.Equals(
                computerName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingLaps
            .Select(row => row.ComputerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Skip(1)
            .Any())
        {
            return true;
        }

        return matchingRecovery
            .Select(row => row.ComputerDistinguishedName)
            .Concat(matchingLaps.Select(row => row.ComputerDistinguishedName))
            .Where(dn => !string.IsNullOrWhiteSpace(dn))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Skip(1)
            .Any();
    }

    public static bool ShouldRetryBitLockerInDomain(
        int scopedRecoveryCount,
        string selectedSearchBase,
        string domainNamingContext) =>
        scopedRecoveryCount == 0 &&
        !string.IsNullOrWhiteSpace(domainNamingContext) &&
        !string.Equals(
            selectedSearchBase,
            domainNamingContext,
            StringComparison.OrdinalIgnoreCase);

    public static bool ShouldSelectDomainForRecovery(
        HomeSearchResult? selected,
        string? selectedSearchBase,
        string? domainNamingContext) =>
        selected is not null &&
        (selected.BitLockerOutsideSelectedScope ||
         selected.BitLockerKeyCount == 0) &&
        !string.IsNullOrWhiteSpace(domainNamingContext) &&
        !string.Equals(
            selectedSearchBase,
            domainNamingContext,
            StringComparison.OrdinalIgnoreCase);

    // UI navigation only: the actual secret-read permission is enforced by
    // AuthorizeLapsAccess before any LAPS password attribute is requested.
    public static bool CanAttemptLapsRead(bool connected, HomeSearchResult? selected) =>
        connected && selected is not null && !selected.IdentityAmbiguous;

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
