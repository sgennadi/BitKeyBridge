using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BitKeyBridge;

/// <summary>
/// Optional, operator-initiated, read-only remote WMI inventory. Never reads
/// BitLocker key-protector data, LAPS attributes, TPM secrets or credentials.
/// Uses the signed-in Windows identity; no UAC or credential fallback.
/// </summary>
internal sealed record Device360HardwareInventory
{
    internal string Source { get; init; } = "Remote WMI (Windows integrated)";
    internal string Manufacturer { get; init; } = string.Empty;
    internal string Model { get; init; } = string.Empty;
    internal string BiosVersion { get; init; } = string.Empty;
    internal string BiosManufacturer { get; init; } = string.Empty;
    internal string BiosReleaseDate { get; init; } = string.Empty;
    internal string TpmSpecification { get; init; } = string.Empty;
    internal string TpmEnabled { get; init; } = string.Empty;
    internal string TpmActivated { get; init; } = string.Empty;
    internal string TpmOwned { get; init; } = string.Empty;
    internal string TpmDiagnostic { get; init; } = string.Empty;
    internal DateTime CollectedUtc { get; init; } = DateTime.UtcNow;
}

internal sealed record Device360HardwareOutcome(
    bool Succeeded, Device360HardwareInventory? Inventory,
    string Explanation);

internal static class Device360HardwareService
{
    // A verified AD DNS host is required: NO discovery by display name,
    // unqualified NetBIOS alias, IP, UPN, serial or Intune-only identity.
    internal static bool CanQuery(Device360Snapshot snapshot)
        => snapshot.IdentityVerified && !snapshot.IdentityAmbiguous &&
           snapshot.InAd == true && IsStrictDnsHost(snapshot.AdDnsHostName) &&
           HasSameAdDomainSuffix(snapshot.AdDnsHostName,
               snapshot.AdDistinguishedName);

    // Restrict Windows integrated authentication to the AD DNS domain
    // proven by the exact selected object. Do not contact arbitrary foreign
    // hosts even if an attacker-controlled dnsHostName is present in LDAP.
    internal static bool HasSameAdDomainSuffix(string host, string? objectDn)
    {
        if (string.IsNullOrWhiteSpace(objectDn)) return false;
        var parts = Regex.Matches(objectDn,
                @"(?:^|,)\s*DC=([^,]+)", RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value.Trim())
            .ToArray();
        if (parts.Length < 2 || parts.Any(label =>
                !Regex.IsMatch(label,
                    @"\A[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\z",
                    RegexOptions.CultureInvariant)))
            return false;
        var suffix = "." + string.Join(".", parts);
        return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            host.Length > suffix.Length;
    }

    internal static bool IsStrictDnsHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 ||
            host.Contains("..", StringComparison.Ordinal))
            return false;
        var parts = host.Split('.');
        if (parts.Length < 2) return false;
        return parts.All(label =>
            label.Length is >= 1 and <= 63 &&
            Regex.IsMatch(label, @"\A[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\z",
                RegexOptions.CultureInvariant));
    }

    internal static Task<Device360HardwareOutcome> QueryAsync(
        Device360Snapshot snapshot, CancellationToken ct)
    {
        if (!CanQuery(snapshot))
            return Task.FromResult(new Device360HardwareOutcome(false, null,
                "Remote hardware check requires an exact AD object with a verified DNS hostname. A matching name or cloud ID is insufficient."));
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new Device360HardwareOutcome(false, null,
                "Read-only WMI checks require Windows."));

        var dns = snapshot.AdDnsHostName;
        return Task.Run(() => QueryWindows(dns, ct), ct);
    }

    private static Device360HardwareOutcome QueryWindows(
        string dnsHost, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var scope = new ManagementScope(
                @"\\" + dnsHost + @"\root\cimv2", Options());
            scope.Connect();

            var manufacturer = string.Empty;
            var model = string.Empty;
            var biosVersion = string.Empty;
            var biosManufacturer = string.Empty;
            var biosDate = string.Empty;

            using (var searcher = NewSearcher(scope,
                "SELECT Manufacturer,Model FROM Win32_ComputerSystem"))
            using (var objects = searcher.Get())
            {
                foreach (ManagementObject row in objects)
                {
                    using (row)
                    {
                        manufacturer = Read(row, "Manufacturer");
                        model = Read(row, "Model");
                    }
                    break;
                }
            }
            ct.ThrowIfCancellationRequested();

            using (var searcher = NewSearcher(scope,
                "SELECT Manufacturer,SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS"))
            using (var objects = searcher.Get())
            {
                foreach (ManagementObject row in objects)
                {
                    using (row)
                    {
                        biosManufacturer = Read(row, "Manufacturer");
                        biosVersion = Read(row, "SMBIOSBIOSVersion");
                        var rawDate = Read(row, "ReleaseDate");
                        if (rawDate.Length >= 8 &&
                            DateTime.TryParseExact(rawDate[..8], "yyyyMMdd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None,
                                out var date))
                            biosDate = date.ToString("yyyy-MM-dd",
                                System.Globalization.CultureInfo.InvariantCulture);
                    }
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(manufacturer) &&
                string.IsNullOrWhiteSpace(model) &&
                string.IsNullOrWhiteSpace(biosVersion))
                return new Device360HardwareOutcome(false, null,
                    "Remote WMI returned no computer-system or BIOS inventory. Check WMI service and read permissions.");

            var tpmSpec = string.Empty;
            var enabled = string.Empty;
            var activated = string.Empty;
            var owned = string.Empty;
            var tpmDiagnostic = "Not verified: TPM namespace not queried.";
            try
            {
                ct.ThrowIfCancellationRequested();
                var tpmScope = new ManagementScope(
                    @"\\" + dnsHost + @"\root\cimv2\security\microsofttpm", Options());
                tpmScope.Connect();
                using var searcher = NewSearcher(tpmScope,
                    "SELECT SpecVersion,IsEnabled_InitialValue,IsActivated_InitialValue,IsOwned_InitialValue FROM Win32_Tpm");
                using var objects = searcher.Get();
                var observed = false;
                foreach (ManagementObject row in objects)
                {
                    using (row)
                    {
                        tpmSpec = Read(row, "SpecVersion");
                        enabled = Boolean(row, "IsEnabled_InitialValue");
                        activated = Boolean(row, "IsActivated_InitialValue");
                        owned = Boolean(row, "IsOwned_InitialValue");
                    }
                    observed = true;
                    break;
                }
                tpmDiagnostic = observed
                    ? "Observed through remote read-only WMI; TPM keys and ownership secrets were not requested."
                    : "TPM was not returned by the selected Windows host.";
            }
            catch (Exception ex) when (IsExpectedWmiFailure(ex))
            {
                tpmDiagnostic = "TPM not verified: " +
                    ErrorCategory(ex) + ". BIOS/computer inventory is still available.";
            }
            ct.ThrowIfCancellationRequested();
            return new Device360HardwareOutcome(true,
                new Device360HardwareInventory
                {
                    Manufacturer = manufacturer,
                    Model = model,
                    BiosVersion = biosVersion,
                    BiosManufacturer = biosManufacturer,
                    BiosReleaseDate = biosDate,
                    TpmSpecification = tpmSpec,
                    TpmEnabled = enabled,
                    TpmActivated = activated,
                    TpmOwned = owned,
                    TpmDiagnostic = tpmDiagnostic
                }, "Non-secret remote WMI inventory received for the verified AD DNS host.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new Device360HardwareOutcome(false, null,
                "Hardware check canceled; no data was changed.");
        }
        catch (Exception ex) when (IsExpectedWmiFailure(ex))
        {
            var category = ErrorCategory(ex);
            return new Device360HardwareOutcome(false, null,
                "Remote WMI not available (" + category + "). " +
                FailureGuidance(category) + " Nothing was modified.");
        }
    }

    private static ConnectionOptions Options() => new()
    {
        Authentication = AuthenticationLevel.PacketPrivacy,
        Impersonation = ImpersonationLevel.Impersonate,
        EnablePrivileges = false,
        Timeout = TimeSpan.FromSeconds(7)
    };

    private static ManagementObjectSearcher NewSearcher(
        ManagementScope scope, string query) =>
        new(scope, new ObjectQuery(query),
            new System.Management.EnumerationOptions
            {
                ReturnImmediately = false,
                Timeout = TimeSpan.FromSeconds(7)
            });

    private static string Read(ManagementBaseObject row, string property)
    {
        var text = row[property]?.ToString() ?? string.Empty;
        return text.Length > 180 ? text[..180] : text;
    }

    private static string Boolean(ManagementBaseObject row, string property) =>
        row[property] is bool value ? value ? "Yes" : "No" : "Not verified";

    private static bool IsExpectedWmiFailure(Exception ex) =>
        ex is ManagementException or COMException or UnauthorizedAccessException
            or System.Security.SecurityException or TimeoutException
            or InvalidOperationException or ArgumentException;

    // A generic COMException is NOT proof that the firewall/RPC failed.
    // Distinguish real RPC failures, WMI provider faults, access denial and
    // timeouts without exposing remote exception messages or account details.
    // ManagementException exposes a WBEM status rather than an ordinary
    // COM HResult, so use ErrorCode for its classification.
    internal static string ErrorCategory(Exception ex)
    {
        if (ex is UnauthorizedAccessException or System.Security.SecurityException)
            return "AccessDenied";
        if (ex is TimeoutException)
            return "Timeout";

        var code = ex switch
        {
            ManagementException management => (int)management.ErrorCode,
            COMException com => com.HResult,
            _ => 0
        };
        return code switch
        {
            unchecked((int)0x80070005) or // E_ACCESSDENIED
            unchecked((int)0x80041003) or // WBEM_E_ACCESS_DENIED
            unchecked((int)0x80041062) => "AccessDenied", // WBEM_E_PRIVILEGE_NOT_HELD
            unchecked((int)0x80041069) or // WBEM_E_TIMED_OUT
            unchecked((int)0x80041088) or // WBEM_E_PROVIDER_TIMED_OUT
            unchecked((int)0x800705B4) => "Timeout", // WAIT_TIMEOUT
            unchecked((int)0x800706BA) or // RPC_S_SERVER_UNAVAILABLE
            unchecked((int)0x800706BE) or // RPC_S_CALL_FAILED
            unchecked((int)0x800706D9) => "DCOM/RPCUnavailable", // EPT_S_NOT_REGISTERED
            unchecked((int)0x8004100E) or // WBEM_E_INVALID_NAMESPACE
            unchecked((int)0x80041010) or // WBEM_E_INVALID_CLASS
            unchecked((int)0x80041004) => "WmiError", // WBEM_E_PROVIDER_FAILURE
            _ => ex is ManagementException ? "WmiError" : "Unavailable"
        };
    }

    private static string FailureGuidance(string category) => category switch
    {
        "AccessDenied" => "Check the signed-in operator's existing DCOM/WMI namespace read permissions.",
        "Timeout" => "The remote query timed out. Check host availability and the authorized network path.",
        "DCOM/RPCUnavailable" => "Check DNS, TCP 135, dynamic RPC connectivity and the existing firewall policy.",
        "WmiError" => "Check the WMI namespace, service and provider state on the selected Windows host.",
        _ => "Check device connectivity and read-only WMI availability."
    };
}
