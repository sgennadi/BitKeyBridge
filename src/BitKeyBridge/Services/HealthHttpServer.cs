using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class HealthHttpServer : IDisposable
{
    private readonly AppConfig _config;
    private readonly TcpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public HealthHttpServer(AppConfig config)
    {
        _config = config;
        var port = Math.Clamp(config.HealthEndpointPort, 1024, 65535);
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start(CancellationToken externalToken = default)
    {
        if (_loop is not null) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _listener.Start(16);
        _loop = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "Bad Request\n", ct);
                return;
            }

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var method = parts.Length > 0 ? parts[0] : string.Empty;
            var path = parts.Length > 1 ? parts[1].Split('?', 2)[0] : string.Empty;

            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "Method Not Allowed\n", ct);
                return;
            }

            if (path is "/health/live" or "/live")
            {
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8",
                    "{\"status\":\"Alive\"}\n", ct);
                return;
            }

            if (path is not "/health" and
                path is not "/" and
                path is not "/health/ready" and
                path is not "/ready" and
                path is not "/health/security" and
                path is not "/health/coverage")
            {
                await WriteResponseAsync(
                    stream,
                    404,
                    "text/plain; charset=utf-8",
                    "Not Found\n",
                    ct);
                return;
            }

            var snapshot =
                new HealthService(_config)
                    .GetSnapshot();

            object payload = path switch
            {
                "/health/ready" or "/ready" => new
                {
                    status = snapshot.OverallStatus,
                    ready =
                        snapshot.OverallStatus != "Error",
                    serviceState =
                        snapshot.ServiceState,
                    lastSuccessfulExport =
                        snapshot.LastSuccessfulExport,
                    lastSuccessfulExportAgeHours =
                        snapshot.LastSuccessfulExportAgeHours,
                    lastSuccessfulExportStale =
                        snapshot.LastSuccessfulExportStale,
                    errors =
                        snapshot.Errors.Count
                },
                "/health/security" => new
                {
                    status = snapshot.OverallStatus,
                    rbacEnabled =
                        snapshot.RbacEnabled,
                    rbacValidationErrors =
                        snapshot.RbacValidationErrors,
                    auditIntegrityStatus =
                        snapshot.AuditIntegrityStatus,
                    auditIntegrityVerifiedAtUtc =
                        snapshot.AuditIntegrityVerifiedAtUtc,
                    auditSigningEnabled =
                        snapshot.AuditSigningEnabled,
                    auditSigningStatus =
                        snapshot.AuditSigningStatus,
                    auditSigningSignatureValid =
                        snapshot.AuditSigningSignatureValid,
                    auditSigningCurrentHeadSigned =
                        snapshot.AuditSigningCurrentHeadSigned,
                    auditSigningCertificateExpiresUtc =
                        snapshot.AuditSigningCertificateExpiresUtc,
                    auditSigningCertificateDaysRemaining =
                        snapshot.AuditSigningCertificateDaysRemaining,
                    machineCloudConfigured =
                        snapshot.MachineCloudConfigured,
                    machineCloudKeyAccessStatus =
                        snapshot.MachineCloudKeyAccessStatus,
                    remoteApiEnabled =
                        snapshot.RemoteApiEnabled,
                    remoteApiManagementEnabled =
                        snapshot.RemoteApiManagementEnabled,
                    remoteApiAdminTokenConfigured =
                        snapshot.RemoteApiAdminTokenConfigured,
                    remoteApiReadTokenConfigured =
                        snapshot.RemoteApiReadTokenConfigured,
                    remoteApiCoverageRunTokenConfigured =
                        snapshot.RemoteApiCoverageRunTokenConfigured,
                    remoteApiExportTokenConfigured =
                        snapshot.RemoteApiExportTokenConfigured,
                    remoteApiCertificateStatus =
                        snapshot.RemoteApiCertificateStatus,
                    remoteApiCertificateExpiresUtc =
                        snapshot.RemoteApiCertificateExpiresUtc,
                    remoteApiCertificateDaysRemaining =
                        snapshot.RemoteApiCertificateDaysRemaining,
                    remoteApiKeyAccessStatus =
                        snapshot.RemoteApiKeyAccessStatus,
                    certificateStatus =
                        snapshot.CertificateStatus,
                    certificateExpires =
                        snapshot.CertificateExpires,
                    certificateDaysRemaining =
                        snapshot.CertificateDaysRemaining,
                    errors =
                        snapshot.Errors.Count,
                    warnings =
                        snapshot.Warnings.Count
                },
                "/health/coverage" => new
                {
                    enabled =
                        snapshot.ServiceCoverageEnabled,
                    lastSuccess =
                        snapshot.LastCoverageSuccess,
                    finishedUtc =
                        snapshot.LastCoverageFinishedUtc,
                    ageHours =
                        snapshot.LastCoverageAgeHours,
                    totalDevices =
                        snapshot.CoverageTotalDevices,
                    noRecoveryKey =
                        snapshot.CoverageNoRecoveryKey,
                    intuneNotEncrypted =
                        snapshot.CoverageIntuneNotEncrypted,
                    intuneStale =
                        snapshot.CoverageIntuneStale,
                    oldCloudKey =
                        snapshot.CoverageOldCloudKey,
                    policyEnabled =
                        snapshot.CoveragePolicyEnabled,
                    policyCompliant =
                        snapshot.CoveragePolicyCompliant,
                    policyErrors =
                        snapshot.CoveragePolicyErrors,
                    policyWarnings =
                        snapshot.CoveragePolicyWarnings
                },
                _ => snapshot
            };

            var json = JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                }) + "\n";

            var unavailable =
                path is "/health/ready" or "/ready"
                    ? snapshot.OverallStatus == "Error"
                    : path is "/health/security"
                        ? snapshot.AuditIntegrityStatus == "Invalid" ||
                          (_config.AuditSigningEnabled &&
                           snapshot.AuditSigningStatus is not "Valid" and not "ValidCheckpointStale")
                        : false;

            await WriteResponseAsync(
                stream,
                unavailable ? 503 : 200,
                "application/json; charset=utf-8",
                json,
                ct);
        }
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string contentType,
        string body,
        CancellationToken ct)
    {
        var reason = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            503 => "Service Unavailable",
            _ => "Error"
        };

        var payload = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
    }
}
