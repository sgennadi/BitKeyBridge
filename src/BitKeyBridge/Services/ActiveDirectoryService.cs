using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace BitKeyBridge;

public sealed class ActiveDirectoryService
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public string GetCurrentDomainName()
    {
        using var domain = Domain.GetComputerDomain();
        return domain.Name;
    }

    public string GetPreferredWritableDc()
    {
        var domainName = GetCurrentDomainName();
        var inventory = DiscoverDomainControllers(domainName);
        var local = inventory.FirstOrDefault(x =>
            !x.IsReadOnly &&
            (string.Equals(x.Name, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(x.HostName.Split('.')[0], Environment.MachineName, StringComparison.OrdinalIgnoreCase)));
        if (local is not null) return local.HostName;

        var writable = inventory.FirstOrDefault(x => !x.IsReadOnly && x.Reachable);
        if (writable is not null) return writable.HostName;

        throw new InvalidOperationException($"No reachable writable domain controller was discovered for {domainName}.");
    }

    public List<DomainControllerInfo> DiscoverDomainControllers(string? domainName = null)
    {
        domainName ??= GetCurrentDomainName();
        var result = new List<DomainControllerInfo>();
        var context = new DirectoryContext(DirectoryContextType.Domain, domainName);
        var collection = DomainController.FindAll(context);

        foreach (DomainController dc in collection)
        {
            using (dc)
            {
                var host = dc.Name;
                var root = TryGetRootDse(host);
                var isRodc = root.TryGetValue("isRODC", out var rodcText) &&
                             string.Equals(rodcText, "TRUE", StringComparison.OrdinalIgnoreCase);
                var ipv4 = string.Empty;
                try
                {
                    ipv4 = Dns.GetHostAddresses(host)
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
                }
                catch { }

                var site = string.Empty;
                var isGlobalCatalog = false;
                var operatingSystem = string.Empty;
                try { site = dc.SiteName ?? string.Empty; } catch { }
                try { isGlobalCatalog = dc.IsGlobalCatalog(); } catch { }
                try { operatingSystem = dc.OSVersion ?? string.Empty; } catch { }

                result.Add(new DomainControllerInfo
                {
                    Name = host.Split('.')[0],
                    HostName = host,
                    Site = site,
                    IPv4Address = ipv4,
                    IsReadOnly = isRodc,
                    IsGlobalCatalog = isGlobalCatalog,
                    OperatingSystem = operatingSystem,
                    Reachable = root.Count > 0
                });
            }
        }

        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public Dictionary<string, string> GetRootDse(string server)
    {
        var result = TryGetRootDse(server);
        if (result.Count == 0) throw new InvalidOperationException($"Could not read RootDSE from {server}.");
        return result;
    }

    private Dictionary<string, string> TryGetRootDse(string server)
    {
        try
        {
            using var connection = CreateConnection(server);
            var request = new SearchRequest(
                string.Empty,
                "(objectClass=*)",
                SearchScope.Base,
                "defaultNamingContext", "rootDomainNamingContext", "configurationNamingContext",
                "dnsHostName", "isRODC");
            var response = (SearchResponse)connection.SendRequest(request, _timeout);
            var entry = response.Entries.Count > 0 ? response.Entries[0] : null;
            if (entry is null) return [];

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in entry.Attributes.AttributeNames)
            {
                var attr = entry.Attributes[name];
                if (attr is not null && attr.Count > 0) dict[name] = Convert.ToString(attr[0]) ?? string.Empty;
            }
            return dict;
        }
        catch
        {
            return [];
        }
    }

    public string GetDomainSid(string server)
    {
        var root = GetRootDse(server);
        var namingContext = root["defaultNamingContext"];
        using var connection = CreateConnection(server);
        var request = new SearchRequest(namingContext, "(objectClass=domainDNS)", SearchScope.Base, "objectSid");
        var response = (SearchResponse)connection.SendRequest(request, _timeout);
        if (response.Entries.Count == 0) throw new InvalidOperationException("Domain object was not returned by LDAP.");
        var bytes = response.Entries[0].Attributes["objectSid"]?[0] as byte[];
        if (bytes is null) throw new InvalidOperationException("Domain SID was not returned by LDAP.");
        return new SecurityIdentifier(bytes, 0).Value;
    }

    public List<BitLockerScope> ListOrganizationalUnits(string server, string? filterText = null)
    {
        var root = GetRootDse(server);
        var baseDn = root["defaultNamingContext"];
        using var connection = CreateConnection(server);
        var request = new SearchRequest(baseDn, "(objectClass=organizationalUnit)", SearchScope.Subtree, "name", "distinguishedName");
        var rows = SendPaged(connection, request)
            .Select(entry => new BitLockerScope(
                GetString(entry, "name") ?? "OU",
                entry.DistinguishedName))
            .Where(x => string.IsNullOrWhiteSpace(filterText) ||
                        x.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                        x.SearchBase.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SearchBase, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return rows;
    }

    public List<RecoveryRecord> GetRecoveryRecords(
        string server,
        BitLockerScope scope,
        DateTime runTimestamp,
        Action<string>? warning = null)
    {
        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            scope.SearchBase,
            "(objectClass=msFVE-RecoveryInformation)",
            SearchScope.Subtree,
            "msFVE-RecoveryPassword", "msFVE-RecoveryGuid");

        var rows = new List<RecoveryRecord>();
        foreach (var entry in SendPaged(connection, request))
        {
            var password = GetString(entry, "msFVE-RecoveryPassword");
            var guidBytes = entry.Attributes["msFVE-RecoveryGuid"] is { Count: > 0 } guidAttr
                ? guidAttr[0] as byte[]
                : null;

            if (string.IsNullOrWhiteSpace(password) || guidBytes is not { Length: 16 })
                throw new InvalidDataException($"Missing or invalid BitLocker recovery attributes for '{entry.DistinguishedName}'.");

            var computerName = GetParentComputerName(entry.DistinguishedName);
            var recoveryGuid = new Guid(guidBytes).ToString("D");
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"^\d{6}(?:-\d{6}){7}$"))
                warning?.Invoke($"Unexpected recovery password format for {computerName}, recovery ID {recoveryGuid}.");

            rows.Add(new RecoveryRecord(computerName, recoveryGuid, password, runTimestamp));
        }
        return rows;
    }

    public int CountRecoveryObjects(string server, BitLockerScope scope)
    {
        using var connection = CreateConnection(server);
        var request = new SearchRequest(scope.SearchBase, "(objectClass=msFVE-RecoveryInformation)", SearchScope.Subtree, "1.1");
        return SendPaged(connection, request).Count;
    }

    private LdapConnection CreateConnection(string server)
    {
        var identifier = new LdapDirectoryIdentifier(server, 389, true, false);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Negotiate,
            Timeout = _timeout
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.Signing = true;
        connection.SessionOptions.Sealing = true;
        connection.Bind();
        return connection;
    }

    private List<SearchResultEntry> SendPaged(LdapConnection connection, SearchRequest request)
    {
        const int pageSize = 1000;
        var results = new List<SearchResultEntry>();
        var page = new PageResultRequestControl(pageSize);
        request.Controls.Add(page);

        while (true)
        {
            var response = (SearchResponse)connection.SendRequest(request, _timeout);
            foreach (SearchResultEntry entry in response.Entries) results.Add(entry);
            var paging = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault();
            if (paging?.Cookie is not { Length: > 0 }) break;
            page.Cookie = paging.Cookie;
        }
        return results;
    }

    private static string? GetString(SearchResultEntry entry, string attributeName)
    {
        var attr = entry.Attributes[attributeName];
        if (attr is null || attr.Count == 0) return null;
        if (attr[0] is byte[] bytes) return Encoding.UTF8.GetString(bytes);
        return Convert.ToString(attr[0]);
    }

    public static string GetParentComputerName(string distinguishedName)
    {
        var first = FindUnescapedComma(distinguishedName, 0);
        if (first < 0 || first + 1 >= distinguishedName.Length)
            throw new FormatException($"Cannot parse recovery object DN '{distinguishedName}'.");
        var second = FindUnescapedComma(distinguishedName, first + 1);
        var parent = second < 0
            ? distinguishedName[(first + 1)..]
            : distinguishedName[(first + 1)..second];
        if (!parent.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Parent of recovery object is not a computer CN in '{distinguishedName}'.");
        return UnescapeDn(parent[3..]);
    }

    private static int FindUnescapedComma(string value, int start)
    {
        for (var i = start; i < value.Length; i++)
        {
            if (value[i] != ',') continue;
            var slashes = 0;
            for (var j = i - 1; j >= 0 && value[j] == '\\'; j--) slashes++;
            if (slashes % 2 == 0) return i;
        }
        return -1;
    }

    private static string UnescapeDn(string value)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                sb.Append(value[i]);
                continue;
            }

            if (i + 2 < value.Length && IsHex(value[i + 1]) && IsHex(value[i + 2]))
            {
                sb.Append((char)Convert.ToByte(value.Substring(i + 1, 2), 16));
                i += 2;
            }
            else
            {
                sb.Append(value[i + 1]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static bool IsHex(char c) => Uri.IsHexDigit(c);

}
