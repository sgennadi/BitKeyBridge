namespace BitKeyBridge;

public enum EnvironmentCheckState
{
    Available,
    Warning,
    Failed,
    NotVerified,
    Skipped
}

public sealed record EnvironmentCheckRow(
    string Area,
    string Check,
    EnvironmentCheckState State,
    string Detail);

public sealed class EnvironmentDiagnosticReport
{
    public string DomainController { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; } = DateTimeOffset.Now;
    public List<EnvironmentCheckRow> Checks { get; } = [];

    public int Failures => Checks.Count(x => x.State == EnvironmentCheckState.Failed);
    public int Warnings => Checks.Count(x => x.State == EnvironmentCheckState.Warning);
    public int NotVerified => Checks.Count(x => x.State == EnvironmentCheckState.NotVerified);

    public string SafeText()
    {
        var lines = new List<string>
        {
            "BitKeyBridge environment diagnostics",
            "Checked: " + CheckedAt.ToString("O"),
            "Domain controller: " + DomainController,
            "Target computer: " + (string.IsNullOrWhiteSpace(ComputerName) ? "(none)" : ComputerName),
            "Mode: connectivity and metadata only. Password/decrypt permissions are NOT independently verified.",
            ""
        };
        lines.AddRange(Checks.Select(c => c.Area + " | " + c.Check + " | " + c.State + " | " + c.Detail));
        lines.Add("");
        lines.Add("No BitLocker passwords, LAPS secrets, encrypted password attributes or tokens are included.");
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record EnvironmentPortCheck(
    string Check,
    int Port,
    bool Essential,
    string Purpose);

public static class EnvironmentDiagnosticPolicy
{
    // Observed site-specific endpoint for encrypted Windows LAPS / GKDI.
    // This is NOT a protocol-defined static KDS port; RPC Endpoint Mapper on
    // TCP 135 can advertise another endpoint, potentially on another DC.
    public const int ObservedKdsRpcPort = 1344;

    internal static EnvironmentCheckState ClassifyPort(
        TcpProbeState state,
        bool essential,
        bool observedCandidate = false) =>
        state == TcpProbeState.Connected
            ? EnvironmentCheckState.Available
            : essential
                ? EnvironmentCheckState.Failed
                : observedCandidate
                    ? EnvironmentCheckState.NotVerified
                    : EnvironmentCheckState.Warning;

    public static IReadOnlyList<EnvironmentPortCheck> DomainPorts(bool ldaps) =>
    [
        new(ldaps ? "LDAPS / TLS" : "LDAP signing and sealing", ldaps ? 636 : 389, true,
            ldaps ? "TLS certificate/identity is checked separately by the LDAP bind." :
                    "TCP connectivity only; successful signed/sealed LDAP bind is checked separately."),
        new("Kerberos KDC (TCP)", 88, false,
            "Used by domain authentication; a TCP probe does not prove the user's Kerberos ticket is valid."),
        new("RPC Endpoint Mapper", 135, false,
            "Useful for DPAPI-NG/KDS discovery. Port 135 alone never proves that KDS/decryption works."),
        new("Observed KDS/GKDI RPC candidate", ObservedKdsRpcPort, false,
            "Site-specific observation: opening TCP 1344 restored encrypted Windows LAPS decryption in this deployment. " +
            "This is NOT a fixed/default KDS port; the current RPC endpoint may differ by DC or restart. " +
            "A TCP connection does not establish the service identity, authenticated GKDI access, or decryption permissions."),
        new("SMB / SYSVOL", 445, false,
            "Optional for BitKeyBridge's direct LDAP reads; useful for normal domain policy and SYSVOL access.")
    ];

    public static string BitLockerMetadataDetail(int count) =>
        count > 0
            ? count + " recovery object(s) visible. The confidential msFVE-RecoveryPassword attribute was NOT requested."
            : "No recovery objects visible. This does NOT prove no backup: check scope, backup policy, read ACL and replication.";

    public static string LapsMetadataDetail(bool indicatorPresent) =>
        indicatorPresent
            ? "Non-secret LAPS backup indicator(s) were returned. Password attribute presence and read/decrypt rights are NOT established."
            : "No LAPS backup indicators visible. This does NOT prove missing backups or denied read permission.";

    public static string KdsVerificationDetail =>
        "NOT VERIFIED: TCP 135 reachability is insufficient. This deployment observed successful encrypted LAPS decryption " +
        "after permitting TCP 1344, but 1344 is not a universal static KDS port. The actual GKDI/RPC endpoint may vary by DC " +
        "and service start; TCP port probes cannot identify or authenticate it. Verify the active endpoint, AD encryption " +
        "principal, and a separately authorized live decrypt. Unbounded dynamic RPC ports are not scanned.";
}
