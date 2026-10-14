namespace BitKeyBridge;

/// <summary>
/// Centralized recovery authorization, mandatory pre-audit and helpdesk access
/// context, shared by LAPS, BitLocker and Microsoft Graph recovery surfaces.
/// </summary>
public sealed partial class MainForm
{
    private AuditEntry? WriteRecoveryAudit(
        string action,
        RecoveryAccessContext context,
        string result = "Success",
        string? computerName = null,
        string? recoveryId = null,
        string? source = null,
        string? authMode = null,
        string? details = null,
        bool rotationRequested = false,
        bool rotationSucceeded = false)
    {
        var entry = _audit.Write(
            action,
            result,
            computerName,
            recoveryId,
            source,
            authMode,
            details,
            context.Reference,
            context.Reason,
            context.SessionId);

        try
        {
            _ = new RecoveryIncidentService()
                .Append(
                    context,
                    entry,
                    rotationRequested,
                    rotationSucceeded);
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                $"Recovery incident bundle update failed. Session={context.SessionId}; Action={action}; Error={ex.Message}",
                EventLogSeverity.Warning,
                4538,
                "RecoveryIncident");
        }

        return entry;
    }

    private bool AuthorizeAction(
        BitKeyBridgePermission permission,
        string action,
        string? computerName = null,
        string? recoveryId = null,
        string? source = null)
    {
        var decision = new AuthorizationService(_config).Check(permission);
        if (decision.Allowed)
            return true;

        _audit.Write(
            action,
            "Denied",
            computerName,
            recoveryId,
            source,
            details:
                $"Permission={decision.Permission}; Identity={decision.Identity}; Reason={decision.Reason}");

        WindowsEventLogService.TryWrite(
            $"RBAC denied {action}. Permission={decision.Permission}; Identity={decision.Identity}; Computer={computerName}; RecoveryId={recoveryId}; Source={source}.",
            EventLogSeverity.Warning,
            4501,
            "RBAC");

        ShowAppMessage(
                "BitKeyBridge RBAC",
                $"Access denied for {permission}.{Environment.NewLine}{Environment.NewLine}" +
            $"Windows identity: {decision.Identity}{Environment.NewLine}" +
            decision.Reason);

        return false;
    }

    private bool AuthorizePrivilegedRecoveryAccess(
        RecoveryAccessContext context,
        string action,
        string computerName,
        string recoveryId,
        string source)
    {
        PrivilegedAccessDecision decision;

        try
        {
            decision =
                new PrivilegedAccessPolicyService(
                    _config)
                    .EvaluateRecoveryAccess(
                        context,
                        computerName,
                        recoveryId,
                        source,
                        _audit);
        }
        catch (Exception ex)
        {
            decision =
                new PrivilegedAccessDecision
                {
                    Allowed = false,
                    Status =
                        "PolicyError",
                    Message =
                        ex.Message
                };
        }

        if (decision.Allowed)
        {
            // Record the authorization decision BEFORE reading any AD/Entra
            // secret. A lost audit write must never silently expose a password.
            // The central protected audit is mandatory in enterprise mode.
            var authorizationEntry = _audit.Write(
                "AuthorizeRecoveryAccess",
                result: "Authorized",
                computerName: computerName,
                recoveryId: recoveryId,
                source: source,
                details: "Action=" + action,
                reference: context.Reference,
                reason: context.Reason,
                correlationId: context.SessionId,
                requireProtectedStorage: _config.RequireProtectedAuditForRecovery,
                preferBroker: _config.RequireProtectedAuditForRecovery &&
                              _config.AuditBrokerEnabled);

            if (authorizationEntry is null)
            {
                ShowAppMessage(
                    "Recovery access blocked: audit is unavailable",
                    "The authorization event could not be saved. No recovery secret will be read. " +
                    (_config.RequireProtectedAuditForRecovery
                        ? _config.AuditBrokerEnabled
                            ? "Protected audit broker is unavailable or rejected this Windows account. " +
                              "Ask an administrator to check the BitKeyBridge service, RBAC RecoveryReaders, " +
                              "broker provisioning and protected journal; no UAC is needed in this GUI."
                            : "Protected auditing is mandatory: the machine journal must already exist " +
                              "with a trusted SYSTEM/Administrators/service owner and restricted NTFS write ACL. " +
                              "Your account also needs an approved protected writer or service."
                        : "Check machine audit write access and the current-user portable audit folder.") +
                    " Do not weaken AD/Entra permissions or launch the entire GUI elevated.");
                WindowsEventLogService.TryWrite(
                    "Recovery access denied because required audit storage failed. " +
                    "Action=" + action + "; Computer=" + computerName +
                    "; RecoveryId=" + recoveryId + "; Source=" + source,
                    EventLogSeverity.Error, 4682, "RecoveryAudit");
                return false;
            }

            if (_audit.LastWriteUsedUserFallback && !_userAuditFallbackWarningShown)
            {
                _userAuditFallbackWarningShown = true;
                ShowAppMessage(
                    "Portable audit journal in use",
                    "The machine-protected audit journal is unavailable to your account. " +
                    "Access events are being saved under your user profile. This fallback is " +
                    "not tamper-proof: use a managed service or enable 'Require protected audit' " +
                    "for centrally enforced enterprise audit policy.");
            }

            return true;
        }

        WriteRecoveryAudit(
            action,
            context,
            result:
                "Denied",
            computerName:
                computerName,
            recoveryId:
                recoveryId,
            source:
                source,
            details:
                $"PrivilegedPolicy={decision.Status}; JIT={decision.Jit.Status}; Approval={decision.Approval.Status}; SIEM={decision.SiemStatus}; Message={decision.Message}");

        WindowsEventLogService.TryWrite(
            $"Privileged recovery policy denied {action}. Status={decision.Status}; " +
            $"Computer={computerName}; RecoveryId={recoveryId}; Source={source}; " +
            $"Session={context.SessionId}.",
            EventLogSeverity.Warning,
            4651,
            "PrivilegedAccess");

        var message =
            decision.Message;

        if (decision.Status ==
            "ApprovalPending")
        {
            message +=
                Environment.NewLine +
                Environment.NewLine +
                "Session ID:" +
                Environment.NewLine +
                context.SessionId +
                Environment.NewLine +
                Environment.NewLine +
                "A second authorized Windows user can approve this session from BitKeyBridge Privileged Access settings or with --approval-approve.";
        }

        ShowAppMessage(
                "Privileged Recovery Access",
                message);

        return false;
    }

    private RecoveryAccessContext? GetOrRequestRecoveryAccessContext(
        string source,
        string recoveryId,
        string computerName,
        bool allowRotationReminder)
    {
        _ = allowRotationReminder;

        var normalizedId = string.IsNullOrWhiteSpace(recoveryId)
            ? computerName
            : recoveryId;
        var cacheKey = source + ":" + normalizedId;
        var computerCacheKey =
            source + ":computer:" + computerName.Trim();

        if (_recoveryAccessContexts.TryGetValue(
                cacheKey,
                out var existing))
        {
            return existing;
        }

        if (_recoveryAccessContexts.TryGetValue(
                computerCacheKey,
                out existing))
        {
            _recoveryAccessContexts[cacheKey] =
                existing;
            return existing;
        }

        // Rotation reminders are intentionally post-recovery and are never a
        // reason to interrupt secret retrieval with a ticket dialog.
        if (!_config.RequireRecoveryAccessReference)
        {
            var empty =
                new RecoveryAccessContext();
            _recoveryAccessContexts[cacheKey] =
                empty;
            return empty;
        }

        using var dialog =
            new RecoveryAccessDialog(
                computerName,
                normalizedId,
                requireReference:
                    true,
                referencePattern:
                    _config.RecoveryReferencePattern,
                referenceExample:
                    _config.RecoveryReferenceExample);

        if (dialog.ShowDialog(this) !=
            DialogResult.OK)
        {
            return null;
        }

        var context =
            dialog.Context;

        _recoveryAccessContexts[cacheKey] =
            context;

        if (context.RememberForComputer)
        {
            _recoveryAccessContexts[computerCacheKey] =
                context;
        }

        return context;
    }

}
