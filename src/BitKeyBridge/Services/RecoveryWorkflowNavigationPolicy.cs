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
    /// <summary>
    /// "INTUNE:UNRESOLVED" and "ENTRA:KEY" are useful search metadata but
    /// do not identify a single physical Entra/Intune device. They must
    /// never be advertised as a verified device in a recovery shortcut.
    /// </summary>
    internal static bool HasAuthoritativeCloudDeviceId(HomeSearchResult? row)
    {
        if (row is null || !row.IsCloudRecord || row.IdentityAmbiguous)
            return false;
        var identity = row.SourceIdentity?.Trim() ?? string.Empty;
        return HasId(identity, "INTUNE:MANAGED:") ||
               HasId(identity, "INTUNE:DEVICE:") ||
               HasId(identity, "ENTRA:DEVICE:");

        static bool HasId(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(value[prefix.Length..]);
    }

    /// <summary>
    /// A hostname-only AD search hit does not identify an exact directory
    /// object. Require a source-returned DN or parseable nonempty object GUID
    /// before claiming this guide can route to the selected AD computer.
    /// This checks metadata identity only, never secret-read delegation.
    /// </summary>
    internal static bool HasAuthoritativeAdComputerId(HomeSearchResult? row)
    {
        if (row is null || row.IsCloudRecord || row.IdentityAmbiguous)
            return false;

        if (!string.IsNullOrWhiteSpace(row.AdComputerDistinguishedName))
            return true;

        return Guid.TryParse(row.ComputerId, out var id) && id != Guid.Empty;
    }

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
            return route == RecoveryWorkflowRoute.Devices &&
                   cloudConnected &&
                   HasAuthoritativeCloudDeviceId(initial);
        }

        return adConnected &&
               HasAuthoritativeAdComputerId(initial) &&
               route is RecoveryWorkflowRoute.BitLocker or RecoveryWorkflowRoute.Laps;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        var original = new HomeSearchResult
        {
            ComputerName = "PC-TEST", BitLockerKeyCount = 1,
            AdComputerDistinguishedName = "CN=PC-TEST,OU=Clients,DC=example,DC=com"
        };
        if (!CanOpen(original, original, RecoveryWorkflowRoute.BitLocker, true, false, true) ||
            !CanOpen(original, original, RecoveryWorkflowRoute.Laps, true, false, true))
            failures.Add("AD guided recovery navigation was blocked.");

        original.AdComputerDistinguishedName = string.Empty;
        if (HasAuthoritativeAdComputerId(original) ||
            CanOpen(original, original, RecoveryWorkflowRoute.BitLocker,
                true, false, true))
            failures.Add("Hostname-only AD metadata passed guided recovery navigation.");

        original.ComputerId = Guid.NewGuid().ToString();
        if (!HasAuthoritativeAdComputerId(original) ||
            !CanOpen(original, original, RecoveryWorkflowRoute.Laps,
                true, false, true))
            failures.Add("An exact AD object GUID could not route to metadata.");
        original.ComputerId = "not-a-guid";
        if (HasAuthoritativeAdComputerId(original))
            failures.Add("Invalid AD object GUID was treated as authoritative.");
        original.ComputerId = string.Empty;
        original.AdComputerDistinguishedName =
            "CN=PC-TEST,OU=Clients,DC=example,DC=com";

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
        foreach (var notADevice in new[]
        {
            "INTUNE:UNRESOLVED:1",
            "ENTRA:UNRESOLVED:2",
            "ENTRA:KEY:recovery-id",
            "INTUNE:MANAGED:",
            "ENTRA:DEVICE:"
        })
        {
            cloud.SourceIdentity = notADevice;
            if (HasAuthoritativeCloudDeviceId(cloud) ||
                CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices,
                    false, true, true))
                failures.Add("Cloud identity without a unique device ID was accepted: " +
                    notADevice);
        }
        cloud.SourceIdentity = "ENTRA:DEVICE:device-id";
        cloud.IdentityAmbiguous = true;
        if (CanOpen(cloud, cloud, RecoveryWorkflowRoute.Devices, true, true, true))
            failures.Add("Guided recovery accepted an ambiguous cloud device.");
        return failures;
    }
}
