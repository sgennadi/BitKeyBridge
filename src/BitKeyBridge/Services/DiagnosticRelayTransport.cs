using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace BitKeyBridge;

/// <summary>
/// Sends an allowlisted JSON report to an administrator-managed HTTPS relay.
/// GitHub credentials stay on the relay, never on helpdesk computers.
/// The ingest key is an organization-scoped *upload-only* key: it is not a
/// GitHub PAT and must be protected, rotated and rate-limited at the edge.
/// </summary>
internal static class DiagnosticRelayTransport
{
    internal sealed record Delivery(bool Accepted, string Message, string IssueUrl = "");

    internal static bool IsManaged(AppConfig config) =>
        config.DiagnosticRelayEnabled && config.DiagnosticRelayManagedAutoSend;

    internal static bool IsSafeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 300 ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttps &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment) &&
               uri.AbsolutePath.Equals("/v1/diagnostics", StringComparison.Ordinal) &&
               !uri.IsLoopback &&
               !uri.HostNameType.Equals(UriHostNameType.IPv4) &&
               !uri.HostNameType.Equals(UriHostNameType.IPv6);
    }

    internal static bool IsValidIngestKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length is >= 32 and <= 256 &&
        key.All(ch => ch is >= '!' and <= '~') &&
        !key.Any(ch => ch is '"' or '\\' or '\'');

    // Null means the organization has not deployed a relay. A non-null
    // failure retains the local queue and never silently falls back to
    // a public, user-authenticated GitHub CLI upload.
    internal static async Task<Delivery?> SubmitAsync(string payload)
    {
        AppConfig config;
        try
        {
            config = ConfigService.LoadAppConfig();
        }
        catch (Exception)
        {
            return new Delivery(false,
                "Managed diagnostic relay configuration is unreadable. Report remains local.");
        }

        if (!config.DiagnosticRelayEnabled)
            return null;
        if (!IsSafeEndpoint(config.DiagnosticRelayUrl) ||
            !IsValidIngestKey(config.DiagnosticRelayIngestToken))
            return new Delivery(false,
                "Managed diagnostic relay is not configured correctly. Ask IT to deploy the HTTPS endpoint and ingest key.");

        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            using var request = new HttpRequestMessage(
                HttpMethod.Post, config.DiagnosticRelayUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", config.DiagnosticRelayIngestToken);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Created)
                return new Delivery(false,
                    "Diagnostic relay did not accept the batch (HTTP " +
                    (int)response.StatusCode + "). Counters were retained for retry.");

            // Worker must acknowledge a completed GitHub Issue creation before
            // we remove counters from the user profile.
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var bounded = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var n = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (n == 0) break;
                if (bounded.Length + n > 4096)
                    return new Delivery(false, "Relay acknowledgement was invalid; counters retained.");
                bounded.Write(buffer, 0, n);
            }
            bounded.Position = 0;
            using var reply = System.Text.Json.JsonDocument.Parse(bounded);
            var root = reply.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !root.TryGetProperty("status", out var status) ||
                status.GetString() != "accepted" ||
                !root.TryGetProperty("issueUrl", out var issue) ||
                issue.ValueKind != System.Text.Json.JsonValueKind.String)
                return new Delivery(false, "Relay acknowledgement was invalid; counters retained.");

            var url = issue.GetString() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(url,
                @"^https://github[.]com/sgennadi/BitKeyBridge/issues/[1-9][0-9]*$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                return new Delivery(false, "Relay acknowledgement contained an invalid issue URL; counters retained.");

            return new Delivery(true, "Anonymous diagnostic counters delivered via managed HTTPS relay.", url);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or
                  IOException or System.Text.Json.JsonException)
        {
            return new Delivery(false,
                "Managed HTTPS relay is unreachable (" + ex.GetType().Name +
                "). Counters were retained locally.");
        }
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        foreach (var invalid in new[]
        {
            "", "http://example.org/v1/diagnostics",
            "https://example.org/v1/diagnostics?token=secret",
            "https://user:secret@example.org/v1/diagnostics",
            "https://localhost/v1/diagnostics",
            "https://192.0.2.10/v1/diagnostics",
            "https://example.org/wrong"
        })
        {
            if (IsSafeEndpoint(invalid))
                failures.Add("Relay unexpectedly allowed an unsafe endpoint.");
        }
        if (!IsSafeEndpoint("https://reports.example.org/v1/diagnostics"))
            failures.Add("Relay rejected a valid HTTPS endpoint.");
        if (!IsValidIngestKey(new string('x', 48)) ||
            IsValidIngestKey("short") ||
            IsValidIngestKey("leaked\n" + new string('x', 48)))
            failures.Add("Relay ingest-key guard is incorrect.");
        return failures;
    }
}
