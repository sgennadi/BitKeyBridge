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
    internal string SerialNumber { get; init; } = string.Empty;
    internal string CpuName { get; init; } = string.Empty;
    internal int CpuLogicalProcessors { get; init; }
    internal decimal MemoryGiB { get; init; }
    internal int PhysicalDiskCount { get; init; }
    internal decimal PhysicalStorageGiB { get; init; }
    internal int PhysicalNetworkAdapterCount { get; init; }
    // Safe typed status only, no raw provider exceptions or addresses.
    internal string ExtendedDiagnostic { get; init; } = string.Empty;
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
    string Explanation)
{
    internal string FailureCategory { get; init; } = string.Empty;
}

internal sealed record Device360OptionalWmiFailure(string Component, string Category);

internal static class Device360HardwareService
{
    // Only locally classified failures are eligible for display or export.
    // Never surface arbitrary WMI exceptions, paths, hostnames or tokens.
    internal static IReadOnlyList<Device360OptionalWmiFailure> OptionalFailures(
        string? status)
    {
        var failures = new List<Device360OptionalWmiFailure>();
        if (string.IsNullOrWhiteSpace(status)) return failures;
        foreach (var segment in status.Split(';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries).Take(8))
        {
            var colon = segment.IndexOf(": ", StringComparison.Ordinal);
            if (colon < 1) continue;
            var component = segment[..colon];
            var category = segment[(colon + 2)..];
            if (component is not ("CPU" or "Disk" or "Network" or "Memory") ||
                category is not ("NoRecord" or "AccessDenied" or "Timeout" or
                    "DCOM/RPCUnavailable" or "WmiError" or "Unavailable") ||
                failures.Any(x => x.Component == component))
                continue;
            failures.Add(new Device360OptionalWmiFailure(component, category));
        }
        return failures;
    }

    internal static string SafeOptionalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return string.Empty;
        var known = OptionalFailures(status);
        if (known.Count == 0) return "Unknown optional WMI status";
        var safe = string.Join("; ", known.Select(x =>
            x.Component + ": " + x.Category));
        var count = status.Split(';',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries).Length;
        return count > known.Count ? safe + "; Other: Unverified" : safe;
    }

    // Contradictory WMI results must not reach UI, Health or export.
    // Typed local failures take precedence over untrusted inventory data.
    internal static Device360HardwareOutcome NormalizeOutcome(
        Device360HardwareOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Succeeded && outcome.Inventory is not null &&
            string.IsNullOrEmpty(outcome.FailureCategory))
            return outcome;

        var category = outcome.FailureCategory switch
        {
            "NoRecord" or "IdentityMismatch" or "AccessDenied" or
                "Timeout" or "DCOM/RPCUnavailable" or "WmiError" or
                "Unavailable" => outcome.FailureCategory,
            _ => outcome.Succeeded && outcome.Inventory is null
                ? "NoRecord" : "Unavailable"
        };
        return new Device360HardwareOutcome(false, null,
            "Hardware inventory was not verified.")
        {
            FailureCategory = category
        };
    }

    // A verified AD DNS host is required: NO discovery by display name,
    // unqualified NetBIOS alias, IP, UPN, serial or Intune-only identity.
    internal static bool CanQuery(Device360Snapshot snapshot)
        => snapshot.IdentityVerified && !snapshot.IdentityAmbiguous &&
           snapshot.InAd == true && IsStrictDnsHost(snapshot.AdDnsHostName) &&
           HasSameAdDomainSuffix(snapshot.AdDnsHostName,
               snapshot.AdDistinguishedName) &&
           HasSameAdComputerName(snapshot.AdDnsHostName,
               snapshot.AdDistinguishedName);

    /// <summary>
    /// A selected AD computer can have a stale dNSHostName that points to a
    /// different computer in the same domain. This is checked offline before
    /// enabling the WMI button or sending any Windows credentials.
    /// </summary>
    internal static bool HasAdDnsTargetMismatch(Device360Snapshot snapshot) =>
        snapshot.IdentityVerified && !snapshot.IdentityAmbiguous &&
        snapshot.InAd == true && IsStrictDnsHost(snapshot.AdDnsHostName) &&
        HasSameAdDomainSuffix(snapshot.AdDnsHostName,
            snapshot.AdDistinguishedName) &&
        !HasSameAdComputerName(snapshot.AdDnsHostName,
            snapshot.AdDistinguishedName);

    // Restrict Windows integrated authentication to the AD DNS domain
    // proven by the exact selected object. Do not contact arbitrary foreign
    // hosts even if an attacker-controlled dnsHostName is present in LDAP.
    internal static bool HasSameAdDomainSuffix(string host, string? objectDn)
    {
        var domain = AdDnsDomain(objectDn);
        if (domain is null) return false;
        var suffix = "." + domain;
        return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            host.Length > suffix.Length;
    }

    private static string? AdDnsDomain(string? objectDn)
    {
        if (string.IsNullOrWhiteSpace(objectDn)) return null;
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
            return null;
        return string.Join(".", parts);
    }

    /// <summary>
    /// Require the selected AD computer RDN and its DNS hostname to agree
    /// *before* any WMI connection. Reject malformed/escaped computer RDNs,
    /// stale DNS attributes and misleading aliases.
    /// </summary>
    internal static bool HasSameAdComputerName(
        string? dnsHost, string? selectedAdDn)
    {
        if (!IsStrictDnsHost(dnsHost) ||
            string.IsNullOrWhiteSpace(selectedAdDn))
            return false;
        var adCn = Regex.Match(selectedAdDn,
            @"\A\s*CN=([a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)\s*,",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return adCn.Success &&
            string.Equals(dnsHost!.Split('.')[0], adCn.Groups[1].Value,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Consistency check for a remote Windows computer, not independent
    /// cryptographic host authentication. Fail closed for stale DNS/aliases.
    /// </summary>
    internal static bool HasMatchingRemoteWmiIdentity(
        string? dnsHost, string? selectedAdDn, string? remoteName,
        string? remoteDomain, bool joined)
    {
        if (!joined || !IsStrictDnsHost(dnsHost) ||
            !HasSameAdDomainSuffix(dnsHost!, selectedAdDn))
            return false;
        var domain = AdDnsDomain(selectedAdDn);
        if (string.IsNullOrEmpty(domain) || string.IsNullOrWhiteSpace(remoteName) ||
            string.IsNullOrWhiteSpace(remoteDomain) ||
            !HasSameAdComputerName(dnsHost, selectedAdDn))
            return false;
        return string.Equals(dnsHost!.Split('.')[0], remoteName.Trim(),
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(domain, remoteDomain.Trim().TrimEnd('.'),
                   StringComparison.OrdinalIgnoreCase);
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
        var adDn = snapshot.AdDistinguishedName;
        return Task.Run(() => QueryWindows(dns, adDn, ct), ct);
    }

    internal static string FormatGiB(decimal size) =>
        size <= 0 ? "Not verified" :
        size.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GiB";

    private static decimal GiB(decimal bytes) => bytes / 1073741824m;

    private static Device360HardwareOutcome QueryWindows(
        string dnsHost, string? selectedAdDn, CancellationToken ct)
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
            var serialNumber = string.Empty;
            decimal memoryGiB = 0m;
            var remoteName = string.Empty;
            var remoteDomain = string.Empty;
            var domainJoined = false;
            var foundComputerSystem = false;

            using (var searcher = NewSearcher(scope,
                "SELECT Name,Domain,PartOfDomain,Manufacturer,Model,TotalPhysicalMemory FROM Win32_ComputerSystem"))
            using (var objects = searcher.Get())
            {
                foreach (ManagementObject row in objects)
                {
                    using (row)
                    {
                        foundComputerSystem = true;
                        remoteName = Read(row, "Name");
                        remoteDomain = Read(row, "Domain");
                        domainJoined = row["PartOfDomain"] is bool joined && joined;
                        manufacturer = Read(row, "Manufacturer");
                        model = Read(row, "Model");
                        if (ulong.TryParse(Read(row, "TotalPhysicalMemory"),
                                System.Globalization.NumberStyles.None,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var memoryBytes) && memoryBytes > 0)
                            memoryGiB = GiB(memoryBytes);
                    }
                    break;
                }
            }
            ct.ThrowIfCancellationRequested();

            if (!foundComputerSystem)
                return new Device360HardwareOutcome(false, null,
                    "The remote WMI computer-system record was not verified.")
                {
                    FailureCategory = "NoRecord"
                };
            if (!HasMatchingRemoteWmiIdentity(dnsHost, selectedAdDn,
                    remoteName, remoteDomain, domainJoined))
                return new Device360HardwareOutcome(false, null,
                    "Remote Windows identity did not match the selected AD record; inventory rejected.")
                {
                    FailureCategory = "IdentityMismatch"
                };

            // Never query BIOS, TPM, CPU, disks or adapters if the reported
            // machine identity differs from the selected AD computer.
            using (var searcher = NewSearcher(scope,
                "SELECT Manufacturer,SMBIOSBIOSVersion,ReleaseDate,SerialNumber FROM Win32_BIOS"))
            using (var objects = searcher.Get())
            {
                foreach (ManagementObject row in objects)
                {
                    using (row)
                    {
                        biosManufacturer = Read(row, "Manufacturer");
                        biosVersion = Read(row, "SMBIOSBIOSVersion");
                        serialNumber = Read(row, "SerialNumber");
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
                    "Remote WMI returned no verified computer-system or BIOS record.")
                {
                    FailureCategory = "NoRecord"
                };

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
            // Optional read-only providers. A provider failure must never
            // suppress earlier BIOS/TPM evidence or trigger permission changes.
            var partial = new List<string>();
            var cpuName = string.Empty;
            var logicalProcessors = 0;
            var diskCount = 0;
            decimal diskGiB = 0m;
            var adapterCount = 0;
            try
            {
                ct.ThrowIfCancellationRequested();
                using var searcher = NewSearcher(scope,
                    "SELECT Name,NumberOfLogicalProcessors FROM Win32_Processor");
                using var rows = searcher.Get();
                foreach (ManagementObject row in rows)
                {
                    using (row)
                    {
                        if (cpuName.Length == 0) cpuName = Read(row, "Name");
                        if (int.TryParse(Read(row, "NumberOfLogicalProcessors"),
                                out var count) && count > 0 &&
                            logicalProcessors <= int.MaxValue - count)
                            logicalProcessors += count;
                    }
                }
                if (cpuName.Length == 0) partial.Add("CPU: NoRecord");
            }
            catch (Exception ex) when (IsExpectedWmiFailure(ex))
            {
                partial.Add("CPU: " + ErrorCategory(ex));
            }
            try
            {
                ct.ThrowIfCancellationRequested();
                using var searcher = NewSearcher(scope,
                    "SELECT Size FROM Win32_DiskDrive");
                using var rows = searcher.Get();
                decimal totalBytes = 0m;
                foreach (ManagementObject row in rows)
                {
                    using (row)
                    {
                        ++diskCount;
                        if (ulong.TryParse(Read(row, "Size"),
                                System.Globalization.NumberStyles.None,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var size))
                            totalBytes += size;
                    }
                }
                diskGiB = GiB(totalBytes);
                if (diskCount == 0) partial.Add("Disk: NoRecord");
            }
            catch (Exception ex) when (IsExpectedWmiFailure(ex))
            {
                partial.Add("Disk: " + ErrorCategory(ex));
            }
            try
            {
                ct.ThrowIfCancellationRequested();
                using var searcher = NewSearcher(scope,
                    "SELECT PhysicalAdapter FROM Win32_NetworkAdapter");
                using var rows = searcher.Get();
                foreach (ManagementObject row in rows)
                {
                    using (row)
                    {
                        if (row["PhysicalAdapter"] is bool physical && physical)
                            ++adapterCount;
                    }
                }
                if (adapterCount == 0)
                    partial.Add("Network: NoRecord");
            }
            catch (Exception ex) when (IsExpectedWmiFailure(ex))
            {
                partial.Add("Network: " + ErrorCategory(ex));
            }
            if (memoryGiB <= 0) partial.Add("Memory: NoRecord");

            ct.ThrowIfCancellationRequested();
            return new Device360HardwareOutcome(true,
                new Device360HardwareInventory
                {
                    Manufacturer = manufacturer,
                    Model = model,
                    SerialNumber = serialNumber,
                    CpuName = cpuName,
                    CpuLogicalProcessors = logicalProcessors,
                    MemoryGiB = memoryGiB,
                    PhysicalDiskCount = diskCount,
                    PhysicalStorageGiB = diskGiB,
                    PhysicalNetworkAdapterCount = adapterCount,
                    ExtendedDiagnostic = string.Join("; ", partial),
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
                FailureGuidance(category) + " Nothing was modified.")
            {
                FailureCategory = category
            };
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
