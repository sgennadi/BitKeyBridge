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

        using (var document = System.Text.Json.JsonDocument.Parse(
            """
            [
              { "name": "BitKeyBridge-v0.30.0-Windows-x64-MANUAL-UNSIGNED.zip" },
              { "name": "BitKeyBridge-v0.30.0-Windows-arm64-MANUAL-UNSIGNED.zip" }
            ]
            """))
        {
            var assets = document.RootElement;
            if (!UpdateService.HasManualOnlyPackage(assets, "v0.30.0", "win-x64") ||
                !UpdateService.HasManualOnlyPackage(assets, "v0.30.0", "win-arm64") ||
                UpdateService.HasManualOnlyPackage(assets, "v0.30.0", "win-x86") ||
                UpdateService.HasManualOnlyPackage(assets, "v0.30.1", "win-x64") ||
                UpdateService.HasManualOnlyPackage(assets, "v0.30.0", "windows-x64"))
                failures.Add("Manual release fallback must match the exact tag and architecture.");
        }

        return failures;
    }
}
