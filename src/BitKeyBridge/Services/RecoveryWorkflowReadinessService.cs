namespace BitKeyBridge;

/// <summary>
/// Secret-free, read-only recovery planning. Never authorizes retrieval:
/// actual RecoveryRead/JIT/approval, ticket and durable audit remain mandatory
/// within the existing reveal/copy handlers.
/// </summary>
internal static class RecoveryWorkflowReadinessService
{
    internal sealed record Step(string Name, string Status, string Explanation);
    internal sealed record Snapshot(
        string Computer, string Source, bool CanNavigateToRecoveryScreen,
        IReadOnlyList<Step> Steps, string Notice);

    internal static Snapshot Build(AppConfig config, HomeSearchResult? selected,
        bool adConnected, bool cloudConnected, AuthorizationDecision? authorization)
    {
        var known = selected is not null &&
                    !string.IsNullOrWhiteSpace(selected.ComputerName);
        var cloud = selected?.IsCloudRecord == true;
        var identifier = selected?.SourceIdentity ?? string.Empty;
        var qualifiedCloudId = identifier.StartsWith("ENTRA:", StringComparison.OrdinalIgnoreCase) ||
                               identifier.StartsWith("INTUNE:", StringComparison.OrdinalIgnoreCase);
        var identitySafe = known && !selected!.IdentityAmbiguous && (!cloud || qualifiedCloudId);
        var sourceReady = known && (cloud ? cloudConnected : adConnected);
        var rbacAllowed = authorization?.Allowed == true;
        var invalidBroker = config.AuditBrokerEnabled &&
            (!config.RequireProtectedAuditForRecovery || !config.RbacEnabled ||
             config.RbacRecoveryReaders.Count == 0);

        var steps = new List<Step>
        {
            new("1. Exact device identity", identitySafe ? "OK" : "Action required",
                !known ? "Select an exact computer/device in Start." :
                selected!.IdentityAmbiguous ? "Identity collision: confirm the AD object GUID or Entra/Intune device ID." :
                !identitySafe ? "Cloud result lacks an authoritative source-qualified ID. Search again." :
                "A source-qualified record is selected. Its identity is rechecked at retrieval."),
            new("2. Directory source", sourceReady ? "OK" : "Action required",
                !known ? "Connect to AD or Microsoft 365 and search first." :
                sourceReady ? "Directory session is present; individual permissions remain unverified." :
                cloud ? "Connect to Microsoft Entra / Intune with the required Graph session." :
                        "Connect Active Directory with the intended helpdesk Windows identity."),
            new("3. BitLocker/LAPS metadata", !known ? "Not tested" :
                selected!.BitLockerLookupFailed || selected.LapsLookupFailed ? "Unverified" : "Info",
                !known ? "Choose a device before checking recovery metadata." :
                selected!.BitLockerLookupFailed || selected.LapsLookupFailed
                    ? "Metadata lookup was incomplete. Never treat a failed query as proof of no backup."
                    : selected.BitLockerKeyCount > 0
                        ? selected.BitLockerKeyCount + " BitLocker recovery ID(s) visible, but no key value was read."
                        : selected.LapsCandidateFound
                            ? "LAPS candidate metadata is visible. Password read access is not proven."
                            : "No confirmed backup metadata. Search diagnostics may be needed."),
            new("4. Windows RecoveryRead RBAC", rbacAllowed ? "OK" : "Action required",
                authorization is null ? "Current token could not be verified. Check Access Health." :
                rbacAllowed ? "Windows RBAC permits an attempt. AD/Graph/JIT/approval still apply." :
                              "RecoveryRead denied to this Windows token. Open Access Health."),
            new("5. Ticket / reference",
                config.RequireRecoveryAccessReference ? "Required at retrieval" : "Optional",
                config.RequireRecoveryAccessReference
                    ? "The existing recovery dialog must validate a ticket/reference before reading a secret."
                    : "A ticket/reference is optional under current machine policy."),
            new("6. Durable recovery audit",
                invalidBroker ? "Action required" :
                    config.RequireProtectedAuditForRecovery ? "Pending protected ACK" : "Policy-dependent",
                invalidBroker ? "Broker policy is incomplete. Configure strict audit and explicit RBAC readers." :
                config.RequireProtectedAuditForRecovery
                    ? "A real protected audit commit must succeed BEFORE any password/key read. This preview writes no event."
                    : "Audit is enforced by the actual handler; portable user audit is not tamper-resistant."),
            new("7. Explicit reveal / copy / rotation", "Explicit action only",
                "Use an existing BitLocker, LAPS or Devices action. The handler rechecks access and auditing. Rotation never starts here.")
        };

        return new Snapshot(
            known ? DiagnosticRedaction.Sanitize(selected!.ComputerName) : "No device selected",
            !known ? "Not selected" : cloud ? "Entra / Intune" : "Active Directory",
            identitySafe && sourceReady && rbacAllowed && !invalidBroker,
            steps,
            "Read-only preflight, NOT an authorization. No recovery secrets were requested. " +
            "Actual recovery still requires a fresh policy decision and its durable audit ACK.");
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var config = new AppConfig
        {
            RequireProtectedAuditForRecovery = true,
            AuditBrokerEnabled = true, RbacEnabled = true,
            RbacRecoveryReaders = ["S-1-5-32-544"],
            RequireRecoveryAccessReference = true
        };
        var allow = new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" };
        var deny = new AuthorizationDecision { Allowed = false, Permission = "RecoveryRead" };
        var ad = new HomeSearchResult { ComputerName = "PC-TEST", BitLockerKeyCount = 2 };
        var good = Build(config, ad, true, false, allow);
        if (!good.CanNavigateToRecoveryScreen ||
            good.Steps[4].Status != "Required at retrieval" ||
            good.Steps[5].Status != "Pending protected ACK")
            errors.Add("Recovery workflow wrongly bypassed ticket/protected audit status.");
        if (Build(config, null, true, true, allow).CanNavigateToRecoveryScreen ||
            Build(config, ad, false, true, allow).CanNavigateToRecoveryScreen ||
            Build(config, ad, true, false, deny).CanNavigateToRecoveryScreen)
            errors.Add("Recovery workflow accepted missing target, directory or RBAC.");
        ad.IdentityAmbiguous = true;
        if (Build(config, ad, true, false, allow).CanNavigateToRecoveryScreen)
            errors.Add("Ambiguous AD identity passed recovery preflight.");
        ad.IdentityAmbiguous = false;
        ad.BitLockerLookupFailed = true;
        if (Build(config, ad, true, false, allow).Steps[2].Status != "Unverified")
            errors.Add("Failed BitLocker metadata lookup was treated as a confirmed absence.");
        var cloud = new HomeSearchResult
        {
            ComputerName = "PC-CLOUD", IsCloudRecord = true, BitLockerKeyCount = 1
        };
        if (Build(config, cloud, true, true, allow).CanNavigateToRecoveryScreen)
            errors.Add("Cloud hostname without authoritative ID was accepted.");
        cloud.SourceIdentity = "INTUNE:MANAGED:demo-guid";
        if (!Build(config, cloud, false, true, allow).CanNavigateToRecoveryScreen ||
            Build(config, cloud, true, false, allow).CanNavigateToRecoveryScreen)
            errors.Add("Cloud preflight used AD connection instead of Graph session.");
        config.RbacRecoveryReaders.Clear();
        if (Build(config, ad, true, false, allow).CanNavigateToRecoveryScreen)
            errors.Add("Incomplete broker policy was accepted.");
        return errors;
    }
}
