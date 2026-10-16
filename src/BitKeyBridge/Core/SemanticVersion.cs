using System.Text.RegularExpressions;

namespace BitKeyBridge;

/// <summary>
/// SemVer 2.0.0 precedence for releases. Windows assembly/file versions are
/// numeric; the prerelease suffix lives in ProductVersion/InformationalVersion.
/// Build metadata is deliberately ignored for update precedence.
/// </summary>
internal readonly record struct SemanticVersion(
    int Major, int Minor, int Patch, string Prerelease)
    : IComparable<SemanticVersion>
{
    private static readonly Regex VersionPattern = new(
        @"^v?(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:\.(?<revision>0))?(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+(?<meta>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    // Preserve compatibility with the former System.Version-based self-test.
    public int Build => Patch;
    public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

    public static SemanticVersion FromAssembly(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build), string.Empty);

    public static SemanticVersion Parse(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length is 0 or > 160)
            throw new InvalidOperationException($"Invalid semantic version '{text}'.");

        var match = VersionPattern.Match(text);
        if (!match.Success ||
            !int.TryParse(match.Groups["major"].Value, out var major) ||
            !int.TryParse(match.Groups["minor"].Value, out var minor) ||
            !int.TryParse(match.Groups["patch"].Value, out var patch))
            throw new InvalidOperationException($"Invalid semantic version '{text}'.");

        var pre = match.Groups["pre"].Value;
        if (pre.Length > 0)
        {
            foreach (var part in pre.Split('.'))
            {
                if (IsNumeric(part) && part.Length > 1 && part[0] == '0')
                    throw new InvalidOperationException(
                        $"Invalid numeric prerelease identifier in '{text}'.");
            }
        }

        return new SemanticVersion(major, minor, patch, pre);
    }

    public int CompareTo(SemanticVersion other)
    {
        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0) return comparison;
        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0) return comparison;
        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0) return comparison;

        if (!IsPrerelease) return other.IsPrerelease ? 1 : 0;
        if (!other.IsPrerelease) return -1;

        var left = Prerelease.Split('.');
        var right = other.Prerelease.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftNumeric = IsNumeric(left[i]);
            var rightNumeric = IsNumeric(right[i]);
            if (leftNumeric && rightNumeric)
            {
                comparison = left[i].Length.CompareTo(right[i].Length);
                if (comparison == 0)
                    comparison = string.Compare(left[i], right[i], StringComparison.Ordinal);
            }
            else if (leftNumeric != rightNumeric)
                comparison = leftNumeric ? -1 : 1;
            else
                comparison = string.Compare(left[i], right[i], StringComparison.Ordinal);

            if (comparison != 0) return comparison;
        }

        return left.Length.CompareTo(right.Length);
    }

    public static bool operator >(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) > 0;
    public static bool operator <(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) < 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) >= 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) <= 0;

    private static bool IsNumeric(string text) =>
        text.Length > 0 && text.All(c => c is >= '0' and <= '9');

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}" +
        (IsPrerelease ? "-" + Prerelease : string.Empty);
}
