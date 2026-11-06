namespace BitKeyBridge;

/// <summary>
/// UI-only localization of health observations. Reports deliberately retain
/// neutral English technical reason codes for cross-team portability.
/// </summary>
internal static class Device360HealthUiFormatter
{
    internal static string Summary(IReadOnlyList<Device360Check> checks,
        string? language)
    {
        var code = UiLanguageService.Normalize(language);
        if (code == "en")
            return Device360HealthService.Summary(checks);
        var attention = checks.Count(x => x.State == Device360CheckState.Attention);
        var unknown = checks.Count(x => x.State == Device360CheckState.NotVerified);
        var observed = checks.Count(x => x.State == Device360CheckState.Observed);
        var key = attention > 0 ? "Device360V2HealthAttention" :
            unknown > 0 ? "Device360V2HealthPartial" :
            "Device360V2HealthObserved";
        string Tr(string resource) => UiLanguageService.Translate(code, resource);
        return Tr(key) + ": " + attention + " " + Tr("Device360V2StateAttention") +
            ", " + unknown + " " + Tr("Device360V2StateNotVerified") +
            ", " + observed + " " + Tr("Device360V2StateObserved");
    }

    internal static string Format(Device360Check check, string? language)
    {
        var code = UiLanguageService.Normalize(language);
        if (code == "en")
            return check.Area + " [" + check.State + "]" +
                Environment.NewLine + check.Explanation;
        var areaKey = check.Area switch
        {
            "Identity" => "Device360V2CheckIdentity",
            "Active Directory" => "Device360V2CheckAd",
            "Entra ID" => "Device360V2CheckEntra",
            "Intune" => "Device360V2CheckIntune",
            "BitLocker metadata" => "Device360V2CheckBitLocker",
            "LAPS metadata" => "Device360V2CheckLaps",
            "Intune compliance" => "Device360V2CheckCompliance",
            "Disk encryption" => "Device360V2CheckEncryption",
            "Intune last sync" => "Device360V2CheckSync",
            "Search completeness" => "Device360V2CheckCompleteness",
            "Remote WMI inventory" => "Device360V36CheckWmi",
            "Remote TPM status" => "Device360V36CheckTpm",
            "Optional WMI inventory" => "Device360V36CheckOptional",
            _ => string.Empty
        };
        var stateKey = check.State switch
        {
            Device360CheckState.Observed => "Device360V2StateObserved",
            Device360CheckState.Attention => "Device360V2StateAttention",
            _ => "Device360V2StateNotVerified"
        };
        var actionKey = check.State switch
        {
            Device360CheckState.Observed => "Device360V2ActionObserved",
            Device360CheckState.Attention => "Device360V2ActionAttention",
            _ => "Device360V2ActionUnknown"
        };
        string Tr(string resource) => UiLanguageService.Translate(code, resource);
        // Do not discard the original classified reason. The localized
        // operator guidance comes first, followed by technical English
        // detail (typed, already sanitized and never a server exception).
        return (string.IsNullOrWhiteSpace(areaKey) ? check.Area : Tr(areaKey)) +
            " [" + Tr(stateKey) + "]" +
            Environment.NewLine + Tr(actionKey) +
            Environment.NewLine + check.Explanation;
    }
}
