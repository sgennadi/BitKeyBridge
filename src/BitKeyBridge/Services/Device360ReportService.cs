using System.Text;

namespace BitKeyBridge;

/// <summary>
/// Explicit operator export. Only allowlisted non-secret Device360Snapshot
/// and health fields; never serializes the original Graph/AD search row,
/// recovery IDs, clipboard contents, LAPS attributes or OAuth token.
/// </summary>
internal static class Device360ReportService
{
    internal static string Build(
        Device360Snapshot snapshot,
        Device360OperationalStatus status,
        Device360HardwareInventory? hardware = null,
        DateTime? createdUtc = null,
        string? hardwareFailureCategory = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(status);
        // Do not export stale WMI data even if a future caller accidentally
        // passes hardware along with a rejected/failed WMI outcome.
        (hardware, hardwareFailureCategory) =
            Device360HardwareService.NormalizeEvidence(
                hardware, hardwareFailureCategory);
        var checks = Device360HealthService.Evaluate(snapshot, status,
            createdUtc ?? DateTime.UtcNow, hardware, hardwareFailureCategory);
        var sb = new StringBuilder(2600);
        Line("BITKEYBRIDGE DEVICE 360 - NON-SECRET HELPDESK REPORT");
        Field("Created UTC", (createdUtc ?? DateTime.UtcNow).ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture));
        Line("Scope: metadata observed by existing source searches; this report performs no network queries.");
        Line("");

        Line("DEVICE IDENTITY");
        Field("Computer (display only)", snapshot.Computer);
        Field("Qualified source identity", snapshot.QualifiedIdentity);
        Field("Verified source identity", snapshot.IdentityVerified ? "Yes" : "No");
        Field("Ambiguous identity", snapshot.IdentityAmbiguous ? "Yes" : "No");
        Field("Identity warning", snapshot.IdentityWarning);
        Field("AD object GUID", snapshot.AdObjectGuid);
        Field("AD distinguished name", snapshot.AdDistinguishedName);
        Field("AD DNS hostname", snapshot.AdDnsHostName);
        Field("Entra device ID", snapshot.EntraDeviceId);
        Field("Intune managed device ID", snapshot.IntuneManagedDeviceId);
        Field("Cross-source match", "Not assumed from computer name, serial number or UPN");
        Line("");

        Line("HARDWARE AND MANAGEMENT");
        Field("Operating system", snapshot.OperatingSystem);
        Field("Manufacturer / model", snapshot.Hardware);
        Field("Serial number", snapshot.SerialNumber);
        Field("Assigned user", snapshot.AssignedUser);
        Field("Intune compliance", snapshot.Compliance);
        Field("Encryption reported", snapshot.Encrypted?.ToString() ?? "Not verified");
        Field("Last Intune sync UTC", Date(snapshot.LastSyncUtc));
        Field("AD last logon UTC", Date(snapshot.AdLastLogonUtc));
        Field("Remote serial number", hardware?.SerialNumber ?? string.Empty);
        Field("CPU model", hardware?.CpuName ?? string.Empty);
        Field("Logical processors", hardware is { CpuLogicalProcessors: > 0 }
            ? hardware.CpuLogicalProcessors.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : "Not verified");
        Field("Physical RAM", hardware is null ? "Not verified" :
            Device360HardwareService.FormatGiB(hardware.MemoryGiB));
        Field("Physical disk count", hardware is { PhysicalDiskCount: > 0 }
            ? hardware.PhysicalDiskCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : "Not verified");
        Field("Raw disk capacity", hardware is null ? "Not verified" :
            Device360HardwareService.FormatGiB(hardware.PhysicalStorageGiB));
        Field("Physical network adapters",
            hardware is { PhysicalNetworkAdapterCount: > 0 }
                ? hardware.PhysicalNetworkAdapterCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                : "Not verified");
        Field("Optional WMI inventory", hardware is null ? "Not queried" :
            hardware.ExtendedDiagnostic.Length == 0 ?
                "No optional WMI failures" :
                Device360HardwareService.SafeOptionalStatus(
                    hardware.ExtendedDiagnostic));
        Field("BIOS version", hardware?.BiosVersion ?? string.Empty);
        Field("BIOS manufacturer", hardware?.BiosManufacturer ?? string.Empty);
        Field("BIOS release date", hardware?.BiosReleaseDate ?? string.Empty);
        Field("Remote computer model", hardware is null ? string.Empty :
            (hardware.Manufacturer + " " + hardware.Model).Trim());
        Field("TPM specification", hardware?.TpmSpecification ?? string.Empty);
        Field("TPM enabled", hardware?.TpmEnabled ?? string.Empty);
        Field("TPM activated", hardware?.TpmActivated ?? string.Empty);
        Field("TPM owned", hardware?.TpmOwned ?? string.Empty);
        Field("TPM diagnostic", hardware?.TpmDiagnostic ?? string.Empty);
        Field("Hardware evidence source", hardware?.Source ?? "Not queried");
        Field("Remote hardware checked UTC", hardware is null
            ? "Not queried" : Date(hardware.CollectedUtc));
        Line("");

        Line("RECOVERY METADATA AND POLICY");
        Field("BitLocker metadata count", snapshot.BitLockerMetadataCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Field("BitLocker metadata incomplete", snapshot.BitLockerMetadataIncomplete ? "Yes" : "No");
        Field("LAPS metadata detected", snapshot.LapsMetadataDetected ? "Yes" : "Not verified");
        Field("LAPS metadata expired", snapshot.LapsMetadataExpired ? "Yes" : "Not reported");
        Field("AD search evidence", status.Ad.ToString());
        Field("AD failure reason", status.AdFailureKind.ToString());
        Field("Entra search evidence", status.Entra.ToString());
        Field("Entra failure reason", status.EntraFailureKind.ToString());
        Field("Intune search evidence", status.Intune.ToString());
        Field("Intune failure reason", status.IntuneFailureKind.ToString());
        Field("Metadata search partial", status.SearchPartial ? "Yes" : "No");
        Field("RBAC configured", status.RbacEnabledByConfiguration ? "Yes" : "No");
        Field("Recovery ticket required", status.TicketRequiredByConfiguration ? "Yes" : "No");
        Field("Protected audit required", status.ProtectedAuditRequiredByConfiguration ? "Yes" : "No");
        Field("Audit broker configured", status.AuditBrokerEnabledByConfiguration ? "Yes" : "No");
        Field("Effective recovery permissions", "Not checked (checked on Reveal/Copy only)");
        Line("");

        Line("DEVICE HEALTH / SMART DIAGNOSTICS");
        Line(Device360HealthService.Summary(checks));
        Line(Device360HealthAssessmentService.Summary(
            Device360HealthAssessmentService.Assess(checks), "en"));
        foreach (var check in checks)
            Line(Flat(check.Area) + ": " + check.State + " | " +
                Flat(check.Explanation));
        Line("");
        Line(Device360SmartDiagnosticsService.Format(
            Device360SmartDiagnosticsService.Evaluate(snapshot, status,
                hardware, hardwareFailureCategory,
                nowUtc: createdUtc), "en"));
        Line("");
        Line("Security: no recovery password, recovery ID, LAPS secret, token, " +
             "certificate private key or credential was retrieved or included.");
        return sb.ToString();

        void Line(string value) => sb.AppendLine(value);
        void Field(string name, string? value) =>
            Line(name + ": " + (string.IsNullOrWhiteSpace(value)
                ? "Not verified" : Flat(value)));
    }

    private static string Date(DateTime? utc) =>
        utc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'",
            System.Globalization.CultureInfo.InvariantCulture) ?? "Not verified";

    // Neutralize control chars and multiline injection in directory/Graph
    // display fields; never print unbounded remote exception messages.
    private static string Flat(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Not verified";
        var trimmed = raw.Trim();
        var clean = new string(trimmed
            .Take(320)
            .Select(c => char.IsControl(c) ? ' ' : c)
            .ToArray());
        return clean;
    }
}
