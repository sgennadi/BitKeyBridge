using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BitKeyBridge;

public sealed class EntraSetupService : IDisposable
{
    private const string GraphAppId = "00000003-0000-0000-c000-000000000000";
    private const string DelegatedBitLockerId = "b27a61ec-b99c-4d6a-b126-c4375d08ae30";
    private const string DelegatedDeviceId = "951183d1-1a61-466f-a6d1-1fde911bfd95";
    private const string AppBitLockerId = "57f1cf28-c0c4-4ec3-9a30-19a2eaaf2f6e";
    private const string AppDeviceId = "7438b122-aefc-4978-80ed-43db9fcc7715";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly CertificateService _certificates = new();

    public async Task<EntraSetupResult> RunAsync(
        string tenant,
        string bootstrapClientId,
        string displayName,
        CloudAuthConfig? existingConfig,
        Func<DeviceCodeInfo, Task> showDeviceCode,
        IProgress<string>? progress = null,
        bool rotateCertificate = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bootstrapClientId))
            throw new ArgumentException("Bootstrap Client ID is required for first-run native Entra setup.");
        tenant = string.IsNullOrWhiteSpace(tenant) ? "organizations" : tenant.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? "BitKeyBridge" : displayName.Trim();

        progress?.Report("Requesting Entra device code...");
        var managementToken = await AcquireManagementTokenByDeviceCodeAsync(tenant, bootstrapClientId.Trim(), showDeviceCode, ct);
        var tenantId = ResolveTenantIdFromAccessToken(managementToken);
        progress?.Report($"Connected to tenant {tenantId}.");

        var graphSp = await FindServicePrincipalByAppIdAsync(managementToken, GraphAppId, ct)
            ?? throw new InvalidOperationException("Microsoft Graph service principal was not found in the tenant.");
        var graphSpId = graphSp["id"]?.GetValue<string>() ?? throw new InvalidOperationException("Graph service principal ID is missing.");

        JsonObject? application = null;
        if (!string.IsNullOrWhiteSpace(existingConfig?.ClientId))
            application = await FindApplicationByAppIdAsync(managementToken, existingConfig.ClientId, ct);
        application ??= await FindSingleApplicationByNameAsync(managementToken, displayName, ct);

        var requiredResourceAccess = BuildRequiredResourceAccess();
        if (application is null)
        {
            progress?.Report($"Creating App Registration '{displayName}'...");
            application = await GraphObjectAsync(HttpMethod.Post, managementToken, "https://graph.microsoft.com/v1.0/applications",
                new JsonObject
                {
                    ["displayName"] = displayName,
                    ["signInAudience"] = "AzureADMyOrg",
                    ["isFallbackPublicClient"] = true,
                    ["requiredResourceAccess"] = requiredResourceAccess
                }, ct);
        }
        else
        {
            progress?.Report("Updating App Registration permissions/public-client settings...");
            var appObjectId = application["id"]!.GetValue<string>();
            var mergedResourceAccess = MergeRequiredResourceAccess(application["requiredResourceAccess"] as JsonArray);
            await GraphNoContentAsync(new HttpMethod("PATCH"), managementToken,
                $"https://graph.microsoft.com/v1.0/applications/{appObjectId}",
                new JsonObject
                {
                    ["isFallbackPublicClient"] = true,
                    ["requiredResourceAccess"] = mergedResourceAccess
                }, ct);
            application = await GetApplicationByObjectIdAsync(managementToken, appObjectId, ct);
        }

        var applicationId = application["appId"]!.GetValue<string>();
        var applicationObjectId = application["id"]!.GetValue<string>();
        var servicePrincipal = await FindServicePrincipalByAppIdAsync(managementToken, applicationId, ct);
        if (servicePrincipal is null)
        {
            progress?.Report("Creating Enterprise Application (service principal)...");
            servicePrincipal = await GraphObjectAsync(HttpMethod.Post, managementToken,
                "https://graph.microsoft.com/v1.0/servicePrincipals",
                new JsonObject { ["appId"] = applicationId }, ct);
        }
        var servicePrincipalId = servicePrincipal["id"]!.GetValue<string>();

        progress?.Report("Granting Microsoft Graph application permissions...");
        await EnsureAppRoleAssignmentAsync(managementToken, servicePrincipalId, graphSpId, AppBitLockerId, ct);
        await EnsureAppRoleAssignmentAsync(managementToken, servicePrincipalId, graphSpId, AppDeviceId, ct);

        progress?.Report("Granting tenant-wide delegated admin consent...");
        await EnsureDelegatedGrantAsync(managementToken, servicePrincipalId, graphSpId,
            "BitlockerKey.Read.All Device.Read.All", ct);

        X509Certificate2 cert;
        if (!rotateCertificate && !string.IsNullOrWhiteSpace(existingConfig?.CertificateThumbprint))
        {
            try
            {
                cert = _certificates.FindByThumbprint(existingConfig.CertificateThumbprint);
                if (cert.NotAfter < DateTime.Now.AddDays(90)) throw new InvalidOperationException("Certificate is near expiry.");
            }
            catch
            {
                cert = _certificates.CreateLocalMachineCertificate(displayName, 3);
            }
        }
        else
        {
            cert = _certificates.CreateLocalMachineCertificate(displayName, 3);
        }

        progress?.Report("Uploading certificate credential to App Registration...");
        await EnsureCertificateCredentialAsync(
            managementToken,
            applicationObjectId,
            cert,
            existingConfig?.CertificateThumbprint,
            ct);

        var config = new CloudAuthConfig
        {
            TenantId = tenantId,
            ClientId = applicationId,
            Username = existingConfig?.Username ?? string.Empty,
            CertificateThumbprint = cert.Thumbprint,
            AuthMode = existingConfig?.AuthMode ?? "Password",
            BootstrapClientId = bootstrapClientId.Trim()
        };
        JsonStore.WriteAtomic(AppPaths.CloudConfigFile, config);

        var setupResult = new EntraSetupResult
        {
            TenantId = tenantId,
            DisplayName = application["displayName"]?.GetValue<string>() ?? displayName,
            ClientId = applicationId,
            ApplicationObjectId = applicationObjectId,
            ServicePrincipalId = servicePrincipalId,
            CertificateThumbprint = cert.Thumbprint,
            CertificateNotAfter = cert.NotAfter
        };
        JsonStore.WriteAtomic(Path.Combine(AppPaths.LocalConfigDirectory, "cloud_app_setup.json"), setupResult);
        progress?.Report("Entra setup completed successfully.");
        return setupResult;
    }

    private async Task<string> AcquireManagementTokenByDeviceCodeAsync(
        string tenant,
        string clientId,
        Func<DeviceCodeInfo, Task> showDeviceCode,
        CancellationToken ct)
    {
        var scopes = "https://graph.microsoft.com/Application.ReadWrite.All " +
                     "https://graph.microsoft.com/AppRoleAssignment.ReadWrite.All " +
                     "https://graph.microsoft.com/DelegatedPermissionGrant.ReadWrite.All offline_access";
        var deviceUri = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/devicecode";
        using var deviceResponse = await _http.PostAsync(deviceUri,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scopes
            }), ct);
        var deviceText = await deviceResponse.Content.ReadAsStringAsync(ct);
        if (!deviceResponse.IsSuccessStatusCode) throw new InvalidOperationException("Device-code request failed: " + ExtractError(deviceText));
        using var deviceJson = JsonDocument.Parse(deviceText);
        var root = deviceJson.RootElement;
        var deviceCode = root.GetProperty("device_code").GetString()!;
        var userCode = root.GetProperty("user_code").GetString()!;
        var verification = root.TryGetProperty("verification_uri", out var v) ? v.GetString()! : "https://microsoft.com/devicelogin";
        var message = root.TryGetProperty("message", out var m) ? m.GetString()! : $"Open {verification} and enter {userCode}.";
        var expires = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900;
        var interval = root.TryGetProperty("interval", out var i) ? i.GetInt32() : 5;

        await showDeviceCode(new DeviceCodeInfo
        {
            UserCode = userCode,
            VerificationUri = verification,
            Message = message,
            ExpiresIn = expires
        });

        var tokenUri = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/token";
        var stopAt = DateTime.UtcNow.AddSeconds(expires);
        while (DateTime.UtcNow < stopAt)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
            using var response = await _http.PostAsync(tokenUri,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode
                }), ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(text);
                return json.RootElement.GetProperty("access_token").GetString()!;
            }
            using var errorJson = JsonDocument.Parse(text);
            var error = errorJson.RootElement.TryGetProperty("error", out var er) ? er.GetString() : string.Empty;
            if (error == "authorization_pending") continue;
            if (error == "slow_down") { interval += 5; continue; }
            throw new InvalidOperationException("Device-code authentication failed: " + ExtractError(text));
        }
        throw new TimeoutException("Device-code authentication expired before sign-in completed.");
    }

    private static string ResolveTenantIdFromAccessToken(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2) throw new InvalidOperationException("The Entra access token is not a JWT.");
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += new string('=', (4 - payload.Length % 4) % 4);
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (json.RootElement.TryGetProperty("tid", out var tid) && Guid.TryParse(tid.GetString(), out var tenantId))
            return tenantId.ToString("D");
        throw new InvalidOperationException("The Entra access token does not contain a valid tenant ID (tid) claim.");
    }

    private static JsonArray BuildRequiredResourceAccess() =>
        new()
        {
            new JsonObject
            {
                ["resourceAppId"] = GraphAppId,
                ["resourceAccess"] = new JsonArray
                {
                    new JsonObject { ["id"] = DelegatedBitLockerId, ["type"] = "Scope" },
                    new JsonObject { ["id"] = DelegatedDeviceId, ["type"] = "Scope" },
                    new JsonObject { ["id"] = AppBitLockerId, ["type"] = "Role" },
                    new JsonObject { ["id"] = AppDeviceId, ["type"] = "Role" }
                }
            }
        };

    private static JsonArray MergeRequiredResourceAccess(JsonArray? existing)
    {
        var result = new JsonArray();
        if (existing is not null)
        {
            foreach (var node in existing)
                result.Add(node?.DeepClone());
        }

        var graphEntry = result.OfType<JsonObject>().FirstOrDefault(x =>
            string.Equals(x["resourceAppId"]?.GetValue<string>(), GraphAppId, StringComparison.OrdinalIgnoreCase));

        if (graphEntry is null)
        {
            graphEntry = new JsonObject
            {
                ["resourceAppId"] = GraphAppId,
                ["resourceAccess"] = new JsonArray()
            };
            result.Add(graphEntry);
        }

        var access = graphEntry["resourceAccess"] as JsonArray;
        if (access is null)
        {
            access = new JsonArray();
            graphEntry["resourceAccess"] = access;
        }

        var required = new (string Id, string Type)[]
        {
            (DelegatedBitLockerId, "Scope"),
            (DelegatedDeviceId, "Scope"),
            (AppBitLockerId, "Role"),
            (AppDeviceId, "Role")
        };

        foreach (var item in required)
        {
            var exists = access.OfType<JsonObject>().Any(x =>
                string.Equals(x["id"]?.GetValue<string>(), item.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x["type"]?.GetValue<string>(), item.Type, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                access.Add(new JsonObject { ["id"] = item.Id, ["type"] = item.Type });
        }

        return result;
    }

    private async Task<JsonObject?> FindApplicationByAppIdAsync(string token, string appId, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"appId eq '{appId.Replace("'", "''")}'");
        var result = await GraphObjectAsync(HttpMethod.Get, token,
            $"https://graph.microsoft.com/v1.0/applications?$filter={filter}&$select=id,appId,displayName,keyCredentials,requiredResourceAccess", null, ct);
        return FirstValue(result);
    }

    private async Task<JsonObject?> FindSingleApplicationByNameAsync(string token, string name, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"displayName eq '{name.Replace("'", "''")}'");
        var result = await GraphObjectAsync(HttpMethod.Get, token,
            $"https://graph.microsoft.com/v1.0/applications?$filter={filter}&$select=id,appId,displayName,keyCredentials,requiredResourceAccess", null, ct);
        if (result["value"] is not JsonArray arr || arr.Count == 0) return null;
        if (arr.Count > 1) throw new InvalidOperationException($"More than one App Registration named '{name}' exists.");
        return arr[0] as JsonObject;
    }

    private Task<JsonObject> GetApplicationByObjectIdAsync(string token, string objectId, CancellationToken ct) =>
        GraphObjectAsync(HttpMethod.Get, token,
            $"https://graph.microsoft.com/v1.0/applications/{objectId}?$select=id,appId,displayName,keyCredentials,requiredResourceAccess", null, ct);

    private async Task<JsonObject?> FindServicePrincipalByAppIdAsync(string token, string appId, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"appId eq '{appId.Replace("'", "''")}'");
        var result = await GraphObjectAsync(HttpMethod.Get, token,
            $"https://graph.microsoft.com/v1.0/servicePrincipals?$filter={filter}&$select=id,appId,displayName", null, ct);
        return FirstValue(result);
    }

    private async Task EnsureAppRoleAssignmentAsync(string token, string principalId, string resourceId, string roleId, CancellationToken ct)
    {
        var existing = await GraphObjectAsync(HttpMethod.Get, token,
            $"https://graph.microsoft.com/v1.0/servicePrincipals/{principalId}/appRoleAssignments?$top=999", null, ct);
        if (existing["value"] is JsonArray arr && arr.OfType<JsonObject>().Any(x =>
                string.Equals(x["resourceId"]?.GetValue<string>(), resourceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x["appRoleId"]?.GetValue<string>(), roleId, StringComparison.OrdinalIgnoreCase)))
            return;

        await GraphObjectAsync(HttpMethod.Post, token,
            $"https://graph.microsoft.com/v1.0/servicePrincipals/{principalId}/appRoleAssignments",
            new JsonObject
            {
                ["principalId"] = principalId,
                ["resourceId"] = resourceId,
                ["appRoleId"] = roleId
            }, ct);
    }

    private async Task EnsureDelegatedGrantAsync(string token, string clientSpId, string resourceSpId, string scopes, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"clientId eq '{clientSpId}'");
        var grants = await GraphObjectAsync(HttpMethod.Get, token,
            $"https://graph.microsoft.com/v1.0/oauth2PermissionGrants?$filter={filter}", null, ct);
        var existing = (grants["value"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x =>
            string.Equals(x["resourceId"]?.GetValue<string>(), resourceSpId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x["consentType"]?.GetValue<string>(), "AllPrincipals", StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            await GraphObjectAsync(HttpMethod.Post, token, "https://graph.microsoft.com/v1.0/oauth2PermissionGrants",
                new JsonObject
                {
                    ["clientId"] = clientSpId,
                    ["consentType"] = "AllPrincipals",
                    ["resourceId"] = resourceSpId,
                    ["scope"] = scopes
                }, ct);
            return;
        }

        var merged = (existing["scope"]?.GetValue<string>() ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Concat(scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        var mergedText = string.Join(' ', merged);
        if (!string.Equals(mergedText, existing["scope"]?.GetValue<string>(), StringComparison.Ordinal))
        {
            var id = existing["id"]!.GetValue<string>();
            await GraphNoContentAsync(new HttpMethod("PATCH"), token,
                $"https://graph.microsoft.com/v1.0/oauth2PermissionGrants/{id}",
                new JsonObject { ["scope"] = mergedText }, ct);
        }
    }

    private async Task EnsureCertificateCredentialAsync(
        string token,
        string applicationObjectId,
        X509Certificate2 newCertificate,
        string? previousManagedThumbprint,
        CancellationToken ct)
    {
        var application = await GetApplicationByObjectIdAsync(token, applicationObjectId, ct);
        var keyCredentials = application["keyCredentials"] as JsonArray ?? new JsonArray();
        var newThumb = Convert.ToBase64String(newCertificate.GetCertHash());
        var alreadyPresent = keyCredentials.OfType<JsonObject>().Any(x =>
            string.Equals(x["customKeyIdentifier"]?.GetValue<string>(), newThumb, StringComparison.Ordinal));
        if (alreadyPresent) return;

        var newCredential = new JsonObject
        {
            ["type"] = "AsymmetricX509Cert",
            ["usage"] = "Verify",
            ["key"] = Convert.ToBase64String(newCertificate.RawData),
            ["displayName"] = "BitKeyBridge " + newCertificate.Thumbprint[..Math.Min(12, newCertificate.Thumbprint.Length)],
            ["startDateTime"] = newCertificate.NotBefore.ToUniversalTime().ToString("O"),
            ["endDateTime"] = newCertificate.NotAfter.ToUniversalTime().ToString("O")
        };

        // With no existing keys, PATCH is the documented bootstrap path.
        if (keyCredentials.Count == 0)
        {
            await GraphNoContentAsync(new HttpMethod("PATCH"), token,
                $"https://graph.microsoft.com/v1.0/applications/{applicationObjectId}",
                new JsonObject { ["keyCredentials"] = new JsonArray(newCredential) }, ct);
            return;
        }

        // If all existing credentials are expired, there is no valid key that can sign
        // an addKey proof. Microsoft documents PATCH as the recovery path in that case.
        var now = DateTimeOffset.UtcNow;
        var hasValidExistingCredential = keyCredentials.OfType<JsonObject>().Any(x =>
        {
            var endText = x["endDateTime"]?.GetValue<string>();
            return DateTimeOffset.TryParse(endText, out var endTime) && endTime > now;
        });

        if (!hasValidExistingCredential)
        {
            await GraphNoContentAsync(new HttpMethod("PATCH"), token,
                $"https://graph.microsoft.com/v1.0/applications/{applicationObjectId}",
                new JsonObject { ["keyCredentials"] = new JsonArray(newCredential) }, ct);
            return;
        }

        // Preserve active credentials by using addKey. That action requires proof of
        // possession of one existing valid certificate. We only use a certificate that
        // this tool previously recorded and whose private key is available locally.
        if (string.IsNullOrWhiteSpace(previousManagedThumbprint))
            throw new InvalidOperationException(
                "The existing App Registration has active certificate credentials that are not managed by this installation. " +
                "To avoid deleting them, certificate rotation was stopped. Use a dedicated App Registration or configure the existing managed certificate thumbprint.");

        X509Certificate2 previousCertificate;
        try
        {
            previousCertificate = _certificates.FindByThumbprint(previousManagedThumbprint);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "The App Registration has an active certificate, but the previously managed private certificate is unavailable locally. " +
                "Automatic safe rotation cannot continue without risking other credentials.", ex);
        }

        var proof = CreateKeyRollingProof(applicationObjectId, previousCertificate);
        await GraphObjectAsync(HttpMethod.Post, token,
            $"https://graph.microsoft.com/v1.0/applications/{applicationObjectId}/addKey",
            new JsonObject
            {
                ["keyCredential"] = newCredential,
                ["passwordCredential"] = null,
                ["proof"] = proof
            }, ct);
    }

    private static string CreateKeyRollingProof(string applicationObjectId, X509Certificate2 signingCertificate)
    {
        var now = DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["x5t"] = Base64Url(System.Security.Cryptography.SHA1.HashData(signingCertificate.RawData))
        });
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["aud"] = "00000002-0000-0000-c000-000000000000",
            ["iss"] = applicationObjectId,
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(10).ToUnixTimeSeconds()
        });
        var unsigned = Base64Url(Encoding.UTF8.GetBytes(header)) + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
        using var rsa = signingCertificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The previous certificate RSA private key is unavailable.");
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(unsigned),
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        return unsigned + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<JsonObject> GraphObjectAsync(HttpMethod method, string token, string uri, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Graph {method} failed: {ExtractError(text)}");
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
    }

    private async Task GraphNoContentAsync(HttpMethod method, string token, string uri, JsonNode body, CancellationToken ct)
    {
        _ = await GraphObjectAsync(method, token, uri, body, ct);
    }

    private static JsonObject? FirstValue(JsonObject result)
    {
        if (result["value"] is JsonArray arr && arr.Count > 0) return arr[0] as JsonObject;
        return null;
    }

    private static string ExtractError(string text)
    {
        try
        {
            var node = JsonNode.Parse(text) as JsonObject;
            if (node?["error_description"] is JsonValue d) return d.GetValue<string>();
            if (node?["error"] is JsonObject e && e["message"] is JsonValue m) return m.GetValue<string>();
            if (node?["error"] is JsonValue ev) return ev.GetValue<string>();
        }
        catch { }
        return text.Length > 1200 ? text[..1200] : text;
    }

    public void Dispose() => _http.Dispose();
}
