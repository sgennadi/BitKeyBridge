using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class CloudGraphService : IDisposable
{
    private readonly HttpClient _http;
    private readonly CertificateService _certificates = new();

    public CloudGraphService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BitKeyBridge/0.1");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("ocp-client-name", "BitKeyBridge");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("ocp-client-version", "0.1");
    }

    public async Task<GraphToken> AcquirePasswordTokenAsync(
        string tenantId,
        string clientId,
        string username,
        string password,
        CancellationToken ct = default)
    {
        Require(tenantId, nameof(tenantId));
        Require(clientId, nameof(clientId));
        Require(username, nameof(username));
        Require(password, nameof(password));

        var body = new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["grant_type"] = "password",
            ["username"] = username.Trim(),
            ["password"] = password,
            ["scope"] = "https://graph.microsoft.com/BitlockerKey.Read.All https://graph.microsoft.com/Device.Read.All"
        };
        return await AcquireTokenAsync(tenantId, body, "Password", username.Trim(), ct);
    }

    public async Task<GraphToken> AcquireCertificateTokenAsync(
        string tenantId,
        string clientId,
        string thumbprint,
        CancellationToken ct = default)
    {
        Require(tenantId, nameof(tenantId));
        Require(clientId, nameof(clientId));
        var cert = _certificates.FindByThumbprint(thumbprint);
        var tokenUri = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId.Trim())}/oauth2/v2.0/token";
        var assertion = CreateClientAssertion(tokenUri, clientId.Trim(), cert);
        var body = new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["grant_type"] = "client_credentials",
            ["scope"] = "https://graph.microsoft.com/.default",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion
        };
        return await AcquireTokenAsync(tenantId, body, "Certificate", null, ct);
    }

    public async Task<int> TestAccessAsync(string accessToken, CancellationToken ct = default)
    {
        using var json = await GetJsonAsync(accessToken,
            "https://graph.microsoft.com/v1.0/informationProtection/bitlocker/recoveryKeys?$top=10", ct);
        return json.RootElement.TryGetProperty("value", out var value) ? value.GetArrayLength() : 0;
    }

    public async Task<List<CloudRecoveryMetadata>> SearchAsync(string accessToken, string query, CancellationToken ct = default)
    {
        query = query.Trim();
        if (Guid.TryParse(query, out var exactId))
        {
            var meta = await GetRecoveryMetadataByIdAsync(accessToken, exactId.ToString("D"), ct);
            return meta is null ? [] : [meta];
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            var all = await GetCollectionAsync(accessToken,
                "https://graph.microsoft.com/v1.0/informationProtection/bitlocker/recoveryKeys?$top=100", 100, ct);
            return await ConvertRecoveryMetadataAsync(accessToken, all, ct);
        }

        var escaped = query.Replace("'", "''");
        var filter = Uri.EscapeDataString($"startswith(displayName,'{escaped}')");
        List<JsonElement> devices;
        try
        {
            devices = await GetCollectionAsync(accessToken,
                $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$select=id,deviceId,displayName&$top=100", 1000, ct);
        }
        catch
        {
            var allDevices = await GetCollectionAsync(accessToken,
                "https://graph.microsoft.com/v1.0/devices?$select=id,deviceId,displayName&$top=999", 50000, ct);
            devices = allDevices.Where(x =>
                GetString(x, "displayName").StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var result = new List<CloudRecoveryMetadata>();
        foreach (var device in devices)
        {
            var deviceId = GetString(device, "deviceId");
            var displayName = GetString(device, "displayName");
            if (string.IsNullOrWhiteSpace(deviceId)) continue;
            var keyFilter = Uri.EscapeDataString($"deviceId eq '{deviceId.Replace("'", "''")}'");
            var keys = await GetCollectionAsync(accessToken,
                $"https://graph.microsoft.com/v1.0/informationProtection/bitlocker/recoveryKeys?$filter={keyFilter}", 5000, ct);
            foreach (var key in keys)
                result.Add(ConvertMetadata(key, displayName));
        }
        return result;
    }

    public async Task<string> GetRecoveryKeyValueAsync(string accessToken, string recoveryId, CancellationToken ct = default)
    {
        using var json = await GetJsonAsync(accessToken,
            $"https://graph.microsoft.com/v1.0/informationProtection/bitlocker/recoveryKeys/{Uri.EscapeDataString(recoveryId)}?$select=key", ct);
        var key = GetString(json.RootElement, "key");
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Microsoft Graph returned the recovery object without the key property. Verify BitlockerKey.Read.All and admin consent.");
        return key;
    }

    private async Task<CloudRecoveryMetadata?> GetRecoveryMetadataByIdAsync(string token, string recoveryId, CancellationToken ct)
    {
        using var json = await GetJsonAsync(token,
            $"https://graph.microsoft.com/v1.0/informationProtection/bitlocker/recoveryKeys/{Uri.EscapeDataString(recoveryId)}", ct);
        var element = json.RootElement;
        var deviceId = GetString(element, "deviceId");
        var name = await GetDeviceNameAsync(token, deviceId, ct);
        return ConvertMetadata(element, name);
    }

    private async Task<List<CloudRecoveryMetadata>> ConvertRecoveryMetadataAsync(string token, List<JsonElement> keys, CancellationToken ct)
    {
        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CloudRecoveryMetadata>();
        foreach (var key in keys)
        {
            var deviceId = GetString(key, "deviceId");
            if (!cache.TryGetValue(deviceId, out var name))
            {
                name = await GetDeviceNameAsync(token, deviceId, ct);
                cache[deviceId] = name;
            }
            result.Add(ConvertMetadata(key, name));
        }
        return result;
    }

    private async Task<string> GetDeviceNameAsync(string token, string deviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return string.Empty;
        var filter = Uri.EscapeDataString($"deviceId eq '{deviceId.Replace("'", "''")}'");
        using var json = await GetJsonAsync(token,
            $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$select=deviceId,displayName&$top=1", ct);
        if (!json.RootElement.TryGetProperty("value", out var value) || value.GetArrayLength() == 0) return string.Empty;
        return GetString(value[0], "displayName");
    }

    private static CloudRecoveryMetadata ConvertMetadata(JsonElement item, string computerName)
    {
        DateTime? created = null;
        if (item.TryGetProperty("createdDateTime", out var c) && c.ValueKind == JsonValueKind.String && DateTime.TryParse(c.GetString(), out var parsed))
            created = parsed;
        return new CloudRecoveryMetadata
        {
            RecoveryId = GetString(item, "id"),
            DeviceId = GetString(item, "deviceId"),
            ComputerName = computerName,
            VolumeType = ConvertVolumeType(item.TryGetProperty("volumeType", out var v) ? v.ToString() : string.Empty),
            CreatedDateTime = created
        };
    }

    private static string ConvertVolumeType(string value) => value switch
    {
        "1" or "operatingSystemVolume" => "OS",
        "2" or "fixedDataVolume" => "Fixed Data",
        "3" or "removableDataVolume" => "Removable",
        "4" or "unknownFutureValue" => "Unknown/Future",
        _ => value
    };

    private async Task<GraphToken> AcquireTokenAsync(
        string tenantId,
        Dictionary<string, string> body,
        string authMode,
        string? username,
        CancellationToken ct)
    {
        var uri = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId.Trim())}/oauth2/v2.0/token";
        using var response = await _http.PostAsync(uri, new FormUrlEncodedContent(body), ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Microsoft Entra authentication failed: " + ExtractError(content));
        using var json = JsonDocument.Parse(content);
        var token = GetString(json.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Authentication returned no access token.");
        var expires = json.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var sec) ? sec : 3600;
        return new GraphToken
        {
            AccessToken = token,
            ExpiresAt = DateTime.Now.AddSeconds(Math.Max(60, expires - 120)),
            AuthMode = authMode,
            Username = username
        };
    }

    private async Task<JsonDocument> GetJsonAsync(string token, string uri, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Microsoft Graph request failed: " + ExtractError(content));
        return JsonDocument.Parse(content);
    }

    private async Task<List<JsonElement>> GetCollectionAsync(string token, string uri, int maximumItems, CancellationToken ct)
    {
        var result = new List<JsonElement>();
        string? next = uri;
        while (!string.IsNullOrWhiteSpace(next) && result.Count < maximumItems)
        {
            using var json = await GetJsonAsync(token, next, ct);
            if (json.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var item in value.EnumerateArray())
                {
                    result.Add(item.Clone());
                    if (result.Count >= maximumItems) break;
                }
            }
            next = json.RootElement.TryGetProperty("@odata.nextLink", out var nextLink) ? nextLink.GetString() : null;
        }
        return result;
    }

    private static string CreateClientAssertion(string tokenUri, string clientId, X509Certificate2 cert)
    {
        var x5t = Base64Url(SHA1.HashData(cert.RawData));
        var now = DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["x5t"] = x5t
        });
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["aud"] = tokenUri,
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["jti"] = Guid.NewGuid().ToString(),
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(10).ToUnixTimeSeconds()
        });
        var unsigned = Base64Url(Encoding.UTF8.GetBytes(header)) + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
        using var rsa = cert.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificate RSA private key is unavailable.");
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string GetString(JsonElement element, string property) => element.TryGetProperty(property, out var value) ? value.ToString() : string.Empty;
    private static void Require(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required."); }

    private static string ExtractError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error_description", out var d)) return d.ToString();
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message)) return message.ToString();
                return error.ToString();
            }
        }
        catch { }
        return json.Length > 1000 ? json[..1000] : json;
    }

    public void Dispose() => _http.Dispose();
}
