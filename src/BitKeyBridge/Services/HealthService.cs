using System.Security.Cryptography.X509Certificates;

namespace BitKeyBridge;

public sealed class HealthService
{
    private readonly AppConfig _config;

    public HealthService(AppConfig config) => _config = config;

    public HealthSnapshot GetSnapshot()
    {
        var snapshot = new HealthSnapshot
        {
            TimestampUtc = DateTime.UtcNow,
            Version = typeof(HealthService).Assembly.GetName().Version?.ToString() ?? "unknown",
            MachineName = Environment.MachineName,
            OutputDirectory = _config.OutputDirectory,
            OutputDirectoryExists = Directory.Exists(_config.OutputDirectory),
            CsvExists = File.Exists(_config.OutputCsv),
            CsvRows = CsvUtility.CountDataRows(_config.OutputCsv),
            ServiceCoverageEnabled = _config.ServiceCoverageEnabled,
            ServiceCoverageIntervalMinutes = _config.ServiceCoverageIntervalMinutes,
            MachineCloudConfigured = File.Exists(AppPaths.MachineCloudConfigFile),
            RbacEnabled = _config.RbacEnabled,
            RbacAllowLocalAdministrators =
                _config.RbacAllowLocalAdministrators,
            RbacRecoveryReaderPrincipals =
                _config.RbacRecoveryReaders.Count,
            RbacRotationOperatorPrincipals =
                _config.RbacRotationOperators.Count,
            HealthEndpoint = _config.HealthEndpointEnabled
                ? $"http://127.0.0.1:{Math.Clamp(_config.HealthEndpointPort, 1024, 65535)}/health"
                : "Disabled"
        };

        try
        {
            var service = WindowsServiceHost.GetInfo();
            snapshot.ServiceInstalled = service.Installed;
            snapshot.ServiceState = service.State;
            snapshot.ServiceIdentity = service.Identity;
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Service status: " + ex.Message);
        }

        try
        {
            var rbacErrors =
                new AuthorizationService(_config)
                    .ValidateConfiguredPrincipals();
            snapshot.RbacValidationErrors = rbacErrors.Count;

            if (_config.RbacEnabled && rbacErrors.Count > 0)
            {
                snapshot.Errors.Add(
                    $"RBAC has {rbacErrors.Count} unresolved principal(s).");
            }

            if (_config.RbacEnabled &&
                !_config.RbacAllowLocalAdministrators &&
                _config.RbacRecoveryReaders.Count == 0 &&
                _config.RbacRotationOperators.Count == 0)
            {
                snapshot.Errors.Add(
                    "RBAC is enabled with no configured privileged principals and no local Administrators bypass.");
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add(
                "RBAC validation: " + ex.Message);
        }

        try
        {
            var status = JsonStore.Read<ExportResult>(_config.StatusFile);
            if (status is not null)
            {
                snapshot.LastRunSuccess = status.Success;
                snapshot.LastRunFinished = status.Finished == default ? null : status.Finished;
                snapshot.LastRunPublished = status.Published;
                snapshot.LastRunError = status.ErrorMessage ?? string.Empty;
                snapshot.LastRunRows = status.ValidRows;
                snapshot.LastRunDc = status.AdServer ?? string.Empty;
                snapshot.ReplicationHealthy = status.ReplicationHealthy;
                snapshot.ReplicationErrors = status.ReplicationErrors.Count;
                snapshot.ReplicationWarnings = status.ReplicationWarnings.Count;
                if (!status.Success && !string.IsNullOrWhiteSpace(status.ErrorMessage))
                    snapshot.Errors.Add("Last export: " + status.ErrorMessage);
                if (status.ReplicationErrors.Count > 0)
                    snapshot.Errors.Add($"Replication errors: {status.ReplicationErrors.Count}");
                if (status.ReplicationWarnings.Count > 0)
                    snapshot.Warnings.Add($"Replication warnings: {status.ReplicationWarnings.Count}");
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Export status: " + ex.Message);
        }

        try
        {
            var last = JsonStore.Read<LastSuccessInfo>(_config.LastSuccessFile);
            if (last is not null && last.Finished != default)
            {
                snapshot.LastSuccessfulExport = last.Finished;
                snapshot.LastSuccessfulExportAgeHours = (DateTime.Now - last.Finished).TotalHours;
                snapshot.LastSuccessfulExportStale =
                    snapshot.LastSuccessfulExportAgeHours > _config.StaleSuccessHours;
                if (snapshot.LastSuccessfulExportStale)
                    snapshot.Warnings.Add(
                        $"Last successful export is {snapshot.LastSuccessfulExportAgeHours:0.0} hours old.");
            }
            else
            {
                snapshot.Warnings.Add("No successful published export has been recorded yet.");
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Last-success status: " + ex.Message);
        }

        try
        {
            var coverage = CoverageReportService.ReadStatus();
            if (coverage is not null)
            {
                snapshot.LastCoverageSuccess = coverage.Success;
                snapshot.LastCoverageFinishedUtc =
                    coverage.FinishedUtc == default
                        ? null
                        : coverage.FinishedUtc;
                snapshot.LastCoverageError = coverage.ErrorMessage;
                snapshot.CoverageTotalDevices = coverage.Summary.TotalDevices;
                snapshot.CoverageNoRecoveryKey = coverage.Summary.NoRecoveryKey;
                snapshot.CoverageIntuneNotEncrypted =
                    coverage.Summary.IntuneNotEncrypted;
                snapshot.CoverageIntuneStale = coverage.Summary.IntuneStale;
                snapshot.CoverageOldCloudKey = coverage.Summary.OldCloudKey;
                snapshot.CoveragePolicyEnabled = coverage.Policy.Enabled;
                snapshot.CoveragePolicyCompliant = coverage.Policy.Compliant;
                snapshot.CoveragePolicyErrors = coverage.Policy.ErrorCount;
                snapshot.CoveragePolicyWarnings = coverage.Policy.WarningCount;

                if (snapshot.LastCoverageFinishedUtc is not null)
                {
                    snapshot.LastCoverageAgeHours =
                        (DateTime.UtcNow -
                         snapshot.LastCoverageFinishedUtc.Value.ToUniversalTime())
                        .TotalHours;
                }

                if (_config.ServiceCoverageEnabled && !coverage.Success)
                {
                    snapshot.Errors.Add(
                        "Last coverage run failed: " +
                        (string.IsNullOrWhiteSpace(coverage.ErrorMessage)
                            ? "unknown error"
                            : coverage.ErrorMessage));
                }

                if (coverage.Success)
                {
                    if (coverage.Policy.Enabled)
                    {
                        foreach (var violation in coverage.Policy.Violations)
                        {
                            var message =
                                $"Coverage policy {violation.Code}: {violation.Message}";
                            if (violation.Severity.Equals(
                                    "Error",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                snapshot.Errors.Add(message);
                            }
                            else if (violation.Severity.Equals(
                                         "Warning",
                                         StringComparison.OrdinalIgnoreCase))
                            {
                                snapshot.Warnings.Add(message);
                            }
                        }
                    }
                    else
                    {
                        if (coverage.Summary.NoRecoveryKey > 0)
                            snapshot.Warnings.Add(
                                $"Coverage: {coverage.Summary.NoRecoveryKey} device(s) have no recovery metadata.");
                        if (coverage.Summary.IntuneNotEncrypted > 0)
                            snapshot.Warnings.Add(
                                $"Coverage: {coverage.Summary.IntuneNotEncrypted} Intune device(s) are reported not encrypted.");
                        if (coverage.Summary.IntuneStale > 0)
                            snapshot.Warnings.Add(
                                $"Coverage: {coverage.Summary.IntuneStale} Intune device(s) are stale.");
                    }
                }

                if (_config.ServiceCoverageEnabled &&
                    snapshot.LastCoverageAgeHours >
                    Math.Max(1, _config.ServiceCoverageIntervalMinutes) / 60.0 * 2.0)
                {
                    snapshot.Warnings.Add(
                        $"Last coverage report is {snapshot.LastCoverageAgeHours:0.0} hours old.");
                }
            }
            else if (_config.ServiceCoverageEnabled)
            {
                snapshot.Warnings.Add(
                    "Scheduled Coverage is enabled, but no coverage status has been recorded yet.");
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Coverage status: " + ex.Message);
        }

        try
        {
            if (File.Exists(AppPaths.MachineCloudConfigFile))
            {
                var machineCloud = ConfigService.LoadMachineCloudConfig();
                snapshot.MachineCloudConfigured =
                    !string.IsNullOrWhiteSpace(machineCloud.TenantId) &&
                    !string.IsNullOrWhiteSpace(machineCloud.ClientId) &&
                    !string.IsNullOrWhiteSpace(machineCloud.CertificateThumbprint);
                snapshot.MachineCloudCertificateThumbprint =
                    machineCloud.CertificateThumbprint;

                try
                {
                    new CertificateService().FindLocalMachineByThumbprint(
                        machineCloud.CertificateThumbprint);

                    if (snapshot.ServiceInstalled &&
                        !string.IsNullOrWhiteSpace(snapshot.ServiceIdentity))
                    {
                        var access =
                            new CertificatePrivateKeyAccessService()
                                .GetStatus(
                                    machineCloud.CertificateThumbprint,
                                    snapshot.ServiceIdentity);

                        snapshot.MachineCloudKeyAccessStatus =
                            access.Status;
                        snapshot.MachineCloudKeyAccessAccount =
                            access.Account;
                        snapshot.MachineCloudKeyProvider =
                            access.Provider;

                        if (_config.ServiceCoverageEnabled &&
                            access.AccessRequired &&
                            (!access.ExplicitReadAllowed ||
                             access.ExplicitReadDenied))
                        {
                            snapshot.Errors.Add(
                                $"Machine cloud private-key access for {access.Account}: {access.Status}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_config.ServiceCoverageEnabled)
                    {
                        snapshot.Errors.Add(
                            "Machine cloud certificate/private-key access: " +
                            ex.Message);
                    }
                    else
                    {
                        snapshot.Warnings.Add(
                            "Machine cloud certificate/private-key access: " +
                            ex.Message);
                    }
                }
            }
            else if (_config.ServiceCoverageEnabled)
            {
                snapshot.Errors.Add(
                    "Scheduled Coverage is enabled, but machine cloud configuration is missing.");
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Machine cloud configuration: " + ex.Message);
        }

        try
        {
            var cloud = ConfigService.LoadCloudConfig();
            snapshot.CloudConfigured =
                !string.IsNullOrWhiteSpace(cloud.TenantId) &&
                !string.IsNullOrWhiteSpace(cloud.ClientId);
            snapshot.CloudAuthMode = cloud.AuthMode;
            snapshot.CertificateThumbprint = cloud.CertificateThumbprint;
            snapshot.CertificateConfigured = !string.IsNullOrWhiteSpace(cloud.CertificateThumbprint);
            if (snapshot.CertificateConfigured)
                FillCertificateHealth(snapshot, cloud.CertificateThumbprint);
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Cloud configuration: " + ex.Message);
        }

        snapshot.RemoteApiEnabled = _config.RemoteApiEnabled;
        snapshot.RemoteApiPort = _config.RemoteApiPort;
        snapshot.RemoteApiManagementEnabled = _config.RemoteApiAllowManagement;
        snapshot.RemoteApiCertificateThumbprint = _config.RemoteApiCertificateThumbprint;

        try
        {
            var update = JsonStore.Read<UpdateInfo>(AppPaths.UpdateStatusFile);
            if (update is not null)
            {
                snapshot.UpdateCheckedAtUtc = update.CheckedAtUtc;
                snapshot.LatestVersion = update.LatestVersion;
                snapshot.UpdateAvailable = update.UpdateAvailable;
                snapshot.UpdateError = update.Error;
                if (update.UpdateAvailable)
                    snapshot.Warnings.Add(
                        $"BitKeyBridge update {update.LatestVersion} is available.");
                if (!string.IsNullOrWhiteSpace(update.Error))
                    snapshot.Warnings.Add("Update check: " + update.Error);
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Update status: " + ex.Message);
        }

        if (!snapshot.OutputDirectoryExists)
            snapshot.Errors.Add("Output directory does not exist.");
        if (snapshot.LastRunSuccess == false && string.IsNullOrWhiteSpace(snapshot.LastRunError))
            snapshot.Errors.Add("The last export run failed.");

        snapshot.OverallStatus = snapshot.Errors.Count > 0
            ? "Error"
            : snapshot.Warnings.Count > 0
                ? "Warning"
                : "Healthy";

        return snapshot;
    }

    private static void FillCertificateHealth(HealthSnapshot snapshot, string thumbprint)
    {
        var normalized = new string(thumbprint.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
        X509Certificate2? found = null;

        foreach (var location in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            found = store.Certificates
                .OfType<X509Certificate2>()
                .FirstOrDefault(x =>
                    string.Equals(
                        new string(x.Thumbprint.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray()),
                        normalized,
                        StringComparison.OrdinalIgnoreCase));
            if (found is not null) break;
        }

        if (found is null)
        {
            snapshot.CertificateStatus = "Missing";
            snapshot.Errors.Add("Configured Entra certificate was not found in the Windows certificate stores.");
            return;
        }

        snapshot.CertificateExpires = found.NotAfter;
        snapshot.CertificateDaysRemaining = (found.NotAfter - DateTime.Now).TotalDays;

        if (snapshot.CertificateDaysRemaining <= 0)
        {
            snapshot.CertificateStatus = "Expired";
            snapshot.Errors.Add($"Entra certificate expired on {found.NotAfter:yyyy-MM-dd}.");
        }
        else if (snapshot.CertificateDaysRemaining <= 30)
        {
            snapshot.CertificateStatus = "Expiring";
            snapshot.Warnings.Add($"Entra certificate expires in {snapshot.CertificateDaysRemaining:0} days.");
        }
        else
        {
            snapshot.CertificateStatus = "Healthy";
        }
    }
}
