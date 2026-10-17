namespace BitKeyBridge;

/// <summary>
/// Revalidates an AD BitLocker recovery selection after every async boundary
/// (LDAP metadata query, authorization dialog, protected audit and secret
/// read). A matching hostname by itself is never enough to reuse a key.
/// </summary>
internal static class HomeRecoverySelectionGuard
{
    internal static bool IsCurrent(
        HomeSearchResult? original, HomeSearchResult? now,
        RecoverySearchResult? recovery)
    {
        if (original is null || recovery is null ||
            !ReferenceEquals(original, now) ||
            original.IdentityAmbiguous || original.IsCloudRecord ||
            original.BitLockerKeyCount <= 0 ||
            string.IsNullOrWhiteSpace(original.ComputerName) ||
            !string.Equals(original.ComputerName, recovery.ComputerName,
                StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedId = SearchText.NormalizeIdentifierFragment(
            original.LatestRecoveryId);
        var returnedId = SearchText.NormalizeIdentifierFragment(
            recovery.RecoveryId);
        if (expectedId.Length == 0 ||
            !string.Equals(expectedId, returnedId, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(original.AdComputerDistinguishedName) &&
            !string.Equals(original.AdComputerDistinguishedName,
                recovery.ComputerDistinguishedName,
                StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        const string guid = "{12345678-1234-1234-1234-123456789ABC}";
        var selected = new HomeSearchResult
        {
            ComputerName = "PC-TEST", BitLockerKeyCount = 1,
            LatestRecoveryId = guid,
            AdComputerDistinguishedName =
                "CN=PC-TEST,OU=Workstations,DC=example,DC=com"
        };
        var row = new RecoverySearchResult
        {
            ComputerName = "PC-TEST",
            RecoveryId = "12345678-1234-1234-1234-123456789abc",
            ComputerDistinguishedName = selected.AdComputerDistinguishedName
        };
        if (!IsCurrent(selected, selected, row))
            errors.Add("Same AD computer, DN and normalized Recovery ID was rejected.");

        var changedSelection = new HomeSearchResult
        {
            ComputerName = selected.ComputerName,
            BitLockerKeyCount = 1,
            LatestRecoveryId = selected.LatestRecoveryId,
            AdComputerDistinguishedName = selected.AdComputerDistinguishedName
        };
        if (IsCurrent(selected, changedSelection, row) ||
            IsCurrent(selected, null, row))
            errors.Add("A different row or absent selection retained a stale BitLocker key.");

        selected.IdentityAmbiguous = true;
        if (IsCurrent(selected, selected, row))
            errors.Add("Ambiguous AD device was permitted to reveal a recovery key.");
        selected.IdentityAmbiguous = false;
        selected.IsCloudRecord = true;
        if (IsCurrent(selected, selected, row))
            errors.Add("Cloud metadata was permitted to use AD BitLocker retrieval.");
        selected.IsCloudRecord = false;

        row.ComputerDistinguishedName = "CN=PC-TEST,OU=Other,DC=example,DC=com";
        if (IsCurrent(selected, selected, row))
            errors.Add("BitLocker recovery result for another AD object was accepted.");
        row.ComputerDistinguishedName = selected.AdComputerDistinguishedName;

        row.RecoveryId = Guid.NewGuid().ToString();
        if (IsCurrent(selected, selected, row))
            errors.Add("New or unrelated Recovery ID was accepted after an async lookup.");
        row.RecoveryId = guid;
        selected.LatestRecoveryId = string.Empty;
        if (IsCurrent(selected, selected, row))
            errors.Add("BitLocker secret was allowed without a verified metadata Recovery ID.");

        return errors;
    }
}
