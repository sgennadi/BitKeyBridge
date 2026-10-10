namespace BitKeyBridge;

public enum EvidenceStrength { Confirmed, Likely, Possible, NotVerified }
public enum DiagnosticSeverity { Info, Warning, Error }

public sealed record DiagnosticFinding(
    string Code,
    DiagnosticSeverity Severity,
    EvidenceStrength Evidence,
    string Summary,
    string Explanation,
    string NextAction,
    string RunOn);

public sealed class DeviceDcEvidence
{
    public string Server { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool ComputerFound { get; set; }
    public bool QuerySucceeded { get; set; }
    public bool? ReplicationHealthy { get; set; }
    public string ComputerDistinguishedName { get; set; } = string.Empty;
    public DateTimeOffset? WindowsLapsExpiry { get; set; }
    public DateTimeOffset? LegacyLapsExpiry { get; set; }
    public string WindowsLapsVersion { get; set; } = string.Empty;
    public List<string> RecoveryIds { get; set; } = [];
    public string Error { get; set; } = string.Empty;
    public double ElapsedMs { get; set; }
}

public sealed class DeviceConsistencyReport
{
    public string Computer { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DeviceDcEvidence> Controllers { get; } = [];
    public List<DiagnosticFinding> Findings { get; } = [];
    public int Queried => Controllers.Count(x => x.QuerySucceeded);
    public int Failed => Controllers.Count(x => !x.QuerySucceeded);
}

public sealed class PermissionInspectionReport
{
    public string Computer { get; set; } = string.Empty;
    public string QueriedDc { get; set; } = string.Empty;
    public string IdentityUsed { get; set; } = string.Empty;
    public bool ExplicitCredentials { get; set; }
    public string ComputerDn { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public bool DescriptorRetrieved { get; set; }
    public string Status { get; set; } = "NotVerified";
    public List<string> DescriptorRules { get; } = [];
    public List<DiagnosticFinding> Findings { get; } = [];
}

public sealed class RecoverySourceComparison
{
    public string Computer { get; set; } = string.Empty;
    public string EntraDeviceId { get; set; } = string.Empty;
    public string DomainController { get; set; } = string.Empty;
    public bool AdQueried { get; set; }
    public bool EntraQueried { get; set; }
    public List<string> AdRecoveryIds { get; } = [];
    public List<string> EntraRecoveryIds { get; } = [];
    public List<DiagnosticFinding> Findings { get; } = [];
}

public static class SmartDiagnosticEngine
{
    public static IReadOnlyList<DiagnosticFinding> Explain(DeviceConsistencyReport report)
    {
        var working = report.Controllers.Where(x => x.QuerySucceeded && x.ComputerFound).ToList();
        var errors = report.Controllers.Where(x => !x.QuerySucceeded).ToList();
        var findings = new List<DiagnosticFinding>();

        if (report.Controllers.Count == 0)
            findings.Add(new("DC_NOT_DISCOVERED", DiagnosticSeverity.Warning,
                EvidenceStrength.Confirmed, "No domain controllers were enumerated",
                "Controller enumeration returned zero entries. This does not indicate missing keys.",
                "Check AD DNS discovery, domain and explicit credentials.", "Operator workstation"));
        if (errors.Count > 0)
            findings.Add(new("DC_UNAVAILABLE", DiagnosticSeverity.Warning,
                EvidenceStrength.Confirmed, "At least one domain controller could not be queried",
                string.Join("; ", errors.Select(x => x.Server + ": " + x.Error).Take(5)),
                "Check LDAP bind, credentials, DNS and firewall to each named controller.", "Operator workstation"));
        if (working.Count == 0)
        {
            if (report.Controllers.Count > 0)
                findings.Add(new("COMPUTER_UNVERIFIED", DiagnosticSeverity.Warning,
                    EvidenceStrength.NotVerified, "The requested computer could not be confirmed on a readable DC",
                    "No successful computer-object lookup. Do not infer absence of a password or recovery backup.",
                    "Check exact AD name, domain, computer OU and directory read access.", "DC / RSAT workstation"));
            return findings;
        }

        var missingComputers = report.Controllers.Where(x => x.QuerySucceeded && !x.ComputerFound).ToList();
        if (missingComputers.Count > 0)
            findings.Add(new("COMPUTER_DC_VARIANCE", DiagnosticSeverity.Warning,
                EvidenceStrength.Confirmed, "Computer-object visibility differs between domain controllers",
                "Not found / not visible on " + string.Join(", ", missingComputers.Select(x => x.Server)) + ".",
                "Check AD replication, deleted/moved object state, search identity and permissions on each DC.", "Domain controller"));

        var variants = working.GroupBy(x => string.Join(",", x.RecoveryIds.OrderBy(y => y,
            StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase).ToList();
        if (variants.Count > 1)
            findings.Add(new("BITLOCKER_DC_VARIANCE", DiagnosticSeverity.Warning,
                EvidenceStrength.Confirmed, "BitLocker Recovery ID metadata differs between DCs",
                string.Join("; ", working.Select(x => x.Server + "=" + x.RecoveryIds.Count + " IDs")),
                "Compare metadata by Recovery ID on each DC and inspect replication status. No password was read.", "DC / RSAT workstation"));

        if (working.All(x => x.RecoveryIds.Count == 0))
            findings.Add(new("BITLOCKER_NO_METADATA", DiagnosticSeverity.Warning,
                EvidenceStrength.Possible, "No BitLocker recovery objects visible on queried DCs",
                "Metadata-only reads cannot distinguish missing escrow from filtered object visibility.",
                "Verify affected-device BitLocker AD backup GPO, exact OU, recovery-object ACLs and replication.", "Affected device + DC"));

        var lapsVariant = working.Select(x =>
                (x.WindowsLapsExpiry?.UtcTicks.ToString() ?? "-") + "|" +
                (x.LegacyLapsExpiry?.UtcTicks.ToString() ?? "-") + "|" +
                x.WindowsLapsVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (lapsVariant > 1)
            findings.Add(new("LAPS_DC_VARIANCE", DiagnosticSeverity.Warning,
                EvidenceStrength.Confirmed, "LAPS non-secret metadata differs between DCs",
                "Expiration/version differs. This does not prove password or decryption differences.",
                "Check DC replication and compare password backup indicator timestamps.", "DC / RSAT workstation"));

        if (working.All(x => x.WindowsLapsExpiry is null && x.LegacyLapsExpiry is null &&
                             string.IsNullOrWhiteSpace(x.WindowsLapsVersion)))
            findings.Add(new("LAPS_NO_INDICATOR", DiagnosticSeverity.Info,
                EvidenceStrength.NotVerified, "No LAPS backup indicators returned",
                "A hidden/absent metadata indicator does not prove there is no password or that rights were denied.",
                "Check client LAPS policy, Windows LAPS Operational log and OU read rights.", "Affected device + DC"));

        findings.Add(new("SECRET_RIGHTS_NOT_TESTED", DiagnosticSeverity.Info,
            EvidenceStrength.NotVerified, "Password read and DPAPI-NG decryption not tested",
            "The consistency test never requests msFVE-RecoveryPassword or any LAPS password attribute.",
            "Run a separately authorized targeted read/decrypt test only if required.", "Authorized reader workstation"));

        return findings;
    }

    public static IReadOnlyList<DiagnosticFinding> ExplainSources(RecoverySourceComparison report)
    {
        var findings = new List<DiagnosticFinding>();
        if (!report.AdQueried || !report.EntraQueried)
        {
            findings.Add(new("SOURCE_PARTIAL", DiagnosticSeverity.Info,
                EvidenceStrength.NotVerified, "AD / Entra comparison incomplete",
                "One recovery source could not be queried; differences cannot be established.",
                "Confirm LDAP and Graph metadata access and exact Entra device ID.", "Operator workstation"));
            return findings;
        }

        if (report.AdRecoveryIds.Count == 0 && report.EntraRecoveryIds.Count == 0)
            findings.Add(new("SOURCES_NO_BACKUP", DiagnosticSeverity.Warning,
                EvidenceStrength.Possible, "No recovery metadata returned by either source",
                    "AD/Entra backup policies, object visibility and device identity require independent review.",
                    "Check affected device escrow events and the correct AD/Entra device identity.", "Affected device"));
        else if (report.AdRecoveryIds.Count == 0 || report.EntraRecoveryIds.Count == 0)
            findings.Add(new("SOURCES_ONE_SIDED", DiagnosticSeverity.Info,
                EvidenceStrength.Confirmed, "Recovery metadata returned by only one source",
                    "BitLocker may be configured to back up to only one directory. One-sided results are not automatically an error.",
                    "Compare intended escrow GPO/MDM policy and known device identity.", "Affected device / Intune"));
        else
            findings.Add(new("SOURCES_PRESENT", DiagnosticSeverity.Info,
                EvidenceStrength.Confirmed, "Both AD and Entra returned BitLocker metadata",
                    "Recovery IDs may legitimately differ due to separate backup events or rotations; equality is not required.",
                    "Review chronology and recovery IDs only if there is an operational mismatch.", "Helpdesk workstation"));

        return findings;
    }
}
