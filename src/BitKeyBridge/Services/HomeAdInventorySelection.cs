namespace BitKeyBridge;

/// <summary>
/// Lists independently verified AD inventory records alongside recovery
/// metadata on Start. Names are display/search keys ONLY. Identity and any
/// attached hardware must be proven by an AD DN/GUID from the same search.
/// </summary>
internal static class HomeAdInventorySelection
{
    internal static List<string> BuildNames(
        IEnumerable<RecoverySearchResult> recovery,
        IEnumerable<LapsSearchResult> laps,
        IEnumerable<UnifiedDeviceInfo> inventory,
        int maximum = 100)
    {
        // Preserve recovery/LAPS results when a broad AD inventory query
        // reaches the display cap. Sorting happens AFTER prioritization.
        var metadataNames = recovery.Select(row => row.ComputerName)
            .Concat(laps.Select(row => row.ComputerName));
        var inventoryNames = inventory
            .Where(row => row.FoundInAd &&
                !string.IsNullOrWhiteSpace(row.AdDistinguishedName) &&
                !string.IsNullOrWhiteSpace(row.ComputerName))
            .Select(row => UnifiedDeviceMerge.NormalizeName(row.ComputerName));
        return metadataNames
            .Concat(inventoryNames)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximum, 1, 1000))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<UnifiedDeviceInfo> MatchingAdCandidates(
        IEnumerable<UnifiedDeviceInfo> inventory, string computerName)
    {
        var shortName = UnifiedDeviceMerge.NormalizeName(computerName);
        if (string.IsNullOrWhiteSpace(shortName))
            return [];
        return inventory.Where(row =>
            row.FoundInAd &&
            !string.IsNullOrWhiteSpace(row.AdDistinguishedName) &&
            (row.IdentityKey.StartsWith("AD:GUID:",
                StringComparison.OrdinalIgnoreCase) ||
             row.IdentityKey.StartsWith("AD:DN:",
                StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(UnifiedDeviceMerge.NormalizeName(row.ComputerName),
                shortName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    internal static bool IsAmbiguous(
        IReadOnlyList<UnifiedDeviceInfo> matchingInventory,
        IEnumerable<RecoverySearchResult> recovery,
        IEnumerable<LapsSearchResult> laps,
        string computerName)
    {
        if (matchingInventory.Count == 0)
            return false;

        if (matchingInventory.Any(row => row.IdentityStatus is
            "Name collision" or "Ambiguous device ID"))
            return true;

        var distinctDns = matchingInventory
            .Select(row => row.AdDistinguishedName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var distinctGuids = matchingInventory
            .Select(row => row.AdObjectGuid)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctDns.Length != 1 || distinctGuids.Length > 1)
            return true;

        var metadataDns = recovery
            .Where(row => NameMatches(row.ComputerName, computerName))
            .Select(row => row.ComputerDistinguishedName)
            .Concat(laps
                .Where(row => NameMatches(row.ComputerName, computerName))
                .Select(row => row.ComputerDistinguishedName))
            .Where(dn => !string.IsNullOrWhiteSpace(dn));
        if (metadataDns.Any(dn => !string.Equals(dn, distinctDns[0],
                StringComparison.OrdinalIgnoreCase)))
            return true;

        if (distinctGuids.Length == 1)
        {
            var lapsGuids = laps
                .Where(row => NameMatches(row.ComputerName, computerName))
                .Select(row => row.ComputerId)
                .Where(id => Guid.TryParse(id, out _));
            if (lapsGuids.Any(id => !Guid.TryParse(id, out var guid) ||
                !Guid.TryParse(distinctGuids[0], out var inventoryGuid) ||
                guid != inventoryGuid))
                return true;
        }
        return false;
    }

    private static bool NameMatches(string left, string right) =>
        string.Equals(UnifiedDeviceMerge.NormalizeName(left),
            UnifiedDeviceMerge.NormalizeName(right),
            StringComparison.OrdinalIgnoreCase);
}
