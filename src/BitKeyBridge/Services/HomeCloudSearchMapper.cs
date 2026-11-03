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
                        row.IdentityStatus is "Ambiguous device ID" or "Name collision",
                    // Preserve inventory returned by this same authenticated
                    // Graph query. Device 360 can render it WITHOUT another
                    // search, identity guess or Microsoft 365 sign-in.
                    InventoryMetadata = !ambiguousGroup &&
                        row.IdentityStatus is not ("Ambiguous device ID" or "Name collision")
                        ? row : null,
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
                                : "Entra recovery backup: Not verified",
                    LapsStatus = "Entra LAPS: Not checked",
                    SourceSummary = row.FoundInIntune
                        ? row.FoundInEntra
                            ? "Intune / Entra"
                            : "Intune (Entra ID reference only)"
                        : "Entra"
                });
            })
            .ToList();
    }
}
