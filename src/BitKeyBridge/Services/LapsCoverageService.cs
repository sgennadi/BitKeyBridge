using System.DirectoryServices.Protocols;
using System.Globalization;

namespace BitKeyBridge;

/// <summary>
/// LDAP-only, metadata-only LAPS inventory. Never asks for password or history
/// attributes. An absent indicator means NOT VERIFIED, not missing backup.
/// </summary>
public sealed class LapsCoverageService
{
    private readonly ActiveDirectoryService _directory;
    public LapsCoverageService(AppConfig config)
    {
        _directory = new ActiveDirectoryService(config);
    }

    public Task<LapsCoverageSnapshot> ScanAsync(
        string? scopeDn = null, int maximumDevices = 25000,
        IProgress<string>? progress = null, CancellationToken ct = default) =>
        Task.Run(() => Scan(scopeDn, maximumDevices, progress, ct), ct);

    public LapsCoverageSnapshot Scan(
        string? scopeDn = null, int maximumDevices = 25000,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        maximumDevices = Math.Clamp(maximumDevices, 1, 50000);
        ct.ThrowIfCancellationRequested();
        var server = _directory.GetPreferredWritableDc();
        var domainDn = _directory.GetRootDse(server)["defaultNamingContext"];
        if (string.IsNullOrWhiteSpace(domainDn))
            throw new InvalidOperationException("AD default naming context is unavailable.");

        var searchBase = string.IsNullOrWhiteSpace(scopeDn)
            ? domainDn
            : scopeDn.Trim();
        if (!searchBase.Equals(domainDn, StringComparison.OrdinalIgnoreCase) &&
            !searchBase.EndsWith("," + domainDn, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The LAPS coverage scope must be inside the connected AD domain.");
        }

        using var connection = _directory.CreateConnection(server);
        var request = new SearchRequest(
            searchBase,
            "(objectCategory=computer)",
            SearchScope.Subtree,
            "name", "objectGUID", "lastLogonTimestamp",
            "ms-Mcs-AdmPwdExpirationTime",
            "msLAPS-PasswordExpirationTime",
            "msLAPS-CurrentPasswordVersion");
        var page = new PageResultRequestControl(500);
        request.Controls.Add(page);
        var result = new LapsCoverageSnapshot
        {
            DomainController = server,
            SearchBaseDn = searchBase,
            GeneratedAtUtc = DateTime.UtcNow
        };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var response = (SearchResponse)connection.SendRequest(
                request, TimeSpan.FromSeconds(45));
            foreach (SearchResultEntry entry in response.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var objectGuid = ReadGuid(entry, "objectGUID");
                var key = !string.IsNullOrWhiteSpace(objectGuid)
                    ? "AD:GUID:" + objectGuid
                    : "AD:DN:" + entry.DistinguishedName;
                if (!seen.Add(key))
                    continue;

                var winExpiry = ReadFileTime(entry, "msLAPS-PasswordExpirationTime");
                var legacyExpiry = ReadFileTime(entry, "ms-Mcs-AdmPwdExpirationTime");
                var windowsEvidence =
                    winExpiry.HasValue ||
                    !string.IsNullOrWhiteSpace(ReadGuid(
                        entry, "msLAPS-CurrentPasswordVersion"));
                var legacyEvidence = legacyExpiry.HasValue;
                var name = ReadText(entry, "name") ??
                    entry.DistinguishedName;

                result.Devices.Add(new LapsCoverageDevice
                {
                    ComputerName = name,
                    IdentityKey = key,
                    ObjectGuid = objectGuid,
                    DistinguishedName = entry.DistinguishedName,
                    OrganizationalUnit = GetOrganizationalUnit(entry.DistinguishedName),
                    DirectoryServer = server,
                    WindowsMetadataVisible = windowsEvidence,
                    LegacyMetadataVisible = legacyEvidence,
                    WindowsExpirationUtc = winExpiry,
                    LegacyExpirationUtc = legacyExpiry,
                    AdLastLogonUtc = ReadFileTime(entry, "lastLogonTimestamp")
                });

                if (result.Devices.Count >= maximumDevices)
                {
                    result.Truncated = true;
                    break;
                }
            }
            progress?.Report(
                $"LAPS metadata scan: {result.Devices.Count} AD computers on {server}");

            if (result.Truncated)
                break;

            var cookie = response.Controls
                .OfType<PageResultResponseControl>()
                .FirstOrDefault()?.Cookie;
            if (cookie is not { Length: > 0 })
                break;
            page.Cookie = cookie;
        }

        result.Devices = result.Devices
            .OrderBy(x => x.ComputerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.Summary = Summarize(result.Devices, DateTime.UtcNow);
        return result;
    }

    public static LapsCoverageSummary Summarize(
        IEnumerable<LapsCoverageDevice> rows, DateTime nowUtc)
    {
        var data = rows.ToList();
        return new LapsCoverageSummary
        {
            Scanned = data.Count,
            WindowsMetadata = data.Count(x => x.BackupEvidence == "Windows"),
            LegacyMetadata = data.Count(x => x.BackupEvidence == "Legacy"),
            BothMetadata = data.Count(x => x.BackupEvidence == "Windows + Legacy"),
            Unverified = data.Count(x => x.BackupEvidence == "Not verified"),
            Expired = data.Count(x => x.ExpirationState(nowUtc) == "Expired"),
            ExpiringSoon = data.Count(x => x.ExpirationState(nowUtc) == "Expiring soon"),
            Active = data.Count(x => x.ExpirationState(nowUtc) == "Scheduled")
        };
    }

    private static string? ReadText(SearchResultEntry entry, string attribute)
    {
        var value = entry.Attributes[attribute];
        return value is { Count: > 0 }
            ? value.GetValues(typeof(string))[0] as string
            : null;
    }

    private static string ReadGuid(SearchResultEntry entry, string name) =>
        entry.Attributes[name] is { Count: > 0 } values &&
        values[0] is byte[] { Length: 16 } bytes
            ? new Guid(bytes).ToString("D")
            : string.Empty;

    private static DateTime? ReadFileTime(
        SearchResultEntry entry, string name)
    {
        if (!long.TryParse(ReadText(entry, name), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var fileTime))
            return null;
        try
        {
            return DateTime.FromFileTimeUtc(fileTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string GetOrganizationalUnit(string dn)
    {
        var index = dn.IndexOf(",OU=", StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? dn[(index + 1)..] : dn;
    }
}
