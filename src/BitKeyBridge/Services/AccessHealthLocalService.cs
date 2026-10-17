using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>
/// Read-only Access Health v2 preflight for the current caller. Reports
/// Windows token/RBAC, strict broker policy, SCM and protected NTFS findings.
/// It never reads BitLocker or LAPS secrets, opens a recovery pipe, writes an
/// audit record or modifies Windows permissions.
/// </summary>
internal static class AccessHealthLocalService
{
    internal sealed record Finding(
        string Source, string Check, string State, string Detail);

    internal static Finding EvaluateProtectedPolicy(AppConfig config)
    {
        if (!config.RequireProtectedAuditForRecovery &&
            !config.AuditBrokerEnabled)
            return new Finding("Audit", "Protected broker policy", "Info",
                "Strict protected audit is not enabled. Per-user audit fallback " +
                "is not tamper-resistant; enable managed broker policy when required.");

        if (config.RequireProtectedAuditForRecovery != config.AuditBrokerEnabled)
            return new Finding("Audit", "Protected broker policy", "Failed",
                "Strict audit and AuditBrokerEnabled must be enabled together " +
                "for managed non-admin recovery.");

        if (!config.RbacEnabled || config.RbacRecoveryReaders.Count == 0)
            return new Finding("Audit", "Protected broker policy", "Failed",
                "Configure RBAC and at least one explicit RecoveryReaders SID/group. " +
                "Local Administrators membership is not a broker bypass.");

        return new Finding("Audit", "Protected broker policy", "OK",
            "Strict broker auditing and explicit RecoveryReaders are configured. " +
            "This does not by itself prove a durable broker acknowledgement.");
    }

    internal static IReadOnlyList<Finding> ReadOnly(AppConfig config)
    {
        var findings = new List<Finding>();
        var policy = EvaluateProtectedPolicy(config);
        findings.Add(policy);

        try
        {
            var auth = new AuthorizationService(config);
            var read = auth.Check(BitKeyBridgePermission.RecoveryRead);
            findings.Add(new Finding("RBAC", "RecoveryRead (current token)",
                !config.RbacEnabled ? "Info" : read.Allowed ? "OK" : "Failed",
                DiagnosticRedaction.Sanitize(read.Reason) +
                " AD/Entra delegated rights and protected ACK are separate checks."));

            var rotate = auth.Check(BitKeyBridgePermission.Rotate);
            findings.Add(new Finding("RBAC", "Rotate (optional permission)",
                !config.RbacEnabled ? "Info" : rotate.Allowed ? "OK" : "Warning",
                DiagnosticRedaction.Sanitize(rotate.Reason) +
                " Rotation access is optional and never granted by this test."));
        }
        catch (Exception ex)
        {
            findings.Add(new Finding("RBAC", "Current Windows token", "Failed",
                "Token membership cannot be established: " +
                DiagnosticRedaction.Sanitize(ex.Message)));
        }

        if (!config.RequireProtectedAuditForRecovery &&
            !config.AuditBrokerEnabled)
            return findings;

        // The broker's explicit allowlist is authoritative even if normal
        // RBAC enables the optional local-administrator bypass.
        if (config.RbacEnabled && config.RbacRecoveryReaders.Count > 0)
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                var readers = ProtectedAuditBrokerServer.ResolveExplicitReaders(
                    config.RbacRecoveryReaders);
                var enabled = identity.User is not null &&
                    readers.Any(sid => identity.User.Equals(sid) ||
                                       principal.IsInRole(sid));
                findings.Add(new Finding(
                    "Audit", "Broker RecoveryReaders (current token)",
                    enabled ? "OK" : "Failed",
                    enabled
                        ? "Current enabled Windows token matches the explicit broker allowlist."
                        : "Current enabled Windows token is not in the broker allowlist. " +
                          "Re-sign in after AD group membership changes."));
            }
            catch (Exception ex)
            {
                findings.Add(new Finding(
                    "Audit", "Broker RecoveryReaders (current token)", "Failed",
                    "Broker SID/group validation failed: " +
                    DiagnosticRedaction.Sanitize(ex.Message)));
            }
        }

        try
        {
            var service = WindowsServiceHost.GetInfo();
            var running = service.Installed &&
                string.Equals(service.State, "Running",
                    StringComparison.OrdinalIgnoreCase);
            findings.Add(new Finding("Audit", "Broker Windows Service",
                running ? "OK" : "Failed",
                running
                    ? "BitKeyBridge Windows Service is Running."
                    : service.Installed
                        ? "Windows Service state is " + service.State +
                          "; start or diagnose it from an elevated console."
                        : "BitKeyBridge Windows Service is not installed."));

            if (service.Installed)
            {
                var system = string.Equals(service.Identity, "LocalSystem",
                    StringComparison.OrdinalIgnoreCase);
                findings.Add(new Finding("Audit", "Broker service identity",
                    system ? "OK" : "Failed",
                    system
                        ? "Service runs as LocalSystem, required for the protected broker."
                        : "Configured service identity is " +
                          DiagnosticRedaction.Sanitize(service.Identity) +
                          ". This broker requires LocalSystem."));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new Finding("Audit", "Broker Windows Service", "Warning",
                "Service state/configuration could not be inspected by this caller: " +
                DiagnosticRedaction.Sanitize(ex.Message) +
                ". Read-only SCM access or elevated service diagnostics are needed."));
        }

        if (SecurityContext.IsAdministrator())
        {
            findings.Add(new Finding("Audit", "Journal owner / NTFS ACL",
                ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(
                    AppPaths.BrokerAuditLogFile) ? "OK" : "Failed",
                "Checks the protected current journal, companion files and ancestor " +
                "ACLs; does not display audit entries."));
        }
        else
        {
            findings.Add(new Finding("Audit", "Journal owner / NTFS ACL",
                "Info",
                "Not inspected: standard users must not receive journal read/write " +
                "permissions. Run the existing administrator audit preflight."));
        }

        findings.Add(new Finding("Audit", "Protected durable acknowledgement",
            "Info",
            "Not probed. A real ACK would write a synthetic journal event; " +
            "run --audit-broker-preflight --probe explicitly when authorized."));

        return findings;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        if (EvaluateProtectedPolicy(new AppConfig()).State != "Info")
            errors.Add("Standalone policy was incorrectly classified as strict audit failure.");

        if (EvaluateProtectedPolicy(new AppConfig
            {
                RequireProtectedAuditForRecovery = true,
                AuditBrokerEnabled = false
            }).State != "Failed")
            errors.Add("Inconsistent strict audit policy was not rejected.");

        if (EvaluateProtectedPolicy(new AppConfig
            {
                RequireProtectedAuditForRecovery = true,
                AuditBrokerEnabled = true,
                RbacEnabled = false
            }).State != "Failed")
            errors.Add("Protected broker was accepted without RBAC.");

        if (EvaluateProtectedPolicy(new AppConfig
            {
                RequireProtectedAuditForRecovery = true,
                AuditBrokerEnabled = true,
                RbacEnabled = true,
                RbacRecoveryReaders = ["S-1-5-32-544"]
            }).State != "OK")
            errors.Add("Valid broker policy was not recognized.");

        return errors;
    }
}
