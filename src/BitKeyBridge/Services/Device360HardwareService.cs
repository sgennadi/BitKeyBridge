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
           snapshot.InAd == true && IsStrictDnsHost(snapshot.AdDnsHostName);

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
            using var scope = new ManagementScope(
                @"\\" + dnsHost + @"\root\cimv2", Options());
            scope.Connect();

            var manufacturer = string.Empty;
            var model = string.Empty;
            var biosVersion = string.Empty;
            var biosManufacturer = string.Empty;
            var biosDate = string.Empty;

            using (var objects = Find(scope,
                "SELECT Manufacturer,Model FROM Win32_ComputerSystem"))
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

            using (var objects = Find(scope,
                "SELECT Manufacturer,SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS"))
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
                using var tpmScope = new ManagementScope(
                    @"\\" + dnsHost + @"\root\cimv2\security\microsofttpm", Options());
                tpmScope.Connect();
                using var objects = Find(tpmScope,
                    "SELECT SpecVersion,IsEnabled_InitialValue,IsActivated_InitialValue,IsOwned_InitialValue FROM Win32_Tpm");
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
            return new Device360HardwareOutcome(false, null,
                "Remote WMI not available (" + ErrorCategory(ex) +
                "). Check device reachability, DCOM/RPC firewall policy, WMI service and delegated read permissions. Nothing was modified.");
        }
    }

    private static ConnectionOptions Options() => new()
    {
        Authentication = AuthenticationLevel.PacketPrivacy,
        Impersonation = ImpersonationLevel.Impersonate,
        EnablePrivileges = false,
        Timeout = TimeSpan.FromSeconds(7)
    };

    private static ManagementObjectCollection Find(
        ManagementScope scope, string query)
    {
        using var searcher = new ManagementObjectSearcher(scope,
            new ObjectQuery(query), new EnumerationOptions
            {
                ReturnImmediately = false,
                Timeout = TimeSpan.FromSeconds(7)
            });
        return searcher.Get();
    }

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

    private static string ErrorCategory(Exception ex) => ex switch
    {
        UnauthorizedAccessException or System.Security.SecurityException
            => "AccessDenied",
        TimeoutException => "Timeout",
        COMException => "DCOM/RPCUnavailable",
        ManagementException => "WmiError",
        _ => "Unavailable"
    };
}
