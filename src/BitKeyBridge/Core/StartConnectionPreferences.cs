namespace BitKeyBridge;

/// <summary>
/// Non-privileged per-user source preference. Saved beneath LocalAppData so
/// a normal helpdesk operator never needs to modify machine-wide settings.
/// </summary>
public sealed class StartConnectionPreferences
{
    public string Source { get; set; } = StartConnectionPolicy.Choose;

    // Explicit per-user opt-in. Legacy installations continue showing the
    // startup chooser; previously persisted Source alone never auto-connects.
    public bool RememberLastConnectionOnStart { get; set; } = false;

    // Remember only the authentication METHOD, never an AD username/password.
    // An alternate AD account always needs fresh interactive credentials.
    public bool PreferOtherAdAccountOnStart { get; set; } = false;

    // User-specific visual preference. No credentials or secrets are persisted.
    public bool RecoveryCardsOnLeft { get; set; } = true;

    // Three draggable equal-size panes; contains only their IDs, never secrets.
    // Empty indicates migration from the earlier two-pane left/right choice.
    public string HomePaneOrder { get; set; } = string.Empty;

    // Non-secret per-user recovery workspace choices. Legacy machine-wide
    // preferences remain readable until a user saves their own selection.
    public string RecoverySearchSource { get; set; } = string.Empty;
    public string LastRecoveryScopeName { get; set; } = string.Empty;
    public string LastRecoveryScopeSearchBase { get; set; } = string.Empty;
}

public static class StartConnectionPolicy
{
    public const string Choose = "Choose";
    public const string ActiveDirectory = "AD";
    public const string MicrosoftCloud = "Cloud";
    public const string Both = "Both";

    public static string Normalize(string? source) => source switch
    {
        ActiveDirectory => ActiveDirectory,
        MicrosoftCloud => MicrosoftCloud,
        Both => Both,
        _ => Choose
    };

    public static bool NeedsActiveDirectory(string? source) =>
        Normalize(source) is ActiveDirectory or Both;

    public static bool NeedsMicrosoftCloud(string? source) =>
        Normalize(source) is MicrosoftCloud or Both;

    // No implicit enrollment: only the explicit wizard checkbox may suppress
    // the next startup prompt. The administrator's AutoConnectOnStart switch
    // remains authoritative, and "Choose later" cannot trigger sign-in.
    public static bool ShouldAutoConnect(
        StartConnectionPreferences? preferences,
        bool automaticConnectionsEnabled) =>
        automaticConnectionsEnabled &&
        preferences is { RememberLastConnectionOnStart: true } &&
        Normalize(preferences.Source) != Choose;

    public static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var preferences = new StartConnectionPreferences();
        if (preferences.RememberLastConnectionOnStart ||
            ShouldAutoConnect(preferences, automaticConnectionsEnabled: true))
            errors.Add("First launch must require an explicit connection choice.");

        // Older per-user preference files may contain a Source but no opt-in.
        preferences.Source = ActiveDirectory;
        if (ShouldAutoConnect(preferences, automaticConnectionsEnabled: true))
            errors.Add("Legacy saved source silently enabled auto-connect.");

        preferences.RememberLastConnectionOnStart = true;
        if (!ShouldAutoConnect(preferences, automaticConnectionsEnabled: true) ||
            ShouldAutoConnect(preferences, automaticConnectionsEnabled: false))
            errors.Add("Remember-last connection ignored user or administrator control.");

        preferences.Source = MicrosoftCloud;
        if (!ShouldAutoConnect(preferences, automaticConnectionsEnabled: true))
            errors.Add("Explicit cloud source was not eligible for remembered startup.");

        preferences.Source = Both;
        preferences.PreferOtherAdAccountOnStart = true;
        if (!ShouldAutoConnect(preferences, automaticConnectionsEnabled: true))
            errors.Add("Explicit combined source was not eligible for remembered startup.");

        preferences.Source = Choose;
        if (ShouldAutoConnect(preferences, automaticConnectionsEnabled: true))
            errors.Add("Choose-later preference attempted a background connection.");

        preferences.Source = "Invalid";
        if (ShouldAutoConnect(preferences, automaticConnectionsEnabled: true) ||
            ShouldAutoConnect(null, automaticConnectionsEnabled: true))
            errors.Add("Invalid or missing connection preference was accepted.");

        return errors;
    }
}
