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
    private readonly AppConfig _config;

    public ActiveDirectoryService(AppConfig? config = null)
    {
        _config = config ?? ConfigService.LoadAppConfig();
    }

    public string GetCurrentDomainName()
    {
        if (!string.IsNullOrWhiteSpace(_config.AdDomain))
            return _config.AdDomain.Trim();

        try
        {
            using var domain = Domain.GetComputerDomain();
            return domain.Name;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(_config.AdServer))
            {
                var root = GetRootDse(_config.AdServer.Trim());
                if (root.TryGetValue("defaultNamingContext", out var dn) &&
                    !string.IsNullOrWhiteSpace(dn))
                    return DistinguishedNameToDnsName(dn);
            }

            throw new InvalidOperationException(
                "This computer is not joined to an Active Directory domain. " +
                "Configure an explicit DC and domain in Directory Connection.");
        }
    }

    public string GetPreferredWritableDc()
    {
        if (UseExplicitServer())
        {
            var server = _config.AdServer.Trim();
            var root = GetRootDse(server);
            if (root.TryGetValue("isRODC", out var rodcText) &&
                string.Equals(rodcText, "TRUE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Configured domain controller '{server}' is read-only. " +
                    "Choose a writable DC for BitLocker recovery operations.");
            return server;
        }

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

        if (UseExplicitServer() && string.IsNullOrWhiteSpace(domainName))
            domainName = GetCurrentDomainName();

        var result = new List<DomainControllerInfo>();
        var context = CreateDirectoryContext(DirectoryContextType.Domain, domainName);
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

    public List<AdComputerInfo> SearchComputers(string server, string query, int maximumItems = 500)
    {
        var root = GetRootDse(server);
        var baseDn = root["defaultNamingContext"];
        query = query?.Trim() ?? string.Empty;

        var filter = "(objectCategory=computer)";
        if (!string.IsNullOrWhiteSpace(query))
        {
            var escaped = EscapeLdapFilter(query);
            filter = $"(&(objectCategory=computer)(|(name=*{escaped}*)(dNSHostName=*{escaped}*)))";
        }

        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            baseDn,
            filter,
            SearchScope.Subtree,
            "name",
            "distinguishedName",
            "dNSHostName",
            "operatingSystem",
            "operatingSystemVersion",
            "lastLogonTimestamp");

        var result = new List<AdComputerInfo>();
        foreach (var entry in SendPaged(connection, request))
        {
            result.Add(new AdComputerInfo
            {
                ComputerName = GetString(entry, "name") ?? string.Empty,
                DistinguishedName = entry.DistinguishedName,
                DnsHostName = GetString(entry, "dNSHostName") ?? string.Empty,
                OperatingSystem = GetString(entry, "operatingSystem") ?? string.Empty,
                OperatingSystemVersion = GetString(entry, "operatingSystemVersion") ?? string.Empty,
                LastLogonTimestamp = GetFileTime(entry, "lastLogonTimestamp")
            });
            if (result.Count >= Math.Clamp(maximumItems, 1, 5000)) break;
        }

        return result
            .OrderBy(x => x.ComputerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<AdComputerInfo> SearchComputersInScope(
        string server,
        BitLockerScope scope,
        string query,
        int maximumItems = 100)
    {
        query = query?.Trim() ?? string.Empty;
        var filter = "(objectCategory=computer)";

        if (!string.IsNullOrWhiteSpace(query))
        {
            var escaped = EscapeLdapFilter(query);
            filter =
                $"(&(objectCategory=computer)(|(name=*{escaped}*)(dNSHostName=*{escaped}*)))";
        }

        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            scope.SearchBase,
            filter,
            SearchScope.Subtree,
            "name",
            "distinguishedName",
            "dNSHostName",
            "operatingSystem",
            "operatingSystemVersion",
            "lastLogonTimestamp");

        var result = new List<AdComputerInfo>();

        foreach (var entry in SendPaged(connection, request))
        {
            result.Add(new AdComputerInfo
            {
                ComputerName = GetString(entry, "name") ?? string.Empty,
                DistinguishedName = entry.DistinguishedName,
                DnsHostName = GetString(entry, "dNSHostName") ?? string.Empty,
                OperatingSystem = GetString(entry, "operatingSystem") ?? string.Empty,
                OperatingSystemVersion = GetString(entry, "operatingSystemVersion") ?? string.Empty,
                LastLogonTimestamp = GetFileTime(entry, "lastLogonTimestamp")
            });

            if (result.Count >= Math.Clamp(maximumItems, 1, 5000))
                break;
        }

        return result
            .OrderBy(x => x.ComputerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<RecoverySearchResult> GetRecoveryMetadataForComputer(
        string server,
        string computerDistinguishedName)
    {
        if (string.IsNullOrWhiteSpace(computerDistinguishedName))
            return [];

        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            computerDistinguishedName,
            "(objectClass=msFVE-RecoveryInformation)",
            SearchScope.OneLevel,
            "msFVE-RecoveryGuid",
            "whenCreated");

        var rows = new List<RecoverySearchResult>();

        foreach (var entry in SendPaged(connection, request))
        {
            var guidBytes =
                entry.Attributes["msFVE-RecoveryGuid"] is { Count: > 0 } guidAttr
                    ? guidAttr[0] as byte[]
                    : null;

            if (guidBytes is not { Length: 16 })
                continue;

            rows.Add(new RecoverySearchResult
            {
                ComputerName = GetParentComputerName(entry.DistinguishedName),
                RecoveryId = new Guid(guidBytes).ToString("D"),
                CreatedDateTime = ParseLdapDateTime(
                    GetString(entry, "whenCreated")),
                Source = "AD Live",
                ComputerDistinguishedName = computerDistinguishedName,
                RecoveryDistinguishedName = entry.DistinguishedName
            });
        }

        return rows
            .OrderByDescending(x => x.CreatedDateTime)
            .ToList();
    }

    public string GetRecoveryPasswordByDistinguishedName(
        string server,
        string recoveryDistinguishedName,
        string expectedRecoveryId)
    {
        if (string.IsNullOrWhiteSpace(recoveryDistinguishedName))
            throw new ArgumentException(
                "Recovery object distinguished name is required.",
                nameof(recoveryDistinguishedName));

        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            recoveryDistinguishedName,
            "(objectClass=msFVE-RecoveryInformation)",
            SearchScope.Base,
            "msFVE-RecoveryPassword",
            "msFVE-RecoveryGuid");

        var response =
            (SearchResponse)connection.SendRequest(
                request,
                _timeout);

        if (response.Entries.Count != 1)
            throw new InvalidDataException(
                "The selected BitLocker recovery object could not be read.");

        var entry = response.Entries[0];
        var password = GetString(
            entry,
            "msFVE-RecoveryPassword");
        var guidBytes =
            entry.Attributes["msFVE-RecoveryGuid"] is { Count: > 0 } guidAttr
                ? guidAttr[0] as byte[]
                : null;

        if (string.IsNullOrWhiteSpace(password) ||
            guidBytes is not { Length: 16 })
        {
            throw new InvalidDataException(
                "The selected BitLocker recovery object is missing required recovery attributes.");
        }

        var actualRecoveryId =
            new Guid(guidBytes).ToString("D");

        if (!string.Equals(
                actualRecoveryId,
                expectedRecoveryId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The selected recovery object no longer matches the requested Recovery ID.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                password,
                @"^\d{6}(?:-\d{6}){7}$"))
        {
            throw new InvalidDataException(
                "The selected BitLocker recovery password has an unexpected format.");
        }

        return password;
    }

    public List<AdComputerInfo> GetComputersInScope(
        string server,
        BitLockerScope scope,
        int maximumItems = 50000)
    {
        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            scope.SearchBase,
            "(objectCategory=computer)",
            SearchScope.Subtree,
            "name",
            "distinguishedName",
            "dNSHostName",
            "operatingSystem",
            "operatingSystemVersion",
            "lastLogonTimestamp");

        var rows = new List<AdComputerInfo>();
        foreach (var entry in SendPaged(connection, request))
        {
            rows.Add(new AdComputerInfo
            {
                ComputerName = GetString(entry, "name") ?? string.Empty,
                DistinguishedName = entry.DistinguishedName,
                DnsHostName = GetString(entry, "dNSHostName") ?? string.Empty,
                OperatingSystem = GetString(entry, "operatingSystem") ?? string.Empty,
                OperatingSystemVersion = GetString(entry, "operatingSystemVersion") ?? string.Empty,
                LastLogonTimestamp = GetFileTime(entry, "lastLogonTimestamp")
            });

            if (rows.Count >= Math.Clamp(maximumItems, 1, 100000))
                break;
        }

        return rows;
    }

    public List<RecoverySearchResult> SearchRecoveryMetadataInScope(
        string server,
        BitLockerScope scope,
        string recoveryIdQuery,
        int maximumItems = 200)
    {
        var query =
            (recoveryIdQuery ?? string.Empty)
                .Trim()
                .Trim('{', '}');

        if (string.IsNullOrWhiteSpace(query))
            return [];

        var escaped =
            EscapeLdapFilter(query);

        using var connection =
            CreateConnection(server);

        // msFVE-RecoveryInformation object CNs include the recovery GUID.
        // Searching the CN server-side avoids enumerating every recovery
        // object in a large OU just to match a partial Recovery ID.
        var request =
            new SearchRequest(
                scope.SearchBase,
                $"(&(objectClass=msFVE-RecoveryInformation)(name=*{escaped}*))",
                SearchScope.Subtree,
                "msFVE-RecoveryGuid",
                "whenCreated");

        var rows =
            new List<RecoverySearchResult>();

        foreach (var entry in
                 SendPaged(
                     connection,
                     request))
        {
            var guidBytes =
                entry.Attributes["msFVE-RecoveryGuid"] is
                    { Count: > 0 } guidAttr
                    ? guidAttr[0] as byte[]
                    : null;

            if (guidBytes is not { Length: 16 })
                continue;

            var recoveryId =
                new Guid(
                    guidBytes)
                    .ToString("D");

            if (!recoveryId.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(
                new RecoverySearchResult
                {
                    ComputerName =
                        GetParentComputerName(
                            entry.DistinguishedName),
                    RecoveryId =
                        recoveryId,
                    CreatedDateTime =
                        ParseLdapDateTime(
                            GetString(
                                entry,
                                "whenCreated")),
                    Source =
                        "AD Live",
                    ComputerDistinguishedName =
                        GetParentDistinguishedName(
                            entry.DistinguishedName),
                    RecoveryDistinguishedName =
                        entry.DistinguishedName
                });

            if (rows.Count >=
                Math.Clamp(
                    maximumItems,
                    1,
                    5000))
            {
                break;
            }
        }

        return rows
            .OrderByDescending(
                x => x.CreatedDateTime)
            .ToList();
    }

    public List<AdRecoveryMetadata> GetRecoveryMetadata(
        string server,
        BitLockerScope scope,
        int maximumItems = 100000)
    {
        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            scope.SearchBase,
            "(objectClass=msFVE-RecoveryInformation)",
            SearchScope.Subtree,
            "msFVE-RecoveryGuid",
            "whenCreated");

        var rows = new List<AdRecoveryMetadata>();
        foreach (var entry in SendPaged(connection, request))
        {
            var guidBytes =
                entry.Attributes["msFVE-RecoveryGuid"] is { Count: > 0 } guidAttr
                    ? guidAttr[0] as byte[]
                    : null;

            if (guidBytes is not { Length: 16 })
                continue;

            var computerDn =
                GetParentDistinguishedName(
                    entry.DistinguishedName);

            rows.Add(new AdRecoveryMetadata
            {
                ComputerName = GetParentComputerName(entry.DistinguishedName),
                RecoveryId = new Guid(guidBytes).ToString("D"),
                CreatedDateTime = ParseLdapDateTime(
                    GetString(entry, "whenCreated")),
                ComputerDistinguishedName = computerDn,
                RecoveryDistinguishedName = entry.DistinguishedName
            });

            if (rows.Count >= Math.Clamp(maximumItems, 1, 200000))
                break;
        }

        return rows;
    }

    public AdComputerInfo? FindComputerByName(string server, string computerName)
    {
        if (string.IsNullOrWhiteSpace(computerName)) return null;
        var root = GetRootDse(server);
        var baseDn = root["defaultNamingContext"];
        var escaped = EscapeLdapFilter(computerName.Trim());

        using var connection = CreateConnection(server);
        var request = new SearchRequest(
            baseDn,
            $"(&(objectCategory=computer)(name={escaped}))",
            SearchScope.Subtree,
            "name",
            "distinguishedName",
            "dNSHostName",
            "operatingSystem",
            "operatingSystemVersion",
            "lastLogonTimestamp");
        var response = (SearchResponse)connection.SendRequest(request, _timeout);
        if (response.Entries.Count == 0) return null;
        var entry = response.Entries[0];
        return new AdComputerInfo
        {
            ComputerName = GetString(entry, "name") ?? string.Empty,
            DistinguishedName = entry.DistinguishedName,
            DnsHostName = GetString(entry, "dNSHostName") ?? string.Empty,
            OperatingSystem = GetString(entry, "operatingSystem") ?? string.Empty,
            OperatingSystemVersion = GetString(entry, "operatingSystemVersion") ?? string.Empty,
            LastLogonTimestamp = GetFileTime(entry, "lastLogonTimestamp")
        };
    }

    public int CountRecoveryObjects(string server, BitLockerScope scope)
    {
        using var connection = CreateConnection(server);
        var request = new SearchRequest(scope.SearchBase, "(objectClass=msFVE-RecoveryInformation)", SearchScope.Subtree, "1.1");
        return SendPaged(connection, request).Count;
    }

    public Dictionary<string, string> TestConnection(string? server = null)
    {
        var target = string.IsNullOrWhiteSpace(server)
            ? GetPreferredWritableDc()
            : server.Trim();
        return GetRootDse(target);
    }

    private bool UseExplicitServer() =>
        string.Equals(_config.AdConnectionMode, "Explicit", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_config.AdServer);

    public DirectoryContext CreateDirectoryContext(
        DirectoryContextType type,
        string name)
    {
        var credential = AdSessionCredentials.CreateNetworkCredential(_config);
        return credential is null
            ? new DirectoryContext(type, name)
            : new DirectoryContext(
                type,
                name,
                FormatDirectoryContextUsername(credential),
                credential.Password);
    }

    private static string FormatDirectoryContextUsername(NetworkCredential credential)
    {
        if (!string.IsNullOrWhiteSpace(credential.Domain))
            return credential.Domain + "\\" + credential.UserName;
        return credential.UserName;
    }

    private static string DistinguishedNameToDnsName(string distinguishedName)
    {
        return string.Join(
            ".",
            distinguishedName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
                .Select(x => x[3..]));
    }

    private LdapConnection CreateConnection(string server)
    {
        var port = Math.Clamp(_config.AdPort, 1, 65535);
        var identifier = new LdapDirectoryIdentifier(server, port, true, false);
        var credential = AdSessionCredentials.CreateNetworkCredential(_config);
        var connection = credential is null
            ? new LdapConnection(identifier)
            : new LdapConnection(identifier, credential, AuthType.Negotiate);
        connection.AuthType = AuthType.Negotiate;
        connection.Timeout = _timeout;
        connection.SessionOptions.ProtocolVersion = 3;

        var useLdaps = _config.AdUseLdaps || port == 636;
        if (useLdaps)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }
        else
        {
            connection.SessionOptions.Signing = true;
            connection.SessionOptions.Sealing = true;
        }

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

    private static DateTime? ParseLdapDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var formats = new[]
        {
            "yyyyMMddHHmmss.0'Z'",
            "yyyyMMddHHmmss'Z'",
            "yyyyMMddHHmmss.f'Z'",
            "yyyyMMddHHmmss.ff'Z'",
            "yyyyMMddHHmmss.fff'Z'"
        };

        if (DateTime.TryParseExact(
                value,
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var exact))
        {
            return exact.ToLocalTime();
        }

        return DateTime.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.ToLocalTime()
            : null;
    }

    private static DateTime? GetFileTime(SearchResultEntry entry, string attributeName)
    {
        var text = GetString(entry, attributeName);
        if (!long.TryParse(text, out var value) || value <= 0) return null;
        try { return DateTime.FromFileTimeUtc(value).ToLocalTime(); }
        catch { return null; }
    }

    private static string EscapeLdapFilter(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            sb.Append(ch switch
            {
                '\\' => @"\5c",
                '*' => @"\2a",
                '(' => @"\28",
                ')' => @"\29",
                '\0' => @"\00",
                _ => ch.ToString()
            });
        }
        return sb.ToString();
    }

    private static string? GetString(SearchResultEntry entry, string attributeName)
    {
        var attr = entry.Attributes[attributeName];
        if (attr is null || attr.Count == 0) return null;
        if (attr[0] is byte[] bytes) return Encoding.UTF8.GetString(bytes);
        return Convert.ToString(attr[0]);
    }

    private static string GetParentDistinguishedName(
        string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
            return string.Empty;

        var comma = distinguishedName.IndexOf(',');
        return comma >= 0 && comma + 1 < distinguishedName.Length
            ? distinguishedName[(comma + 1)..]
            : string.Empty;
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
