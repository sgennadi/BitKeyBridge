using System.Runtime.InteropServices;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>Metadata-only endpoint inventory. No outbound telemetry or secrets.</summary>
internal static class FleetInventoryService
{
    internal const int SchemaVersion = 1;
    internal sealed record FleetReport(
        int SchemaVersion, string Machine, DateTime CollectedUtc,
        string Version, string RuntimeId, string OsArchitecture,
        string ServiceState, bool ServiceInstalled,
        bool AuditBrokerConfigured, bool ProtectedAuditRequired, bool RbacConfigured,
        bool AutoInstallConfigured, bool AutoInstallPermitted,
        bool UnsignedConsent, bool RollbackHold, string DataClassification);

    internal static FleetReport Build(
        AppConfig config, string machine, ServiceInfo service, bool rollbackHold)
    {
        var state = service.Installed
            ? service.State switch
            {
                "Running" or "Stopped" or "StartPending" or
                "StopPending" or "Paused" => service.State,
                _ => "Unknown"
            }
            : "NotInstalled";
        return new FleetReport(
            SchemaVersion, DiagnosticRedaction.Sanitize(machine),
            DateTime.UtcNow, UpdateService.GetCurrentVersion().ToString(),
            UpdateService.GetRid(), RuntimeInformation.OSArchitecture.ToString(),
            state, service.Installed, config.AuditBrokerEnabled,
            config.RequireProtectedAuditForRecovery, config.RbacEnabled,
            config.AutoInstallUpdatesOnStart,
            config.CheckForUpdatesOnStart && config.AutoInstallUpdatesOnStart &&
                UpdateTrustPolicy.CanInstallUnattended(config) && !rollbackHold,
            config.AllowUnsignedUnattendedUpdates, rollbackHold,
            "NonSecretMetadataOnly");
    }

    internal static string Serialize(FleetReport report) =>
        JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    internal static int PrintJson(AppConfig config)
    {
        var service = new ServiceInfo();
        var exit = 0;
        try { service = WindowsServiceHost.GetInfo(); }
        catch (Exception ex)
        {
            // SCM access denied is an unknown state, never healthy Running.
            WindowsEventLogService.TryWrite(
                "Fleet status SCM lookup unavailable: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name),
                EventLogSeverity.Warning, 4561, "Fleet");
            service = new ServiceInfo { Installed = true, State = "Unknown" };
            exit = 2;
        }
        try
        {
            Console.WriteLine(Serialize(Build(config, Environment.MachineName,
                service, UpdateRollbackHoldService.IsActiveForCurrentBuild())));
            return exit;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Fleet inventory unavailable: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
            return 2;
        }
    }

    internal static IReadOnlyList<string> RunOfflineTests()
    {
        var failures = new List<string>();
        var fresh = Build(new AppConfig(), "PC-DEMO",
            new ServiceInfo(), rollbackHold: false);
        if (fresh.AutoInstallPermitted || fresh.ServiceInstalled ||
            fresh.ServiceState != "NotInstalled" || fresh.SchemaVersion != SchemaVersion)
            failures.Add("Fleet inventory default machine is not accurate.");

        var approved = new AppConfig
        {
            UpdateRepository = UpdateTrustPolicy.OfficialRepository,
            CheckForUpdatesOnStart = true,
            AutoInstallUpdatesOnStart = true,
            AllowUnsignedUnattendedUpdates = true,
            AuditBrokerEnabled = true,
            RequireProtectedAuditForRecovery = true
        };
        var running = new ServiceInfo { Installed = true, State = "Running" };
        var yes = Build(approved, "PC-DEMO", running, rollbackHold: false);
        var held = Build(approved, "PC-DEMO", running, rollbackHold: true);
        if (!yes.AutoInstallPermitted || held.AutoInstallPermitted ||
            !yes.AuditBrokerConfigured || !yes.ProtectedAuditRequired)
            failures.Add("Fleet inventory did not honor explicit consent and rollback.");
        var json = Serialize(yes);
        foreach (var forbidden in new[]
        {
            "password", "recoveryKey", "secret", "accessToken",
            "credential", "tenantId", "privateKey", "thumbprint"
        })
            if (json.Contains("\"" + forbidden + "\"",
                    StringComparison.OrdinalIgnoreCase))
                failures.Add("Fleet JSON exposed forbidden sensitive field: " + forbidden);
        return failures;
    }
}
