namespace BitKeyBridge;

/// <summary>
/// Maps only source-qualified cloud identities to Start rows. A recovery ID
/// is metadata, never a recovery password or automatic authorization.
/// </summary>
public static class HomeCloudSearchMapper
{
    public static List<HomeSearchResult> Map(IEnumerable<UnifiedDeviceInfo> devices)
    {
        return devices
            .Where(row => !row.FoundInAd &&
                          (row.FoundInIntune || row.FoundInEntra))
            .GroupBy(row => row.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(row => new HomeSearchResult
            {
                ComputerName = row.ComputerName,
                ComputerId = row.EntraDeviceId,
                SourceIdentity = row.IdentityKey,
                IsCloudRecord = true,
                IdentityAmbiguous = row.IdentityStatus == "Ambiguous device ID",
                BitLockerKeyCount = row.RecoveryKeyCount,
                BitLockerLookupFailed = row.RecoveryMetadataIncomplete,
                LatestRecoveryId = row.RecoveryIds.FirstOrDefault() ?? string.Empty,
                BitLockerStatus = row.RecoveryMetadataIncomplete
                    ? "Cloud: Unverified"
                    : row.RecoveryKeyCount > 0
                        ? $"Entra: {row.RecoveryKeyCount} ID(s)"
                        : "No Entra IDs reported",
                LapsStatus = "Entra LAPS: Not checked",
                SourceSummary = row.FoundInIntune ? "Intune / Entra" : "Entra"
            })
            .ToList();
    }
}
