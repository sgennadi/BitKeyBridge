namespace BitKeyBridge;

/// <summary>
/// Maps source-qualified cloud identities to Start rows. A recovery ID is
/// metadata, never a recovery password or authorization. Duplicate or missing
/// source identities must remain visible as ambiguous, not silently collapse.
/// </summary>
public static class HomeCloudSearchMapper
{
    public static List<HomeSearchResult> Map(IEnumerable<UnifiedDeviceInfo> devices)
    {
        return devices
            .Where(row => !row.FoundInAd &&
                          (row.FoundInIntune || row.FoundInEntra))
            .GroupBy(row => row.IdentityKey ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                // A malformed Graph result can report two conflicting devices
                // with the same identity key. Keep both metadata rows but
                // disable guided recovery until the conflict is resolved.
                var ambiguousGroup = group.Count() > 1 ||
                                     string.IsNullOrWhiteSpace(group.Key);
                return group.Select(row => new HomeSearchResult
                {
                    ComputerName = row.ComputerName,
                    ComputerId = row.EntraDeviceId,
                    SourceIdentity = row.IdentityKey ?? string.Empty,
                    IsCloudRecord = true,
                    IdentityAmbiguous = ambiguousGroup ||
                        row.IdentityStatus == "Ambiguous device ID",
                    BitLockerKeyCount = row.RecoveryKeyCount,
                    BitLockerLookupFailed = row.RecoveryMetadataIncomplete ||
                                           ambiguousGroup,
                    LatestRecoveryId = row.RecoveryIds.FirstOrDefault() ?? string.Empty,
                    BitLockerStatus = ambiguousGroup
                        ? "Cloud: Identity unverified"
                        : row.RecoveryMetadataIncomplete
                            ? "Cloud: Unverified"
                            : row.RecoveryKeyCount > 0
                                ? $"Entra: {row.RecoveryKeyCount} ID(s)"
                                : "No Entra IDs reported",
                    LapsStatus = "Entra LAPS: Not checked",
                    SourceSummary = row.FoundInIntune ? "Intune / Entra" : "Entra"
                });
            })
            .ToList();
    }
}
