using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitKeyBridge;

public sealed class SiemForwardingService
{
    private static readonly SemaphoreSlim FlushLock =
        new(
            1,
            1);

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

    private readonly AppConfig _config;
    private readonly string _outboxDirectory;
    private readonly string _statusPath;

    public SiemForwardingService(
        AppConfig config,
        string? outboxDirectory = null,
        string? statusPath = null)
    {
        _config = config;
        _outboxDirectory =
            Path.GetFullPath(
                outboxDirectory ??
                AppPaths.SiemOutboxDirectory);
        _statusPath =
            Path.GetFullPath(
                statusPath ??
                AppPaths.SiemStatusFile);
    }

    public static void TryQueueFromAudit(
        AuditEntry entry)
    {
        try
        {
            var config =
                ConfigService.LoadAppConfig();

            if (!config.SiemEnabled)
                return;

            new SiemForwardingService(
                config)
                .Queue(
                    entry);
        }
        catch
        {
            // SIEM forwarding must never expose a recovery password or
            // recursively break the local tamper-evident audit path.
        }
    }

    public void Queue(
        AuditEntry entry)
    {
        if (!_config.SiemEnabled)
            return;

        Directory.CreateDirectory(
            _outboxDirectory);

        var pending =
            CountPending();

        var maximum =
            Math.Clamp(
                _config.SiemMaxOutboxEvents,
                100,
                100000);

        if (pending >= maximum)
        {
            var status =
                ReadStatus() ??
                new SiemStatus();

            status.CheckedAtUtc =
                DateTime.UtcNow;
            status.Enabled =
                true;
            status.Mode =
                NormalizeMode(
                    _config.SiemMode);
            status.Ready =
                false;
            status.PendingEvents =
                pending;
            status.LastError =
                $"SIEM outbox limit {maximum} has been reached.";

            TryPersistStatus(
                status);

            throw new InvalidOperationException(
                status.LastError);
        }

        var siemEvent =
            new SiemEvent
            {
                TimestampUtc =
                    entry.TimestampUtc,
                Machine =
                    Sanitize(
                        entry.Host),
                User =
                    Sanitize(
                        entry.User),
                Action =
                    Sanitize(
                        entry.Action),
                Result =
                    Sanitize(
                        entry.Result),
                ComputerName =
                    Sanitize(
                        entry.ComputerName),
                RecoveryId =
                    Sanitize(
                        entry.RecoveryId),
                Source =
                    Sanitize(
                        entry.Source),
                AuthMode =
                    Sanitize(
                        entry.AuthMode),
                Reference =
                    Sanitize(
                        entry.Reference),
                Reason =
                    Sanitize(
                        entry.Reason),
                CorrelationId =
                    Sanitize(
                        entry.CorrelationId),
                AuditEntryHash =
                    Sanitize(
                        entry.EntryHash)
            };

        var filename =
            $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{siemEvent.EventId}.json";

        JsonStore.WriteAtomic(
            Path.Combine(
                _outboxDirectory,
                filename),
            siemEvent);

        var queued =
            ReadStatus() ??
            new SiemStatus();

        queued.CheckedAtUtc =
            DateTime.UtcNow;
        queued.Enabled =
            true;
        queued.Mode =
            NormalizeMode(
                _config.SiemMode);
        queued.Ready =
            true;
        queued.PendingEvents =
            pending + 1;
        queued.LastError =
            string.Empty;

        TryPersistStatus(
            queued);
    }

    public SiemStatus CheckReadiness()
    {
        var status =
            ReadStatus() ??
            new SiemStatus();

        status.CheckedAtUtc =
            DateTime.UtcNow;
        status.Enabled =
            _config.SiemEnabled;

        if (!_config.SiemEnabled)
        {
            status.Mode =
                "Disabled";
            status.Ready =
                true;
            status.PendingEvents =
                CountPending();
            status.LastError =
                string.Empty;
            return status;
        }

        status.Mode =
            NormalizeMode(
                _config.SiemMode);
        status.PendingEvents =
            CountPending();

        try
        {
            var maximum =
                Math.Clamp(
                    _config.SiemMaxOutboxEvents,
                    100,
                    100000);

            if (status.PendingEvents >=
                maximum)
            {
                throw new InvalidOperationException(
                    $"SIEM outbox limit {maximum} has been reached.");
            }

            Directory.CreateDirectory(
                _outboxDirectory);

            if (status.Mode ==
                "FileJsonl")
            {
                var path =
                    GetFileTarget();

                var directory =
                    Path.GetDirectoryName(
                        path);

                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                using var stream =
                    new FileStream(
                        path,
                        FileMode.OpenOrCreate,
                        FileAccess.Write,
                        FileShare.ReadWrite);

                stream.Seek(
                    0,
                    SeekOrigin.End);
            }
            else if (status.Mode ==
                     "Webhook")
            {
                _ =
                    GetWebhookUri();

                if (!string.IsNullOrWhiteSpace(
                        _config
                            .SiemClientCertificateThumbprint))
                {
                    _ =
                        new CertificateService()
                            .FindLocalMachineCertificate(
                                _config
                                    .SiemClientCertificateThumbprint,
                                requirePrivateKey:
                                    true,
                                requireCurrentValidity:
                                    true);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported SIEM mode '{_config.SiemMode}'.");
            }

            status.Ready =
                true;
            status.LastError =
                string.Empty;
        }
        catch (Exception ex)
        {
            status.Ready =
                false;
            status.LastError =
                Sanitize(
                    ex.Message);
        }

        TryPersistStatus(
            status);

        return status;
    }

    public async Task<SiemStatus> FlushAsync(
        CancellationToken cancellationToken =
            default)
    {
        await FlushLock.WaitAsync(
            cancellationToken);

        try
        {
            var status =
                CheckReadiness();

            status.LastAttemptUtc =
                DateTime.UtcNow;

            if (!_config.SiemEnabled)
            {
                TryPersistStatus(
                    status);
                return status;
            }

            if (!status.Ready)
            {
                TryPersistStatus(
                    status);
                return status;
            }

            var files =
                EnumerateOutboxFiles()
                    .Take(500)
                    .ToArray();

            if (files.Length == 0)
            {
                status.LastBatchCount =
                    0;
                status.PendingEvents =
                    0;
                status.LastSuccessfulFlushUtc =
                    DateTime.UtcNow;
                status.LastError =
                    string.Empty;
                TryPersistStatus(
                    status);
                return status;
            }

            var events =
                new List<SiemEvent>(
                    files.Length);

            foreach (var path in files)
            {
                try
                {
                    var item =
                        JsonStore
                            .Read<SiemEvent>(
                                path);

                    if (item is not null)
                        events.Add(item);
                }
                catch
                {
                    // Keep malformed files in the outbox for diagnostics.
                }
            }

            if (events.Count !=
                files.Length)
            {
                status.Ready =
                    false;
                status.LastError =
                    "One or more SIEM outbox events could not be parsed.";
                status.PendingEvents =
                    CountPending();
                TryPersistStatus(
                    status);
                return status;
            }

            if (status.Mode ==
                "FileJsonl")
            {
                await FlushFileAsync(
                    events,
                    cancellationToken);
            }
            else
            {
                await FlushWebhookAsync(
                    events,
                    cancellationToken);
            }

            foreach (var path in files)
            {
                File.Delete(
                    path);
            }

            status.CheckedAtUtc =
                DateTime.UtcNow;
            status.Ready =
                true;
            status.LastSuccessfulFlushUtc =
                DateTime.UtcNow;
            status.LastBatchCount =
                events.Count;
            status.PendingEvents =
                CountPending();
            status.LastError =
                string.Empty;

            TryPersistStatus(
                status);

            return status;
        }
        catch (Exception ex)
        {
            var status =
                ReadStatus() ??
                new SiemStatus();

            status.CheckedAtUtc =
                DateTime.UtcNow;
            status.LastAttemptUtc =
                DateTime.UtcNow;
            status.Enabled =
                _config.SiemEnabled;
            status.Mode =
                NormalizeMode(
                    _config.SiemMode);
            status.Ready =
                false;
            status.PendingEvents =
                CountPending();
            status.LastError =
                Sanitize(
                    ex.Message);

            TryPersistStatus(
                status);

            return status;
        }
        finally
        {
            FlushLock.Release();
        }
    }

    public SiemStatus? ReadStatus()
    {
        try
        {
            return JsonStore
                .Read<SiemStatus>(
                    _statusPath);
        }
        catch
        {
            return null;
        }
    }

    public int CountPending()
    {
        if (!Directory.Exists(
                _outboxDirectory))
        {
            return 0;
        }

        try
        {
            return Directory
                .EnumerateFiles(
                    _outboxDirectory,
                    "*.json",
                    SearchOption.TopDirectoryOnly)
                .Take(
                    Math.Clamp(
                        _config.SiemMaxOutboxEvents,
                        100,
                        100000) +
                    1)
                .Count();
        }
        catch
        {
            return int.MaxValue;
        }
    }

    public static string NormalizeMode(
        string? value)
    {
        var normalized =
            (value ?? string.Empty)
            .Trim()
            .Replace(
                "-",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "_",
                string.Empty,
                StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized switch
        {
            "file" or
            "jsonl" or
            "filejsonl" =>
                "FileJsonl",
            "webhook" or
            "https" or
            "httpwebhook" =>
                "Webhook",
            _ =>
                string.IsNullOrWhiteSpace(
                    normalized)
                    ? "FileJsonl"
                    : value!.Trim()
        };
    }

    private async Task FlushFileAsync(
        IReadOnlyList<SiemEvent> events,
        CancellationToken cancellationToken)
    {
        var path =
            GetFileTarget();

        var directory =
            Path.GetDirectoryName(
                path);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        await using var stream =
            new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                64 * 1024,
                useAsync: true);

        await using var writer =
            new StreamWriter(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false));

        foreach (var item in events)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await writer.WriteLineAsync(
                JsonSerializer.Serialize(
                    item,
                    JsonOptions));
        }

        await writer.FlushAsync(
            cancellationToken);
    }

    private async Task FlushWebhookAsync(
        IReadOnlyList<SiemEvent> events,
        CancellationToken cancellationToken)
    {
        var uri =
            GetWebhookUri();

        using var handler =
            new HttpClientHandler();

        if (!string.IsNullOrWhiteSpace(
                _config
                    .SiemClientCertificateThumbprint))
        {
            var certificate =
                new CertificateService()
                    .FindLocalMachineCertificate(
                        _config
                            .SiemClientCertificateThumbprint,
                        requirePrivateKey:
                            true,
                        requireCurrentValidity:
                            true);

            handler.ClientCertificates.Add(
                certificate);
        }

        using var client =
            new HttpClient(
                handler)
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            _config
                                .SiemWebhookTimeoutSeconds,
                            2,
                            120))
            };

        using var response =
            await client.PostAsJsonAsync(
                uri,
                events,
                JsonOptions,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"SIEM webhook returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }

    private string GetFileTarget()
    {
        var configured =
            Environment
                .ExpandEnvironmentVariables(
                    _config.SiemFilePath ??
                    string.Empty)
                .Trim();

        return Path.GetFullPath(
            string.IsNullOrWhiteSpace(
                configured)
                ? AppPaths.DefaultSiemJsonlFile
                : configured);
    }

    private Uri GetWebhookUri()
    {
        if (!Uri.TryCreate(
                _config.SiemWebhookUrl,
                UriKind.Absolute,
                out var uri))
        {
            throw new InvalidOperationException(
                "SIEM webhook URL is invalid.");
        }

        if (!string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SIEM webhook must use HTTPS.");
        }

        return uri;
    }

    private IEnumerable<string> EnumerateOutboxFiles()
    {
        if (!Directory.Exists(
                _outboxDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(
                _outboxDirectory,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .OrderBy(
                x => x,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void TryPersistStatus(
        SiemStatus status)
    {
        try
        {
            JsonStore.WriteAtomic(
                _statusPath,
                status);
        }
        catch
        {
        }
    }

    private static string Sanitize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        var redacted =
            Regex.Replace(
                value,
                @"\b\d{6}(?:-\d{6}){7}\b",
                "[REDACTED-BITLOCKER-KEY]");

        redacted =
            Regex.Replace(
                redacted,
                @"(?i)(Authorization\s*:\s*Bearer\s+)[A-Za-z0-9+/=_-]+",
                "$1[REDACTED]");

        redacted =
            Regex.Replace(
                redacted,
                @"(?i)(password\s*[=:]\s*)[^\s;,\r\n]+",
                "$1[REDACTED]");

        return redacted;
    }
}
