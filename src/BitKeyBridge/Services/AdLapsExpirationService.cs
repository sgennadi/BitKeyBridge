using System.DirectoryServices.Protocols;
using System.Globalization;

namespace BitKeyBridge;

/// <summary>
/// Schedules a LAPS rotation at the device's NEXT policy processing cycle by
/// expiring only its AD timestamp attribute. No password value is read/written.
/// Callers must enforce RBAC, operator opt-in, exact-object selection and audit.
/// </summary>
public sealed class AdLapsExpirationService
{
    private readonly ActiveDirectoryService _ad;
    public AdLapsExpirationService(AppConfig config)
    {
        _ad = new ActiveDirectoryService(config);
    }

    public static string AttributeFor(LapsCoverageDevice row) =>
        row.BackupEvidence switch
        {
            "Windows" => "msLAPS-PasswordExpirationTime",
            "Legacy" => "ms-Mcs-AdmPwdExpirationTime",
            _ => throw new InvalidOperationException(
                "Only exactly identified Windows or Legacy LAPS metadata " +
                "may be expired. Mixed/unverified policies require manual review.")
        };

    public Task RequestExpirationAsync(
        LapsCoverageDevice selected, CancellationToken ct = default)
    {
        if (!selected.CanRequestAdExpiration)
            throw new InvalidOperationException(
                "An exact object GUID and a single LAPS policy are required.");
        return Task.Run(() => RequestExpiration(selected, ct), ct);
    }

    private void RequestExpiration(LapsCoverageDevice selected, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Guid.TryParse(selected.ObjectGuid, out var guid))
            throw new ArgumentException("Invalid AD computer objectGUID.");

        var attribute = AttributeFor(selected);
        var dc = _ad.GetPreferredWritableDc();
        var baseDn = _ad.GetRootDse(dc)["defaultNamingContext"];
        var hexFilter = string.Concat(guid.ToByteArray()
            .Select(b => "\\" + b.ToString("x2", CultureInfo.InvariantCulture)));
        var filter = "(&(objectCategory=computer)(objectGUID=" + hexFilter + "))";

        using var connection = _ad.CreateConnection(dc);
        var request = new SearchRequest(
            baseDn, filter, SearchScope.Subtree,
            "name", "objectGUID", attribute);
        var response = (SearchResponse)connection.SendRequest(
            request, TimeSpan.FromSeconds(30));
        if (response.Entries.Count != 1)
            throw new InvalidOperationException(
                "Exact AD computer objectGUID could not be resolved uniquely.");

        var entry = response.Entries[0];
        if (!entry.DistinguishedName.Equals(selected.DistinguishedName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "AD object DN changed since the metadata scan. Scan again.");
        }

        if (entry.Attributes[attribute] is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                "The selected LAPS expiration attribute is no longer visible. " +
                "Permissions/policy may have changed; scan again.");
        }

        // The expiration time is in the past. The remote computer will rotate
        // only when it next applies its LAPS policy (not synchronously here).
        var expiration = DateTime.UtcNow.AddMinutes(-1)
            .ToFileTimeUtc()
            .ToString(CultureInfo.InvariantCulture);
        var change = new DirectoryAttributeModification
        {
            Name = attribute,
            Operation = DirectoryAttributeOperation.Replace
        };
        change.Add(expiration);
        ct.ThrowIfCancellationRequested();
        var write = new ModifyRequest(entry.DistinguishedName, change);
        var written = (ModifyResponse)connection.SendRequest(
            write, TimeSpan.FromSeconds(30));
        if (written.ResultCode != ResultCode.Success)
            throw new DirectoryOperationException(written);
    }
}
