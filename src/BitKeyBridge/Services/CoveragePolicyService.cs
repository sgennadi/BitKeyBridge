namespace BitKeyBridge;

public sealed class CoveragePolicyService
{
    private readonly AppConfig _config;

    public CoveragePolicyService(AppConfig config) => _config = config;

    public CoveragePolicyResult Evaluate(CoverageSummary summary)
    {
        var result = new CoveragePolicyResult
        {
            Enabled = _config.CoveragePolicyEnabled,
            EvaluatedAtUtc = DateTime.UtcNow,
            Compliant = true
        };

        if (!result.Enabled)
            return result;

        AddIfExceeded(
            result,
            "NO_RECOVERY_KEY",
            "NoRecoveryKey",
            summary.NoRecoveryKey,
            _config.CoveragePolicyMaxNoRecoveryKey,
            _config.CoveragePolicyNoRecoveryKeySeverity,
            "device(s) have no BitLocker recovery metadata in AD or Entra");

        AddIfExceeded(
            result,
            "INTUNE_NOT_ENCRYPTED",
            "IntuneNotEncrypted",
            summary.IntuneNotEncrypted,
            _config.CoveragePolicyMaxIntuneNotEncrypted,
            _config.CoveragePolicyIntuneNotEncryptedSeverity,
            "Intune-managed device(s) are reported not encrypted");

        AddIfExceeded(
            result,
            "INTUNE_STALE",
            "IntuneStale",
            summary.IntuneStale,
            _config.CoveragePolicyMaxIntuneStale,
            _config.CoveragePolicyIntuneStaleSeverity,
            "Intune-managed device(s) exceed the configured stale-sync age");

        AddIfExceeded(
            result,
            "OLD_CLOUD_KEY",
            "OldCloudKey",
            summary.OldCloudKey,
            _config.CoveragePolicyMaxOldCloudKey,
            _config.CoveragePolicyOldCloudKeySeverity,
            "device(s) have Entra recovery-key metadata older than the configured age");

        result.ErrorCount = result.Violations.Count(x =>
            x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        result.WarningCount = result.Violations.Count(x =>
            x.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        result.Compliant = result.Violations.Count == 0;

        return result;
    }

    public static string NormalizeSeverity(string? severity)
    {
        var value = (severity ?? string.Empty).Trim();

        if (value.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            return "Error";

        if (value.Equals("Info", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Information", StringComparison.OrdinalIgnoreCase))
            return "Info";

        return "Warning";
    }

    private static void AddIfExceeded(
        CoveragePolicyResult result,
        string code,
        string metric,
        int actual,
        int maximum,
        string severity,
        string description)
    {
        maximum = Math.Max(0, maximum);
        if (actual <= maximum)
            return;

        var normalizedSeverity = NormalizeSeverity(severity);

        result.Violations.Add(new CoveragePolicyViolation
        {
            Code = code,
            Severity = normalizedSeverity,
            Metric = metric,
            Actual = actual,
            Maximum = maximum,
            Message =
                $"{actual} {description}; allowed maximum is {maximum}."
        });
    }
}
