namespace BitKeyBridge;

/// <summary>
/// Non-privileged per-user source preference. Saved beneath LocalAppData so
/// a normal helpdesk operator never needs to modify machine-wide settings.
/// </summary>
public sealed class StartConnectionPreferences
{
    public string Source { get; set; } = StartConnectionPolicy.Choose;

    // User-specific visual preference. No credentials or secrets are persisted.
    public bool RecoveryCardsOnLeft { get; set; } = true;
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
}
