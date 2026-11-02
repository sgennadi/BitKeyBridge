namespace BitKeyBridge;

/// <summary>
/// Compare two source-provided AD identities. The selected Start object may
/// use its DN while Unified Devices uses an AD GUID. That is allowed only
/// when every available authoritative identifier agrees; a common display
/// name, FQDN, serial number or UPN is NEVER identity evidence.
/// </summary>
internal static class Device360AdIdentityMatcher
{
    internal static bool Matches(HomeSearchResult selected, UnifiedDeviceInfo inventory)
    {
        if (selected.IsCloudRecord || selected.IdentityAmbiguous ||
            !inventory.FoundInAd || inventory.IdentityStatus is
                "Name collision" or "Ambiguous device ID")
            return false;

        var selectedDn = selected.AdComputerDistinguishedName.Trim();
        var inventoryDn = inventory.AdDistinguishedName.Trim();
        var selectedHasGuid = Guid.TryParse(selected.ComputerId,
            out var selectedGuid);
        var inventoryHasGuid = Guid.TryParse(inventory.AdObjectGuid,
            out var inventoryGuid);

        if (selectedDn.Length == 0 && !selectedHasGuid)
            return false;

        // Require ALL provided authoritative identifiers to agree.
        // Never prefer a matching DN over an explicitly conflicting GUID.
        if (selectedDn.Length > 0 &&
            !string.Equals(selectedDn, inventoryDn,
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (selectedHasGuid &&
            (!inventoryHasGuid || selectedGuid != inventoryGuid))
            return false;

        // A source-qualified row must also prove that its own key
        // corresponds to its AD DN or GUID (not an unresolved/forged key).
        const string guidPrefix = "AD:GUID:";
        const string dnPrefix = "AD:DN:";
        var identityKey = inventory.IdentityKey.Trim();
        if (identityKey.StartsWith(guidPrefix,
                StringComparison.OrdinalIgnoreCase))
            return inventoryHasGuid &&
                Guid.TryParse(identityKey[guidPrefix.Length..], out var keyGuid) &&
                keyGuid == inventoryGuid;

        return identityKey.StartsWith(dnPrefix,
                StringComparison.OrdinalIgnoreCase) &&
            inventoryDn.Length > 0 &&
            string.Equals(identityKey[dnPrefix.Length..],
                inventoryDn, StringComparison.OrdinalIgnoreCase);
    }
}
