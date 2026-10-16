namespace BitKeyBridge;

internal static class UnsignedUpdateSelfTest
{
    internal static IEnumerable<string> Run()
    {
        var failures = new List<string>();
        var config = new AppConfig { UpdateRepository = UpdateTrustPolicy.OfficialRepository };
        if (UpdateTrustPolicy.CanInstallUnattended(config))
            failures.Add("Unsigned auto-update must never be enabled by default.");

        config.AutoInstallUpdatesOnStart = true;
        if (UpdateTrustPolicy.CanInstallUnattended(config))
            failures.Add("Legacy AutoInstall=true must not imply unsigned consent.");

        config.AllowUnsignedUnattendedUpdates = true;
        if (!UpdateTrustPolicy.CanInstallUnattended(config) ||
            !UpdateTrustPolicy.UseUnsignedOptInChannel(config))
            failures.Add("Explicit consent should allow the official unsigned opt-in channel.");

        config.UpdateRepository = "untrusted/fork";
        if (UpdateTrustPolicy.CanInstallUnattended(config))
            failures.Add("Unsigned auto-update must reject repository redirection.");

        config.UpdateRepository = UpdateTrustPolicy.OfficialRepository;
        config.RequireTrustedUpdateSignature = true;
        if (UpdateTrustPolicy.CanInstallUnattended(config))
            failures.Add("Unsigned consent must not override Authenticode requirement.");

        config.RequireTrustedUpdateSignature = false;
        config.TrustedUpdatePublisher = "Unexpected Publisher";
        if (UpdateTrustPolicy.CanInstallUnattended(config))
            failures.Add("Unsigned consent must not override a configured publisher.");

        config.RequireTrustedUpdateSignature = true;
        if (!UpdateTrustPolicy.CanInstallUnattended(config))
            failures.Add("Signed publisher-pinned auto-update must remain available.");

        return failures;
    }
}
