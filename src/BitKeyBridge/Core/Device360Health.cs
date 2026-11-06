namespace BitKeyBridge;

/// <summary>
/// Read-only health observations, not proof that recovery will succeed.
/// Missing data is NEVER treated as healthy or as evidence of a missing key.
/// </summary>
internal enum Device360CheckState
{
    Observed,
    Attention,
    NotVerified
}

internal sealed record Device360Check(
    string Area, Device360CheckState State, string Explanation);

internal static class Device360HealthService
{
    internal static IReadOnlyList<Device360Check> Evaluate(
        Device360Snapshot device, Device360OperationalStatus sources,
        DateTime? nowUtc = null,
        Device360HardwareInventory? hardware = null,
        string? hardwareFailureCategory = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(sources);
        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();
        var items = new List<Device360Check>();

        items.Add(new Device360Check("Identity",
            device.IdentityAmbiguous ? Device360CheckState.Attention :
            device.IdentityVerified ? Device360CheckState.Observed :
                Device360CheckState.NotVerified,
            device.IdentityAmbiguous
                ? "Conflicting or duplicate identities. Do not join by name; check exact AD/Entra/Intune identifiers."
                : device.IdentityVerified
                    ? "A source-qualified identifier was observed. Cross-source equivalence is not implied."
                    : "No authoritative device identity was verified. Recovery navigation remains restricted."));

        AddSource("Active Directory", sources.Ad, sources.AdFailureKind);
        AddSource("Entra ID", sources.Entra, sources.EntraFailureKind);
        AddSource("Intune", sources.Intune, sources.IntuneFailureKind);

        items.Add(new Device360Check("BitLocker metadata",
            device.BitLockerMetadataCount > 0
                ? device.BitLockerMetadataIncomplete
                    ? Device360CheckState.NotVerified
                    : Device360CheckState.Observed
                : Device360CheckState.NotVerified,
            device.BitLockerMetadataCount > 0
                ? device.BitLockerMetadataIncomplete
                    ? "Some recovery metadata was returned, but the source lookup was incomplete. Key accessibility is unverified."
                    : "Recovery metadata was found. Protection and access to the secret were not tested."
                : "No confirmed recovery metadata from this search. This does NOT prove no backup exists."));

        items.Add(new Device360Check("LAPS metadata",
            device.LapsMetadataExpired ? Device360CheckState.Attention :
                device.LapsMetadataDetected ? Device360CheckState.Observed :
                Device360CheckState.NotVerified,
            device.LapsMetadataExpired
                ? "Observed LAPS expiration is in the past. Inspect the authoritative LAPS source."
                : device.LapsMetadataDetected
                    ? "A LAPS backup candidate was detected; the password was not read."
                    : "LAPS was not confirmed by this search. Use LAPS diagnostics to verify."));

        var compliant = device.Compliance.Trim();
        var complianceState = compliant.Equals("compliant",
                StringComparison.OrdinalIgnoreCase)
            ? Device360CheckState.Observed
            : compliant.Equals("noncompliant",
                StringComparison.OrdinalIgnoreCase)
                ? Device360CheckState.Attention
                : Device360CheckState.NotVerified;
        items.Add(new Device360Check("Intune compliance", complianceState,
            complianceState == Device360CheckState.Observed
                ? "Intune reports compliant; the policy evaluation was not re-run."
                : complianceState == Device360CheckState.Attention
                    ? "Intune reports noncompliant. Inspect the Intune compliance policy."
                    : string.IsNullOrWhiteSpace(compliant)
                        ? "No Intune compliance value was returned or matched by exact identity."
                        : "Intune returned an unrecognized compliance state. Do not infer compliance."));

        items.Add(new Device360Check("Disk encryption",
            device.Encrypted == true ? Device360CheckState.Observed :
                device.Encrypted == false ? Device360CheckState.Attention :
                    Device360CheckState.NotVerified,
            device.Encrypted == true
                ? "The inventory source reports encrypted. Local BitLocker protection is not tested."
                : device.Encrypted == false
                    ? "Intune reports not encrypted. Validate the actual volume state."
                    : "Encryption status was not returned by a verified inventory source."));

        var syncUtc = device.LastSyncUtc?.ToUniversalTime();
        items.Add(new Device360Check("Intune last sync",
            syncUtc is null || syncUtc > now.AddMinutes(5)
                ? Device360CheckState.NotVerified
                : syncUtc < now.AddDays(-14)
                    ? Device360CheckState.Attention
                    : Device360CheckState.Observed,
            syncUtc is null
                ? "No last-sync timestamp available from verified Intune inventory."
                : syncUtc > now.AddMinutes(5)
                    ? "Intune sync timestamp is in the future; verify source time."
                    : syncUtc < now.AddDays(-14)
                        ? "Intune last sync is older than 14 days; device might be inactive."
                        : "Intune reported a sync within the last 14 days."));

        if (sources.SearchPartial)
            items.Add(new Device360Check("Search completeness",
                Device360CheckState.Attention,
                "At least one metadata source or enrichment query was incomplete. Treat missing fields as unverified."));

        // WMI checks exist only after an explicit technician action.
        // Unknown providers never count as healthy or authorize recovery.
        if (hardware is not null)
        {
            var coreObserved = !string.IsNullOrWhiteSpace(hardware.Manufacturer) ||
                !string.IsNullOrWhiteSpace(hardware.Model) ||
                !string.IsNullOrWhiteSpace(hardware.BiosVersion);
            items.Add(new Device360Check("Remote WMI inventory",
                coreObserved ? Device360CheckState.Observed :
                    Device360CheckState.NotVerified,
                coreObserved
                    ? "Read-only WMI returned computer or BIOS metadata. It does not verify recovery access."
                    : "Remote WMI returned no confirmed core computer or BIOS metadata."));

            var tpmState = hardware.TpmEnabled switch
            {
                "Yes" => Device360CheckState.Observed,
                "No" => Device360CheckState.Attention,
                _ => Device360CheckState.NotVerified
            };
            items.Add(new Device360Check("Remote TPM status", tpmState,
                tpmState == Device360CheckState.Observed
                    ? "WMI reported TPM enabled at collection time; no TPM keys or attestation were retrieved."
                    : tpmState == Device360CheckState.Attention
                        ? "WMI reported TPM disabled. Verify the actual endpoint state using approved tools."
                        : "TPM status not verified; an unavailable provider does not prove TPM is absent."));

            var optionalComplete = hardware.ExtendedDiagnostic.Length == 0 &&
                !string.IsNullOrWhiteSpace(hardware.CpuName) &&
                hardware.CpuLogicalProcessors > 0 &&
                hardware.MemoryGiB > 0m &&
                hardware.PhysicalDiskCount > 0 &&
                hardware.PhysicalStorageGiB > 0m &&
                hardware.PhysicalNetworkAdapterCount > 0;
            // An explicit provider ACCESS_DENIED/TIMEOUT/etc is actionable,
            // while a no-record or missing metric is merely unverified.
            // This matches Smart Diagnostics priority for the same evidence.
            var optionalActionNeeded =
                Device360HardwareService.OptionalFailures(hardware.ExtendedDiagnostic)
                    .Any(failure => failure.Category != "NoRecord");
            items.Add(new Device360Check("Optional WMI inventory",
                optionalComplete ? Device360CheckState.Observed :
                optionalActionNeeded ? Device360CheckState.Attention :
                    Device360CheckState.NotVerified,
                optionalComplete
                    ? "CPU, memory, disk aggregates and physical adapter counts were observed by read-only WMI."
                    : optionalActionNeeded
                        ? "An optional WMI provider returned a classified failure. Inspect the relevant class and existing read permissions."
                        : "At least one optional hardware class was not verified. Confirmed inventory remains available."));
        }
        else if (hardwareFailureCategory is "AccessDenied" or "Timeout" or
                 "DCOM/RPCUnavailable" or "WmiError" or "Unavailable")
        {
            // Only locally classified reason codes are included, never the
            // raw exception message or remote provider payload.
            items.Add(new Device360Check("Remote WMI inventory",
                Device360CheckState.Attention,
                "The explicit WMI inventory request failed (" +
                hardwareFailureCategory +
                "). Check existing connectivity and WMI read permissions."));
        }

        return items;

        void AddSource(string name, Device360SourceEvidence state,
            MetadataFailureKind reason)
        {
            items.Add(new Device360Check(name,
                state == Device360SourceEvidence.FoundInSearch
                    ? Device360CheckState.Observed
                    : state == Device360SourceEvidence.LookupFailed
                        ? Device360CheckState.Attention
                        : Device360CheckState.NotVerified,
                state switch
                {
                    Device360SourceEvidence.FoundInSearch =>
                        "Record returned by this source during the selected search; no fresh probe was performed.",
                    Device360SourceEvidence.LookupFailed =>
                        MetadataFailureGuidance.TechnicianSummary(
                            reason == MetadataFailureKind.None
                                ? MetadataFailureKind.Unknown : reason,
                            name.StartsWith("Entra", StringComparison.Ordinal)
                                ? "Entra" :
                            name == "Active Directory" ? "AD" : name) +
                        " A failed lookup does NOT establish the absence of a backup.",
                    Device360SourceEvidence.SessionPresentUnverified =>
                        "Session exists but this exact device was not verified in this source.",
                    _ =>
                        "No authenticated source data was available for this identity."
                }));
        }
    }

    internal static string Summary(IReadOnlyList<Device360Check> checks)
    {
        var attention = checks.Count(x => x.State == Device360CheckState.Attention);
        var unknown = checks.Count(x => x.State == Device360CheckState.NotVerified);
        var observed = checks.Count(x => x.State == Device360CheckState.Observed);
        return attention > 0
            ? $"Review needed: {attention} attention, {unknown} unverified, {observed} observed"
            : unknown > 0
                ? $"Partial picture: {unknown} unverified, {observed} observed"
                : $"{observed} metadata observations; recovery rights not tested";
    }

}
