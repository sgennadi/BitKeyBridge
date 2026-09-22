namespace BitKeyBridge;

public sealed class RecoveryApprovalService
{
    private readonly AppConfig _config;
    private readonly string _requestsDirectory;
    private readonly string _decisionsDirectory;
    private readonly string _auditPath;

    public RecoveryApprovalService(
        AppConfig config,
        string? requestsDirectory = null,
        string? decisionsDirectory = null,
        string? auditPath = null)
    {
        _config = config;
        _requestsDirectory =
            Path.GetFullPath(
                requestsDirectory ??
                AppPaths.ApprovalRequestsDirectory);
        _decisionsDirectory =
            Path.GetFullPath(
                decisionsDirectory ??
                AppPaths.ApprovalDecisionsDirectory);
        _auditPath =
            Path.GetFullPath(
                auditPath ??
                AppPaths.AuditLogFile);
    }

    public RecoveryApprovalRequest EnsureRequest(
        RecoveryAccessContext context,
        string computerName,
        string recoveryId,
        string source,
        AuditService? audit = null)
    {
        if (!_config.TwoPersonApprovalEnabled)
        {
            throw new InvalidOperationException(
                "Two-person recovery approval is disabled.");
        }

        var sessionId =
            NormalizeId(
                context.SessionId,
                "recovery session");

        var existing =
            ReadRequest(
                sessionId);

        if (existing is not null &&
            existing.ExpiresAtUtc.ToUniversalTime() >
                DateTime.UtcNow)
        {
            if (!ValidateRequestAnchor(
                    existing,
                    out var error))
            {
                throw new InvalidOperationException(
                    "Existing recovery approval request is invalid: " +
                    error);
            }

            return existing;
        }

        var now =
            DateTime.UtcNow;

        var request =
            new RecoveryApprovalRequest
            {
                SessionId =
                    sessionId,
                Requester =
                    AuthorizationService
                        .CurrentIdentityName(),
                RequesterSid =
                    AuthorizationService
                        .CurrentIdentitySid(),
                ComputerName =
                    Sanitize(
                        computerName),
                RecoveryId =
                    Sanitize(
                        recoveryId),
                Source =
                    Sanitize(
                        source),
                Reference =
                    Sanitize(
                        context.Reference),
                Reason =
                    Sanitize(
                        context.Reason),
                CreatedAtUtc =
                    now,
                ExpiresAtUtc =
                    now.AddMinutes(
                        Math.Clamp(
                            _config.TwoPersonApprovalMinutes,
                            1,
                            1440))
            };

        if (string.IsNullOrWhiteSpace(
                request.RequesterSid))
        {
            throw new InvalidOperationException(
                "Two-person approval requires a Windows user SID.");
        }

        var writer =
            audit ??
            new AuditService(
                _auditPath);

        var entry =
            writer.Write(
                "RecoveryApprovalRequested",
                result:
                    "Pending",
                computerName:
                    request.ComputerName,
                recoveryId:
                    request.RecoveryId,
                source:
                    request.Source,
                details:
                    BuildRequestDetails(
                        request),
                reference:
                    request.Reference,
                reason:
                    request.Reason,
                correlationId:
                    request.SessionId)
            ?? throw new InvalidOperationException(
                "Recovery approval request was not saved because its audit anchor could not be written.");

        request.AuditEntryHash =
            entry.EntryHash;

        Directory.CreateDirectory(
            _requestsDirectory);

        JsonStore.WriteAtomic(
            GetRequestPath(
                sessionId),
            request);

        try
        {
            var decisionPath =
                GetDecisionPath(
                    sessionId);

            if (File.Exists(
                    decisionPath))
            {
                File.Delete(
                    decisionPath);
            }
        }
        catch
        {
        }

        return request;
    }

    public RecoveryApprovalDecision Decide(
        string sessionId,
        bool approved,
        string? comment = null,
        AuditService? audit = null)
    {
        new AuthorizationService(
            _config)
            .Demand(
                BitKeyBridgePermission.RecoveryApprove);

        var normalized =
            NormalizeId(
                sessionId,
                "recovery session");

        var request =
            ReadRequest(
                normalized)
            ?? throw new InvalidOperationException(
                "Recovery approval request was not found.");

        if (request.ExpiresAtUtc.ToUniversalTime() <=
            DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                "Recovery approval request has expired.");
        }

        if (!ValidateRequestAnchor(
                request,
                out var requestError))
        {
            throw new InvalidOperationException(
                "Recovery approval request failed audit verification: " +
                requestError);
        }

        var approver =
            AuthorizationService
                .CurrentIdentityName();

        var approverSid =
            AuthorizationService
                .CurrentIdentitySid();

        if (string.IsNullOrWhiteSpace(
                approverSid))
        {
            throw new InvalidOperationException(
                "Recovery approval requires a Windows user SID.");
        }

        if (string.Equals(
                approverSid,
                request.RequesterSid,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "The requester cannot approve their own recovery request. Sign in as a different authorized Windows user.");
        }

        var now =
            DateTime.UtcNow;

        var decision =
            new RecoveryApprovalDecision
            {
                RequestId =
                    request.RequestId,
                SessionId =
                    request.SessionId,
                RequesterSid =
                    request.RequesterSid,
                Approver =
                    approver,
                ApproverSid =
                    approverSid,
                Approved =
                    approved,
                DecidedAtUtc =
                    now,
                ExpiresAtUtc =
                    request.ExpiresAtUtc
                        .ToUniversalTime(),
                Comment =
                    Sanitize(
                        comment)
            };

        var writer =
            audit ??
            new AuditService(
                _auditPath);

        var entry =
            writer.Write(
                approved
                    ? "RecoveryApprovalGranted"
                    : "RecoveryApprovalDenied",
                result:
                    approved
                        ? "Approved"
                        : "Denied",
                computerName:
                    request.ComputerName,
                recoveryId:
                    request.RecoveryId,
                source:
                    request.Source,
                details:
                    BuildDecisionDetails(
                        decision),
                reference:
                    request.Reference,
                reason:
                    decision.Comment,
                correlationId:
                    request.SessionId)
            ?? throw new InvalidOperationException(
                "Recovery approval decision was not saved because its audit anchor could not be written.");

        decision.AuditEntryHash =
            entry.EntryHash;

        Directory.CreateDirectory(
            _decisionsDirectory);

        JsonStore.WriteAtomic(
            GetDecisionPath(
                normalized),
            decision);

        return decision;
    }

    public RecoveryApprovalStatus GetStatus(
        string sessionId)
    {
        var status =
            new RecoveryApprovalStatus
            {
                Enabled =
                    _config.TwoPersonApprovalEnabled,
                ApprovalRequired =
                    _config.TwoPersonApprovalEnabled
            };

        if (!_config.TwoPersonApprovalEnabled)
        {
            status.Status =
                "Disabled";
            status.Approved =
                true;
            return status;
        }

        var normalized =
            NormalizeId(
                sessionId,
                "recovery session");

        var request =
            ReadRequest(
                normalized);

        if (request is null)
        {
            status.Status =
                "RequestMissing";
            return status;
        }

        status.RequestFound =
            true;
        status.RequestId =
            request.RequestId;

        if (request.ExpiresAtUtc.ToUniversalTime() <=
            DateTime.UtcNow)
        {
            status.Status =
                "RequestExpired";
            status.ExpiresAtUtc =
                request.ExpiresAtUtc.ToUniversalTime();
            return status;
        }

        if (!ValidateRequestAnchor(
                request,
                out var requestError))
        {
            status.Status =
                "InvalidRequestAnchor";
            status.Error =
                requestError;
            return status;
        }

        status.RequestValid =
            true;

        var decision =
            ReadDecision(
                normalized);

        if (decision is null)
        {
            status.Status =
                "Pending";
            status.ExpiresAtUtc =
                request.ExpiresAtUtc.ToUniversalTime();
            return status;
        }

        status.DecisionFound =
            true;
        status.Approver =
            decision.Approver;
        status.ExpiresAtUtc =
            decision.ExpiresAtUtc.ToUniversalTime();

        if (decision.ExpiresAtUtc.ToUniversalTime() <=
            DateTime.UtcNow)
        {
            status.Status =
                "DecisionExpired";
            return status;
        }

        if (string.Equals(
                decision.ApproverSid,
                request.RequesterSid,
                StringComparison.OrdinalIgnoreCase))
        {
            status.Status =
                "SelfApprovalRejected";
            status.Error =
                "Requester and approver SID are identical.";
            return status;
        }

        if (!ValidateDecisionAnchor(
                request,
                decision,
                out var decisionError))
        {
            status.Status =
                "InvalidDecisionAnchor";
            status.Error =
                decisionError;
            return status;
        }

        status.Approved =
            decision.Approved;
        status.Status =
            decision.Approved
                ? "Approved"
                : "Denied";

        return status;
    }

    public RecoveryApprovalRequest? ReadRequest(
        string sessionId)
    {
        try
        {
            return JsonStore
                .Read<RecoveryApprovalRequest>(
                    GetRequestPath(
                        NormalizeId(
                            sessionId,
                            "recovery session")));
        }
        catch
        {
            return null;
        }
    }

    public RecoveryApprovalDecision? ReadDecision(
        string sessionId)
    {
        try
        {
            return JsonStore
                .Read<RecoveryApprovalDecision>(
                    GetDecisionPath(
                        NormalizeId(
                            sessionId,
                            "recovery session")));
        }
        catch
        {
            return null;
        }
    }

    public int CountPendingRequests()
    {
        if (!_config.TwoPersonApprovalEnabled ||
            !Directory.Exists(
                _requestsDirectory))
        {
            return 0;
        }

        var count = 0;

        foreach (var path in
                 Directory.EnumerateFiles(
                     _requestsDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                var request =
                    JsonStore
                        .Read<RecoveryApprovalRequest>(
                            path);

                if (request is null ||
                    request.ExpiresAtUtc.ToUniversalTime() <=
                        DateTime.UtcNow)
                {
                    continue;
                }

                var status =
                    GetStatus(
                        request.SessionId);

                if (status.Status ==
                    "Pending")
                {
                    count++;
                }
            }
            catch
            {
            }
        }

        return count;
    }

    public int CountActiveApprovals()
    {
        if (!_config.TwoPersonApprovalEnabled ||
            !Directory.Exists(
                _decisionsDirectory))
        {
            return 0;
        }

        var count = 0;

        foreach (var path in
                 Directory.EnumerateFiles(
                     _decisionsDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                var decision =
                    JsonStore
                        .Read<RecoveryApprovalDecision>(
                            path);

                if (decision is null)
                    continue;

                var status =
                    GetStatus(
                        decision.SessionId);

                if (status.Approved)
                    count++;
            }
            catch
            {
            }
        }

        return count;
    }

    private bool ValidateRequestAnchor(
        RecoveryApprovalRequest request,
        out string error)
    {
        error = string.Empty;

        var integrity =
            AuditIntegrityService.Verify(
                _auditPath);

        if (!integrity.Valid)
        {
            error =
                "Audit chain is invalid: " +
                integrity.FirstError;
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                request.AuditEntryHash))
        {
            error =
                "Request has no audit anchor.";
            return false;
        }

        var entries =
            AuditIntegrityService
                .ReadEntriesByHash(
                    _auditPath,
                    [request.AuditEntryHash],
                    out _,
                    out _);

        if (!entries.TryGetValue(
                request.AuditEntryHash,
                out var entry))
        {
            error =
                "Request audit anchor is not retained.";
            return false;
        }

        if (!string.Equals(
                entry.Action,
                "RecoveryApprovalRequested",
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Result,
                "Pending",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.CorrelationId,
                request.SessionId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.User,
                request.Requester,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.ComputerName,
                request.ComputerName,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.RecoveryId,
                request.RecoveryId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.Source,
                request.Source,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Reference,
                request.Reference,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Reason,
                request.Reason,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Details,
                BuildRequestDetails(
                    request),
                StringComparison.Ordinal))
        {
            error =
                "Request metadata does not match its audit anchor.";
            return false;
        }

        return true;
    }

    private bool ValidateDecisionAnchor(
        RecoveryApprovalRequest request,
        RecoveryApprovalDecision decision,
        out string error)
    {
        error = string.Empty;

        if (!string.Equals(
                decision.RequestId,
                request.RequestId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                decision.SessionId,
                request.SessionId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                decision.RequesterSid,
                request.RequesterSid,
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                "Decision is not bound to this request.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                decision.AuditEntryHash))
        {
            error =
                "Decision has no audit anchor.";
            return false;
        }

        var entries =
            AuditIntegrityService
                .ReadEntriesByHash(
                    _auditPath,
                    [decision.AuditEntryHash],
                    out _,
                    out _);

        if (!entries.TryGetValue(
                decision.AuditEntryHash,
                out var entry))
        {
            error =
                "Decision audit anchor is not retained.";
            return false;
        }

        var expectedAction =
            decision.Approved
                ? "RecoveryApprovalGranted"
                : "RecoveryApprovalDenied";

        var expectedResult =
            decision.Approved
                ? "Approved"
                : "Denied";

        if (!string.Equals(
                entry.Action,
                expectedAction,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Result,
                expectedResult,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.CorrelationId,
                decision.SessionId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.User,
                decision.Approver,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.Reason,
                decision.Comment,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Details,
                BuildDecisionDetails(
                    decision),
                StringComparison.Ordinal))
        {
            error =
                "Decision metadata does not match its audit anchor.";
            return false;
        }

        return true;
    }

    private static string BuildRequestDetails(
        RecoveryApprovalRequest request) =>
        $"RequestId={request.RequestId}; " +
        $"SessionId={request.SessionId}; " +
        $"RequesterSid={request.RequesterSid}; " +
        $"ExpiresAtUtc={request.ExpiresAtUtc.ToUniversalTime():O}";

    private static string BuildDecisionDetails(
        RecoveryApprovalDecision decision) =>
        $"DecisionId={decision.DecisionId}; " +
        $"RequestId={decision.RequestId}; " +
        $"SessionId={decision.SessionId}; " +
        $"RequesterSid={decision.RequesterSid}; " +
        $"ApproverSid={decision.ApproverSid}; " +
        $"Approved={decision.Approved}; " +
        $"ExpiresAtUtc={decision.ExpiresAtUtc.ToUniversalTime():O}";

    private string GetRequestPath(
        string sessionId) =>
        Path.Combine(
            _requestsDirectory,
            sessionId +
            ".json");

    private string GetDecisionPath(
        string sessionId) =>
        Path.Combine(
            _decisionsDirectory,
            sessionId +
            ".json");

    private static string NormalizeId(
        string value,
        string label)
    {
        var normalized =
            new string(
                (value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .ToArray());

        if (normalized.Length is < 16 or > 64)
        {
            throw new ArgumentException(
                $"{label} ID is invalid.");
        }

        return normalized;
    }

    private static string Sanitize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex
            .Replace(
                value,
                @"\b\d{6}(?:-\d{6}){7}\b",
                "[REDACTED-BITLOCKER-KEY]");
    }
}
