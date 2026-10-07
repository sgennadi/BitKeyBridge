using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed partial class CloudGraphService
{
    private const string LapsDelegatedScopes =
        "https://graph.microsoft.com/DeviceLocalCredential.Read.All https://graph.microsoft.com/Device.Read.All";

    public async Task<LapsAccessCheckResult> CheckLapsAccessAsync(
        string token,
        string query,
        CancellationToken ct = default)
    {
        Require(
            query,
            nameof(query));

        var (
            deviceId,
            deviceName) =
            await ResolveLapsDeviceAsync(
                token,
                query,
                ct);

        var result =
            new LapsAccessCheckResult
            {
                Source = "Microsoft Entra ID",
                ComputerName = deviceName,
                ComputerId = deviceId,
                DirectoryServer = "Microsoft Graph"
            };

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Entra device resolution",
                State = LapsAccessState.Available,
                Detail =
                    $"Resolved Entra device ID {deviceId}."
            });

        var uri =
            $"https://graph.microsoft.com/v1.0/directory/deviceLocalCredentials/{deviceId}?$select=id,deviceName,lastBackupDateTime,refreshDateTime";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                uri);
        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var response =
            await _http.SendAsync(
                request,
                ct);

        if (!response.IsSuccessStatusCode)
        {
            result.Checks.Add(
                new LapsAccessCheckItem
                {
                    Name = "DeviceLocalCredential.Read.All",
                    State = LapsAccessState.Failed,
                    Detail =
                        $"Metadata-only LAPS endpoint returned HTTP {(int)response.StatusCode}. Verify DeviceLocalCredential.Read.All consent and an applicable Entra role."
                });

            return result;
        }

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "DeviceLocalCredential.Read.All",
                State = LapsAccessState.Available,
                Detail =
                    "Metadata-only deviceLocalCredentials request succeeded without requesting the credentials collection."
            });

        try
        {
            using var json =
                await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(
                        ct),
                    cancellationToken:
                        ct);

            var root =
                json.RootElement;
            var lastBackup =
                GetString(
                    root,
                    "lastBackupDateTime");
            var refresh =
                GetString(
                    root,
                    "refreshDateTime");

            result.Checks.Add(
                new LapsAccessCheckItem
                {
                    Name = "Entra LAPS backup indicator",
                    State =
                        string.IsNullOrWhiteSpace(
                            lastBackup) &&
                        string.IsNullOrWhiteSpace(
                            refresh)
                            ? LapsAccessState.NotDetected
                            : LapsAccessState.Available,
                    Detail =
                        !string.IsNullOrWhiteSpace(
                            lastBackup)
                            ? $"Last backup metadata: {lastBackup}."
                            : !string.IsNullOrWhiteSpace(
                                refresh)
                                ? $"Refresh metadata: {refresh}."
                                : "No backup/refresh metadata was returned."
                });
        }
        catch (JsonException ex)
        {
            result.Checks.Add(
                new LapsAccessCheckItem
                {
                    Name = "Entra LAPS metadata",
                    State = LapsAccessState.Failed,
                    Detail =
                        "Metadata response could not be parsed: " +
                        DiagnosticRedaction.Sanitize(
                            ex.Message)
                });
        }

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Password retrieval",
                State = LapsAccessState.NotProbed,
                Detail =
                    "Not probed. This access check intentionally omits the credentials property so no LAPS password is returned."
            });

        return result;
    }

    public async Task<List<LapsSearchResult>> SearchLapsDevicesAsync(
        string token,
        string query,
        int maximumItems = 100,
        CancellationToken ct = default)
    {
        Require(
            token,
            nameof(token));

        query =
            (query ?? string.Empty).Trim();

        if (query.Length == 0)
            return [];

        var devices =
            await GetCollectionAsync(
                token,
                "https://graph.microsoft.com/v1.0/devices?$select=deviceId,displayName&$top=999",
                50000,
                ct);

        var rows =
            devices
                .Select(
                    item =>
                        new LapsSearchResult
                        {
                            ComputerName =
                                GetString(
                                    item,
                                    "displayName"),
                            ComputerId =
                                GetString(
                                    item,
                                    "deviceId"),
                            Source =
                                "Microsoft Entra ID",
                            DirectoryServer =
                                "Microsoft Graph"
                        })
                .Where(
                    row =>
                        row.ComputerName.Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase) ||
                        SearchText.IdentifierContains(
                            row.ComputerId,
                            query))
                .OrderBy(
                    row =>
                        row.ComputerName,
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    Math.Clamp(
                        maximumItems,
                        1,
                        500))
                .ToList();

        using var limiter =
            new SemaphoreSlim(
                8,
                8);

        var metadataTasks =
            rows.Select(
                async row =>
                {
                    await limiter.WaitAsync(
                        ct);

                    try
                    {
                        var uri =
                            $"https://graph.microsoft.com/v1.0/directory/deviceLocalCredentials/{Uri.EscapeDataString(row.ComputerId)}?$select=id,deviceName,lastBackupDateTime,refreshDateTime";

                        using var request =
                            new HttpRequestMessage(
                                HttpMethod.Get,
                                uri);
                        request.Headers.Authorization =
                            new AuthenticationHeaderValue(
                                "Bearer",
                                token);

                        using var response =
                            await _http.SendAsync(
                                request,
                                ct);

                        if (!response.IsSuccessStatusCode)
                            return;

                        using var json =
                            await JsonDocument.ParseAsync(
                                await response.Content.ReadAsStreamAsync(
                                    ct),
                                cancellationToken:
                                    ct);

                        var lastBackup =
                            GetString(
                                json.RootElement,
                                "lastBackupDateTime");
                        var refresh =
                            GetString(
                                json.RootElement,
                                "refreshDateTime");

                        if (DateTime.TryParse(
                                lastBackup,
                                out var backupDate))
                        {
                            row.KeyDateUtc =
                                backupDate.ToUniversalTime();
                            row.IsLatest =
                                true;
                        }
                        else if (DateTime.TryParse(
                                     refresh,
                                     out var refreshDate))
                        {
                            row.KeyDateUtc =
                                refreshDate.ToUniversalTime();
                            row.IsLatest =
                                true;
                        }
                    }
                    catch (JsonException)
                    {
                        // Discovery remains usable even when one metadata
                        // object is malformed. Passwords are never requested.
                    }
                    finally
                    {
                        limiter.Release();
                    }
                });

        await Task.WhenAll(
            metadataTasks);

        return rows;
    }

    private async Task<(string DeviceId, string DeviceName)> ResolveLapsDeviceAsync(
        string token,
        string query,
        CancellationToken ct)
    {
        if (Guid.TryParse(
                query,
                out var id))
        {
            return (
                id.ToString("D"),
                query.Trim());
        }

        var escaped =
            query.Trim()
                .Replace(
                    "'",
                    "''");
        var filter =
            Uri.EscapeDataString(
                $"displayName eq '{escaped}'");

        var devices =
            await GetCollectionAsync(
                token,
                $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$select=deviceId,displayName&$top=2",
                2,
                ct);

        if (devices.Count == 0)
        {
            throw new InvalidOperationException(
                "The exact device name was not found in Entra ID.");
        }

        if (devices.Count > 1)
        {
            throw new InvalidOperationException(
                "Several Entra devices have this name. Enter the Entra device ID.");
        }

        var deviceId =
            GetString(
                devices[0],
                "deviceId");
        var deviceName =
            GetString(
                devices[0],
                "displayName");

        if (!Guid.TryParse(
                deviceId,
                out _))
        {
            throw new InvalidOperationException(
                "Entra returned an invalid device ID.");
        }

        return (
            deviceId,
            deviceName);
    }

    public async Task<LapsReadResult> ReadLapsPasswordsAsync(
        string token,
        string query,
        bool includeHistory,
        CancellationToken ct = default)
    {
        Require(
            query,
            nameof(query));

        var (
            deviceId,
            deviceName) =
            await ResolveLapsDeviceAsync(
                token,
                query,
                ct);

        var uri =
            $"https://graph.microsoft.com/v1.0/directory/deviceLocalCredentials/{deviceId}?$select=id,deviceName,lastBackupDateTime,refreshDateTime,credentials";

        string firstParseFailure =
            string.Empty;

        for (var attempt = 0;
             attempt < 2;
             attempt++)
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    uri);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            if (attempt > 0)
            {
                request.Headers.CacheControl =
                    new CacheControlHeaderValue
                    {
                        NoCache =
                            true,
                        NoStore =
                            true
                    };
            }

            using var response =
                await _http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Entra LAPS request failed (HTTP {(int)response.StatusCode}). " +
                    "Verify the Entra device ID, LAPS backup, DeviceLocalCredential.Read.All consent and an allowed Entra role.");
            }

            var body =
                await response.Content.ReadAsByteArrayAsync(
                    ct);

            try
            {
                using var json =
                    ParseLapsJsonResponse(
                        body,
                        response.Content.Headers.ContentType?.CharSet);

                return LapsCloudCodec.Read(
                    json.RootElement,
                    deviceId,
                    deviceName,
                    includeHistory);
            }
            catch (JsonException ex)
            {
                var metadata =
                    BuildLapsResponseMetadata(
                        response,
                        body.Length);

                if (attempt == 0)
                {
                    firstParseFailure =
                        metadata;
                    continue;
                }

                throw new InvalidOperationException(
                    "Entra returned a non-JSON LAPS response after one retry. " +
                    metadata +
                    (string.IsNullOrWhiteSpace(
                        firstParseFailure)
                        ? string.Empty
                        : " First attempt: " +
                          firstParseFailure),
                    ex);
            }
            finally
            {
                if (body.Length > 0)
                {
                    CryptographicOperations.ZeroMemory(
                        body);
                }
            }
        }

        throw new InvalidOperationException(
            "Entra LAPS response could not be read.");
    }

    private static JsonDocument ParseLapsJsonResponse(
        byte[] body,
        string? charset)
    {
        if (body.Length == 0)
        {
            throw new JsonException(
                "The response body was empty.");
        }

        var offset =
            0;

        if (body.Length >= 3 &&
            body[0] == 0xEF &&
            body[1] == 0xBB &&
            body[2] == 0xBF)
        {
            offset =
                3;
        }

        var utf16Le =
            body.Length - offset >= 2 &&
            body[offset] == 0xFF &&
            body[offset + 1] == 0xFE;
        var utf16Be =
            body.Length - offset >= 2 &&
            body[offset] == 0xFE &&
            body[offset + 1] == 0xFF;
        var declaredUtf16 =
            !string.IsNullOrWhiteSpace(
                charset) &&
            charset.Contains(
                "utf-16",
                StringComparison.OrdinalIgnoreCase);

        if (utf16Le ||
            utf16Be ||
            declaredUtf16)
        {
            Encoding encoding =
                utf16Be
                    ? Encoding.BigEndianUnicode
                    : Encoding.Unicode;

            if (utf16Le ||
                utf16Be)
            {
                offset +=
                    2;
            }

            var text =
                encoding.GetString(
                    body,
                    offset,
                    body.Length - offset)
                    .TrimStart(
                        '\uFEFF');

            return JsonDocument.Parse(
                text);
        }

        return JsonDocument.Parse(
            body.AsMemory(
                offset));
    }

    private static string BuildLapsResponseMetadata(
        HttpResponseMessage response,
        int bodyLength)
    {
        var contentType =
            response.Content.Headers.ContentType?.ToString() ??
            "(none)";

        var requestId =
            TryGetResponseHeader(
                response,
                "request-id");

        if (string.IsNullOrWhiteSpace(
                requestId))
        {
            requestId =
                TryGetResponseHeader(
                    response,
                    "client-request-id");
        }

        return
            $"HTTP {(int)response.StatusCode}; " +
            $"Content-Type={contentType}; " +
            $"Bytes={bodyLength}; " +
            $"RequestId={(string.IsNullOrWhiteSpace(requestId) ? "(none)" : requestId)}.";
    }

    private static string TryGetResponseHeader(
        HttpResponseMessage response,
        string name)
    {
        if (!response.Headers.TryGetValues(
                name,
                out var values))
        {
            return string.Empty;
        }

        return values.FirstOrDefault() ??
               string.Empty;
    }
}

