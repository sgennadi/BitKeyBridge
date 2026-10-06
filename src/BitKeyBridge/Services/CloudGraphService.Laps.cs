using System.Net.Http.Headers;
using System.Text.Json;

namespace BitKeyBridge;

public sealed partial class CloudGraphService
{
    private const string LapsDelegatedScopes =
        "https://graph.microsoft.com/DeviceLocalCredential.Read.All https://graph.microsoft.com/Device.Read.All";

    public async Task<LapsReadResult> ReadLapsPasswordsAsync(string token, string query,
        bool includeHistory, CancellationToken ct = default)
    {
        Require(query, nameof(query));
        string deviceId;
        string deviceName = query.Trim();
        if (Guid.TryParse(query, out var id)) deviceId = id.ToString("D");
        else
        {
            var filter = Uri.EscapeDataString($"displayName eq '{query.Trim().Replace("'", "''")}'");
            var devices = await GetCollectionAsync(token,
                $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$select=deviceId,displayName&$top=2", 2, ct);
            if (devices.Count == 0) throw new InvalidOperationException("The exact device name was not found in Entra ID.");
            if (devices.Count > 1) throw new InvalidOperationException("Several Entra devices have this name. Enter the Entra device ID.");
            deviceId = GetString(devices[0], "deviceId");
            deviceName = GetString(devices[0], "displayName");
            if (!Guid.TryParse(deviceId, out _)) throw new InvalidOperationException("Entra returned an invalid device ID.");
        }

        var uri = $"https://graph.microsoft.com/v1.0/directory/deviceLocalCredentials/{deviceId}?$select=id,deviceName,lastBackupDateTime,refreshDateTime,credentials";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Entra LAPS request failed (HTTP {(int)response.StatusCode}). " +
                "Verify the Entra device ID, LAPS backup, DeviceLocalCredential.Read.All consent and an allowed Entra role.");
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return LapsCloudCodec.Read(json.RootElement, deviceId, deviceName, includeHistory);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Entra returned an invalid LAPS response.");
        }
    }
}

