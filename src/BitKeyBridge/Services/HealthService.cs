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
            HealthEndpoint = _config.HealthEndpointEnabled
                ? $"http://127.0.0.1:{Math.Clamp(_config.HealthEndpointPort, 1024, 65535)}/health"
                : "Disabled"
        };

        try
        {
            var service = WindowsServiceHost.GetInfo();
            snapshot.ServiceInstalled = service.Installed;
            snapshot.ServiceState = service.State;
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add("Service status: " + ex.Message);
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
