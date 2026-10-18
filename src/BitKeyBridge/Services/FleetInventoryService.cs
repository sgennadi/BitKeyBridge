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
        bool UnsignedConsent, bool RollbackHold,
        string BrokerServiceHealth, string LastUpdateOutcome,
        DateTime? LastUpdateUtc, string DataClassification);

    internal static FleetReport Build(
        AppConfig config, string machine, ServiceInfo service, bool rollbackHold,
        UpdateHistoryEntry? lastUpdate = null)
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
            BrokerServiceHealth(config, service),
            lastUpdate?.Result switch
            {
                "Success" => "Success",
                "Failed" => "Failed",
                null => "UnknownOrNotRecorded",
                _ => "Unclassified"
            },
            lastUpdate?.TimestampUtc == default ? null : lastUpdate?.TimestampUtc,
            "NonSecretMetadataOnly");
    }

    private static string BrokerServiceHealth(AppConfig config, ServiceInfo service)
    {
        if (!config.AuditBrokerEnabled)
            return "NotConfigured";
        if (!config.RequireProtectedAuditForRecovery || !config.RbacEnabled ||
            config.RbacRecoveryReaders.Count == 0)
            return "PolicyIncomplete";
        if (!service.Installed)
            return "ServiceNotInstalled";
        if (!string.Equals(service.State, "Running",
                StringComparison.OrdinalIgnoreCase))
            return "ServiceNotRunningOrUnknown";
        if (!string.Equals(service.Identity, "LocalSystem",
                StringComparison.OrdinalIgnoreCase))
            return "ServiceIdentityUnexpected";
        // SCM Running is not proof of a SYSTEM-owned pipe, durable ACK,
        // journal hash continuity or actual delegated recovery rights.
        return "ServiceRunningNotVerified";
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
            var history = new UpdateHistoryService().Read(1).FirstOrDefault();
            Console.WriteLine(Serialize(Build(config, Environment.MachineName,
                service, UpdateRollbackHoldService.IsActiveForCurrentBuild(),
                history)));
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
        approved.RbacEnabled = true;
        approved.RbacRecoveryReaders = ["S-1-5-32-544"];
        var running = new ServiceInfo
        {
            Installed = true, State = "Running", Identity = "LocalSystem"
        };
        var latest = new UpdateHistoryEntry
        {
            TimestampUtc = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc),
            Result = "Failed",
            Details = "SECRET_FIXTURE_MUST_NOT_LEAK",
            Publisher = "PublisherFixtureMustNotLeak"
        };
        var yes = Build(approved, "PC-DEMO", running, rollbackHold: false,
            lastUpdate: latest);
        var held = Build(approved, "PC-DEMO", running, rollbackHold: true);
        if (yes.AutoInstallPermitted != ManagedUpdatePolicy.IsPermitted(approved) ||
            held.AutoInstallPermitted ||
            !yes.AuditBrokerConfigured || !yes.ProtectedAuditRequired ||
            yes.BrokerServiceHealth != "ServiceRunningNotVerified" ||
            yes.LastUpdateOutcome != "Failed" ||
            yes.LastUpdateUtc != latest.TimestampUtc ||
            held.LastUpdateOutcome != "UnknownOrNotRecorded")
            failures.Add("Fleet inventory did not honor consent, broker state, last result or rollback.");
        var json = Serialize(yes);
        if (json.Contains("SECRET_FIXTURE_MUST_NOT_LEAK",
                StringComparison.Ordinal) ||
            json.Contains("PublisherFixtureMustNotLeak",
                StringComparison.Ordinal))
            failures.Add("Fleet JSON exposed update history details or publisher metadata.");

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
