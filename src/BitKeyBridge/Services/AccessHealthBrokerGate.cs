namespace BitKeyBridge;

/// <summary>
/// Lightweight, offline-testable policy explanation. A successful policy
/// evaluation is NOT a broker probe or authorization to read any secret.
/// </summary>
internal static class AccessHealthBrokerGate
{
    internal sealed record Gate(string Status, string Explanation);

    internal static Gate Evaluate(AppConfig config)
    {
        if (!config.RequireProtectedAuditForRecovery)
            return new Gate("Optional",
                "Protected audit is not mandatory in this machine policy. " +
                "This is not a statement about AD delegation or recovery rights.");

        if (!config.AuditBrokerEnabled)
            return new Gate("Failed",
                "Recovery requires a protected audit journal but the broker is disabled. " +
                "An administrator must provision the managed broker and NTFS ACLs.");

        if (!config.RbacEnabled)
            return new Gate("Failed",
                "Protected audit requires RBAC enabled with explicit RecoveryReaders.");

        if (config.RbacRecoveryReaders is null ||
            !config.RbacRecoveryReaders.Any(x => !string.IsNullOrWhiteSpace(x)))
            return new Gate("Failed",
                "Protected audit has no explicit RecoveryReaders. Add a scoped AD security group.");

        return new Gate("Configured",
            "Strict protected audit and RBAC RecoveryReaders are configured. " +
            "Service health, caller SID delegation and a durable broker ACK remain separate checks.");
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var optional = Evaluate(new AppConfig());
        if (optional.Status != "Optional")
            errors.Add("Access Health classified optional broker auditing as required.");

        var required = new AppConfig { RequireProtectedAuditForRecovery = true };
        if (Evaluate(required).Status != "Failed")
            errors.Add("Access Health failed to reject a missing required protected broker.");

        required.AuditBrokerEnabled = true;
        required.RbacEnabled = true;
        if (Evaluate(required).Status != "Failed")
            errors.Add("Access Health failed to require explicit RBAC reader configuration.");

        required.RbacRecoveryReaders.Add("DOMAIN\\RecoveryReaders");
        if (Evaluate(required).Status != "Configured")
            errors.Add("Access Health rejected configured protected broker policy.");
        return errors;
    }
}
