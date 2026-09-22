namespace BitKeyBridge;

public sealed class PrivilegedAccessPolicyService
{
    private readonly AppConfig _config;
    private readonly string _auditPath;

    public PrivilegedAccessPolicyService(
        AppConfig config,
        string? auditPath = null)
    {
        _config = config;
        _auditPath =
            Path.GetFullPath(
                auditPath ??
                AppPaths.AuditLogFile);
    }

    public PrivilegedAccessDecision EvaluateRecoveryAccess(
        RecoveryAccessContext context,
        string computerName,
        string recoveryId,
        string source,
        AuditService? audit = null)
    {
        var decision =
            new PrivilegedAccessDecision
            {
                Allowed = true,
                Status = "Allowed",
                Message =
                    "Optional privileged-access controls are satisfied."
            };

        if (!_config.JitRecoveryEnabled &&
            !_config.TwoPersonApprovalEnabled &&
            !_config.SiemEnabled)
        {
            decision.Jit =
                new JitRecoveryStatus
                {
                    Enabled = false,
                    GrantRequired = false,
                    GrantValid = true,
                    Status = "Disabled"
                };

            decision.Approval =
                new RecoveryApprovalStatus
                {
                    Enabled = false,
                    ApprovalRequired = false,
                    Approved = true,
                    Status = "Disabled"
                };

            decision.SiemReady = true;
            decision.SiemStatus = "Disabled";

            return decision;
        }

        if (_config.JitRecoveryEnabled ||
            _config.TwoPersonApprovalEnabled)
        {
            var integrity =
                AuditIntegrityService.Verify(
                    _auditPath);

            if (!integrity.Valid)
            {
                decision.Allowed = false;
                decision.Status = "AuditInvalid";
                decision.Message =
                    "Privileged recovery controls require a valid local tamper-evident audit chain. " +
                    integrity.FirstError;
                return decision;
            }
        }

        decision.Jit =
            new JitRecoveryService(
                _config,
                auditPath:
                    _auditPath)
                .GetCurrentStatus();

        if (_config.JitRecoveryEnabled &&
            !decision.Jit.GrantValid)
        {
            decision.Allowed = false;
            decision.Status =
                "JitRequired";
            decision.Message =
                "A valid time-limited JIT recovery grant is required for the current Windows identity." +
                (string.IsNullOrWhiteSpace(
                    decision.Jit.Error)
                    ? string.Empty
                    : " " +
                      decision.Jit.Error);

            return decision;
        }

        if (_config.SiemEnabled)
        {
            var siem =
                new SiemForwardingService(
                    _config)
                    .CheckReadiness();

            decision.SiemReady =
                siem.Ready;
            decision.SiemStatus =
                siem.Ready
                    ? "Ready"
                    : string.IsNullOrWhiteSpace(
                        siem.LastError)
                        ? "NotReady"
                        : siem.LastError;

            if (_config.SiemFailClosed &&
                !siem.Ready)
            {
                decision.Allowed = false;
                decision.Status =
                    "SiemNotReady";
                decision.Message =
                    "SIEM fail-closed mode is enabled and the local SIEM delivery pipeline is not ready. " +
                    siem.LastError;

                return decision;
            }
        }
        else
        {
            decision.SiemReady =
                true;
            decision.SiemStatus =
                "Disabled";
        }

        var approvalService =
            new RecoveryApprovalService(
                _config,
                auditPath:
                    _auditPath);

        decision.Approval =
            approvalService.GetStatus(
                context.SessionId);

        if (_config.TwoPersonApprovalEnabled &&
            !decision.Approval.Approved)
        {
            if (decision.Approval.Status is
                "RequestMissing" or
                "RequestExpired" or
                "DecisionExpired")
            {
                try
                {
                    var request =
                        approvalService
                            .EnsureRequest(
                                context,
                                computerName,
                                recoveryId,
                                source,
                                audit);

                    decision.Approval =
                        approvalService
                            .GetStatus(
                                request.SessionId);
                }
                catch (Exception ex)
                {
                    decision.Allowed = false;
                    decision.Status =
                        "ApprovalRequestFailed";
                    decision.Message =
                        ex.Message;
                    return decision;
                }
            }

            if (!decision.Approval.Approved)
            {
                decision.Allowed = false;
                decision.Status =
                    decision.Approval.Status ==
                    "Denied"
                        ? "ApprovalDenied"
                        : "ApprovalPending";

                decision.Message =
                    decision.Approval.Status ==
                    "Denied"
                        ? "Recovery access was denied by the second approver."
                        : "Two-person approval is required. " +
                          $"Session ID: {context.SessionId}. " +
                          "A different authorized Windows user must approve this session before the recovery key can be accessed.";

                if (!string.IsNullOrWhiteSpace(
                        decision.Approval.Error))
                {
                    decision.Message +=
                        " " +
                        decision.Approval.Error;
                }

                return decision;
            }
        }

        decision.Allowed = true;
        decision.Status = "Allowed";
        decision.Message =
            "Privileged recovery access policy is satisfied.";

        return decision;
    }

    public PrivilegedAccessStatus GetStatus()
    {
        var status =
            new PrivilegedAccessStatus
            {
                CheckedAtUtc =
                    DateTime.UtcNow,
                JitEnabled =
                    _config.JitRecoveryEnabled,
                TwoPersonApprovalEnabled =
                    _config.TwoPersonApprovalEnabled,
                SiemEnabled =
                    _config.SiemEnabled,
                SiemFailClosed =
                    _config.SiemFailClosed
            };

        try
        {
            status.ActiveJitGrants =
                new JitRecoveryService(
                    _config,
                    auditPath:
                        _auditPath)
                    .CountActiveValidGrants();
        }
        catch
        {
            status.ActiveJitGrants = 0;
        }

        try
        {
            var approval =
                new RecoveryApprovalService(
                    _config,
                    auditPath:
                        _auditPath);

            status.PendingApprovalRequests =
                approval.CountPendingRequests();

            status.ActiveApprovals =
                approval.CountActiveApprovals();
        }
        catch
        {
            status.PendingApprovalRequests = 0;
            status.ActiveApprovals = 0;
        }

        try
        {
            var siem =
                new SiemForwardingService(
                    _config)
                    .CheckReadiness();

            status.SiemStatus =
                _config.SiemEnabled
                    ? siem.Ready
                        ? "Ready"
                        : "NotReady"
                    : "Disabled";
        }
        catch
        {
            status.SiemStatus =
                _config.SiemEnabled
                    ? "Error"
                    : "Disabled";
        }

        try
        {
            JsonStore.WriteAtomic(
                AppPaths
                    .PrivilegedAccessStatusFile,
                status);
        }
        catch
        {
        }

        return status;
    }
}
