using System.Text.RegularExpressions;

namespace BitKeyBridge;

/// <summary>
/// Source-qualified diagnostic hints. A failed metadata query cannot prove
/// that a key was never backed up, that delegated permissions are absent, or
/// that changing access would be safe. This layer NEVER changes permissions,
/// obtains secrets, or uses external endpoints.
/// </summary>
internal static class AccessHealthGuidanceService
{
    internal sealed record Hint(string Category, string NextCheck);

    internal static string? SafeAdComputerName(HomeSearchResult? row)
    {
        if (row is null || row.IsCloudRecord || row.IdentityAmbiguous)
            return null;
        var name = (row.ComputerName ?? string.Empty).Trim();
        if (name.Length is 0 or > 253 || name.Any(char.IsControl))
            return null;
        return name;
    }

    internal static string Enrich(string source, string check,
        string state, string detail)
    {
        var safe = DiagnosticRedaction.Sanitize(detail ?? string.Empty);
        var hint = Analyze(source, check, state, safe);
        if (hint is null)
            return safe;
        return safe + " | Next check [" + hint.Category + "]: " + hint.NextCheck;
    }

    internal static Hint? Analyze(string source, string check,
        string state, string detail)
    {
        // Successful, intentionally-unprobed and informational metadata
        // checks must not be presented as access problems.
        if (state.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Available", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("NotProbed", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Info", StringComparison.OrdinalIgnoreCase))
            return null;

        var problem = state.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
                      state.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
                      state.Equals("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
                      state.Equals("DecryptionFailed", StringComparison.OrdinalIgnoreCase) ||
                      state.Equals("NotDetected", StringComparison.OrdinalIgnoreCase);
        if (!problem)
            return null;

        bool Is(string a) => string.Equals(source, a, StringComparison.OrdinalIgnoreCase);
        bool Check(string word) => check.Contains(word, StringComparison.OrdinalIgnoreCase);
        bool Detail(string word) => detail.Contains(word, StringComparison.OrdinalIgnoreCase);
        var graph = Is("Entra") || Is("Intune") || Is("Entra LAPS") || Is("Cloud");
        if (graph)
        {
            var code = HttpStatusFromText(detail);
            if (code == 401)
                return new Hint("Graph authentication",
                    "Reconnect the intended tenant/user or app session; confirm token audience and expiration. This is not evidence that a device has no backup.");
            if (code == 429)
                return new Hint("Graph throttling",
                    "Honor Retry-After/backoff and retry the metadata check later. Do not add permissions to fix throttling.");
            if (code is 500 or 502 or 503 or 504)
                return new Hint("Graph service",
                    "Check Microsoft 365 service health and repeat later; a service error is not evidence of denied permissions.");

            if (Is("Entra LAPS") && (code == 403 || Check("Credential") ||
                Check("LAPS") || Check("Metadata")))
                return new Hint("Entra LAPS permissions",
                    "Check DeviceLocalCredential.Read.All consent and the user's applicable Entra role. Metadata access does not grant password retrieval.");

            if (Is("Intune"))
                return new Hint("Intune permissions",
                    "Check DeviceManagementManagedDevices.Read.All and effective Intune RBAC/device scope, plus enrollment; do not assume Graph consent alone is sufficient.");

            if (Is("Entra") && Check("BitLocker"))
                return new Hint("Entra BitLocker metadata permissions",
                    "Check BitlockerKey.ReadBasic.All metadata permission and admin consent. Secret retrieval requires separate BitlockerKey.Read.All authorization.");

            if (code == 403)
                return new Hint("Graph authorization",
                    "Check Graph scope/app-role consent and applicable directory/RBAC role for this exact endpoint. Do not expose or retry a secret request.");
            return null;
        }

        if (Is("RBAC"))
            return new Hint("Windows token RBAC",
                "Check effective RecoveryReaders/Rotate group SIDs and current enabled Windows token. Re-sign in after membership changes; do not rely on administrator bypass.");

        if (Is("AD") && Check("LDAP"))
            return new Hint("Active Directory connection",
                "Check preferred DC, DNS/network/LDAP reachability, authentication mode and the configured AD account.");
        if (Is("AD") && Check("BitLocker"))
            return new Hint("AD BitLocker delegation",
                "Inspect this computer's msFVE-RecoveryInformation child objects and delegated read access. An LDAP error or empty metadata result does not prove no backup.");

        if (Is("AD LAPS") &&
            (Check("Decrypt") || Check("encrypted") ||
             Detail("decrypt") || Detail("msLAPS-Encrypted")))
            return new Hint("Windows LAPS decrypt",
                "Check effective LAPS authorized decrypt principals and directory encryption policy; reading LAPS metadata does not grant decrypt rights.");

        if (Is("AD LAPS"))
            return new Hint("AD LAPS delegation",
                "Check the exact AD computer object, LAPS schema/backup policy and attribute-read delegation; a failed metadata check does not prove password absence.");

        if (Is("Audit"))
            return new Hint("Protected Audit Broker",
                "Check strict audit policy, explicit RecoveryReaders, LocalSystem service health, protected NTFS ACL and journal chain. Do not relax the journal or bypass the durable ACK.");
        return null;
    }

    private static int? HttpStatusFromText(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return null;
        // Handles typed Graph messages like "(HTTP 403)" and the
        // metadata-only LAPS endpoint's "returned HTTP 403".
        var match = Regex.Match(detail,
            @"\bHTTP\s*[:(]?\s*(401|403|429|500|502|503|504)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return match.Success && int.TryParse(match.Groups[1].Value, out var code)
            ? code : null;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        void AssertCategory(string source, string check, string state,
            string detail, string expected)
        {
            var actual = Analyze(source, check, state, detail)?.Category;
            if (actual != expected)
                failures.Add("Access Health diagnostic classification failed for " +
                    source + "/" + check + ": " + (actual ?? "none"));
        }

        AssertCategory("RBAC", "RecoveryRead (current token)", "Failed",
            "Denied", "Windows token RBAC");
        AssertCategory("AD", "BitLocker metadata", "Failed",
            "Access denied", "AD BitLocker delegation");
        AssertCategory("AD LAPS", "msLAPS-EncryptedPassword", "AccessDenied",
            "decrypt unauthorized", "Windows LAPS decrypt");
        AssertCategory("Entra", "BitLocker metadata", "Failed",
            "Graph failed (HTTP 403)", "Entra BitLocker metadata permissions");
        AssertCategory("Entra LAPS", "DeviceLocalCredential.Read.All", "Failed",
            "LAPS endpoint returned HTTP 403", "Entra LAPS permissions");
        AssertCategory("Intune", "Managed device", "Failed",
            "HTTP 403", "Intune permissions");
        AssertCategory("Intune", "Managed device", "Failed",
            "HTTP 429", "Graph throttling");
        AssertCategory("Entra LAPS", "Metadata access", "Warning",
            "HTTP 503", "Graph service");
        AssertCategory("Cloud", "Authentication", "Failed",
            "HTTP 401", "Graph authentication");
        AssertCategory("Audit", "Journal owner / NTFS ACL", "Failed",
            "Not trusted", "Protected Audit Broker");
        if (Analyze("RBAC", "RecoveryRead", "OK", "passed") is not null ||
            Analyze("Entra LAPS", "Password retrieval", "NotProbed",
                "Intentionally no password read") is not null ||
            Analyze("Entra", "BitLocker metadata", "Info", "No key read") is not null)
            failures.Add("Access Health incorrectly recommended permissions for success/not-probed checks.");

        var ad = new HomeSearchResult { ComputerName = "PC-1" };
        if (SafeAdComputerName(ad) != "PC-1")
            failures.Add("Healthy selected AD computer was not carried to Access Health.");
        ad.IdentityAmbiguous = true;
        if (SafeAdComputerName(ad) is not null)
            failures.Add("Ambiguous AD object was copied to diagnostic lookup.");
        ad.IdentityAmbiguous = false;
        ad.IsCloudRecord = true;
        if (SafeAdComputerName(ad) is not null)
            failures.Add("Cloud hostname was silently reused as an AD object.");
        ad.IsCloudRecord = false;
        ad.ComputerName = "PC-1\nPC-2";
        if (SafeAdComputerName(ad) is not null)
            failures.Add("Invalid computer control characters were copied into diagnostic query.");
        return failures;
    }
}
