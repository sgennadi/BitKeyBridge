namespace BitKeyBridge;

/// <summary>
/// Unattended software replacement is only allowed with an explicit publisher
/// policy. Manual verified updates are a separate, operator-confirmed path.
/// </summary>
internal static class UpdateTrustPolicy
{
    internal static bool CanEnableUnattended(bool requireTrustedSignature, string? publisher) =>
        requireTrustedSignature && !string.IsNullOrWhiteSpace(publisher?.Trim());

    internal static bool CanInstallUnattended(AppConfig config) =>
        CanEnableUnattended(config.RequireTrustedUpdateSignature, config.TrustedUpdatePublisher);
}
