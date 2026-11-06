namespace BitKeyBridge;

/// <summary>
/// Derived, entirely offline and secret-free technician guidance.
/// Only typed metadata states and explicit operator-initiated WMI outcomes
/// are accepted; never parse remote text, exceptions or Graph payloads.
/// </summary>
internal enum Device360FindingPriority
{
    Action,
    Verify
}

internal sealed record Device360DiagnosticFinding(
    string Code, Device360FindingPriority Priority,
    string Source, string Explanation, string NextAction);

internal static class Device360SmartDiagnosticsService
{
    internal static IReadOnlyList<Device360DiagnosticFinding> Evaluate(
        Device360Snapshot snapshot, Device360OperationalStatus status,
        Device360HardwareInventory? hardware = null,
        string? hardwareFailureCategory = null, DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(status);
        var issues = new List<Device360DiagnosticFinding>();
        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        void Add(string code, Device360FindingPriority priority, string source,
            string explanation, string nextAction) =>
            issues.Add(new Device360DiagnosticFinding(code, priority, source,
                explanation, nextAction));

        if (snapshot.IdentityAmbiguous)
            Add("IDENTITY.CONFLICT", Device360FindingPriority.Action, "Identity",
                "One or more source identities are conflicting or duplicated.",
                "Compare the authoritative AD GUID, Entra device ID and Intune managedDeviceId separately. Do not match by display name.");
        else if (!snapshot.IdentityVerified)
            Add("IDENTITY.UNVERIFIED", Device360FindingPriority.Verify, "Identity",
                "A uniquely qualified source identity was not established.",
                "Select the exact authoritative source record before opening a recovery workflow.");

        AddSource("AD", status.Ad, status.AdFailureKind);
        AddSource("ENTRA", status.Entra, status.EntraFailureKind);
        AddSource("INTUNE", status.Intune, status.IntuneFailureKind);

        if (snapshot.BitLockerMetadataIncomplete)
            Add("BITLOCKER.METADATA_INCOMPLETE", Device360FindingPriority.Verify,
                "BitLocker", "This search did not establish complete backup metadata.",
                "Verify the exact device ID and the authoritative backup source. Do not assume the key is missing or readable.");
        if (snapshot.LapsMetadataExpired)
            Add("LAPS.EXPIRED", Device360FindingPriority.Action, "LAPS",
                "Observed expiration is in the past; the password was not read.",
                "Use approved LAPS diagnostics to inspect policy and authorized backup state.");
        else if (!snapshot.LapsMetadataDetected)
            Add("LAPS.UNVERIFIED", Device360FindingPriority.Verify, "LAPS",
                "Current search did not verify a LAPS backup.",
                "Use the dedicated LAPS workspace; do not infer absence.");

        if (snapshot.InIntune == true)
        {
            if (string.Equals(snapshot.Compliance.Trim(), "noncompliant",
                    StringComparison.OrdinalIgnoreCase))
                Add("INTUNE.NONCOMPLIANT", Device360FindingPriority.Action,
                    "Intune", "Intune reports noncompliance; the policy was not re-run.",
                    "Inspect the assigned compliance policy in an authorized Intune session.");
            if (snapshot.Encrypted == false)
                Add("INTUNE.ENCRYPTION_REPORTED_OFF",
                    Device360FindingPriority.Action, "Intune",
                    "Managed inventory reports encryption disabled; local protection was not checked.",
                    "Verify the endpoint's actual BitLocker status using an authorized workflow.");
            if (snapshot.LastSyncUtc is { } sync)
            {
                var utc = sync.ToUniversalTime();
                if (utc <= now && utc < now.AddDays(-14))
                    Add("INTUNE.STALE_SYNC", Device360FindingPriority.Action,
                        "Intune", "Last reported sync is older than 14 days.",
                        "Check device reachability, enrollment and the Intune management status.");
                else if (utc > now.AddMinutes(5))
                    Add("INTUNE.CLOCK_UNVERIFIED", Device360FindingPriority.Verify,
                        "Intune", "Last-sync time is in the future.",
                        "Verify clock skew and source data before assessing compliance freshness.");
            }
        }

        // A WMI failure is classified by the existing typed WMI transport
        // mapper. Unknown or external strings cannot become user-visible codes.
        var wmiCode = hardwareFailureCategory switch
        {
            "AccessDenied" => "ACCESS_DENIED",
            "Timeout" => "TIMEOUT",
            "DCOM/RPCUnavailable" => "RPC_UNAVAILABLE",
            "WmiError" => "PROVIDER_ERROR",
            "Unavailable" => "UNAVAILABLE",
            _ => string.Empty
        };
        if (wmiCode.Length > 0)
        {
            var action = wmiCode switch
            {
                "ACCESS_DENIED" =>
                    "Verify the existing operator DCOM/WMI read permissions; do not grant new rights automatically.",
                "TIMEOUT" =>
                    "Check the authorized host network path and retry only by operator action.",
                "RPC_UNAVAILABLE" =>
                    "Check DNS, TCP 135, dynamic RPC and the existing firewall policy without relaxing security.",
                "PROVIDER_ERROR" =>
                    "Inspect the remote WMI service, namespace and provider using approved tools.",
                _ => "Confirm the endpoint is reachable and WMI inventory is permitted."
            };
            Add("WMI." + wmiCode, Device360FindingPriority.Action,
                "Hardware", "Explicit remote hardware inventory did not complete.",
                action);
        }

        if (hardware is not null)
        {
            if (hardware.TpmEnabled == "No")
                Add("WMI.TPM_DISABLED", Device360FindingPriority.Action,
                    "Hardware", "Remote WMI reports TPM disabled at the sampled state.",
                    "Check TPM state in approved endpoint management; no TPM secret was accessed.");
            else if (hardware.TpmEnabled != "Yes")
                Add("WMI.TPM_UNVERIFIED", Device360FindingPriority.Verify,
                    "Hardware", "TPM status was not verified by remote WMI.",
                    "Check the WMI TPM namespace and existing read permissions.");
            if (hardware.ExtendedDiagnostic.Length > 0)
                Add("WMI.PARTIAL_INVENTORY", Device360FindingPriority.Verify,
                    "Hardware", "At least one optional hardware class was unavailable.",
                    "Review the typed read-only WMI status; existing inventory remains valid.");
        }

        return issues.OrderBy(x => x.Priority).ThenBy(x => x.Code,
            StringComparer.Ordinal).ToArray();

        void AddSource(string name, Device360SourceEvidence evidence,
            MetadataFailureKind reason)
        {
            if (evidence == Device360SourceEvidence.LookupFailed)
            {
                var kind = reason == MetadataFailureKind.None
                    ? MetadataFailureKind.Unknown : reason;
                Add("SOURCE." + name + "." + ReasonCode(kind),
                    kind is MetadataFailureKind.AccessDenied or
                        MetadataFailureKind.SignInRequired or
                        MetadataFailureKind.SourceOffline or
                        MetadataFailureKind.Timeout or
                        MetadataFailureKind.RateLimited
                        ? Device360FindingPriority.Action :
                        Device360FindingPriority.Verify,
                    name, MetadataFailureGuidance.Label(kind),
                    MetadataFailureGuidance.NextAction(kind, name));
            }
            else if (evidence != Device360SourceEvidence.FoundInSearch)
            {
                Add("SOURCE." + name + ".UNVERIFIED",
                    Device360FindingPriority.Verify, name,
                    "This exact device was not verified in the source.",
                    "Use an explicit metadata search or approved connection; a missing record is not proven.");
            }
        }
    }

    private static string ReasonCode(MetadataFailureKind kind) => kind switch
    {
        MetadataFailureKind.NoRecord => "NO_RECORD",
        MetadataFailureKind.AccessDenied => "ACCESS_DENIED",
        MetadataFailureKind.SignInRequired => "SIGN_IN_REQUIRED",
        MetadataFailureKind.SourceOffline => "SOURCE_OFFLINE",
        MetadataFailureKind.Timeout => "TIMEOUT",
        MetadataFailureKind.DecryptionFailed => "DECRYPTION_FAILED",
        MetadataFailureKind.RateLimited => "RATE_LIMITED",
        _ => "UNKNOWN"
    };

    internal static string Format(IReadOnlyList<Device360DiagnosticFinding> findings,
        string? language)
    {
        var code = UiLanguageService.Normalize(language);
        var title = code switch
        {
            "ru" => "Smart Diagnostics — рекомендуемые действия",
            "he" => "אבחון חכם — פעולות מומלצות",
            _ => "Smart Diagnostics — recommended actions"
        };
        if (findings.Count == 0)
        {
            return title + Environment.NewLine + (code switch
            {
                "ru" => "Нет дополнительных замечаний. Неизвестные источники и права восстановления всё равно требуют проверки.",
                "he" => "אין ממצאים נוספים. יש לאמת מקורות לא ידועים והרשאות שחזור.",
                _ => "No additional findings. Unverified recovery access and sources still require validation."
            });
        }

        var actionTitle = code switch
        {
            "ru" => "Проверьте", "he" => "בדיקה", _ => "Verify"
        };
        return title + Environment.NewLine + Environment.NewLine +
            string.Join(Environment.NewLine + Environment.NewLine,
                findings.Select(x => "[" + x.Code + "] " + x.Source +
                    " (" + x.Priority + ")" + Environment.NewLine +
                    x.Explanation + Environment.NewLine +
                    actionTitle + ": " + x.NextAction));
    }
}
