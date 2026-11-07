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
              hardwareFindings.Any(x => x.Code == "WMI.DISK.PROVIDER_ERROR") &&
              hardwareFindings.Any(x => x.Code == "WMI.TPM_UNVERIFIED"),
            "partial WMI did not retain unknown TPM and incomplete evidence");
        Check(Device360HardwareService.FormatGiB(16m) == "16.0 GiB",
            "memory units formatted incorrectly");
        var mixedWmi = partial with
        {
            ExtendedDiagnostic = "Disk: WmiError; Network: AccessDenied; " +
                "Network: token=NOT_FOR_REPORT; User: DO_NOT_INCLUDE"
        };
        var safe = Device360HardwareService.SafeOptionalStatus(
            mixedWmi.ExtendedDiagnostic);
        Check(safe.Contains("Disk: WmiError", StringComparison.Ordinal) &&
              safe.Contains("Network: AccessDenied", StringComparison.Ordinal) &&
              safe.Contains("Other: Unverified", StringComparison.Ordinal) &&
              !safe.Contains("NOT_FOR_REPORT", StringComparison.Ordinal) &&
              !safe.Contains("DO_NOT_INCLUDE", StringComparison.Ordinal),
            "untrusted optional WMI provider output appeared in UI status");
        var detailed = Device360SmartDiagnosticsService.Evaluate(
            unknown, empty, mixedWmi);
        Check(detailed.Any(x => x.Code == "WMI.DISK.PROVIDER_ERROR") &&
              detailed.Any(x => x.Code == "WMI.NETWORK.ACCESS_DENIED") &&
              !Device360SmartDiagnosticsService.Format(detailed, "ru")
                  .Contains("NOT_FOR_REPORT", StringComparison.Ordinal) &&
              !Device360SmartDiagnosticsService.Format(detailed, "he")
                  .Contains("DO_NOT_INCLUDE", StringComparison.Ordinal),
            "typed WMI detail code or privacy regression");
        var safeReport = Device360ReportService.Build(unknown, empty, mixedWmi);
        Check(safeReport.Contains("WMI.DISK.PROVIDER_ERROR", StringComparison.Ordinal) &&
              !safeReport.Contains("NOT_FOR_REPORT", StringComparison.Ordinal) &&
              !safeReport.Contains("DO_NOT_INCLUDE", StringComparison.Ordinal),
            "explicit Helpdesk export contained untrusted WMI text");

        // WMI must never affect health/coverage before an operator requests
        // it. After a successful probe, a failed retry cannot preserve the
        // previous TPM/hardware observation or mark the machine healthy.
        Check(!unverifiedChecks.Any(x => x.Area.StartsWith("Remote WMI",
                  StringComparison.Ordinal)),
            "unrequested WMI was counted as device evidence");
        var withHardware = Device360HealthService.Evaluate(unknown, empty,
            hardware: partial);
        Check(withHardware.Any(x => x.Area == "Remote TPM status" &&
                  x.State == Device360CheckState.NotVerified) &&
              withHardware.Any(x => x.Area == "Optional WMI inventory" &&
                  x.State == Device360CheckState.Attention) &&
              Device360HealthAssessmentService.Assess(withHardware)
                  .Readiness == Device360Readiness.ActionNeeded,
            "optional WMI provider error or unknown TPM had an incorrect health severity");
        var disabledTpm = partial with
        {
            Manufacturer = "Example", TpmEnabled = "No",
            ExtendedDiagnostic = string.Empty
        };
        var disabledChecks = Device360HealthService.Evaluate(unknown,
            empty, hardware: disabledTpm);
        Check(disabledChecks.Any(x => x.Area == "Remote TPM status" &&
                  x.State == Device360CheckState.Attention) &&
              Device360HealthAssessmentService.Assess(disabledChecks)
                  .Readiness == Device360Readiness.ActionNeeded,
            "disabled TPM did not trigger Attention");
        var failedProviderChecks = Device360HealthService.Evaluate(unknown,
            empty, hardware: mixedWmi);
        Check(failedProviderChecks.Any(x =>
                  x.Area == "Optional WMI inventory" &&
                  x.State == Device360CheckState.Attention) &&
              Device360HealthAssessmentService.Assess(failedProviderChecks)
                  .Readiness == Device360Readiness.ActionNeeded,
            "provider error did not produce same actionable severity as diagnostics");
        var onlyNoRecord = mixedWmi with
        {
            ExtendedDiagnostic = "Disk: NoRecord"
        };
        var missingHardwareChecks = Device360HealthService.Evaluate(unknown,
            empty, hardware: onlyNoRecord);
        Check(missingHardwareChecks.Any(x =>
                  x.Area == "Optional WMI inventory" &&
                  x.State == Device360CheckState.NotVerified),
            "missing provider record was incorrectly escalated to an error");
        var failedAfterSuccess = Device360HealthService.Evaluate(unknown,
            empty, hardwareFailureCategory: "AccessDenied");
        Check(failedAfterSuccess.Any(x => x.Area == "Remote WMI inventory" &&
                  x.State == Device360CheckState.Attention) &&
              !failedAfterSuccess.Any(x => x.Area == "Remote TPM status") &&
              Device360HealthAssessmentService.Assess(failedAfterSuccess)
                  .Readiness == Device360Readiness.ActionNeeded,
            "stale WMI success leaked into failed retry health");
        var untrustedFailure = Device360HealthService.Evaluate(unknown, empty,
            hardwareFailureCategory: "secret=DO_NOT_INCLUDE");
        Check(!untrustedFailure.Any(x => x.Explanation.Contains(
                  "DO_NOT_INCLUDE", StringComparison.Ordinal)),
            "untrusted WMI failure text leaked into health checks");
        // Cross-check the remote Win32_ComputerSystem identity against the
        // selected AD DNS domain, without contacting any network service.
        const string adDn = "CN=UI-TEST-AD-01,OU=Labs,DC=example,DC=com";
        Check(Device360HardwareService.HasMatchingRemoteWmiIdentity(
                  "ui-test-ad-01.example.com", adDn, "UI-TEST-AD-01",
                  "EXAMPLE.COM", true) &&
              Device360HardwareService.HasMatchingRemoteWmiIdentity(
                  "ui-test-ad-01.lab.example.com", adDn, "ui-test-ad-01",
                  "example.com", true),
            "legitimate joined WMI host was rejected");
        foreach (var variant in new[]
        {
            ("other.example.com", "OTHER", "example.com", true),
            ("ui-test-ad-01.example.com", "UNRELATED", "example.com", true),
            ("ui-test-ad-01.example.com", "UI-TEST-AD-01", "other.com", true),
            ("ui-test-ad-01.example.com", "UI-TEST-AD-01", "example.com", false),
            ("ui-test-ad-01.example.com", "", "example.com", true),
            ("ui-test-ad-01.example.com", "UI-TEST-AD-01", "", true)
        })
            Check(!Device360HardwareService.HasMatchingRemoteWmiIdentity(
                    variant.Item1, adDn, variant.Item2, variant.Item3,
                    variant.Item4), "remote identity accepted an invalid host");
        Check(!Device360HardwareService.HasMatchingRemoteWmiIdentity(
                  "ui-test-ad-01.example.com", "CN=OTHER,DC=other,DC=com",
                  "UI-TEST-AD-01", "example.com", true),
            "selected AD domain was not checked");
        Check(!Device360HardwareService.HasMatchingRemoteWmiIdentity(
                  "other.example.com", adDn, "OTHER", "example.com", true) &&
              !Device360HardwareService.HasMatchingRemoteWmiIdentity(
                  "ui-test-ad-01.example.com",
                  "CN=DIFFERENT,OU=Labs,DC=example,DC=com",
                  "UI-TEST-AD-01", "example.com", true),
            "remote WMI identity accepted a different in-domain AD computer");
        Check(Device360HardwareService.HasSameAdComputerName(
                  "ui-test-ad-01.example.com", adDn) &&
              !Device360HardwareService.HasSameAdComputerName(
                  "other.example.com", adDn) &&
              !Device360HardwareService.HasSameAdComputerName(
                  "ui-test-ad-01.example.com",
                  "CN=DIFFERENT,OU=Labs,DC=example,DC=com"),
            "pre-connection AD/DNS short-name gate permitted misrouting");
        var verifiedAdHost = new Device360Snapshot
        {
            Computer = "UI-TEST-AD-01",
            IdentityVerified = true, IdentityAmbiguous = false,
            InAd = true, AdDnsHostName = "ui-test-ad-01.example.com",
            AdDistinguishedName = adDn
        };
        var wrongDnsHost = new Device360Snapshot
        {
            Computer = "UI-TEST-AD-01",
            IdentityVerified = true, IdentityAmbiguous = false,
            InAd = true, AdDnsHostName = "other.example.com",
            AdDistinguishedName = adDn
        };
        var wrongAdComputer = new Device360Snapshot
        {
            Computer = "UI-TEST-AD-01",
            IdentityVerified = true, IdentityAmbiguous = false,
            InAd = true, AdDnsHostName = "ui-test-ad-01.example.com",
            AdDistinguishedName = "CN=OTHER,DC=example,DC=com"
        };
        Check(Device360HardwareService.CanQuery(verifiedAdHost) &&
              !Device360HardwareService.CanQuery(wrongDnsHost) &&
              !Device360HardwareService.CanQuery(wrongAdComputer),
            "WMI preflight could contact the wrong same-domain AD host");
        Check(!Device360HardwareService.HasMatchingRemoteWmiIdentity(
                  "ui-test-ad-01.example.com",
                  "CN=UI\\,TEST,DC=example,DC=com",
                  "UI-TEST-AD-01", "example.com", true),
            "ambiguous escaped AD CN must fail closed");

        var mismatch = Device360SmartDiagnosticsService.Evaluate(
            unknown, empty, hardwareFailureCategory: "IdentityMismatch");
        var mismatchHealth = Device360HealthService.Evaluate(
            unknown, empty, hardwareFailureCategory: "IdentityMismatch");
        var mismatchReport = Device360ReportService.Build(unknown, empty,
            hardwareFailureCategory: "IdentityMismatch");
        Check(mismatch.Any(x => x.Code == "WMI.IDENTITY_MISMATCH" &&
                  x.Priority == Device360FindingPriority.Action) &&
              mismatchHealth.Any(x => x.Area == "Remote WMI inventory" &&
                  x.State == Device360CheckState.Attention) &&
              !mismatchHealth.Any(x => x.Area == "Remote TPM status") &&
              mismatchReport.Contains("WMI.IDENTITY_MISMATCH",
                  StringComparison.Ordinal) &&
              !mismatchReport.Contains("Remote serial number: SYNTHETIC",
                  StringComparison.Ordinal),
            "identity mismatch did not reject remote WMI evidence");
        foreach (var language in new[] { "en", "ru", "he" })
            Check(Device360SmartDiagnosticsService.Format(mismatch, language)
                    .Contains("WMI.IDENTITY_MISMATCH", StringComparison.Ordinal),
                "identity mismatch diagnostic code missing in " + language);

        var noRecord = Device360SmartDiagnosticsService.Evaluate(unknown,
            empty, hardwareFailureCategory: "NoRecord");
        var noRecordChecks = Device360HealthService.Evaluate(unknown, empty,
            hardwareFailureCategory: "NoRecord");
        Check(noRecord.Any(x => x.Code == "WMI.NO_RECORD" &&
                  x.Priority == Device360FindingPriority.Verify) &&
              noRecordChecks.Any(x => x.Area == "Remote WMI inventory" &&
                  x.State == Device360CheckState.NotVerified),
            "no core WMI records should be unverified, not actionable");
        var reportedNoRecord = Device360ReportService.Build(unknown, empty,
            hardwareFailureCategory: "NoRecord");
        Check(reportedNoRecord.Contains("WMI.NO_RECORD", StringComparison.Ordinal) &&
              !reportedNoRecord.Contains("Remote WMI inventory: Attention",
                  StringComparison.Ordinal),
            "no core WMI records lost classified report state");
        var reportedAfterFailure = Device360ReportService.Build(unknown, empty,
            hardwareFailureCategory: "AccessDenied");
        Check(reportedAfterFailure.Contains("WMI.ACCESS_DENIED",
                  StringComparison.Ordinal) &&
              reportedAfterFailure.Contains(
                  "Remote WMI inventory: Attention", StringComparison.Ordinal) &&
              !reportedAfterFailure.Contains("Remote TPM status:",
                  StringComparison.Ordinal),
            "report health and diagnostics disagree after failed WMI retry");

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
