namespace BitKeyBridge;

/// <summary>
/// Offline, synthetic v0.36.0 evidence/diagnostics regressions.
/// Never queries AD, Graph, WMI or recovery secrets during --self-test.
/// </summary>
internal static class Device360V36SelfTest
{
    internal static IEnumerable<string> Run()
    {
        var failures = new List<string>();
        void Check(bool valid, string reason)
        {
            if (!valid) failures.Add("Device360 v0.36: " + reason);
        }

        var unknown = new Device360Snapshot
        {
            Computer = "SYNTHETIC-ONLY", IdentityVerified = false,
            BitLockerMetadataIncomplete = true
        };
        var empty = Device360OperationalStatus.Capture(unknown,
            adConnected: false, cloudSessionPresent: false,
            rbacEnabled: false, ticketRequired: true,
            protectedAuditRequired: true, auditBrokerConfigured: false);
        var unverifiedChecks = Device360HealthService.Evaluate(unknown, empty);
        var unverified = Device360HealthAssessmentService.Assess(unverifiedChecks);
        Check(unverified.Readiness == Device360Readiness.Incomplete &&
              unverified.NotVerifiedCount > 0 &&
              unverified.EvidenceCoveragePercent < 100 &&
              !unverified.AuthorizesRecovery,
            "unverified metadata was incorrectly treated as healthy/authorized");

        var conflict = new Device360Snapshot
        {
            IdentityAmbiguous = true, IdentityVerified = false,
            InIntune = true, Encrypted = false, Compliance = "noncompliant",
            LapsMetadataDetected = true, LapsMetadataExpired = true,
            LastSyncUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var failedSources = Device360OperationalStatus.Capture(conflict,
            adConnected: true, cloudSessionPresent: true,
            rbacEnabled: false, ticketRequired: true,
            protectedAuditRequired: true, auditBrokerConfigured: false,
            adLookupFailed: true, adFailureKind: MetadataFailureKind.AccessDenied,
            intuneLookupFailed: true, intuneFailureKind: MetadataFailureKind.RateLimited);
        var findings = Device360SmartDiagnosticsService.Evaluate(conflict,
            failedSources, hardwareFailureCategory: "DCOM/RPCUnavailable",
            nowUtc: new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc));
        var codes = findings.Select(x => x.Code).ToHashSet(
            StringComparer.Ordinal);
        foreach (var code in new[]
        {
            "IDENTITY.CONFLICT", "SOURCE.AD.ACCESS_DENIED",
            "SOURCE.INTUNE.RATE_LIMITED", "LAPS.EXPIRED",
            "INTUNE.NONCOMPLIANT", "INTUNE.ENCRYPTION_REPORTED_OFF",
            "INTUNE.STALE_SYNC", "WMI.RPC_UNAVAILABLE"
        })
            Check(codes.Contains(code), "missing classified finding " + code);
        var urgent = Device360HealthAssessmentService.Assess(
            Device360HealthService.Evaluate(conflict, failedSources));
        Check(urgent.Readiness == Device360Readiness.ActionNeeded &&
              !urgent.AuthorizesRecovery, "attention was treated as safe");

        var unknownWmi = Device360SmartDiagnosticsService.Evaluate(unknown,
            empty, hardwareFailureCategory: "secret=DO_NOT_INCLUDE");
        Check(!unknownWmi.Any(x => x.Code.Contains("secret",
            StringComparison.OrdinalIgnoreCase)) &&
            !Device360SmartDiagnosticsService.Format(unknownWmi, "en")
                .Contains("DO_NOT_INCLUDE", StringComparison.Ordinal),
            "untrusted WMI category reached the display");

        var partial = new Device360HardwareInventory
        {
            Manufacturer = "Example", Model = "Virtual",
            MemoryGiB = 16m, CpuName = "Synthetic CPU",
            CpuLogicalProcessors = 8, PhysicalDiskCount = 1,
            PhysicalStorageGiB = 256m,
            ExtendedDiagnostic = "Disk: WmiError"
        };
        var hardwareFindings = Device360SmartDiagnosticsService.Evaluate(
            unknown, empty, partial);
        Check(hardwareFindings.Any(x => x.Code == "WMI.PARTIAL_INVENTORY") &&
              hardwareFindings.Any(x => x.Code == "WMI.TPM_UNVERIFIED"),
            "partial WMI did not retain unknown TPM and incomplete evidence");
        Check(Device360HardwareService.FormatGiB(16m) == "16.0 GiB",
            "memory units formatted incorrectly");

        foreach (var lang in new[] { "en", "ru", "he" })
        {
            Check(!string.IsNullOrWhiteSpace(
                Device360SmartDiagnosticsService.Format(findings, lang)) &&
                !string.IsNullOrWhiteSpace(
                    Device360HealthAssessmentService.Summary(urgent, lang)),
                "missing language summary for " + lang);
        }
        var russian = Device360SmartDiagnosticsService.Format(findings, "ru");
        var hebrew = Device360SmartDiagnosticsService.Format(findings, "he");
        Check(russian.Contains("Проверьте", StringComparison.Ordinal) &&
              russian.Contains("не", StringComparison.OrdinalIgnoreCase) &&
              hebrew.Contains("בדקו", StringComparison.Ordinal),
            "Smart Diagnostics reasons/actions were not localized to RU/HE");
        Check(russian.Contains("SOURCE.AD.ACCESS_DENIED", StringComparison.Ordinal) &&
              hebrew.Contains("WMI.RPC_UNAVAILABLE", StringComparison.Ordinal),
            "stable technical reason codes lost in localized diagnostics");

        return failures;
    }
}
