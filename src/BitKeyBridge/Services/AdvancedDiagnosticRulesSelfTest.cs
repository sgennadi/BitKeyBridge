namespace BitKeyBridge;

public static class AdvancedDiagnosticRulesSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        void Check(string name, Action verify)
        {
            try { verify(); }
            catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
        }
        static void Require(bool good, string why)
        {
            if (!good) throw new InvalidOperationException(why);
        }

        Check("Different DC recovery metadata is reported without reading passwords", () =>
        {
            var data = new DeviceConsistencyReport { Computer = "PC-TEST" };
            data.Controllers.Add(new DeviceDcEvidence
            {
                Server = "yosh-dc01", QuerySucceeded = true, ComputerFound = true,
                RecoveryIds = ["11111111-2222-3333-4444-555555555555"],
                WindowsLapsExpiry = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)
            });
            data.Controllers.Add(new DeviceDcEvidence
            {
                Server = "yosh-dc02", QuerySucceeded = true, ComputerFound = true,
                RecoveryIds = [],
                WindowsLapsExpiry = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero)
            });
            var rules = SmartDiagnosticEngine.Explain(data);
            Require(rules.Any(x => x.Code == "BITLOCKER_DC_VARIANCE" &&
                x.Evidence == EvidenceStrength.Confirmed), "BitLocker discrepancy missing.");
            Require(rules.Any(x => x.Code == "LAPS_DC_VARIANCE"), "LAPS discrepancy missing.");
            Require(rules.Any(x => x.Code == "SECRET_RIGHTS_NOT_TESTED" &&
                x.Evidence == EvidenceStrength.NotVerified), "Secret permission status is overstated.");
            Require(!rules.Any(x => x.Code.Contains("ACCESS_DENIED")), "False permission denial detected.");
        });

        Check("Unreachable DC cannot prove a missing backup", () =>
        {
            var data = new DeviceConsistencyReport { Computer = "PC-TEST" };
            data.Controllers.Add(new DeviceDcEvidence { Server = "dc01", Error = "Timeout" });
            var rules = SmartDiagnosticEngine.Explain(data);
            Require(rules.Any(x => x.Code == "DC_UNAVAILABLE"), "No DC failure reported.");
            Require(rules.Any(x => x.Code == "COMPUTER_UNVERIFIED" &&
                x.Evidence == EvidenceStrength.NotVerified), "Unverified computer not noted.");
            Require(!rules.Any(x => x.Code == "BITLOCKER_NO_METADATA"),
                "A failed LDAP query was interpreted as no recovery backup.");
        });

        Check("Cloud one-sided metadata is explicitly not an automatic error", () =>
        {
            var data = new RecoverySourceComparison { AdQueried = true, EntraQueried = true };
            data.AdRecoveryIds.Add("11111111-2222-3333-4444-555555555555");
            var result = SmartDiagnosticEngine.ExplainSources(data);
            Require(result.Any(x => x.Code == "SOURCES_ONE_SIDED" &&
                x.Severity == DiagnosticSeverity.Info), "One source should not be marked failed.");
        });

        Check("Cloud unavailable leaves source comparison partial", () =>
        {
            var data = new RecoverySourceComparison { AdQueried = true };
            data.AdRecoveryIds.Add("11111111-2222-3333-4444-555555555555");
            var result = SmartDiagnosticEngine.ExplainSources(data);
            Require(result.Any(x => x.Code == "SOURCE_PARTIAL" &&
                x.Evidence == EvidenceStrength.NotVerified), "Partial cloud access overstated.");
        });

        return failures;
    }
}
