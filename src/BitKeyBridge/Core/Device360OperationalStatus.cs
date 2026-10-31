namespace BitKeyBridge;

/// <summary>
/// Source indicators from a completed metadata search and current session.
/// They are never authorization decisions or live connection probes.
/// </summary>
internal enum Device360SourceEvidence
{
    FoundInSearch,
    SessionPresentUnverified,
    NotConnectedUnverified
}

/// <summary>
/// Safe in-memory overview. Contains no recovery identifiers, keys,
/// LAPS passwords, OAuth tokens, credentials or granted-rights conclusions.
/// </summary>
internal sealed class Device360OperationalStatus
{
    internal Device360SourceEvidence Ad { get; init; }
    internal Device360SourceEvidence Entra { get; init; }
    internal Device360SourceEvidence Intune { get; init; }
    internal bool SearchPartial { get; init; }
    internal bool RbacEnabledByConfiguration { get; init; }
    internal bool TicketRequiredByConfiguration { get; init; }
    internal bool ProtectedAuditRequiredByConfiguration { get; init; }
    internal bool AuditBrokerEnabledByConfiguration { get; init; }

    // The original Reveal/Copy/Rotate handler performs *actual* authorization.
    internal bool EffectiveRecoveryRightsChecked => false;

    internal static Device360OperationalStatus Capture(
        Device360Snapshot record,
        bool adConnected, bool cloudSessionPresent,
        bool rbacEnabled, bool ticketRequired, bool protectedAuditRequired,
        bool auditBrokerConfigured, bool searchPartial = false)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new Device360OperationalStatus
        {
            Ad = Resolve(record.InAd, adConnected),
            Entra = Resolve(record.InEntra, cloudSessionPresent),
            Intune = Resolve(record.InIntune, cloudSessionPresent),
            RbacEnabledByConfiguration = rbacEnabled,
            TicketRequiredByConfiguration = ticketRequired,
            ProtectedAuditRequiredByConfiguration = protectedAuditRequired,
            AuditBrokerEnabledByConfiguration = auditBrokerConfigured,
            SearchPartial = searchPartial
        };
    }

    private static Device360SourceEvidence Resolve(bool? found, bool sessionPresent) =>
        found == true ? Device360SourceEvidence.FoundInSearch :
        sessionPresent ? Device360SourceEvidence.SessionPresentUnverified :
            Device360SourceEvidence.NotConnectedUnverified;
}
