using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class RemoteApiServer : IDisposable
{
    private readonly AppConfig _config;
    private readonly TcpListener _listener;
    private readonly System.Security.Cryptography.X509Certificates.X509Certificate2 _certificate;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public RemoteApiServer(AppConfig config)
    {
        _config = config;
        if (!config.RemoteApiEnabled)
            throw new InvalidOperationException("Remote API is disabled.");
        if (string.IsNullOrWhiteSpace(config.RemoteApiTokenSha256))
            throw new InvalidOperationException("Remote API bearer token is not configured.");
        if (string.IsNullOrWhiteSpace(config.RemoteApiCertificateThumbprint))
            throw new InvalidOperationException("Remote API TLS certificate is not configured.");

        _certificate = new CertificateService().FindByThumbprint(
            config.RemoteApiCertificateThumbprint);
        _listener = new TcpListener(
            IPAddress.Any,
            Math.Clamp(config.RemoteApiPort, 1024, 65535));
    }

    public void Start(CancellationToken externalToken = default)
    {
        if (_loop is not null) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _listener.Start(32);
        _loop = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientSafeAsync(client, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task HandleClientSafeAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            await HandleClientAsync(client, ct);
        }
        catch (AuthenticationException)
        {
            try { client.Dispose(); } catch { }
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Remote API request failed: " + ex.Message,
                EventLogSeverity.Warning,
                4398,
                "RemoteAPI");
            try { client.Dispose(); } catch { }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.ReceiveTimeout = 10000;
            client.SendTimeout = 10000;

            await using var network = client.GetStream();
            await using var ssl = new SslStream(network, leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = _certificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode =
                        System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                },
                ct);

            using var reader = new StreamReader(
                ssl,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                await WriteResponseAsync(ssl, 400, new { error = "Bad Request" }, ct);
                return;
            }

            var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2)
            {
                await WriteResponseAsync(ssl, 400, new { error = "Bad Request" }, ct);
                return;
            }

            var method = requestParts[0].ToUpperInvariant();
            var rawPath = requestParts[1];
            var path = rawPath.Split('?', 2)[0];

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var totalHeaderBytes = 0;
            for (var i = 0; i < 64; i++)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null || line.Length == 0) break;
                totalHeaderBytes += line.Length;
                if (totalHeaderBytes > 32768)
                {
                    await WriteResponseAsync(ssl, 431, new { error = "Request Header Fields Too Large" }, ct);
                    return;
                }

                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }

            if (!headers.TryGetValue("Authorization", out var authorization) ||
                !ValidateAuthorization(authorization))
            {
                await WriteResponseAsync(
                    ssl,
                    401,
                    new { error = "Unauthorized" },
                    ct,
                    ("WWW-Authenticate", "Bearer"));
                return;
            }

            if (method == "GET" && path == "/api/v1/health")
            {
                var health = new HealthService(_config).GetSnapshot();
                await WriteResponseAsync(
                    ssl,
                    health.OverallStatus == "Error" ? 503 : 200,
                    health,
                    ct);
                return;
            }

            if (method == "GET" && path == "/api/v1/service")
            {
                await WriteResponseAsync(ssl, 200, WindowsServiceHost.GetInfo(), ct);
                return;
            }

            if (method == "GET" && path == "/api/v1/version")
            {
                await WriteResponseAsync(
                    ssl,
                    200,
                    new
                    {
                        version = typeof(RemoteApiServer).Assembly.GetName().Version?.ToString(),
                        machine = Environment.MachineName,
                        architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                        managementEnabled = _config.RemoteApiAllowManagement
                    },
                    ct);
                return;
            }

            if (method == "POST" && path == "/api/v1/export")
            {
                if (!_config.RemoteApiAllowManagement)
                {
                    await WriteResponseAsync(ssl, 403, new { error = "Remote management is disabled." }, ct);
                    return;
                }

                WindowsEventLogService.TryWrite(
                    $"Remote API export requested from {client.Client.RemoteEndPoint}.",
                    EventLogSeverity.Warning,
                    4310,
                    "RemoteAPI");
                var result = await new ExportService(_config).RunAsync(
                    dryRun: false,
                    forcePublish: false,
                    selectedScopes: null,
                    progress: null,
                    cancellationToken: ct);
                await WriteResponseAsync(
                    ssl,
                    result.Success ? 200 : 500,
                    new
                    {
                        result.Success,
                        result.ExitCode,
                        result.ValidRows,
                        result.Published,
                        result.AdServer,
                        result.ErrorMessage
                    },
                    ct);
                return;
            }

            await WriteResponseAsync(ssl, 404, new { error = "Not Found" }, ct);
        }
    }

    private bool ValidateAuthorization(string value)
    {
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var token = value[prefix.Length..].Trim();
        if (token.Length < 32) return false;

        try
        {
            var tokenBytes = Convert.FromBase64String(token);
            try
            {
                var actual = SHA256.HashData(tokenBytes);
                var expected = Convert.FromHexString(_config.RemoteApiTokenSha256);
                return actual.Length == expected.Length &&
                       CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(tokenBytes);
            }
        }
        catch
        {
            return false;
        }
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        int statusCode,
        object payload,
        CancellationToken ct,
        params (string Name, string Value)[] additionalHeaders)
    {
        var json = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);
        var reason = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            431 => "Request Header Fields Too Large",
            500 => "Internal Server Error",
            503 => "Service Unavailable",
            _ => "Error"
        };

        var headers =
            $"HTTP/1.1 {statusCode} {reason}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {bytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Connection: close\r\n";
        foreach (var header in additionalHeaders)
            headers += $"{header.Name}: {header.Value}\r\n";
        headers += "\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
        _certificate.Dispose();
    }
}
