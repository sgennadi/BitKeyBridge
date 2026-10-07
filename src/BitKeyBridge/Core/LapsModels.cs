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
    public string PasswordVersion { get; set; } = string.Empty;
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
