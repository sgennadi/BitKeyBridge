using System.Text.Json.Serialization;

namespace BitKeyBridge;

public enum LapsPasswordStatus
{
    Available,
    AccessDenied,
    DecryptionFailed,
    InvalidData,
    NotReturned
}

public sealed class LapsPasswordEntry : IDisposable
{
    private char[] _password = [];

    public string Source { get; init; } = string.Empty;
    public string Attribute { get; init; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountSid { get; init; } = string.Empty;
    public bool IsHistory { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; init; }
    public LapsPasswordStatus Status { get; set; }
    public string StatusDetail { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasPassword => _password.Length > 0;

    public void SetPassword(string password)
    {
        Dispose();
        _password = password.ToCharArray();
    }

    public string CopyPassword() => new(_password);

    internal bool PasswordEquals(string value) => _password.AsSpan().SequenceEqual(value.AsSpan());

    public void Dispose()
    {
        Array.Clear(_password);
        _password = [];
    }
}

public sealed class LapsReadResult : IDisposable
{
    public string ComputerName { get; set; } = string.Empty;
    public string ComputerId { get; set; } = string.Empty;
    public string DirectoryServer { get; set; } = string.Empty;
    public string ComputerDistinguishedName { get; set; } = string.Empty;
    public string PasswordVersion { get; set; } = string.Empty;
    public bool LegacyBackupIndicatorPresent { get; set; }
    public bool WindowsBackupIndicatorPresent { get; set; }
    public bool HasAdBackupIndicators =>
        LegacyBackupIndicatorPresent || WindowsBackupIndicatorPresent;
    public List<LapsPasswordEntry> Entries { get; } = [];
    public string Note { get; set; } = string.Empty;

    public IEnumerable<LapsPasswordEntry> OrderedEntries => Entries
        .OrderBy(x => x.Source, StringComparer.Ordinal)
        .ThenBy(x => x.IsHistory)
        .ThenByDescending(x => x.UpdatedAtUtc);

    public void Dispose()
    {
        foreach (var entry in Entries)
            entry.Dispose();
        Entries.Clear();
    }
}


/// <summary>
/// Creates operator-facing messages without secrets. LDAP metadata alone
/// cannot distinguish a missing backup from a confidential attribute hidden
/// by access control.
/// </summary>
public static class LapsReadDiagnostics
{
    public static string EmptyAdNote(
        bool legacyIndicator,
        bool windowsIndicator)
    {
        if (legacyIndicator || windowsIndicator)
        {
            var type =
                legacyIndicator && windowsIndicator
                    ? "Legacy and Windows LAPS"
                    : windowsIndicator
                        ? "Windows LAPS"
                        : "Legacy LAPS";

            return type +
                " backup metadata is visible, but Active Directory did not " +
                "return any LAPS password attribute. This does not prove " +
                "that a password is stored or that access is denied. " +
                "Check the current user's LAPS password-read delegation " +
                "on the computer's OU and inherited permissions. " +
                "If permissions are correct, check the client's LAPS " +
                "backup policy and LAPS Operational events. " +
                "Encrypted Windows LAPS additionally requires decrypt authorization.";
        }

        return "No LAPS password attribute or backup metadata was returned " +
            "by Active Directory. Verify the domain/computer, the client's " +
            "LAPS AD backup policy and LAPS Operational events, and the " +
            "current user's password-read delegation. The LDAP response " +
            "cannot distinguish a missing backup from filtered attributes.";
    }

    public static string EmptyStatus(
        LapsReadResult result,
        bool cloud)
    {
        if (cloud)
        {
            return "0 records: Entra returned no LAPS credentials. " +
                "Check backup and Entra/Graph access; see diagnostics.";
        }

        if (result.HasAdBackupIndicators)
        {
            return "0 records: LAPS metadata detected, but AD did not return any password attribute. " +
                "Most likely missing LAPS read permission; also check the backup policy. " +
                "Read access has not been independently verified.";
        }

        return "0 records: AD returned no LAPS password or backup metadata. " +
            "Check the client policy, OU and read rights; use AD access help...";
    }

    public static string EmptyDetails(
        LapsReadResult result,
        bool cloud)
    {
        var note =
            string.IsNullOrWhiteSpace(result.Note)
                ? cloud
                    ? "No Entra LAPS credentials were returned. Verify Entra " +
                      "backup policy, device state and Graph permissions."
                    : EmptyAdNote(
                        result.LegacyBackupIndicatorPresent,
                        result.WindowsBackupIndicatorPresent)
                : result.Note;

        var lines = new List<string>
        {
            "Source: " + (cloud ? "Entra" : "Active Directory"),
            "Computer: " + result.ComputerName,
            "Directory: " + result.DirectoryServer,
            "Computer DN: " + result.ComputerDistinguishedName,
            "Records returned: 0"
        };

        if (!cloud)
        {
            lines.Add(
                "Legacy LAPS backup indicator: " +
                (result.LegacyBackupIndicatorPresent
                    ? "Present"
                    : "Not returned"));
            lines.Add(
                "Windows LAPS backup indicator: " +
                (result.WindowsBackupIndicatorPresent
                    ? "Present"
                    : "Not returned"));
            lines.Add(
                "Password-read permission: not independently verified.");
            lines.Add(
                "Encrypted-password decryption: not independently verified.");
        }

        lines.Add("");
        lines.Add(note);
        lines.Add("");
        lines.Add(
            "This report contains no recovery passwords, " +
            "LAPS passwords, encrypted blobs or authentication tokens.");

        return string.Join(Environment.NewLine, lines);
    }
}

public enum LapsAccessState
{
    Available,
    NotDetected,
    NotProbed,
    Failed
}

public static class LapsAccessDiagnostics
{
    public static LapsAccessState ResolveSchemaState(
        bool schemaInspectionCompleted,
        bool schemaAttributeDetected,
        bool metadataEvidence = false)
    {
        if (schemaAttributeDetected || metadataEvidence)
            return LapsAccessState.Available;

        return schemaInspectionCompleted
            ? LapsAccessState.NotDetected
            : LapsAccessState.NotProbed;
    }
}

public sealed class LapsAccessCheckItem
{
    public string Name { get; init; } = string.Empty;
    public LapsAccessState State { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class LapsAccessCheckResult
{
    public string Source { get; init; } = string.Empty;
    public string ComputerName { get; init; } = string.Empty;
    public string ComputerId { get; init; } = string.Empty;
    public string DirectoryServer { get; init; } = string.Empty;
    public List<LapsAccessCheckItem> Checks { get; } = [];

    public bool HasFailure =>
        Checks.Any(
            x => x.State == LapsAccessState.Failed);
}
