using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;

namespace BitKeyBridge;

/// <summary>
/// Local-only, SYSTEM-owned audit writer. No recovery secrets or AD credentials
/// ever cross this pipe. Both the named-pipe DACL and an impersonated client
/// token must match the explicit RecoveryReaders allowlist.
/// </summary>
internal sealed class ProtectedAuditBrokerServer
{
    private const int MaximumSessions = 4;
    private static readonly SecurityIdentifier SystemSid =
        new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier AdministratorsSid =
        new(WellKnownSidType.BuiltinAdministratorsSid, null);

    // Do not allow an administrator to accidentally expose the pipe to broad
    // principals even if they appear in the recovery readers list.
    private static readonly HashSet<string> ForbiddenBroadGroups =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "S-1-1-0",       // Everyone
            "S-1-5-11",      // Authenticated Users
            "S-1-5-32-545",  // BUILTIN\\Users
            "S-1-5-32-546",  // BUILTIN\\Guests
            "S-1-5-2",       // Network
            "S-1-5-4",       // Interactive
            "S-1-5-7"        // Anonymous Logon
        };

    // Excludes FILE_CREATE_PIPE_INSTANCE, which has the same bit as the
    // FILE_APPEND_DATA right included in the dangerous generic Write access.
    internal const PipeAccessRights ClientRights =
        PipeAccessRights.ReadData |
        PipeAccessRights.WriteData |
        PipeAccessRights.ReadAttributes |
        PipeAccessRights.ReadExtendedAttributes |
        PipeAccessRights.ReadPermissions |
        PipeAccessRights.Synchronize;

    private readonly IReadOnlyList<SecurityIdentifier> _readers;
    private readonly PipeSecurity _security;
    private readonly byte[] _machinePolicyFingerprint;
    private readonly AuditService _journal = new(AppPaths.BrokerAuditLogFile);
    private readonly SemaphoreSlim _connections = new(MaximumSessions, MaximumSessions);
    private readonly object _rateLock = new();
    private readonly Dictionary<string, Queue<DateTime>> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _seenIds = new(StringComparer.OrdinalIgnoreCase);

    internal ProtectedAuditBrokerServer(AppConfig config)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Audit broker requires Windows.");

        using var serviceIdentity = WindowsIdentity.GetCurrent();
        if (!SystemSid.Equals(serviceIdentity.User))
            throw new InvalidOperationException(
                "The protected audit broker must run as NT AUTHORITY\\SYSTEM.");

        if (!config.AuditBrokerEnabled || !config.RequireProtectedAuditForRecovery ||
            !config.RbacEnabled || config.RbacRecoveryReaders.Count == 0)
            throw new InvalidOperationException(
                "Protected audit broker requires explicit RBAC readers, " +
                "strict protected audit, and an enabled machine policy.");

        // Reject machine configuration that a local, non-admin user could
        // rewrite to place their own SID into the broker allowlist.
        if (!ProtectedAuditStoragePolicy.IsTrusted(AppPaths.AppSettingsFile))
            throw new InvalidOperationException(
                "Machine policy owner/ACL is not protected; broker will not start.");

        // An interrupted atomic rotation may have left a validated .old
        // and no current file. Restore an empty current segment ONLY in this
        // case; never initialize a missing evidence log from scratch.
        _ = ProtectedAuditArchiveService.RecoverInterruptedRotation();

        if (!ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(AppPaths.BrokerAuditLogFile))
            throw new InvalidOperationException(
                "Protected audit journal was not provisioned with secure NTFS ACLs.");

        if (!AuditIntegrityService.Verify(AppPaths.BrokerAuditLogFile).Valid)
            throw new InvalidOperationException(
                "Protected audit journal hash chain is invalid.");

        // Always resolve the policy currently persisted on disk. If it was
        // modified after the service loaded the startup AppConfig, require a
        // restart instead of accepting a stale RBAC snapshot.
        var currentPolicy = ConfigService.LoadAppConfig();
        if (!currentPolicy.AuditBrokerEnabled ||
            !currentPolicy.RequireProtectedAuditForRecovery ||
            !currentPolicy.RbacEnabled)
            throw new InvalidOperationException(
                "The persisted protected audit broker policy is disabled.");

        _readers = ResolveExplicitReaders(currentPolicy.RbacRecoveryReaders);
        _machinePolicyFingerprint = ReadMachinePolicyFingerprint();
        _security = CreatePipeSecurity(_readers);
    }

    internal static IReadOnlyList<SecurityIdentifier> ResolveExplicitReaders(
        IReadOnlyList<string> names)
    {
        if (names.Count is < 1 or > 64)
            throw new InvalidOperationException(
                "Audit broker requires 1-64 explicitly configured recovery readers.");

        var resolved = new List<SecurityIdentifier>();
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Audit broker reader is empty.");

            var sid = new SecurityIdentifier(
                AuthorizationService.ResolvePrincipalSid(name));
            if (ForbiddenBroadGroups.Contains(sid.Value))
                throw new InvalidOperationException(
                    "A broad all-users principal is forbidden for protected audit.");

            if (!resolved.Any(x => x.Equals(sid)))
                resolved.Add(sid);
        }

        return resolved;
    }

    private static PipeSecurity CreatePipeSecurity(
        IEnumerable<SecurityIdentifier> readers)
    {
        var security = new PipeSecurity();
        security.SetOwner(SystemSid);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        // Remote SMB named-pipe clients must not be treated as local Helpdesk
        // sessions, even when their domain account belongs to a reader group.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(
            SystemSid, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            AdministratorsSid, PipeAccessRights.FullControl, AccessControlType.Allow));
        foreach (var sid in readers)
        {
            // ReadData + WriteData, not generic Write or CreateNewInstance.
            security.AddAccessRule(new PipeAccessRule(
                sid, ClientRights, AccessControlType.Allow));
        }
        return security;
    }

    private NamedPipeServerStream CreateServer() =>
        NamedPipeServerStreamAcl.Create(
            ProtectedAuditBrokerProtocol.PipeName,
            PipeDirection.InOut,
            MaximumSessions,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            ProtectedAuditBrokerProtocol.MaxRequestBytes,
            ProtectedAuditBrokerProtocol.MaxResponseBytes,
            _security);

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _connections.WaitAsync(cancellationToken);
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = CreateServer();
                await pipe.WaitForConnectionAsync(cancellationToken);
                var accepted = pipe;
                pipe = null;
                _ = Task.Run(async () =>
                {
                    try { await ProcessSessionAsync(accepted, cancellationToken); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "Audit broker session: " +
                            DiagnosticRedaction.Sanitize(ex.Message));
                    }
                    finally
                    {
                        await accepted.DisposeAsync();
                        _connections.Release();
                    }
                }, CancellationToken.None);
            }
            catch
            {
                pipe?.Dispose();
                _connections.Release();
                if (cancellationToken.IsCancellationRequested)
                    break;
                throw; // fail closed instead of silently dropping the writer
            }
        }
    }

    private async Task ProcessSessionAsync(
        NamedPipeServerStream pipe, CancellationToken serviceCancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var ct = timeout.Token;

        var request = await ProtectedAuditBrokerProtocol.ReadAsync<ProtectedAuditBrokerRequest>(
            pipe, ProtectedAuditBrokerProtocol.MaxRequestBytes, ct);

        var response = new ProtectedAuditBrokerResponse
        {
            RequestId = request.RequestId
        };

        if (ProtectedAuditBrokerProtocol.Validate(request))
        {
            // Impersonation is synchronous and identity-only. Return to SYSTEM
            // before any file access. A caller cannot claim its own SID/name.
            string actorName = string.Empty;
            string actorSid = string.Empty;
            bool allowed = false;
            pipe.RunAsClient(() =>
            {
                using var identity = WindowsIdentity.GetCurrent();
                actorName = identity.Name;
                actorSid = identity.User?.Value ?? string.Empty;
                var principal = new WindowsPrincipal(identity);
                allowed = identity.User is not null &&
                    _readers.Any(sid =>
                        identity.User.Equals(sid) ||
                        principal.IsInRole(sid));
            });

            // Do not keep trusting a policy file that becomes writable after
            // service startup. Every ACK is conditioned on current NTFS trust.
            if (allowed &&
                ProtectedAuditStoragePolicy.IsTrusted(AppPaths.AppSettingsFile) &&
                MachinePolicyUnchanged() &&
                ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(
                    AppPaths.BrokerAuditLogFile) &&
                AcceptRequest(actorSid, request.RequestId))
            {
                var entry = _journal.WriteBrokerAuthorizedRequest(
                    request, actorName, actorSid);
                if (entry is not null &&
                    entry.EntryHash.Length == 64 &&
                    entry.EntryHash.All(Uri.IsHexDigit))
                {
                    response.Committed = true;
                    response.EntryHash = entry.EntryHash;
                    response.Status = "Committed";
                }
            }
        }

        await ProtectedAuditBrokerProtocol.WriteAsync(
            pipe, response, ProtectedAuditBrokerProtocol.MaxResponseBytes, ct);
    }

    private static byte[] ReadMachinePolicyFingerprint()
    {
        using var input = new FileStream(
            AppPaths.AppSettingsFile, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        return SHA256.HashData(input);
    }

    private bool MachinePolicyUnchanged()
    {
        try
        {
            var current = ReadMachinePolicyFingerprint();
            return CryptographicOperations.FixedTimeEquals(
                current, _machinePolicyFingerprint);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Protected audit policy change check failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
            return false; // Fail closed when the file cannot be re-read.
        }
    }

    private bool AcceptRequest(string sid, string requestId)
    {
        lock (_rateLock)
        {
            var now = DateTime.UtcNow;
            foreach (var id in _seenIds.Where(pair => pair.Value < now.AddMinutes(-10))
                         .Select(pair => pair.Key).ToArray())
                _seenIds.Remove(id);

            if (_seenIds.ContainsKey(requestId))
                return false;

            if (!_attempts.TryGetValue(sid, out var recent))
            {
                recent = new Queue<DateTime>();
                _attempts.Add(sid, recent);
            }
            while (recent.Count > 0 && recent.Peek() < now.AddMinutes(-1))
                recent.Dequeue();

            if (recent.Count >= 60 || _seenIds.Count >= 4096)
                return false;

            recent.Enqueue(now);
            _seenIds.Add(requestId, now);
            return true;
        }
    }
}
