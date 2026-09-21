using System.Globalization;
using System.Text;

namespace BitKeyBridge;

public static class MetricsService
{
    public static string BuildPrometheus(
        HealthSnapshot snapshot)
    {
        var builder =
            new StringBuilder(4096);

        Gauge(
            builder,
            "bitkeybridge_up",
            "BitKeyBridge health endpoint process availability.",
            1);

        Gauge(
            builder,
            "bitkeybridge_ready",
            "1 when the current health snapshot has no Error state.",
            snapshot.OverallStatus == "Error"
                ? 0
                : 1);

        Gauge(
            builder,
            "bitkeybridge_health_errors",
            "Number of health errors.",
            snapshot.Errors.Count);

        Gauge(
            builder,
            "bitkeybridge_health_warnings",
            "Number of health warnings.",
            snapshot.Warnings.Count);

        Gauge(
            builder,
            "bitkeybridge_service_installed",
            "1 when the native Windows Service is installed.",
            snapshot.ServiceInstalled ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_service_running",
            "1 when the native Windows Service is running.",
            string.Equals(
                snapshot.ServiceState,
                "Running",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0);

        Gauge(
            builder,
            "bitkeybridge_export_last_success",
            "1 when the last export run succeeded, 0 when it failed, -1 when unknown.",
            TriState(snapshot.LastRunSuccess));

        Gauge(
            builder,
            "bitkeybridge_export_last_published",
            "1 when the last export was published.",
            snapshot.LastRunPublished ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_export_last_rows",
            "Rows in the last export run.",
            snapshot.LastRunRows);

        GaugeOptional(
            builder,
            "bitkeybridge_export_last_success_age_hours",
            "Age of the last successful published export in hours.",
            snapshot.LastSuccessfulExportAgeHours);

        Gauge(
            builder,
            "bitkeybridge_export_last_success_stale",
            "1 when the last successful export exceeds the configured staleness threshold.",
            snapshot.LastSuccessfulExportStale ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_replication_errors",
            "Replication errors reported by the last export run.",
            snapshot.ReplicationErrors);

        Gauge(
            builder,
            "bitkeybridge_replication_warnings",
            "Replication warnings reported by the last export run.",
            snapshot.ReplicationWarnings);

        Gauge(
            builder,
            "bitkeybridge_coverage_enabled",
            "1 when scheduled Coverage is enabled.",
            snapshot.ServiceCoverageEnabled ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_coverage_last_success",
            "1 when the last Coverage run succeeded, 0 when it failed, -1 when unknown.",
            TriState(snapshot.LastCoverageSuccess));

        GaugeOptional(
            builder,
            "bitkeybridge_coverage_age_hours",
            "Age of the last Coverage result in hours.",
            snapshot.LastCoverageAgeHours);

        Gauge(
            builder,
            "bitkeybridge_coverage_total_devices",
            "Devices in the last Coverage result.",
            snapshot.CoverageTotalDevices);

        Gauge(
            builder,
            "bitkeybridge_coverage_no_recovery_key",
            "Devices without recovery metadata.",
            snapshot.CoverageNoRecoveryKey);

        Gauge(
            builder,
            "bitkeybridge_coverage_intune_not_encrypted",
            "Intune devices reported not encrypted.",
            snapshot.CoverageIntuneNotEncrypted);

        Gauge(
            builder,
            "bitkeybridge_coverage_intune_stale",
            "Stale Intune devices.",
            snapshot.CoverageIntuneStale);

        Gauge(
            builder,
            "bitkeybridge_coverage_old_cloud_key",
            "Devices with cloud recovery metadata older than the configured threshold.",
            snapshot.CoverageOldCloudKey);

        Gauge(
            builder,
            "bitkeybridge_coverage_policy_compliant",
            "1 when the last Coverage policy evaluation is compliant.",
            snapshot.CoveragePolicyCompliant ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_coverage_policy_errors",
            "Coverage policy error violations.",
            snapshot.CoveragePolicyErrors);

        Gauge(
            builder,
            "bitkeybridge_coverage_policy_warnings",
            "Coverage policy warning violations.",
            snapshot.CoveragePolicyWarnings);

        Gauge(
            builder,
            "bitkeybridge_rbac_enabled",
            "1 when Windows RBAC is enabled.",
            snapshot.RbacEnabled ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_rbac_validation_errors",
            "Unresolved RBAC principal count.",
            snapshot.RbacValidationErrors);

        Gauge(
            builder,
            "bitkeybridge_audit_integrity_valid",
            "1 when the latest audit integrity state is Valid.",
            string.Equals(
                snapshot.AuditIntegrityStatus,
                "Valid",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0);

        Gauge(
            builder,
            "bitkeybridge_audit_integrity_stale",
            "1 when the cached audit integrity verification is stale.",
            string.Equals(
                snapshot.AuditIntegrityStatus,
                "Stale",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0);

        Gauge(
            builder,
            "bitkeybridge_audit_entries",
            "Audit entries seen by the latest integrity verification.",
            snapshot.AuditIntegrityEntries);

        Gauge(
            builder,
            "bitkeybridge_audit_chained_entries",
            "Hash-chained audit entries seen by the latest integrity verification.",
            snapshot.AuditIntegrityChainedEntries);

        Gauge(
            builder,
            "bitkeybridge_audit_signing_enabled",
            "1 when audit checkpoint signing is enabled.",
            snapshot.AuditSigningEnabled ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_audit_signing_signature_valid",
            "1 when the current signed checkpoint signature is valid.",
            snapshot.AuditSigningSignatureValid ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_audit_signing_current_head_signed",
            "1 when the current audit head is covered by the signed checkpoint.",
            snapshot.AuditSigningCurrentHeadSigned ? 1 : 0);

        GaugeOptional(
            builder,
            "bitkeybridge_audit_signing_certificate_days_remaining",
            "Days remaining on the configured audit-signing certificate.",
            snapshot.AuditSigningCertificateDaysRemaining);

        Gauge(
            builder,
            "bitkeybridge_audit_signing_transition_count",
            "Verified audit-signing trust-anchor transitions.",
            snapshot.AuditSigningTransitionCount);

        Gauge(
            builder,
            "bitkeybridge_remote_api_enabled",
            "1 when the TLS Remote API is enabled.",
            snapshot.RemoteApiEnabled ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_remote_api_management_enabled",
            "1 when Remote API management operations are globally enabled.",
            snapshot.RemoteApiManagementEnabled ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_remote_api_admin_token_configured",
            "1 when the backward-compatible Admin bearer token is configured.",
            snapshot.RemoteApiAdminTokenConfigured ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_remote_api_read_token_configured",
            "1 when a read-scope bearer token is configured.",
            snapshot.RemoteApiReadTokenConfigured ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_remote_api_coverage_run_token_configured",
            "1 when a coverage-run bearer token is configured.",
            snapshot.RemoteApiCoverageRunTokenConfigured ? 1 : 0);

        Gauge(
            builder,
            "bitkeybridge_remote_api_export_token_configured",
            "1 when an export-scope bearer token is configured.",
            snapshot.RemoteApiExportTokenConfigured ? 1 : 0);

        GaugeOptional(
            builder,
            "bitkeybridge_remote_api_certificate_days_remaining",
            "Days remaining on the Remote API TLS certificate.",
            snapshot.RemoteApiCertificateDaysRemaining);

        GaugeOptional(
            builder,
            "bitkeybridge_entra_certificate_days_remaining",
            "Days remaining on the configured Entra certificate.",
            snapshot.CertificateDaysRemaining);

        return builder.ToString();
    }

    private static int TriState(
        bool? value) =>
        value is null
            ? -1
            : value.Value
                ? 1
                : 0;

    private static void GaugeOptional(
        StringBuilder builder,
        string name,
        string help,
        double? value)
    {
        if (value is null ||
            double.IsNaN(value.Value) ||
            double.IsInfinity(value.Value))
        {
            return;
        }

        Gauge(
            builder,
            name,
            help,
            value.Value);
    }

    private static void Gauge(
        StringBuilder builder,
        string name,
        string help,
        double value)
    {
        builder.Append("# HELP ");
        builder.Append(name);
        builder.Append(' ');
        builder.AppendLine(
            SanitizeHelp(help));

        builder.Append("# TYPE ");
        builder.Append(name);
        builder.AppendLine(" gauge");

        builder.Append(name);
        builder.Append(' ');
        builder.AppendLine(
            value.ToString(
                "0.################",
                CultureInfo.InvariantCulture));
    }

    private static string SanitizeHelp(
        string value) =>
        (value ?? string.Empty)
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "\n",
                " ",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                " ",
                StringComparison.Ordinal);
}
