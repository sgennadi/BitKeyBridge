using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Security.Cryptography;

namespace BitKeyBridge;

/// <summary>
/// Stable, secret-free reason codes for metadata lookups. Classification relies
/// on typed exceptions/status codes, never on arbitrary server error messages.
/// A negative metadata result is NOT proof that a recovery secret is absent.
/// </summary>
public enum MetadataFailureKind
{
    None,
    NoRecord,
    AccessDenied,
    SignInRequired,
    SourceOffline,
    Timeout,
    DecryptionFailed,
    RateLimited,
    Unknown
}

internal static class MetadataFailureGuidance
{
    internal static MetadataFailureKind Classify(
        Exception exception, bool decryptionOperation = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        // A transport exception may be wrapped by a service. Inspect types
        // only; do not parse an exception message that may contain secrets.
        for (Exception? current = exception; current is not null;
             current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: { } status })
                return status switch
                {
                    HttpStatusCode.NotFound => MetadataFailureKind.NoRecord,
                    HttpStatusCode.Unauthorized => MetadataFailureKind.SignInRequired,
                    HttpStatusCode.Forbidden => MetadataFailureKind.AccessDenied,
                    HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                        MetadataFailureKind.Timeout,
                    HttpStatusCode.TooManyRequests => MetadataFailureKind.RateLimited,
                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or
                        HttpStatusCode.InternalServerError => MetadataFailureKind.SourceOffline,
                    _ => MetadataFailureKind.Unknown
                };

            if (current is DirectoryOperationException { Response: { } response })
                return response.ResultCode switch
                {
                    ResultCode.NoSuchObject => MetadataFailureKind.NoRecord,
                    ResultCode.InsufficientAccessRights => MetadataFailureKind.AccessDenied,
                    (ResultCode)49 => MetadataFailureKind.SignInRequired, // LDAP invalidCredentials
                    ResultCode.TimeLimitExceeded => MetadataFailureKind.Timeout,
                    ResultCode.Unavailable => MetadataFailureKind.SourceOffline,
                    _ => MetadataFailureKind.Unknown
                };

            if (current is LdapException ldap)
                return ldap.ErrorCode switch
                {
                    32 => MetadataFailureKind.NoRecord,
                    49 => MetadataFailureKind.SignInRequired,
                    50 => MetadataFailureKind.AccessDenied,
                    85 => MetadataFailureKind.Timeout,
                    81 or 91 => MetadataFailureKind.SourceOffline,
                    _ => MetadataFailureKind.Unknown
                };

            if (current is UnauthorizedAccessException or SecurityException)
                return MetadataFailureKind.AccessDenied;
            if (current is TimeoutException or TaskCanceledException)
                return MetadataFailureKind.Timeout;
            // A certificate/key-store failure is not always a LAPS decrypt
            // failure. Callers must explicitly identify a decrypt operation.
            if (decryptionOperation && current is CryptographicException)
                return MetadataFailureKind.DecryptionFailed;
            if (current is KeyNotFoundException)
                return MetadataFailureKind.NoRecord;
            if (current is SocketException or HttpRequestException)
                return MetadataFailureKind.SourceOffline;
        }

        return MetadataFailureKind.Unknown;
    }

    internal static string Label(MetadataFailureKind kind) => kind switch
    {
        MetadataFailureKind.NoRecord => "No record visible",
        MetadataFailureKind.AccessDenied => "Access denied",
        MetadataFailureKind.SignInRequired => "Sign-in required",
        MetadataFailureKind.SourceOffline => "Source unavailable",
        MetadataFailureKind.Timeout => "Timeout",
        MetadataFailureKind.DecryptionFailed => "Decryption failed",
        MetadataFailureKind.RateLimited => "Source throttled",
        MetadataFailureKind.None => "No failure",
        _ => "Lookup failed"
    };

    internal static string NextAction(MetadataFailureKind kind, string source)
    {
        var cloud = source.Equals("Cloud", StringComparison.OrdinalIgnoreCase) ||
                    source.Equals("Entra", StringComparison.OrdinalIgnoreCase) ||
                    source.Equals("Intune", StringComparison.OrdinalIgnoreCase);
        return kind switch
        {
            MetadataFailureKind.NoRecord =>
                "Check the exact device identity, search scope and sync/backup policy. " +
                "An empty metadata response does not prove no backup exists.",
            MetadataFailureKind.AccessDenied => cloud
                ? "Check effective Graph scopes and Entra/Intune RBAC in Access Health. " +
                  "Do not request a secret or grant broader permissions automatically."
                : "Check effective Windows/AD delegated read rights in Access Health. " +
                  "Do not bypass authorization to retrieve a secret.",
            MetadataFailureKind.SignInRequired => cloud
                ? "Reconnect the intended Entra account using Device Code/MFA or the approved certificate."
                : "Reconnect with the intended domain account and confirm the current security token.",
            MetadataFailureKind.SourceOffline => cloud
                ? "Check network/TLS and Microsoft 365 service health, then retry metadata-only."
                : "Check DNS, VPN and domain controller/LDAP connectivity without relaxing TLS.",
            MetadataFailureKind.Timeout =>
                "Check source connectivity and retry the same metadata-only request once.",
            MetadataFailureKind.DecryptionFailed =>
                "Check the authorized Windows LAPS decrypt principal and GKDI/DC connectivity. " +
                "Do not log or copy encrypted material.",
            MetadataFailureKind.RateLimited =>
                "Honor Retry-After or a bounded backoff; do not retry secret requests.",
            MetadataFailureKind.None => string.Empty,
            _ => "Open sanitized diagnostics and verify source reachability and permissions. " +
                 "A failed lookup does not prove absence of a backup."
        };
    }

    internal static string ShortAction(MetadataFailureKind kind, string source) =>
        kind switch
        {
            MetadataFailureKind.NoRecord => "Verify exact device identity and scope.",
            MetadataFailureKind.AccessDenied => "Check effective rights in Access Health.",
            MetadataFailureKind.SignInRequired => "Reconnect the intended account.",
            MetadataFailureKind.SourceOffline => "Check source connectivity and service health.",
            MetadataFailureKind.Timeout => "Verify connectivity and retry metadata.",
            MetadataFailureKind.DecryptionFailed => "Check authorized LAPS decrypt access and GKDI.",
            MetadataFailureKind.RateLimited => "Retry after the source backoff.",
            MetadataFailureKind.None => string.Empty,
            _ => "Open sanitized diagnostics for the next check."
        };

    internal static string TechnicianSummary(MetadataFailureKind kind, string source) =>
        Label(kind) + ". " + NextAction(kind, source);

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        void Check(Exception exception, MetadataFailureKind expected,
            bool decrypt = false)
        {
            if (Classify(exception, decrypt) != expected)
                failures.Add("Metadata reason misclassified: " + expected);
        }

        Check(new HttpRequestException("synthetic", null, HttpStatusCode.NotFound),
            MetadataFailureKind.NoRecord);
        Check(new HttpRequestException("synthetic", null, HttpStatusCode.Forbidden),
            MetadataFailureKind.AccessDenied);
        Check(new HttpRequestException("synthetic", null, HttpStatusCode.Unauthorized),
            MetadataFailureKind.SignInRequired);
        Check(new HttpRequestException("synthetic", null, HttpStatusCode.GatewayTimeout),
            MetadataFailureKind.Timeout);
        Check(new HttpRequestException("synthetic", null, HttpStatusCode.TooManyRequests),
            MetadataFailureKind.RateLimited);
        Check(new LdapException(81), MetadataFailureKind.SourceOffline);
        Check(new LdapException(50), MetadataFailureKind.AccessDenied);
        Check(new TimeoutException(), MetadataFailureKind.Timeout);
        Check(new UnauthorizedAccessException(), MetadataFailureKind.AccessDenied);
        Check(new CryptographicException("synthetic"), MetadataFailureKind.Unknown);
        Check(new CryptographicException("synthetic"),
            MetadataFailureKind.DecryptionFailed, decrypt: true);

        foreach (var kind in Enum.GetValues<MetadataFailureKind>())
        {
            if (string.IsNullOrWhiteSpace(Label(kind)) ||
                (kind != MetadataFailureKind.None &&
                 (string.IsNullOrWhiteSpace(NextAction(kind, "AD")) ||
                  string.IsNullOrWhiteSpace(ShortAction(kind, "AD")))))
                failures.Add("Missing safe technician guidance for " + kind);
        }

        return failures;
    }
}
