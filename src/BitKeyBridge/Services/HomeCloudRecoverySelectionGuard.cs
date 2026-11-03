namespace BitKeyBridge;

/// <summary>
/// Pure, metadata-only qualification of a Start cloud recovery action.
/// Authorization, protected audit, Graph token and confidential reads are
/// exclusively handled by the existing Devices Reveal/Copy workflow.
/// </summary>
internal static class HomeCloudRecoverySelectionGuard
{
    internal static bool CanOfferRecovery(HomeSearchResult? selected)
    {
        if (selected is not
            {
                IsCloudRecord: true, IdentityAmbiguous: false,
                BitLockerKeyCount: > 0, InventoryMetadata: { } inventory
            } ||
            inventory.IdentityStatus is "Name collision" or "Ambiguous device ID" ||
            inventory.FoundInAd ||
            (!inventory.FoundInIntune && !inventory.FoundInEntra) ||
            inventory.RecoveryIds.Count != selected.BitLockerKeyCount ||
            !inventory.RecoveryIds.Contains(selected.LatestRecoveryId,
                StringComparer.OrdinalIgnoreCase))
            return false;

        var start = Device360Snapshot.FromHome(selected);
        var devices = Device360Snapshot.FromUnified(inventory);
        return start.IdentityVerified && devices.IdentityVerified &&
            string.Equals(start.QualifiedIdentity, devices.QualifiedIdentity,
                StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CanRevealOne(HomeSearchResult? selected) =>
        CanOfferRecovery(selected) &&
        selected!.InventoryMetadata!.RecoveryIds.Count == 1;

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var inventory = new UnifiedDeviceInfo
        {
            ComputerName = "TEST-CLOUD-1",
            IdentityKey = "INTUNE:MANAGED:test-managed-1",
            IdentityStatus = "Intune object",
            FoundInIntune = true,
            ManagedDeviceId = "test-managed-1",
            EntraDeviceId = "d945dd92-5097-4c08-90b2-325e66ba7627",
            RecoveryIds = ["test-recovery-id-1"]
        };
        var original = HomeCloudSearchMapper.Map([inventory]).Single();
        if (!CanOfferRecovery(original) || !CanRevealOne(original))
            errors.Add("Verified Start cloud recovery ID cannot navigate to Devices.");

        original.LatestRecoveryId = "unrelated-recovery-id";
        if (CanOfferRecovery(original))
            errors.Add("Cloud Reveal accepted a mismatching Recovery ID.");
        original.LatestRecoveryId = inventory.RecoveryIds[0];

        original.SourceIdentity = "INTUNE:MANAGED:other-device";
        if (CanOfferRecovery(original))
            errors.Add("Cloud Reveal accepted a forged Start source ID.");
        original.SourceIdentity = inventory.IdentityKey;

        original.IdentityAmbiguous = true;
        if (CanOfferRecovery(original))
            errors.Add("Cloud Reveal accepted an ambiguous device.");
        original.IdentityAmbiguous = false;

        inventory.RecoveryIds.Add("test-recovery-id-2");
        original.BitLockerKeyCount = 2;
        if (!CanOfferRecovery(original) || CanRevealOne(original))
            errors.Add("Multiple recovery IDs were auto-selected for Reveal.");
        inventory.RecoveryIds.RemoveAt(1);
        original.BitLockerKeyCount = 1;

        inventory.IdentityStatus = "Name collision";
        if (CanOfferRecovery(original))
            errors.Add("Cloud Reveal accepted an unresolved name collision.");
        inventory.IdentityStatus = "Intune object";
        inventory.FoundInAd = true;
        if (CanOfferRecovery(original))
            errors.Add("Cloud Reveal crossed AD/cloud sources by a name match.");
        return errors;
    }
}
