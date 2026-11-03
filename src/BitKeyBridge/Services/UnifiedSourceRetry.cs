namespace BitKeyBridge;

/// <summary>
/// The only metadata source selected by an explicit technician retry.
/// No action here reads passwords, refreshes tokens or changes policy.
/// </summary>
public enum UnifiedMetadataSource { Ad, Entra, Intune }

internal sealed record UnifiedSourceRetryState(
    string Query,
    int MaximumItems,
    bool CloudAttempted,
    MetadataLookupResult<AdComputerInfo>? Ad,
    MetadataLookupResult<ManagedDeviceInfo> Intune,
    MetadataLookupResult<CloudRecoveryMetadata> Entra);

internal static class UnifiedSourceRetry
{
    internal static bool CanRetry(UnifiedDeviceSearchReport? report,
        UnifiedMetadataSource source)
    {
        if (report?.SourceState is not { } state)
            return false;
        var (attempted, succeeded, kind) = source switch
        {
            UnifiedMetadataSource.Ad =>
                (report.AdAttempted, report.AdSucceeded, report.AdFailureKind),
            UnifiedMetadataSource.Entra =>
                (state.CloudAttempted && report.EntraAttempted,
                    report.EntraSucceeded, report.EntraFailureKind),
            UnifiedMetadataSource.Intune =>
                (state.CloudAttempted && report.IntuneAttempted,
                    report.IntuneSucceeded, report.IntuneFailureKind),
            _ => (false, true, MetadataFailureKind.None)
        };
        // Denied/401/no-record are NOT transient. Unknown failures can be
        // retried once per explicit operator click, never automatically.
        return attempted && !succeeded && kind is
            (MetadataFailureKind.SourceOffline or MetadataFailureKind.Timeout or
             MetadataFailureKind.RateLimited or MetadataFailureKind.Unknown);
    }

    // Pure reassembly: a healthy source is never called or overwritten by a
    // retry. Preserve earlier enrichment only if immutable source IDs agree.
    internal static UnifiedDeviceSearchReport Apply(
        UnifiedDeviceSearchReport previous, UnifiedMetadataSource source,
        MetadataLookupResult<AdComputerInfo>? ad = null,
        MetadataLookupResult<ManagedDeviceInfo>? intune = null,
        MetadataLookupResult<CloudRecoveryMetadata>? entra = null)
    {
        if (!CanRetry(previous, source) ||
            previous.SourceState is not { } state)
            throw new InvalidOperationException("Source retry not authorized.");
        if ((source == UnifiedMetadataSource.Ad && ad is null) ||
            (source == UnifiedMetadataSource.Entra && entra is null) ||
            (source == UnifiedMetadataSource.Intune && intune is null))
            throw new ArgumentException("Missing response for selected source.");

        var nextAd = source == UnifiedMetadataSource.Ad ? ad : state.Ad;
        var nextIntune = source == UnifiedMetadataSource.Intune ? intune! : state.Intune;
        var nextEntra = source == UnifiedMetadataSource.Entra ? entra! : state.Entra;
        var next = UnifiedDeviceSearchReport.Combine(
            nextAd, nextIntune, nextEntra, state.MaximumItems, state.CloudAttempted);
        next.SourceState = new UnifiedSourceRetryState(
            state.Query, state.MaximumItems, state.CloudAttempted,
            nextAd, nextIntune, nextEntra);

        // An earlier Entra enrichment may be retained ONLY for a single
        // unchanged, unambiguous immutable device ID and Entra ID. Never
        // correlate by name/serial/UPN. Do not overwrite a fresh Entra retry.
        if (source != UnifiedMetadataSource.Entra)
        {
            next.RecoveryEnrichmentFailures = previous.RecoveryEnrichmentFailures;
            foreach (var row in next.Rows)
            {
                if (!row.FoundInEntra || string.IsNullOrWhiteSpace(row.EntraDeviceId) ||
                    row.IdentityStatus is "Name collision" or "Ambiguous device ID")
                    continue;
                var exact = previous.Rows.Where(old =>
                    old.FoundInEntra &&
                    old.IdentityStatus is not ("Name collision" or "Ambiguous device ID") &&
                    string.Equals(old.IdentityKey, row.IdentityKey,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(old.EntraDeviceId, row.EntraDeviceId,
                        StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
                if (exact.Count != 1)
                    continue;
                foreach (var id in exact[0].RecoveryIds)
                    UnifiedDeviceMerge.AddRecoveryId(row, id);
                row.RecoveryMetadataIncomplete |= exact[0].RecoveryMetadataIncomplete;
            }
        }
        // A targeted retry never calls per-managed-device Entra enrichment.
        // Even after a successful bulk Entra lookup, zero recovery IDs on a
        // managed device cannot prove that no backup exists. Do not present
        // this gap as a complete inventory or a missing recovery secret.
        foreach (var row in next.Rows.Where(row =>
            row.FoundInIntune && row.RecoveryIds.Count == 0))
            row.RecoveryMetadataIncomplete = true;
        return next;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        var downAd = new MetadataLookupResult<AdComputerInfo>(
            [], "Synthetic offline", MetadataFailureKind.SourceOffline);
        var healthyIntune = new MetadataLookupResult<ManagedDeviceInfo>(
            [new ManagedDeviceInfo
            {
                DeviceName = "UI-EXACT-DEVICE",
                ManagedDeviceId = "ui-managed-device",
                EntraDeviceId = "00000000-0000-0000-0000-000000000090",
                Model = "Synthetic model"
            }], null);
        var emptyEntra = new MetadataLookupResult<CloudRecoveryMetadata>([], null);
        var first = UnifiedDeviceSearchReport.Combine(
            downAd, healthyIntune, emptyEntra);
        first.SourceState = new UnifiedSourceRetryState(
            "UI", 250, true, downAd, healthyIntune, emptyEntra);

        if (!CanRetry(first, UnifiedMetadataSource.Ad) ||
            CanRetry(first, UnifiedMetadataSource.Intune) ||
            CanRetry(first, UnifiedMetadataSource.Entra))
            failures.Add("Targeted retry offered a healthy source.");

        var recoveredAd = new MetadataLookupResult<AdComputerInfo>(
            [new AdComputerInfo
            {
                ComputerName = "UI-AD-ONLY",
                DistinguishedName = "CN=UI-AD-ONLY,DC=example,DC=com",
                ObjectGuid = "00000000-0000-0000-0000-000000000091"
            }], null);
        var restored = Apply(first, UnifiedMetadataSource.Ad, ad: recoveredAd);
        if (restored.IsPartial || !restored.AdSucceeded ||
            restored.Rows.Count != 2 ||
            restored.Rows.Count(row => row.FoundInIntune &&
                row.Model == "Synthetic model") != 1 ||
            !ReferenceEquals(restored.SourceState!.Intune, healthyIntune) ||
            !ReferenceEquals(restored.SourceState.Entra, emptyEntra) ||
            restored.Rows.Any(row => row.FoundInIntune &&
                row.RecoveryIds.Count == 0 &&
                !row.RecoveryMetadataIncomplete))
            failures.Add("AD-only retry dropped source evidence or implied a missing backup.");

        var throttled = new MetadataLookupResult<CloudRecoveryMetadata>(
            [], "Synthetic 429", MetadataFailureKind.RateLimited);
        var cloudFailure = UnifiedDeviceSearchReport.Combine(
            recoveredAd, healthyIntune, throttled);
        cloudFailure.SourceState = restored.SourceState! with { Entra = throttled };
        if (!CanRetry(cloudFailure, UnifiedMetadataSource.Entra) ||
            CanRetry(cloudFailure, UnifiedMetadataSource.Intune))
            failures.Add("Entra 429 selective retry was not available.");

        var forbidden = new MetadataLookupResult<ManagedDeviceInfo>(
            [], "Synthetic 403", MetadataFailureKind.AccessDenied);
        var denied = UnifiedDeviceSearchReport.Combine(
            recoveredAd, forbidden, emptyEntra);
        denied.SourceState = restored.SourceState! with { Intune = forbidden };
        if (CanRetry(denied, UnifiedMetadataSource.Intune))
            failures.Add("Denied Intune metadata could be retried without consent.");

        var collision = new MetadataLookupResult<AdComputerInfo>(
            [new AdComputerInfo
            {
                ComputerName = "UI-EXACT-DEVICE",
                DistinguishedName = "CN=UI-EXACT-DEVICE,DC=example,DC=com",
                ObjectGuid = "00000000-0000-0000-0000-000000000092"
            }], null);
        var colliding = Apply(first, UnifiedMetadataSource.Ad, ad: collision);
        if (colliding.Rows.Count != 2 || colliding.Rows.Any(row =>
            row.IdentityStatus != "Name collision"))
            failures.Add("Retry merged different AD/Intune identities by name.");

        var allFailed = UnifiedDeviceSearchReport.Combine(
            downAd, forbidden, throttled);
        allFailed.SourceState = new UnifiedSourceRetryState(
            "UI", 250, true, downAd, forbidden, throttled);
        if (allFailed.Rows.Count != 0 || !allFailed.IsPartial ||
            !CanRetry(allFailed, UnifiedMetadataSource.Ad) ||
            CanRetry(allFailed, UnifiedMetadataSource.Intune))
            failures.Add("All-failed source report suppressed a safe retry.");

        var adOnly = UnifiedDeviceSearchReport.Combine(
            downAd, new MetadataLookupResult<ManagedDeviceInfo>([], null),
            emptyEntra, cloudAttempted: false);
        adOnly.SourceState = new UnifiedSourceRetryState("UI", 250, false,
            downAd, new MetadataLookupResult<ManagedDeviceInfo>([], null),
            emptyEntra);
        if (!adOnly.IsPartial || !CanRetry(adOnly, UnifiedMetadataSource.Ad) ||
            CanRetry(adOnly, UnifiedMetadataSource.Entra))
            failures.Add("AD-only search falsely marked disconnected Cloud as failed.");

        return failures;
    }
}
