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
        DateTime? nowUtc = null)
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

        AddSource("Active Directory", sources.Ad);
        AddSource("Entra ID", sources.Entra);
        AddSource("Intune", sources.Intune);

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
                        : "Intune state '" + Short(compliant) + "' is not sufficient to confirm compliance."));

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

        return items;

        void AddSource(string name, Device360SourceEvidence state)
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
                        "The source lookup failed. Check network, permissions and diagnostics; absence is NOT established.",
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

    private static string Short(string value) =>
        value.Length <= 80 ? value : value[..80];
}
