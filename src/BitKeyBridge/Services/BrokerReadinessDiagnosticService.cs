using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Secret-free, local acceptance preflight. This never asks for recovery data,
/// modifies RBAC/NTFS, contacts AD/Graph, or elevates the caller. An optional
/// explicit --probe writes one synthetic broker health event.
/// </summary>
internal static class BrokerReadinessDiagnosticService
{
    private sealed record Finding(string Check, string Status, string Detail);

    private sealed class Report
    {
        public string Component { get; init; } = "BitKeyBridge protected audit broker";
        public string Version { get; init; } = UpdateService.GetCurrentVersion().ToString();
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public string Computer { get; init; } = Environment.MachineName;
        public string Caller { get; init; } = AuthorizationService.CurrentIdentityName();
        public string CallerSid { get; init; } = AuthorizationService.CurrentIdentitySid();
        public bool IsLocalAdministrator { get; init; }
        public bool ProbeRequested { get; init; }
        public string Overall { get; set; } = "PreflightOnly";
        public List<Finding> Checks { get; } = [];
        public string ImportantLimit { get; init; } =
            "Preflight does not prove AD/Entra/LAPS/BitLocker delegated permissions, " +
            "JIT approval, signed deployment or remote SIEM delivery.";
    }

    internal static int Run(AppConfig config, bool probe)
    {
        var isAdmin = SecurityContext.IsAdministrator();
        var report = new Report
        {
            IsLocalAdministrator = isAdmin,
            ProbeRequested = probe
        };

        static void Add(Report report, string check, string status, string detail) =>
            report.Checks.Add(new Finding(check, status, detail));

        if (!OperatingSystem.IsWindows())
        {
            Add(report, "OperatingSystem", "Fail", "Windows is required.");
        }
        else
        {
            var join = GetLocalJoinStatus();
            Add(report, "MachineJoin", "Info",
                join + ". Standalone machines are supported, but domain " +
                "delegation must be tested on a domain-connected workstation.");
        }

        var configReady = config.AuditBrokerEnabled &&
            config.RequireProtectedAuditForRecovery &&
            config.RbacEnabled &&
            config.RbacRecoveryReaders.Count > 0;
        Add(report, "MachineAuditPolicy", configReady ? "Pass" : "Fail",
            configReady
                ? "Strict protected broker and explicit RecoveryReaders are enabled."
                : "Configure strict audit, broker and a narrow RBAC RecoveryReaders group.");

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var readers = ProtectedAuditBrokerServer.ResolveExplicitReaders(
                config.RbacRecoveryReaders);
            var allowed = identity.User is not null &&
                readers.Any(sid =>
                    identity.User.Equals(sid) || principal.IsInRole(sid));
            Add(report, "CallerEnabledRecoveryReader", allowed ? "Pass" : "Fail",
                allowed
                    ? "Current enabled Windows token matches an explicit reader."
                    : "Current token is not in the broker allowlist. Re-sign in " +
                      "after domain group changes; administrator bypass is not allowed.");
        }
        catch (Exception ex)
        {
            Add(report, "CallerEnabledRecoveryReader", "Fail",
                "Reader SID resolution/token check failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        try
        {
            var service = WindowsServiceHost.GetInfo();
            Add(report, "ServiceRunning",
                service.Installed && service.State.Equals("Running",
                    StringComparison.OrdinalIgnoreCase) ? "Pass" : "Fail",
                service.Installed
                    ? "Service state: " + service.State
                    : "Install the test service using an elevated administrator account.");
            if (service.Installed)
            {
                Add(report, "ServiceIdentity",
                    service.Identity.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)
                        ? "Pass" : "Fail",
                    "Service identity: " + service.Identity +
                    "; this preview requires LocalSystem.");

                if (!File.Exists(AppPaths.ServiceExecutable))
                {
                    Add(report, "InstalledExecutable", "Fail",
                        "Installed service executable is missing.");
                }
                else
                {
                    try
                    {
                        var signature = AuthenticodeVerificationService.Verify(
                            AppPaths.ServiceExecutable);
                        Add(report, "InstalledExecutableAuthenticode",
                            signature.Trusted ? "Pass" : "Warn",
                            signature.Trusted
                                ? "Windows trusts the installed Authenticode signature."
                                : "Unsigned or untrusted test binary. Do not deploy " +
                                  "unattended; verify publisher and trust before production.");
                    }
                    catch (Exception ex)
                    {
                        Add(report, "InstalledExecutableAuthenticode", "Warn",
                            "Signing status unavailable: " +
                            DiagnosticRedaction.Sanitize(ex.Message));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Add(report, "ServiceRunning", "Fail",
                "Service state unavailable: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        if (isAdmin)
        {
            Add(report, "MachinePolicyNtfs", 
                ProtectedAuditStoragePolicy.IsTrusted(AppPaths.AppSettingsFile)
                    ? "Pass" : "Fail",
                "Administrator-only owner and NTFS write ACL validation.");
            Add(report, "ProtectedJournalNtfs",
                ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(
                    AppPaths.BrokerAuditLogFile) ? "Pass" : "Fail",
                "Validates journal, rotated archive, lock file and ancestors.");
        }
        else
        {
            Add(report, "ProtectedNtfs", "NotTested",
                "Run --audit-broker-verify in an elevated console. " +
                "No journal read/write permissions are granted to this caller.");
        }

        if (probe)
        {
            var committed = ProtectedAuditBrokerClient.TryProbe();
            Add(report, "AuthenticatedDurableAuditProbe",
                committed ? "Pass" : "Fail",
                committed
                    ? "Current Windows token received a committed SYSTEM audit ACK."
                    : "No protected ACK: check current token, service and policy. " +
                      "Recovery stays blocked; no user-audit fallback.");
        }
        else
        {
            Add(report, "AuthenticatedDurableAuditProbe", "NotTested",
                "Use --audit-broker-preflight --probe to explicitly create " +
                "one synthetic health event and test the protected pipe.");
        }

        Add(report, "AD_EntraRecoveryAuthorization", "NotTested",
            "Requires a real delegated non-admin user and a managed test computer.");
        Add(report, "Approval_Jit_Siem", "NotTested",
            "Verify JIT approvals and SIEM receipt separately in your environment.");

        report.Overall = report.Checks.Any(x => x.Status == "Fail")
            ? "NeedsAttention"
            : "PreflightPassed_ProductionAcceptancePending";

        Console.WriteLine(JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        return report.Overall == "NeedsAttention" ? 3 : 0;
    }

    private static string GetLocalJoinStatus()
    {
        IntPtr namePointer = IntPtr.Zero;
        try
        {
            var error = NetGetJoinInformation(null, out namePointer, out var joinStatus);
            if (error != 0)
                return "Unknown (NetGetJoinInformation error " + error + ")";
            var name = Marshal.PtrToStringUni(namePointer) ?? "(not reported)";
            return joinStatus switch
            {
                3 => "Domain joined: " + name,
                2 => "Workgroup: " + name,
                1 => "Not joined",
                _ => "Unknown"
            };
        }
        catch (Exception ex)
        {
            return "Unavailable: " + DiagnosticRedaction.Sanitize(ex.Message);
        }
        finally
        {
            if (namePointer != IntPtr.Zero)
                NetApiBufferFree(namePointer);
        }
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetGetJoinInformation(
        string? server, out IntPtr nameBuffer, out int joinStatus);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
