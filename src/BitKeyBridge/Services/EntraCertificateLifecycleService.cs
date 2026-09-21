namespace BitKeyBridge;

public sealed class EntraCertificateLifecycleService : IDisposable
{
    private readonly EntraSetupService _setup =
        new();
    private readonly CloudGraphService _graph =
        new();

    public async Task<EntraCertificateRolloverResult> RolloverAsync(
        CloudAuthConfig currentConfig,
        Func<DeviceCodeInfo, Task> showDeviceCode,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Entra certificate rollover is only supported on Windows.");
        }

        if (!SecurityContext.IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator rights are required to roll over the Entra certificate.");
        }

        if (string.IsNullOrWhiteSpace(
                currentConfig.ClientId))
        {
            throw new InvalidOperationException(
                "BitKeyBridge Entra Client ID is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                currentConfig.CertificateThumbprint))
        {
            throw new InvalidOperationException(
                "There is no currently managed Entra certificate thumbprint.");
        }

        var previous =
            Clone(currentConfig);
        var previousThumbprint =
            previous.CertificateThumbprint;

        progress?.Report(
            "Adding a new certificate credential while retaining the current credential...");

        EntraSetupResult setupResult;
        try
        {
            setupResult =
                await _setup.RunAsync(
                    previous.TenantId,
                    previous.BootstrapClientId,
                    "BitKeyBridge",
                    previous,
                    showDeviceCode,
                    progress,
                    rotateCertificate: true,
                    ct);
        }
        catch
        {
            TryRestoreUserConfig(previous);
            throw;
        }

        var result =
            new EntraCertificateRolloverResult
            {
                TenantId =
                    setupResult.TenantId,
                ClientId =
                    setupResult.ClientId,
                PreviousThumbprint =
                    previousThumbprint,
                NewThumbprint =
                    setupResult.CertificateThumbprint,
                NewCertificateNotAfter =
                    setupResult.CertificateNotAfter
            };

        try
        {
            progress?.Report(
                "Verifying app-only authentication with the new certificate...");

            var token =
                await _graph.AcquireCertificateTokenAsync(
                    setupResult.TenantId,
                    setupResult.ClientId,
                    setupResult.CertificateThumbprint,
                    ct);

            result.MetadataObjectsReadDuringTest =
                await _graph.TestAccessAsync(
                    token.AccessToken,
                    ct);
            result.CertificateAuthenticationVerified =
                true;

            progress?.Report(
                "New certificate authentication verified.");

            var service =
                WindowsServiceHost.GetInfo();

            if (service.Installed &&
                !string.IsNullOrWhiteSpace(
                    service.Identity))
            {
                var access =
                    new CertificatePrivateKeyAccessService()
                        .EnsureServiceAccess(
                            setupResult.CertificateThumbprint,
                            service.Identity);

                result.ServiceKeyAccessStatus =
                    access.Status;
            }

            if (File.Exists(
                    AppPaths.MachineCloudConfigFile))
            {
                var machine =
                    ConfigService.LoadMachineCloudConfig();

                var sameManagedCredential =
                    string.Equals(
                        Normalize(
                            machine.CertificateThumbprint),
                        Normalize(
                            previousThumbprint),
                        StringComparison.OrdinalIgnoreCase);

                var sameApplication =
                    !string.IsNullOrWhiteSpace(
                        machine.ClientId) &&
                    string.Equals(
                        machine.ClientId,
                        setupResult.ClientId,
                        StringComparison.OrdinalIgnoreCase);

                if (sameManagedCredential ||
                    sameApplication)
                {
                    machine.TenantId =
                        setupResult.TenantId;
                    machine.ClientId =
                        setupResult.ClientId;
                    machine.CertificateThumbprint =
                        setupResult.CertificateThumbprint;
                    machine.AuthMode =
                        "Certificate";

                    ConfigService.SaveMachineCloudConfig(
                        machine);
                    result.MachineCloudConfigUpdated =
                        true;
                }
                else
                {
                    result.Warnings.Add(
                        "Machine cloud config points to a different application/certificate and was not modified.");
                }
            }

            var verifiedUserConfig =
                ConfigService.LoadCloudConfig();

            verifiedUserConfig.TenantId =
                setupResult.TenantId;
            verifiedUserConfig.ClientId =
                setupResult.ClientId;
            verifiedUserConfig.CertificateThumbprint =
                setupResult.CertificateThumbprint;
            verifiedUserConfig.Username =
                previous.Username;
            verifiedUserConfig.AuthMode =
                previous.AuthMode;
            verifiedUserConfig.BootstrapClientId =
                previous.BootstrapClientId;

            ConfigService.SaveCloudConfig(
                verifiedUserConfig);

            WindowsEventLogService.TryWrite(
                $"Entra certificate rollover completed. Previous={previousThumbprint}; New={setupResult.CertificateThumbprint}; MachineConfigUpdated={result.MachineCloudConfigUpdated}.",
                EventLogSeverity.Warning,
                4060,
                "CertificateLifecycle");

            progress?.Report(
                "Certificate rollover completed. The previous Graph credential and local certificate were retained for rollback/grace.");

            return result;
        }
        catch
        {
            TryRestoreUserConfig(
                previous);

            WindowsEventLogService.TryWrite(
                $"Entra certificate rollover verification failed. Previous={previousThumbprint}; Candidate={setupResult.CertificateThumbprint}. User config was rolled back and the previous credential remains active.",
                EventLogSeverity.Error,
                4069,
                "CertificateLifecycle");

            throw;
        }
    }

    private static CloudAuthConfig Clone(
        CloudAuthConfig config) =>
        new()
        {
            TenantId =
                config.TenantId,
            ClientId =
                config.ClientId,
            Username =
                config.Username,
            CertificateThumbprint =
                config.CertificateThumbprint,
            AuthMode =
                config.AuthMode,
            BootstrapClientId =
                config.BootstrapClientId
        };

    private static void TryRestoreUserConfig(
        CloudAuthConfig config)
    {
        try
        {
            ConfigService.SaveCloudConfig(
                config);
        }
        catch
        {
        }
    }

    private static string Normalize(
        string? value) =>
        new(
            (value ?? string.Empty)
            .Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    public void Dispose()
    {
        _setup.Dispose();
        _graph.Dispose();
    }
}
