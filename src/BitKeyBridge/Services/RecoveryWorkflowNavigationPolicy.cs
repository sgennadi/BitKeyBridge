namespace BitKeyBridge;

/// <summary>
/// A Recovery Steps dialog is only a preflight snapshot: by the time it
/// closes, its Start row or connection may have changed. This guard permits
/// METADATA NAVIGATION only, and never authorizes a secret retrieval.
/// </summary>
internal enum RecoveryWorkflowRoute
{
    None,
    Start,
    AccessHealth,
    BitLocker,
    Laps,
    Devices
}

internal static class RecoveryWorkflowNavigationPolicy
{
    internal static bool CanOpen(
        HomeSearchResult? initial, HomeSearchResult? current,
        RecoveryWorkflowRoute route,
        bool adConnected, bool cloudConnected,
        bool rbacAllowed)
    {
        if (initial is null || !ReferenceEquals(initial, current) ||
            initial.IdentityAmbiguous || !rbacAllowed ||
            string.IsNullOrWhiteSpace(initial.ComputerName))
            return false;

        if (initial.IsCloudRecord)
        {
            var name = initial.SourceIdentity;
            return route == RecoveryWorkflowRoute.Devices &&
                   cloudConnected &&
                   (name.StartsWith("INTUNE:", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("ENTRA:", StringComparison.OrdinalIgnoreCase));
        }

        return adConnected &&
               route is RecoveryWorkflowRoute.BitLocker or RecoveryWorkflowRoute.Laps;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        var original = new HomeSearchResult
        {
            ComputerName = "PC-TEST", BitLockerKeyCount = 1
        };
        if (!CanOpen(original, original, RecoveryWorkflowRoute.BitLocker, true, false, true) ||
            !CanOpen(original, original, RecoveryWorkflowRoute.Laps, true, false, true))
            failures.Add("AD guided recovery navigation was blocked.");

        var replacement = new HomeSearchResult
        {
            ComputerName = original.ComputerName, BitLockerKeyCount = 1
        };
        if (CanOpen(original, replacement, RecoveryWorkflowRoute.BitLocker, true, false, true) ||
            CanOpen(original, original, RecoveryWorkflowRoute.BitLocker, false, true, true) ||
            CanOpen(original, original, RecoveryWorkflowRoute.Devices, true, true, true) ||
            CanOpen(original, original, RecoveryWorkflowRoute.Laps, true, false, false))
            failures.Add("Guided recovery could navigate using a stale, denied or wrong-source AD selection.");

        original.IdentityAmbiguous = true;
        if (CanOpen(original, original, RecoveryWorkflowRoute.BitLocker, true, true, true))
            failures.Add("Guided recovery navigated an ambiguous AD computer.");
        original.IdentityAmbiguous = false;

        var cloud = new HomeSearchResult
        {
            ComputerName = "PC-CLOUD", IsCloudRecord = true,
            SourceIdentity = "INTUNE:MANAGED:real-managed-device-id"
        };
        if (!CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices, false, true, true) ||
            CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices, true, false, true) ||
            CanOpen(cloud, cloud, RecoveryWorkflowRoute.BitLocker, true, true, true) ||
            CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices, true, true, false))
            failures.Add("Guided recovery routed cloud metadata to wrong or denied workspace.");

        cloud.SourceIdentity = "UNRESOLVED:name-only";
        if (CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices, false, true, true))
            failures.Add("Guided recovery accepted an unverified cloud hostname.");
        cloud.SourceIdentity = "ENTRA:DEVICE:device-id";
        cloud.IdentityAmbiguous = true;
        if (CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices, true, true, true))
            failures.Add("Guided recovery accepted an ambiguous cloud device.");
        return failures;
    }
}
