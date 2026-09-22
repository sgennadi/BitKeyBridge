namespace BitKeyBridge;

public sealed class JitRecoveryGrant
{
    public int Version { get; set; } = 1;
    public string GrantId { get; set; } = Guid.NewGuid().ToString("N");
    public string Subject { get; set; } = string.Empty;
    public string SubjectSid { get; set; } = string.Empty;
    public string GrantedBy { get; set; } = string.Empty;
    public string GrantedBySid { get; set; } = string.Empty;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AuditEntryHash { get; set; } = string.Empty;
}

public sealed class JitRecoveryStatus
{
    public bool Enabled { get; set; }
    public bool GrantRequired { get; set; }
    public bool GrantFound { get; set; }
    public bool GrantValid { get; set; }
    public string GrantId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public double? MinutesRemaining { get; set; }
    public string Status { get; set; } = "Disabled";
    public string Error { get; set; } = string.Empty;
}

public sealed class RecoveryApprovalRequest
{
    public int Version { get; set; } = 1;
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string Requester { get; set; } = string.Empty;
    public string RequesterSid { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string RecoveryId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public string AuditEntryHash { get; set; } = string.Empty;
}

public sealed class RecoveryApprovalDecision
{
    public int Version { get; set; } = 1;
    public string DecisionId { get; set; } = Guid.NewGuid().ToString("N");
    public string RequestId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string RequesterSid { get; set; } = string.Empty;
    public string Approver { get; set; } = string.Empty;
    public string ApproverSid { get; set; } = string.Empty;
    public bool Approved { get; set; }
    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string AuditEntryHash { get; set; } = string.Empty;
}

public sealed class RecoveryApprovalStatus
{
    public bool Enabled { get; set; }
    public bool ApprovalRequired { get; set; }
    public bool RequestFound { get; set; }
    public bool RequestValid { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public bool DecisionFound { get; set; }
    public bool Approved { get; set; }
    public string Approver { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "Disabled";
    public string Error { get; set; } = string.Empty;
}

public sealed class PrivilegedAccessDecision
{
    public bool Allowed { get; set; }
    public string Status { get; set; } = "Allowed";
    public string Message { get; set; } = string.Empty;
    public JitRecoveryStatus Jit { get; set; } = new();
    public RecoveryApprovalStatus Approval { get; set; } = new();
    public bool SiemReady { get; set; } = true;
    public string SiemStatus { get; set; } = "Disabled";
}

public sealed class PrivilegedAccessStatus
{
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public bool JitEnabled { get; set; }
    public int ActiveJitGrants { get; set; }
    public bool TwoPersonApprovalEnabled { get; set; }
    public int PendingApprovalRequests { get; set; }
    public int ActiveApprovals { get; set; }
    public bool SiemEnabled { get; set; }
    public bool SiemFailClosed { get; set; }
    public string SiemStatus { get; set; } = "Disabled";
}

public sealed class SiemEvent
{
    public int Version { get; set; } = 1;
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; }
    public string Machine { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string RecoveryId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string AuthMode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string AuditEntryHash { get; set; } = string.Empty;
}

public sealed class SiemStatus
{
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "Disabled";
    public bool Ready { get; set; }
    public int PendingEvents { get; set; }
    public DateTime? LastSuccessfulFlushUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public int LastBatchCount { get; set; }
    public string LastError { get; set; } = string.Empty;
}
