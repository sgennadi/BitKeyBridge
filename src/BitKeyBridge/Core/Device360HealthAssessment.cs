namespace BitKeyBridge;

/// <summary>
/// Metadata evidence coverage, deliberately NOT a security/health score.
/// Missing source data lowers coverage; it cannot turn into a healthy result.
/// </summary>
internal enum Device360Readiness
{
    ActionNeeded,
    Incomplete,
    EvidenceObserved
}

internal sealed record Device360HealthAssessment(
    Device360Readiness Readiness, int AttentionCount, int NotVerifiedCount,
    int ObservedCount, int EvidenceCoveragePercent)
{
    internal bool AuthorizesRecovery => false;
}

internal static class Device360HealthAssessmentService
{
    internal static Device360HealthAssessment Assess(
        IReadOnlyList<Device360Check> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        var attention = checks.Count(x => x.State == Device360CheckState.Attention);
        var unknown = checks.Count(x => x.State == Device360CheckState.NotVerified);
        var observed = checks.Count(x => x.State == Device360CheckState.Observed);
        var total = attention + unknown + observed;

        // Evidence coverage, not percent healthy: negative observations count
        // as known facts but never qualify the device as safe.
        var coverage = total == 0 ? 0 :
            (int)Math.Round(100m * (attention + observed) / total,
                0, MidpointRounding.AwayFromZero);
        var readiness = attention > 0 ? Device360Readiness.ActionNeeded :
            unknown > 0 || total == 0 ? Device360Readiness.Incomplete :
            Device360Readiness.EvidenceObserved;
        return new Device360HealthAssessment(readiness, attention, unknown,
            observed, coverage);
    }

    internal static string Summary(Device360HealthAssessment value,
        string? language)
    {
        var code = UiLanguageService.Normalize(language);
        var heading = code switch
        {
            "ru" => "Полнота данных",
            "he" => "כיסוי נתונים",
            _ => "Evidence coverage"
        };
        var note = code switch
        {
            "ru" => "не оценка безопасности; права на восстановление не проверялись",
            "he" => "לא דירוג אבטחה; הרשאות שחזור לא נבדקו",
            _ => "not a security rating; recovery access not tested"
        };
        return heading + ": " +
            value.EvidenceCoveragePercent.ToString(
                System.Globalization.CultureInfo.InvariantCulture) +
            "% (" + note + ")";
    }
}
