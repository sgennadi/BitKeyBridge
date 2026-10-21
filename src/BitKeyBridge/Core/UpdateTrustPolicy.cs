namespace BitKeyBridge;

/// <summary>
/// Signed and unsigned unattended updates are separate trust modes.
/// The unsigned mode requires deliberate consent and the official source;
/// it NEVER provides the security guarantees of a signed publisher.
/// </summary>
internal static class UpdateTrustPolicy
{
    internal const string OfficialRepository = "sgennadi/BitKeyBridge";

    internal static bool CanEnableUnattended(bool requireTrustedSignature, string? publisher) =>
        requireTrustedSignature && !string.IsNullOrWhiteSpace(publisher?.Trim());

    internal static bool CanEnableUnattended(
        bool requireTrustedSignature,
        string? publisher,
        bool allowUnsigned,
        string? repository)
    {
        if (CanEnableUnattended(requireTrustedSignature, publisher))
            return true;

        // Partial/mismatched publisher policies must never be overridden
        // by unsigned consent. Refuse GitHub repository redirection.
        return allowUnsigned &&
               !requireTrustedSignature &&
               string.IsNullOrWhiteSpace(publisher) &&
               string.Equals(repository?.Trim(), OfficialRepository,
                   StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CanInstallUnattended(AppConfig config) =>
        (string.IsNullOrWhiteSpace(config.TrustedUpdateSignerThumbprint) ||
         config.RequireTrustedUpdateSignature) &&
        CanEnableUnattended(config.RequireTrustedUpdateSignature,
            config.TrustedUpdatePublisher,
            config.AllowUnsignedUnattendedUpdates,
            config.UpdateRepository) &&
        ManagedUpdatePolicy.IsPermitted(config);

    internal static bool UseUnsignedOptInChannel(AppConfig config) =>
        config.AllowUnsignedUnattendedUpdates &&
        !config.RequireTrustedUpdateSignature &&
        string.IsNullOrWhiteSpace(config.TrustedUpdatePublisher) &&
        string.IsNullOrWhiteSpace(config.TrustedUpdateSignerThumbprint) &&
        string.Equals(config.UpdateRepository?.Trim(), OfficialRepository,
            StringComparison.OrdinalIgnoreCase);

    internal static string DescribeMode(AppConfig config) =>
        !ManagedUpdatePolicy.IsPermitted(config)
            ? "Blocked by centrally managed update policy"
            : CanEnableUnattended(config.RequireTrustedUpdateSignature, config.TrustedUpdatePublisher)
            ? "Trusted Authenticode publisher"
            : UseUnsignedOptInChannel(config)
                ? "Unsigned opt-in (GitHub SHA-256, no publisher identity)"
                : "Manual installation only";
}
