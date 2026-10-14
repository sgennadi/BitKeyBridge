using System.Buffers.Binary;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Versioned, bounded, local-only protocol. This request intentionally has NO
/// user name, SID, password, recovery key or authorization-result field.
/// The service obtains the Windows identity from pipe impersonation instead.
/// </summary>
internal sealed class ProtectedAuditBrokerRequest
{
    public int Version { get; set; } = 1;
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string Operation { get; set; } = string.Empty;
    public string Computer { get; set; } = string.Empty;
    public string RecoveryId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

internal sealed class ProtectedAuditBrokerResponse
{
    public int Version { get; set; } = 1;
    public string RequestId { get; set; } = string.Empty;
    public bool Committed { get; set; }
    public string EntryHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Denied";
}

internal static class ProtectedAuditBrokerProtocol
{
    internal const string PipeName = "BitKeyBridge.ProtectedAudit.v1";
    internal const int MaxRequestBytes = 8192;
    internal const int MaxResponseBytes = 1024;

    private static readonly HashSet<string> AllowedOperations =
        new(StringComparer.Ordinal)
        {
            "ReadLapsPasswords",
            "RevealLapsPassword",
            "CopyLapsPassword",
            "RevealRecoveryKey",
            "CopyRecoveryKey"
        };

    internal static bool IsAllowedOperation(string? value) =>
        value is not null && AllowedOperations.Contains(value);

    internal static bool Validate(ProtectedAuditBrokerRequest? request)
    {
        if (request is null || request.Version != 1 ||
            !Guid.TryParseExact(request.RequestId, "N", out _) ||
            !IsAllowedOperation(request.Operation))
            return false;

        return SafeField(request.Computer, 260, required: true) &&
               SafeField(request.RecoveryId, 260) &&
               SafeField(request.Source, 96, required: true) &&
               SafeField(request.Reference, 160) &&
               SafeField(request.Reason, 512) &&
               SafeField(request.SessionId, 128);
    }

    private static bool SafeField(string? value, int limit, bool required = false) =>
        value is not null &&
        value.Length <= limit &&
        (!required || !string.IsNullOrWhiteSpace(value)) &&
        !value.Any(char.IsControl);

    internal static async Task<T> ReadAsync<T>(
        Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (length is < 2 or > MaxRequestBytes || length > maximumBytes)
            throw new InvalidDataException("Audit broker request size is invalid.");

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<T>(payload)
                ?? throw new InvalidDataException("Audit broker message is empty.");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal static async Task WriteAsync<T>(
        Stream stream, T value, int maximumBytes, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            if (payload.Length is < 2 or > MaxRequestBytes ||
                payload.Length > maximumBytes)
                throw new InvalidDataException("Audit broker message exceeds its size limit.");

            var prefix = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
            await stream.WriteAsync(prefix, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }
}
