namespace BitKeyBridge;

public static class SearchText
{
    public const int DebounceMilliseconds = 450;
    public const int MinimumLiveSearchCharacters = 2;

    public static string NormalizeIdentifierFragment(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(
            value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }

    public static bool IdentifierContains(
        string? identifier,
        string? query)
    {
        var candidate =
            NormalizeIdentifierFragment(
                identifier);
        var fragment =
            NormalizeIdentifierFragment(
                query);

        return fragment.Length > 0 &&
               candidate.Contains(
                   fragment,
                   StringComparison.Ordinal);
    }

    public static bool LooksLikeIdentifierFragment(
        string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var normalized =
            NormalizeIdentifierFragment(
                query);

        if (normalized.Length <
            MinimumLiveSearchCharacters)
        {
            return false;
        }

        return normalized.All(
            static ch =>
                ch is >= '0' and <= '9' ||
                ch is >= 'A' and <= 'F');
    }
}

public sealed class LapsSearchResult
{
    public string ComputerName { get; set; } =
        string.Empty;

    public string ComputerId { get; set; } =
        string.Empty;

    public DateTime? KeyDateUtc { get; set; }

    public bool? IsLatest { get; set; }

    public string Source { get; set; } =
        string.Empty;

    public string DirectoryServer { get; set; } =
        string.Empty;

    public string LookupValue =>
        !string.IsNullOrWhiteSpace(
            ComputerId)
            ? ComputerId
            : ComputerName;
}
