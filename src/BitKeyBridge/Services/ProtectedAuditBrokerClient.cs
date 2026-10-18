using System.IO.Pipes;
using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>
/// Only a LOCAL named-pipe endpoint owned by SYSTEM may acknowledge a protected
/// recovery audit. A fake user-owned pipe is always rejected before sending
/// any request. A missing/invalid reply is a hard failure, never success.
/// </summary>
internal static class ProtectedAuditBrokerClient
{
    internal static AuditEntry? TryCommit(AuditEntry entry)
    {
        if (!OperatingSystem.IsWindows() ||
            !string.Equals(entry.Action, "AuthorizeRecoveryAccess",
                StringComparison.Ordinal) ||
            !string.Equals(entry.Result, "Authorized",
                StringComparison.Ordinal))
            return null;

        var prefix = "Action=";
        if (!entry.Details.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var operation = entry.Details[prefix.Length..];
        var request = new ProtectedAuditBrokerRequest
        {
            Operation = operation,
            Computer = entry.ComputerName,
            RecoveryId = entry.RecoveryId,
            Source = entry.Source,
            Reference = entry.Reference,
            Reason = entry.Reason,
            SessionId = entry.CorrelationId
        };

        if (!ProtectedAuditBrokerProtocol.Validate(request))
            return null;

        try
        {
            return CommitAsync(request).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Protected broker audit unavailable: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Test a real SYSTEM-owned pipe, caller RBAC and a durable protected
    /// journal ACK. This writes only a bounded synthetic health event, not
    /// a secret or recovery pre-read authorization. An unprivileged caller
    /// can use this only if it belongs to the configured RecoveryReaders.
    /// </summary>
    internal static bool TryProbe()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var request = new ProtectedAuditBrokerRequest
        {
            Operation = ProtectedAuditBrokerProtocol.ProbeOperation,
            Computer = ProtectedAuditBrokerProtocol.ProbeComputer,
            Source = ProtectedAuditBrokerProtocol.ProbeSource,
            SessionId = Guid.NewGuid().ToString("N")
        };
        if (!ProtectedAuditBrokerProtocol.Validate(request))
            return false;

        try
        {
            return CommitAsync(request).GetAwaiter().GetResult() is not null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Protected broker probe denied or unavailable: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return false;
        }
    }

    private static async Task<AuditEntry?> CommitAsync(
        ProtectedAuditBrokerRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ct = timeout.Token;
        using var pipe = new NamedPipeClientStream(
            ".", ProtectedAuditBrokerProtocol.PipeName,
            ProtectedAuditBrokerServer.ClientRights,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Impersonation,
            HandleInheritability.None);

        await pipe.ConnectAsync(1500, ct);

        // Named-pipe names alone are untrusted. A low-privilege process can
        // create a fake server with the same name; it cannot own it as SYSTEM.
        var security = PipesAclExtensions.GetAccessControl(pipe);
        var owner = security.GetOwner(typeof(SecurityIdentifier))
            as SecurityIdentifier;
        if (owner is null ||
            !owner.IsWellKnown(WellKnownSidType.LocalSystemSid))
            return null;

        await ProtectedAuditBrokerProtocol.WriteAsync(
            pipe, request, ProtectedAuditBrokerProtocol.MaxRequestBytes, ct);
        var response =
            await ProtectedAuditBrokerProtocol.ReadAsync<ProtectedAuditBrokerResponse>(
                pipe, ProtectedAuditBrokerProtocol.MaxResponseBytes, ct);

        if (response.Version != 1 || !response.Committed ||
            !string.Equals(response.Status, "Committed", StringComparison.Ordinal) ||
            !string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal) ||
            response.EntryHash.Length != 64 ||
            !response.EntryHash.All(Uri.IsHexDigit))
            return null;

        return new AuditEntry
        {
            Action = request.Operation == ProtectedAuditBrokerProtocol.ProbeOperation
                ? "AuditBrokerHealthProbeCommitted"
                : "RecoveryPreReadCommitted",
            Result = "Committed",
            ComputerName = request.Computer,
            RecoveryId = request.RecoveryId,
            Source = request.Source,
            Reference = request.Reference,
            CorrelationId = request.SessionId,
            EntryHash = response.EntryHash
        };
    }
}
