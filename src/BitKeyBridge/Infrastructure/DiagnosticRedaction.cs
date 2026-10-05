using System.Text.RegularExpressions;

namespace BitKeyBridge;

public static class DiagnosticRedaction
{
    private const string Redacted = "[REDACTED]";

    private static readonly Regex BitLockerRecoveryPasswordRegex =
        new(
            @"\b\d{6}(?:-\d{6}){7}\b",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex AuthorizationHeaderRegex =
        new(
            @"(?im)(\bAuthorization\s*:\s*(?:Bearer|Basic)\s+)[^\s,\r\n]+",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex JsonSecretRegex =
        new(
            @"(?i)(""(?:(?:remoteapi(?:read|coveragerun|export)?tokensha256)|password|passwd|pwd|token|access[_-]?token|refresh[_-]?token|client[_-]?secret|authorization|account[_-]?key|shared[_-]?access[_-]?signature|sig|signature)""\s*:\s*"")[^""]*("")",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex KeyValueSecretRegex =
        new(
            @"(?i)\b(password|passwd|pwd|token|access[_-]?token|refresh[_-]?token|client[_-]?secret|account[_-]?key|shared[_-]?access[_-]?signature|sig|signature)\b(\s*[=:]\s*)(?:""[^""]*""|'[^']*'|[^\s;,\r\n]+)",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex QuerySecretRegex =
        new(
            @"(?i)([?&](?:token|access_token|refresh_token|client_secret|code|sig|signature|password|key)=)[^&#\s]+",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex UriUserInfoRegex =
        new(
            @"(?i)(https?://[^/\s:@]+:)[^@\s/]+@",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex JwtRegex =
        new(
            @"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var redacted =
            BitLockerRecoveryPasswordRegex.Replace(
                value,
                "[REDACTED-BITLOCKER-KEY]");

        redacted =
            AuthorizationHeaderRegex.Replace(
                redacted,
                "$1" + Redacted);

        redacted =
            JsonSecretRegex.Replace(
                redacted,
                "$1" + Redacted + "$2");

        redacted =
            KeyValueSecretRegex.Replace(
                redacted,
                "$1$2" + Redacted);

        redacted =
            QuerySecretRegex.Replace(
                redacted,
                "$1" + Redacted);

        redacted =
            UriUserInfoRegex.Replace(
                redacted,
                "$1" + Redacted + "@");

        redacted =
            JwtRegex.Replace(
                redacted,
                "[REDACTED-JWT]");

        return redacted;
    }

    public static string SanitizeUriForDiagnostics(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
        {
            return Sanitize(
                value);
        }

        var host =
            uri.Host.Contains(
                ':',
                StringComparison.Ordinal)
                ? "[" + uri.Host + "]"
                : uri.Host;

        var authority =
            uri.IsDefaultPort
                ? host
                : host + ":" + uri.Port;

        return uri.Scheme +
               "://" +
               authority +
               "/[REDACTED]";
    }
}
