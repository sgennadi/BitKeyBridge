using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>Per-device DC consistency, metadata and configured-ACE inspection.
/// Never requests BitLocker/LAPS secret attributes.</summary>
public sealed class AdvancedAdDiagnosticService
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;

    public AdvancedAdDiagnosticService(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService(config);
    }

    public async Task<DeviceConsistencyReport> CompareControllersAsync(
        string exactComputer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exactComputer) || exactComputer.Length > 256)
            throw new ArgumentException("Enter one exact computer name (maximum 256 characters).", nameof(exactComputer));

        var report = new DeviceConsistencyReport { Computer = exactComputer.Trim() };
        var domain = !string.IsNullOrWhiteSpace(_config.AdDomain)
            ? _config.AdDomain : _ad.GetCurrentDomainName();
        report.Domain = domain;

        var dcs = await Task.Run(() => _ad.DiscoverDomainControllers(domain), cancellationToken);
        foreach (var dc in dcs
            .Where(d => !string.IsNullOrWhiteSpace(d.HostName))
            .GroupBy(d => d.HostName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(24))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            var row = new DeviceDcEvidence
            {
                Server = dc.HostName,
                Site = dc.Site,
                IsReadOnly = dc.IsReadOnly
            };

            try
            {
                var info = await Task.Run(() => _ad.FindComputerByName(
                    dc.HostName, exactComputer), cancellationToken);
                row.QuerySucceeded = true;
                if (info is not null)
                {
                    row.ComputerFound = true;
                    row.ComputerDistinguishedName = info.DistinguishedName;
                    var data = await Task.Run(() => ReadMetadata(dc.HostName,
                        info.DistinguishedName), cancellationToken);
                    row.RecoveryIds = data.RecoveryIds;
                    row.WindowsLapsExpiry = data.WindowsExpiry;
                    row.LegacyLapsExpiry = data.LegacyExpiry;
                    row.WindowsLapsVersion = data.Version;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                row.QuerySucceeded = false;
                row.Error = DiagnosticRedaction.Sanitize(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                row.ElapsedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds);
                report.Controllers.Add(row);
            }
        }

        report.Findings.AddRange(SmartDiagnosticEngine.Explain(report));
        return report;
    }

    public async Task<PermissionInspectionReport> InspectPermissionsAsync(
        string exactComputer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exactComputer))
            throw new ArgumentException("An exact computer name is required.", nameof(exactComputer));
        var output = new PermissionInspectionReport
        {
            Computer = exactComputer.Trim(),
            IdentityUsed = _config.AdUseExplicitCredentials
                ? _config.AdUsername : WindowsIdentity.GetCurrent().Name,
            ExplicitCredentials = _config.AdUseExplicitCredentials
        };
        try
        {
            var dc = _ad.GetPreferredWritableDc();
            output.QueriedDc = dc;
            var info = await Task.Run(() => _ad.FindComputerByName(dc, exactComputer), cancellationToken);
            if (info is null)
            {
                output.Status = "NotVerified";
                output.Findings.Add(new("COMPUTER_MISSING", DiagnosticSeverity.Warning,
                    EvidenceStrength.NotVerified, "Computer not found or not visible",
                    "Identity, OU and computer-object visibility must be checked first.",
                    "Find the exact AD computer and verify a readable LDAP bind.", "DC / RSAT workstation"));
                return output;
            }

            output.ComputerDn = info.DistinguishedName;
            var dn = info.DistinguishedName;
            var comma = ParentDelimiter(dn);
            output.Scope = comma >= 0 ? dn[(comma + 1)..] : string.Empty;
            using var conn = _ad.CreateConnection(dc);
            cancellationToken.ThrowIfCancellationRequested();

            var descriptorRequest = new SearchRequest(
                dn, "(objectCategory=computer)", SearchScope.Base, "nTSecurityDescriptor");
            descriptorRequest.Controls.Add(new SecurityDescriptorFlagControl(SecurityMasks.Dacl));
            var response = (SearchResponse)conn.SendRequest(descriptorRequest, TimeSpan.FromSeconds(15));
            if (response.Entries.Count == 0 ||
                response.Entries[0].Attributes["nTSecurityDescriptor"] is not { Count: > 0 } attr ||
                attr[0] is not byte[] securityBytes)
            {
                output.Status = "NotVerified";
                output.Findings.Add(new("ACL_NOT_RETURNED", DiagnosticSeverity.Warning,
                    EvidenceStrength.NotVerified, "The computer's DACL was not returned",
                    "The account may not have READ_CONTROL or the directory request may be filtered.",
                    "Inspect the object's Security > Advanced with an authorized account.", "DC / RSAT workstation"));
                return output;
            }

            var descriptor = new RawSecurityDescriptor(securityBytes, 0);
            output.DescriptorRetrieved = true;
            var dacl = descriptor.DiscretionaryAcl;
            if (dacl is not null)
            {
                foreach (GenericAce generic in dacl.Cast<GenericAce>().Take(250))
                {
                    if (generic is not QualifiedAce ace)
                        continue;
                    var kind = ace.AceQualifier == AceQualifier.AccessAllowed ? "ALLOW" :
                        ace.AceQualifier == AceQualifier.AccessDenied ? "DENY" : "AUDIT";
                    var rights = (ace.AccessMask & 0x100) != 0 ? "ControlAccess" :
                                 (ace.AccessMask & 0x10) != 0 ? "ReadProperty" :
                                 (ace.AccessMask & 0x20000) != 0 ? "ReadControl" : "Other";
                    var objectGuid = generic is ObjectAce obj &&
                                     (obj.ObjectAceFlags & ObjectAceFlags.ObjectAceTypePresent) != 0
                        ? obj.ObjectAceType.ToString("D") : "(not object-specific)";
                    var inheritance = (generic.AceFlags & AceFlags.Inherited) != 0
                        ? "inherited" : "explicit";
                    if (rights == "Other" && kind != "DENY") continue;
                    output.DescriptorRules.Add(
                        kind + " | " + ace.SecurityIdentifier.Value +
                        " | " + rights + " | mask=0x" + ace.AccessMask.ToString("X8") +
                        " | object-type=" + objectGuid + " | " + inheritance);
                }
            }

            try
            {
                var groups = ResolveBoundPrincipalSids(conn, dc);
                output.ResolvedPrincipalSids.AddRange(groups);
                output.GroupResolutionStatus = groups.Count > 0
                    ? "Identity SID/group evidence observed; token and effective rights NotVerified"
                    : "NotVerified (no tokenGroups or Windows SID evidence)";
                var matchingAceCount = output.DescriptorRules.Count(rule =>
                    groups.Any(sid => rule.Contains(" | " + sid + " | ",
                        StringComparison.OrdinalIgnoreCase)));
                output.Findings.Add(new("ACE_PRINCIPAL_MATCH", DiagnosticSeverity.Info,
                    groups.Count > 0 ? EvidenceStrength.Confirmed : EvidenceStrength.NotVerified,
                    "Bound identity SID/group evidence",
                    groups.Count + " SID(s) observed; " + matchingAceCount +
                    " computer DACL ACE(s) reference one of them. An ACE match does NOT prove effective access.",
                    "Review nested group membership, explicit deny, inherited child ACLs and " +
                    "the protected LAPS encryption principal.", "DC / RSAT workstation"));
            }
            catch (Exception ex)
            {
                output.GroupResolutionStatus = "NotVerified: " +
                    DiagnosticRedaction.Sanitize(ex.Message);
            }

            // A recovery password lives on a CHILD recovery-information object,
            // not necessarily on the computer itself. Read up to three child DACLs.
            try
            {
                var children = _ad.GetRecoveryMetadataForComputer(dc, dn).Take(3).ToList();
                foreach (var child in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var request = new SearchRequest(child.RecoveryDistinguishedName,
                            "(objectClass=msFVE-RecoveryInformation)",
                            SearchScope.Base, "nTSecurityDescriptor");
                        request.Controls.Add(new SecurityDescriptorFlagControl(SecurityMasks.Dacl));
                        var reply = (SearchResponse)conn.SendRequest(request, TimeSpan.FromSeconds(12));
                        if (reply.Entries.Count == 1 &&
                            reply.Entries[0].Attributes["nTSecurityDescriptor"] is { Count: > 0 } acl &&
                            acl[0] is byte[] bytes)
                        {
                            var d = new RawSecurityDescriptor(bytes, 0);
                            foreach (GenericAce raw in d.DiscretionaryAcl?.Cast<GenericAce>().Take(50)
                                 ?? Enumerable.Empty<GenericAce>())
                            {
                                if (raw is not QualifiedAce ace) continue;
                                if ((ace.AccessMask & 0x100) == 0 &&
                                    (ace.AccessMask & 0x10) == 0 &&
                                    ace.AceQualifier != AceQualifier.AccessDenied) continue;
                                output.DescriptorRules.Add(
                                    "Recovery child " + child.RecoveryId + " | " +
                                    ace.AceQualifier + " | " +
                                    ace.SecurityIdentifier.Value + " | mask=0x" +
                                    ace.AccessMask.ToString("X8") + " | " +
                                    ((raw.AceFlags & AceFlags.Inherited) != 0
                                        ? "inherited" : "explicit"));
                            }
                        }
                        else
                        {
                            output.DescriptorRules.Add("Recovery child " +
                                child.RecoveryId + " | DACL NotVerified");
                        }
                    }
                    catch (Exception ex)
                    {
                        output.DescriptorRules.Add("Recovery child " + child.RecoveryId +
                            " | DACL NotVerified: " + DiagnosticRedaction.Sanitize(ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                output.DescriptorRules.Add(
                    "Recovery child DACL inspection NotVerified: " +
                    DiagnosticRedaction.Sanitize(ex.Message));
            }

            output.Status = "ACL observed; effective access NotVerified";
            output.Findings.Add(new("ACL_OBSERVED", DiagnosticSeverity.Info,
                EvidenceStrength.Confirmed, "Computer-object DACL was read",
                output.DescriptorRules.Count + " potentially relevant ACE(s) enumerated. " +
                "Identity SID/group candidates and up to three recovery-child DACLs are included where readable. " +
                "No full effective token, attribute property-set grant, inherited-right/deny ordering " +
                "or effective permission is asserted.",
                "For BitLocker, also inspect the exact msFVE-RecoveryInformation child ACL. " +
                "For LAPS, inspect read/decrypt group and encryption-principal membership separately.",
                "ADUC Security > Advanced / DC"));
            output.Findings.Add(new("ACCESS_UNPROVEN", DiagnosticSeverity.Info,
                EvidenceStrength.NotVerified, "BitLocker/LAPS secret read rights not verified",
                "Reading an object DACL cannot prove that a specific principal can read confidential attributes " +
                "or decrypt Windows LAPS protected values.",
                "Use authorized target-specific password retrieval only when necessary.",
                "Authorized helpdesk workstation"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            output.Status = "NotVerified";
            output.Findings.Add(new("ACL_QUERY_ERROR", DiagnosticSeverity.Warning,
                EvidenceStrength.NotVerified, "Read-only ACL query failed",
                DiagnosticRedaction.Sanitize(ex.Message),
                "Check LDAP authentication, computer DN and READ_CONTROL rights.", "DC / RSAT workstation"));
        }

        return output;
    }

    private List<string> ResolveBoundPrincipalSids(LdapConnection connection, string dc)
    {
        if (!_config.AdUseExplicitCredentials)
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sids = new List<string>();
            if (identity.User is not null) sids.Add(identity.User.Value);
            if (identity.Groups is not null)
                sids.AddRange(identity.Groups.OfType<SecurityIdentifier>().Select(x => x.Value));
            return sids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        var original = _config.AdUsername.Trim();
        if (string.IsNullOrWhiteSpace(original)) return [];
        var sam = original.Contains('\\') ? original[(original.LastIndexOf('\\') + 1)..] :
            original.Contains('@') ? original[..original.IndexOf('@')] : original;

        var root = _ad.GetRootDse(dc);
        var baseDn = root.GetValueOrDefault("defaultNamingContext", string.Empty);
        if (string.IsNullOrWhiteSpace(baseDn)) return [];
        var escapedSam = ActiveDirectoryService.EscapeLdapFilter(sam);
        var escapedUpn = ActiveDirectoryService.EscapeLdapFilter(original);
        var request = new SearchRequest(baseDn,
            "(&(objectCategory=person)(objectClass=user)(|(sAMAccountName=" +
            escapedSam + ")(userPrincipalName=" + escapedUpn + ")))",
            SearchScope.Subtree, "objectSid", "tokenGroups")
        { SizeLimit = 2 };

        var response = (SearchResponse)connection.SendRequest(request, TimeSpan.FromSeconds(15));
        if (response.Entries.Count != 1)
            return []; // Ambiguous/missing account: never guess which identity was bound.
        var attrs = response.Entries[0].Attributes;
        var sids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (attrs["objectSid"] is { Count: > 0 } userSid && userSid[0] is byte[] u)
            sids.Add(new SecurityIdentifier(u, 0).Value);
        if (attrs["tokenGroups"] is { Count: > 0 } groups)
        {
            foreach (var sid in groups)
                if (sid is byte[] data) sids.Add(new SecurityIdentifier(data, 0).Value);
        }
        return sids.ToList();
    }

    private (List<string> RecoveryIds, DateTimeOffset? WindowsExpiry,
        DateTimeOffset? LegacyExpiry, string Version) ReadMetadata(string server, string computerDn)
    {
        var ids = _ad.GetRecoveryMetadataForComputer(server, computerDn)
            .Select(x => x.RecoveryId).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        using var conn = _ad.CreateConnection(server);
        var request = new SearchRequest(computerDn,
            "(objectCategory=computer)", SearchScope.Base,
            "msLAPS-PasswordExpirationTime",
            "ms-Mcs-AdmPwdExpirationTime",
            "msLAPS-CurrentPasswordVersion");
        var response = (SearchResponse)conn.SendRequest(request, TimeSpan.FromSeconds(15));
        if (response.Entries.Count != 1)
            throw new InvalidDataException("Computer metadata could not be fetched by exact DN.");
        var attrs = response.Entries[0].Attributes;

        DateTimeOffset? Expiry(string field)
        {
            if (attrs[field] is not { Count: > 0 } a) return null;
            var s = a[0]?.ToString();
            if (!long.TryParse(s, out var ticks) || ticks <= 0) return null;
            try { return new DateTimeOffset(DateTime.FromFileTimeUtc(ticks)); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        var version = attrs["msLAPS-CurrentPasswordVersion"] is { Count: > 0 } v &&
                      v[0] is byte[] bytes && bytes.Length == 16
            ? new Guid(bytes).ToString("D") : string.Empty;
        return (ids, Expiry("msLAPS-PasswordExpirationTime"),
            Expiry("ms-Mcs-AdmPwdExpirationTime"), version);
    }

    private static int ParentDelimiter(string dn)
    {
        for (var i = 0; i < dn.Length; i++)
        {
            if (dn[i] == '\\') { i++; continue; }
            if (dn[i] == ',') return i;
        }
        return -1;
    }
}
