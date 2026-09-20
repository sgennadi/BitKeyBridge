namespace BitKeyBridge;

public sealed class CoverageAutomationService
{
    private readonly AppConfig _config;

    public CoverageAutomationService(AppConfig config) => _config = config;

    public async Task<CoverageRunStatus> RunOnceAsync(
        CancellationToken ct = default)
    {
        var startedUtc = DateTime.UtcNow;

        try
        {
            var cloud = ValidateMachineCloudConfiguration();

            new CertificateService()
                .FindLocalMachineByThumbprint(
                    cloud.CertificateThumbprint);

            using var graph = new CloudGraphService();
            var token = await graph.AcquireCertificateTokenAsync(
                cloud.TenantId,
                cloud.ClientId,
                cloud.CertificateThumbprint,
                ct);

            var coverage = new CoverageService(_config);
            var result = await coverage.RunAsync(
                token.AccessToken,
                null,
                null,
                ct);

            CoverageReportService.WriteResult(
                result,
                _config.CoverageCsv,
                _config.CoverageJson,
                startedUtc,
                _config);

            return CoverageReportService.ReadStatus()
                ?? new CoverageRunStatus
                {
                    Success = true,
                    StartedUtc = startedUtc,
                    FinishedUtc = DateTime.UtcNow,
                    DomainController = result.DomainController,
                    CsvPath = _config.CoverageCsv,
                    JsonPath = _config.CoverageJson,
                    Summary = result.Summary,
                    Policy = new CoveragePolicyService(_config)
                        .Evaluate(result.Summary)
                };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            CoverageReportService.TryWriteFailure(
                startedUtc,
                ex,
                _config.CoverageCsv,
                _config.CoverageJson);

            return CoverageReportService.ReadStatus()
                ?? new CoverageRunStatus
                {
                    Success = false,
                    StartedUtc = startedUtc,
                    FinishedUtc = DateTime.UtcNow,
                    ErrorMessage = ex.Message,
                    CsvPath = _config.CoverageCsv,
                    JsonPath = _config.CoverageJson
                };
        }
    }

    private static CloudAuthConfig ValidateMachineCloudConfiguration()
    {
        if (!File.Exists(AppPaths.MachineCloudConfigFile))
        {
            throw new InvalidOperationException(
                "Machine cloud configuration is missing. " +
                "Run BitKeyBridge.exe --cloud-machine-save first.");
        }

        var cloud = ConfigService.LoadMachineCloudConfig();

        if (!string.Equals(
                cloud.AuthMode,
                "Certificate",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Unattended Coverage requires Certificate authentication.");
        }

        if (string.IsNullOrWhiteSpace(cloud.TenantId) ||
            string.IsNullOrWhiteSpace(cloud.ClientId) ||
            string.IsNullOrWhiteSpace(cloud.CertificateThumbprint))
        {
            throw new InvalidOperationException(
                "Machine cloud configuration is incomplete. " +
                "Tenant ID, Client ID, and certificate thumbprint are required.");
        }

        return cloud;
    }
}
