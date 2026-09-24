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
    internal const int MaximumRequestLineBytes = 4096;
    internal const int MaximumHeaderLineBytes = 8192;
    internal const int MaximumHeaderBytes = 32768;
    internal const int MaximumHeaderCount = 64;
    internal const int MaximumConcurrentClients = 32;
    internal static readonly TimeSpan PreAuthenticationTimeout =
        TimeSpan.FromSeconds(15);

    private enum RemoteApiAccessScope
    {
        Admin,
        Read,
        CoverageRun,
        Export
    }

    private readonly AppConfig _config;
    private readonly TcpListener _listener;
    private readonly System.Security.Cryptography.X509Certificates.X509Certificate2 _certificate;
    private readonly SemaphoreSlim _clientSlots =
        new(MaximumConcurrentClients, MaximumConcurrentClients);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public RemoteApiServer(AppConfig config)
    {
        _config = config;
        if (!config.RemoteApiEnabled)
            throw new InvalidOperationException("Remote API is disabled.");
        if (string.IsNullOrWhiteSpace(config.RemoteApiTokenSha256) &&
            string.IsNullOrWhiteSpace(config.RemoteApiReadTokenSha256) &&
            string.IsNullOrWhiteSpace(config.RemoteApiCoverageRunTokenSha256) &&
            string.IsNullOrWhiteSpace(config.RemoteApiExportTokenSha256))
        {
            throw new InvalidOperationException(
                "Remote API bearer token is not configured.");
        }
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
                var client =
                    await _listener.AcceptTcpClientAsync(
                        ct);

                if (!_clientSlots.Wait(0))
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch
                    {
                    }

                    continue;
                }

                _ = HandleClientWithSlotAsync(
                    client,
                    ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientWithSlotAsync(
        TcpClient client,
        CancellationToken ct)
    {
        try
        {
            await HandleClientSafeAsync(
                client,
                ct);
        }
        finally
        {
            _clientSlots.Release();
        }
    }

    private async Task HandleClientSafeAsync(
        TcpClient client,
        CancellationToken ct)
    {
        try
        {
            await HandleClientAsync(
                client,
                ct);
        }
        catch (OperationCanceledException)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }
        catch (AuthenticationException)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Remote API request failed: " +
                ex.Message,
                EventLogSeverity.Warning,
                4398,
                "RemoteAPI");

            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.ReceiveTimeout = 10000;
            client.SendTimeout = 10000;

            await using var network =
                client.GetStream();
            await using var ssl =
                new SslStream(
                    network,
                    leaveInnerStreamOpen: false);

            using var preAuthentication =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        ct);
            preAuthentication.CancelAfter(
                PreAuthenticationTimeout);

            await ssl.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = _certificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols =
                        SslProtocols.Tls12 |
                        SslProtocols.Tls13,
                    CertificateRevocationCheckMode =
                        System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                },
                preAuthentication.Token);

            string? requestLine;
            try
            {
                requestLine =
                    await ReadAsciiLineAsync(
                        ssl,
                        MaximumRequestLineBytes,
                        preAuthentication.Token);
            }
            catch (InvalidDataException)
            {
                await WriteResponseAsync(
                    ssl,
                    414,
                    new
                    {
                        error =
                            "Request URI Too Long"
                    },
                    ct);
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    requestLine))
            {
                await WriteResponseAsync(
                    ssl,
                    400,
                    new
                    {
                        error =
                            "Bad Request"
                    },
                    ct);
                return;
            }

            var requestParts =
                requestLine.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2)
            {
                await WriteResponseAsync(
                    ssl,
                    400,
                    new
                    {
                        error =
                            "Bad Request"
                    },
                    ct);
                return;
            }

            var method =
                requestParts[0]
                    .ToUpperInvariant();
            var rawPath =
                requestParts[1];
            var path =
                rawPath.Split(
                    '?',
                    2)[0];

            var headers =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            var totalHeaderBytes = 0;
            var headerCount = 0;
            var headersComplete = false;

            while (headerCount <=
                   MaximumHeaderCount)
            {
                string? line;

                try
                {
                    line =
                        await ReadAsciiLineAsync(
                            ssl,
                            MaximumHeaderLineBytes,
                            preAuthentication.Token);
                }
                catch (InvalidDataException)
                {
                    await WriteResponseAsync(
                        ssl,
                        431,
                        new
                        {
                            error =
                                "Request Header Fields Too Large"
                        },
                        ct);
                    return;
                }

                if (line is null)
                {
                    await WriteResponseAsync(
                        ssl,
                        400,
                        new
                        {
                            error =
                                "Bad Request"
                        },
                        ct);
                    return;
                }

                if (line.Length == 0)
                {
                    headersComplete = true;
                    break;
                }

                headerCount++;
                if (headerCount >
                    MaximumHeaderCount)
                {
                    break;
                }

                totalHeaderBytes +=
                    Encoding.ASCII.GetByteCount(
                        line) +
                    2;

                if (totalHeaderBytes >
                    MaximumHeaderBytes)
                {
                    await WriteResponseAsync(
                        ssl,
                        431,
                        new
                        {
                            error =
                                "Request Header Fields Too Large"
                        },
                        ct);
                    return;
                }

                var colon =
                    line.IndexOf(
                        ':');
                if (colon <= 0)
                    continue;

                headers[line[..colon].Trim()] =
                    line[(colon + 1)..].Trim();
            }

            if (!headersComplete)
            {
                await WriteResponseAsync(
                    ssl,
                    431,
                    new
                    {
                        error =
                            "Request Header Fields Too Large"
                    },
                    ct);
                return;
            }

            if (!headers.TryGetValue(
                    "Authorization",
                    out var authorization) ||
                ResolveAuthorizationScope(
                    authorization) is not { } accessScope)
            {
                await WriteResponseAsync(
                    ssl,
                    401,
                    new { error = "Unauthorized" },
                    ct,
                    ("WWW-Authenticate", "Bearer"));
                return;
            }

            preAuthentication.CancelAfter(
                Timeout.InfiniteTimeSpan);

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

            if (method == "GET" && path == "/api/v1/coverage")
            {
                var status = CoverageReportService.ReadStatus();
                if (status is null)
                {
                    await WriteResponseAsync(
                        ssl,
                        404,
                        new { error = "Coverage status is not available." },
                        ct);
                    return;
                }

                await WriteResponseAsync(
                    ssl,
                    status.Success ? 200 : 503,
                    new
                    {
                        status.Success,
                        status.StartedUtc,
                        status.FinishedUtc,
                        status.ErrorMessage,
                        status.DomainController,
                        status.Summary,
                        status.Policy
                    },
                    ct);
                return;
            }

            if (method == "GET" && path == "/api/v1/coverage/policy")
            {
                var status = CoverageReportService.ReadStatus();
                var policy = status is null
                    ? null
                    : new CoveragePolicyService(_config)
                        .Evaluate(status.Summary);

                await WriteResponseAsync(
                    ssl,
                    200,
                    new
                    {
                        enabled = _config.CoveragePolicyEnabled,
                        thresholds = new
                        {
                            noRecoveryKey = _config.CoveragePolicyMaxNoRecoveryKey,
                            intuneNotEncrypted = _config.CoveragePolicyMaxIntuneNotEncrypted,
                            intuneStale = _config.CoveragePolicyMaxIntuneStale,
                            oldCloudKey = _config.CoveragePolicyMaxOldCloudKey
                        },
                        severities = new
                        {
                            noRecoveryKey = CoveragePolicyService.NormalizeSeverity(
                                _config.CoveragePolicyNoRecoveryKeySeverity),
                            intuneNotEncrypted = CoveragePolicyService.NormalizeSeverity(
                                _config.CoveragePolicyIntuneNotEncryptedSeverity),
                            intuneStale = CoveragePolicyService.NormalizeSeverity(
                                _config.CoveragePolicyIntuneStaleSeverity),
                            oldCloudKey = CoveragePolicyService.NormalizeSeverity(
                                _config.CoveragePolicyOldCloudKeySeverity)
                        },
                        lastResult = policy
                    },
                    ct);
                return;
            }

            if (method == "POST" && path == "/api/v1/coverage/run")
            {
                if (!_config.RemoteApiAllowManagement)
                {
                    await WriteResponseAsync(
                        ssl,
                        403,
                        new { error = "Remote management is disabled." },
                        ct);
                    return;
                }

                if (accessScope is not RemoteApiAccessScope.Admin and
                    not RemoteApiAccessScope.CoverageRun)
                {
                    await WriteResponseAsync(
                        ssl,
                        403,
                        new
                        {
                            error =
                                "Bearer token does not have the coverage-run scope."
                        },
                        ct);
                    return;
                }

                WindowsEventLogService.TryWrite(
                    $"Remote API coverage run requested from {client.Client.RemoteEndPoint}. Scope={accessScope}.",
                    EventLogSeverity.Warning,
                    4311,
                    "RemoteAPI");

                var status = await new CoverageAutomationService(_config)
                    .RunOnceAsync(ct);

                await WriteResponseAsync(
                    ssl,
                    status.Success ? 200 : 500,
                    new
                    {
                        status.Success,
                        status.StartedUtc,
                        status.FinishedUtc,
                        status.ErrorMessage,
                        status.DomainController,
                        status.Summary,
                        status.Policy
                    },
                    ct);
                return;
            }

            if (method == "POST" && path == "/api/v1/export")
            {
                if (!_config.RemoteApiAllowManagement)
                {
                    await WriteResponseAsync(
                        ssl,
                        403,
                        new { error = "Remote management is disabled." },
                        ct);
                    return;
                }

                if (accessScope is not RemoteApiAccessScope.Admin and
                    not RemoteApiAccessScope.Export)
                {
                    await WriteResponseAsync(
                        ssl,
                        403,
                        new
                        {
                            error =
                                "Bearer token does not have the export scope."
                        },
                        ct);
                    return;
                }

                WindowsEventLogService.TryWrite(
                    $"Remote API export requested from {client.Client.RemoteEndPoint}. Scope={accessScope}.",
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

    private RemoteApiAccessScope? ResolveAuthorizationScope(
        string value)
    {
        const string prefix = "Bearer ";
        if (!value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token =
            value[prefix.Length..]
                .Trim();
        if (token.Length < 32)
            return null;

        try
        {
            var tokenBytes =
                Convert.FromBase64String(token);
            try
            {
                var actual =
                    SHA256.HashData(tokenBytes);

                if (HashMatches(
                        actual,
                        _config.RemoteApiTokenSha256))
                    return RemoteApiAccessScope.Admin;

                if (HashMatches(
                        actual,
                        _config.RemoteApiReadTokenSha256))
                    return RemoteApiAccessScope.Read;

                if (HashMatches(
                        actual,
                        _config.RemoteApiCoverageRunTokenSha256))
                    return RemoteApiAccessScope.CoverageRun;

                if (HashMatches(
                        actual,
                        _config.RemoteApiExportTokenSha256))
                    return RemoteApiAccessScope.Export;

                return null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    tokenBytes);
            }
        }
        catch
        {
            return null;
        }
    }

    private static bool HashMatches(
        ReadOnlySpan<byte> actual,
        string expectedHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex))
            return false;

        try
        {
            var expected =
                Convert.FromHexString(expectedHex);

            return actual.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(
                       actual,
                       expected);
        }
        catch
        {
            return false;
        }
    }

    internal static async Task<string?> ReadAsciiLineAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken ct)
    {
        if (maximumBytes < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes));

        var buffer =
            new byte[maximumBytes];
        var one =
            new byte[1];
        var length = 0;

        while (true)
        {
            var read =
                await stream.ReadAsync(
                    one.AsMemory(
                        0,
                        1),
                    ct);

            if (read == 0)
            {
                return length == 0
                    ? null
                    : Encoding.ASCII.GetString(
                        buffer,
                        0,
                        length);
            }

            var value =
                one[0];

            if (value == (byte)'\n')
            {
                if (length > 0 &&
                    buffer[length - 1] ==
                    (byte)'\r')
                {
                    length--;
                }

                return Encoding.ASCII.GetString(
                    buffer,
                    0,
                    length);
            }

            if (length >=
                maximumBytes)
            {
                throw new InvalidDataException(
                    $"HTTP line exceeds {maximumBytes} bytes.");
            }

            buffer[length++] =
                value;
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
            414 => "URI Too Long",
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
