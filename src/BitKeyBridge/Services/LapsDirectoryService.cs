using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;

namespace BitKeyBridge;

public sealed class LapsDirectoryService
{
    private readonly ActiveDirectoryService _ad;
    private NetworkCredential? _credential;
    private static readonly string[] Attributes =
    [
        "name", "objectGUID", "ms-Mcs-AdmPwd", "ms-Mcs-AdmPwdExpirationTime",
        "msLAPS-Password", "msLAPS-PasswordExpirationTime", "msLAPS-EncryptedPassword",
        "msLAPS-EncryptedPasswordHistory", "msLAPS-EncryptedDSRMPassword",
        "msLAPS-EncryptedDSRMPasswordHistory", "msLAPS-CurrentPasswordVersion"
    ];

    public LapsDirectoryService(AppConfig config, NetworkCredential? credential)
    {
        _credential = credential;
        _ad = new ActiveDirectoryService(config, credential, useCredentialOverride: true);
    }

    public LapsReadResult Read(string computer, bool includeHistory, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(computer))
            throw new ArgumentException("Enter a computer name, DNS name, AD object GUID, or computer DN.");
        ct.ThrowIfCancellationRequested();
        var server = _ad.GetPreferredWritableDc();
        var root = _ad.GetRootDse(server);
        if (_credential is { } supplied && string.IsNullOrWhiteSpace(supplied.Domain) && !supplied.UserName.Contains('@'))
            _credential = new NetworkCredential(supplied.UserName, supplied.Password,
                ActiveDirectoryService.DistinguishedNameToDnsName(root["defaultNamingContext"]));
        var query = computer.Trim();
        var isDn = query.Contains('=') && query.Contains(',');
        using var connection = _ad.CreateConnection(server);
        var attributes = includeHistory ? Attributes : Attributes
            .Where(x => !x.EndsWith("History", StringComparison.Ordinal)).ToArray();
        var request = new SearchRequest(
            isDn ? query : root["defaultNamingContext"],
            isDn ? "(objectCategory=computer)" : BuildComputerFilter(query),
            isDn ? SearchScope.Base : SearchScope.Subtree, attributes)
        {
            SizeLimit = 2
        };
        var response = (SearchResponse)connection.SendRequest(request, TimeSpan.FromSeconds(30));
        ct.ThrowIfCancellationRequested();
        if (response.Entries.Count == 0)
            throw new InvalidOperationException("The computer was not found in the selected AD domain.");
        if (response.Entries.Count != 1)
            throw new InvalidOperationException("The computer name is ambiguous. Use its DNS name, AD object GUID, or DN.");
        var entry = response.Entries[0];
        var result = new LapsReadResult
        {
            ComputerName = Text(entry, "name") ?? query,
            ComputerId = BinaryGuid(entry, "objectGUID"),
            DirectoryServer = server,
            PasswordVersion = BinaryGuid(entry, "msLAPS-CurrentPasswordVersion"),
            Note = "Legacy LAPS has no password history. Windows LAPS and DSRM history requires AD password encryption and enabled history retention. " +
                "An attribute that is not returned may be absent, not backed up, or hidden by AD permissions."
        };
        try
        {
            var legacyExpiry = Expiration(entry, "ms-Mcs-AdmPwdExpirationTime");
            if (Text(entry, "ms-Mcs-AdmPwd") is { Length: > 0 } legacy)
            {
                var row = new LapsPasswordEntry
                {
                    Source = "Legacy LAPS", Attribute = "ms-Mcs-AdmPwd",
                    AccountName = "Legacy managed account (name not stored in AD)",
                    ExpiresAtUtc = legacyExpiry, Status = LapsPasswordStatus.Available
                };
                row.SetPassword(legacy);
                result.Entries.Add(row);
            }
            var windowsExpiry = Expiration(entry, "msLAPS-PasswordExpirationTime");
            if (Text(entry, "msLAPS-Password") is { Length: > 0 } json)
            {
                var row = new LapsPasswordEntry
                {
                    Source = "Windows LAPS (plain)", Attribute = "msLAPS-Password",
                    ExpiresAtUtc = windowsExpiry
                };
                try { LapsSecretCodec.ReadPasswordJson(json, row); }
                catch (FormatException) { InvalidRow(row); }
                result.Entries.Add(row);
            }

            ReadEncrypted(entry, result, "msLAPS-EncryptedPassword", "Windows LAPS (encrypted)", false, windowsExpiry, ct);
            ReadEncrypted(entry, result, "msLAPS-EncryptedDSRMPassword", "Windows LAPS (DSRM)", false, windowsExpiry, ct);
            if (includeHistory)
            {
                ReadEncrypted(entry, result, "msLAPS-EncryptedPasswordHistory", "Windows LAPS (encrypted)", true, null, ct);
                ReadEncrypted(entry, result, "msLAPS-EncryptedDSRMPasswordHistory", "Windows LAPS (DSRM)", true, null, ct);
            }
            if (result.Entries.Count == 0)
                result.Note = "No readable LAPS password attributes were returned. Check the selected domain, backup policy and read permissions. " + result.Note;
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public LapsAccessCheckResult CheckAccess(
        string computer,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(computer))
        {
            throw new ArgumentException(
                "Enter a computer name, DNS name, AD object GUID, or computer DN.");
        }

        ct.ThrowIfCancellationRequested();

        var server =
            _ad.GetPreferredWritableDc();
        var root =
            _ad.GetRootDse(server);
        var query =
            computer.Trim();
        var isDn =
            query.Contains('=') &&
            query.Contains(',');

        using var connection =
            _ad.CreateConnection(server);

        var request =
            new SearchRequest(
                isDn
                    ? query
                    : root["defaultNamingContext"],
                isDn
                    ? "(objectCategory=computer)"
                    : BuildComputerFilter(query),
                isDn
                    ? SearchScope.Base
                    : SearchScope.Subtree,
                "name",
                "objectGUID",
                "ms-Mcs-AdmPwdExpirationTime",
                "msLAPS-PasswordExpirationTime",
                "msLAPS-CurrentPasswordVersion")
            {
                SizeLimit = 2
            };

        var response =
            (SearchResponse)connection.SendRequest(
                request,
                TimeSpan.FromSeconds(30));

        ct.ThrowIfCancellationRequested();

        if (response.Entries.Count == 0)
        {
            throw new InvalidOperationException(
                "The computer was not found in the selected AD domain.");
        }

        if (response.Entries.Count != 1)
        {
            throw new InvalidOperationException(
                "The computer name is ambiguous. Use its DNS name, AD object GUID, or DN.");
        }

        var entry =
            response.Entries[0];
        var result =
            new LapsAccessCheckResult
            {
                Source = "Active Directory",
                ComputerName =
                    Text(entry, "name") ??
                    query,
                ComputerId =
                    BinaryGuid(
                        entry,
                        "objectGUID"),
                DirectoryServer =
                    server
            };

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "LDAP bind",
                State = LapsAccessState.Available,
                Detail =
                    $"Connected to {server} using the selected AD identity."
            });

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Computer object",
                State = LapsAccessState.Available,
                Detail =
                    "Computer object resolved successfully."
            });

        var schemaAttributes =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var schemaNamingContext =
            root.GetValueOrDefault(
                "schemaNamingContext",
                string.Empty);

        if (!string.IsNullOrWhiteSpace(
                schemaNamingContext))
        {
            try
            {
                var schemaRequest =
                    new SearchRequest(
                        schemaNamingContext,
                        "(|(lDAPDisplayName=ms-Mcs-AdmPwd)(lDAPDisplayName=msLAPS-Password)(lDAPDisplayName=msLAPS-EncryptedPassword)(lDAPDisplayName=msLAPS-EncryptedPasswordHistory)(lDAPDisplayName=msLAPS-EncryptedDSRMPassword)(lDAPDisplayName=msLAPS-EncryptedDSRMPasswordHistory))",
                        SearchScope.Subtree,
                        "lDAPDisplayName");

                var schemaResponse =
                    (SearchResponse)connection.SendRequest(
                        schemaRequest,
                        TimeSpan.FromSeconds(30));

                foreach (SearchResultEntry schemaEntry in
                         schemaResponse.Entries)
                {
                    var name =
                        Text(
                            schemaEntry,
                            "lDAPDisplayName");

                    if (!string.IsNullOrWhiteSpace(
                            name))
                    {
                        schemaAttributes.Add(
                            name);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Checks.Add(
                    new LapsAccessCheckItem
                    {
                        Name = "LAPS schema inspection",
                        State = LapsAccessState.Failed,
                        Detail =
                            "Schema inspection failed: " +
                            DiagnosticRedaction.Sanitize(
                                ex.Message)
                    });
            }
        }

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Legacy Microsoft LAPS schema",
                State =
                    schemaAttributes.Contains(
                        "ms-Mcs-AdmPwd")
                        ? LapsAccessState.Available
                        : LapsAccessState.NotDetected,
                Detail =
                    schemaAttributes.Contains(
                        "ms-Mcs-AdmPwd")
                        ? "Legacy LAPS schema attribute is present."
                        : "Legacy LAPS schema attribute was not detected."
            });

        var windowsSchema =
            schemaAttributes.Contains(
                "msLAPS-Password") ||
            schemaAttributes.Contains(
                "msLAPS-EncryptedPassword");

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Windows LAPS schema",
                State =
                    windowsSchema
                        ? LapsAccessState.Available
                        : LapsAccessState.NotDetected,
                Detail =
                    windowsSchema
                        ? "Windows LAPS schema attributes are present."
                        : "Windows LAPS schema attributes were not detected."
            });

        var historySchema =
            schemaAttributes.Contains(
                "msLAPS-EncryptedPasswordHistory") ||
            schemaAttributes.Contains(
                "msLAPS-EncryptedDSRMPasswordHistory");

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Windows LAPS history schema",
                State =
                    historySchema
                        ? LapsAccessState.Available
                        : LapsAccessState.NotDetected,
                Detail =
                    historySchema
                        ? "Encrypted password-history schema is present."
                        : "Encrypted password-history schema was not detected."
            });

        var legacyExpiry =
            Expiration(
                entry,
                "ms-Mcs-AdmPwdExpirationTime");
        var windowsExpiry =
            Expiration(
                entry,
                "msLAPS-PasswordExpirationTime");
        var passwordVersion =
            BinaryGuid(
                entry,
                "msLAPS-CurrentPasswordVersion");

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Legacy LAPS backup indicator",
                State =
                    legacyExpiry.HasValue
                        ? LapsAccessState.Available
                        : LapsAccessState.NotDetected,
                Detail =
                    legacyExpiry.HasValue
                        ? $"Expiration metadata is present ({legacyExpiry:O})."
                        : "No Legacy LAPS expiration metadata was returned."
            });

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Windows LAPS backup indicator",
                State =
                    windowsExpiry.HasValue ||
                    !string.IsNullOrWhiteSpace(
                        passwordVersion)
                        ? LapsAccessState.Available
                        : LapsAccessState.NotDetected,
                Detail =
                    windowsExpiry.HasValue
                        ? $"Expiration metadata is present ({windowsExpiry:O})."
                        : !string.IsNullOrWhiteSpace(
                            passwordVersion)
                            ? $"Password version metadata is present ({passwordVersion})."
                            : "No Windows LAPS backup indicator was returned."
            });

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Secret attribute read permission",
                State = LapsAccessState.NotProbed,
                Detail =
                    "Not probed by this safe check because requesting Legacy/plaintext LAPS secret attributes would retrieve the password."
            });

        result.Checks.Add(
            new LapsAccessCheckItem
            {
                Name = "Encrypted password decryption",
                State = LapsAccessState.NotProbed,
                Detail =
                    "Not probed by this safe check because DPAPI-NG authorization can only be verified by decrypting an actual protected LAPS value."
            });

        return result;
    }

    public static string BuildComputerFilter(string query)
    {
        if (Guid.TryParse(query, out var id))
            return "(&(objectCategory=computer)(objectGUID=" +
                string.Concat(id.ToByteArray().Select(x => $"\\{x:x2}")) + "))";
        var escaped = ActiveDirectoryService.EscapeLdapFilter(query.TrimEnd('$'));
        return $"(&(objectCategory=computer)(|(name={escaped})(sAMAccountName={escaped}$)(dNSHostName={escaped})))";
    }

    private void ReadEncrypted(SearchResultEntry entry, LapsReadResult result, string attribute,
        string source, bool history, DateTime? expiry, CancellationToken ct)
    {
        var values = entry.Attributes[attribute];
        if (values is null) return;
        foreach (var value in values)
        {
            ct.ThrowIfCancellationRequested();
            var row = new LapsPasswordEntry
            {
                Source = source, Attribute = attribute, IsHistory = history, ExpiresAtUtc = expiry,
                AccountName = source.Contains("DSRM", StringComparison.Ordinal) ? "DSRM" : "Encrypted account"
            };
            result.Entries.Add(row);
            byte[]? payload = null;
            try
            {
                if (value is not byte[] blob) throw new FormatException();
                var unpacked = LapsSecretCodec.UnpackEncryptedBlob(blob);
                payload = unpacked.Payload;
                row.UpdatedAtUtc = unpacked.TimestampUtc;
                var plaintext = AdSessionCredentials.RunWithNetworkIdentity(_credential,
                    () => NativeLapsProtection.Unprotect(payload));
                LapsSecretCodec.ReadDecryptedPassword(plaintext, row);
            }
            catch (LapsDecryptionException ex)
            {
                row.Status = ex.IsAccessDenied ? LapsPasswordStatus.AccessDenied : LapsPasswordStatus.DecryptionFailed;
                row.StatusDetail = ex.Message;
            }
            catch (FormatException) { InvalidRow(row); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException)
            {
                row.Status = LapsPasswordStatus.DecryptionFailed;
                row.StatusDetail = "Native Windows LAPS decryption is unavailable for the selected AD identity on this host.";
            }
            finally
            {
                if (payload is not null) CryptographicOperations.ZeroMemory(payload);
            }
        }
    }

    private static void InvalidRow(LapsPasswordEntry row)
    {
        row.Dispose();
        row.Status = LapsPasswordStatus.InvalidData;
        row.StatusDetail = "The LAPS value has an invalid header, encoding, or JSON format.";
    }

    private static string? Text(SearchResultEntry entry, string attribute)
    {
        var values = entry.Attributes[attribute];
        return values is { Count: > 0 } ? values.GetValues(typeof(string))[0] as string : null;
    }

    private static string BinaryGuid(SearchResultEntry entry, string attribute) =>
        entry.Attributes[attribute] is { Count: > 0 } values && values[0] is byte[] { Length: 16 } bytes
            ? new Guid(bytes).ToString("D") : string.Empty;

    private static DateTime? Expiration(SearchResultEntry entry, string attribute) =>
        long.TryParse(Text(entry, attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var time)
            ? LapsSecretCodec.FileTimeUtc(time) : null;
}
