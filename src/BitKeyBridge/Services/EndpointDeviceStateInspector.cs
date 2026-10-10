using System.Management;

namespace BitKeyBridge;

public sealed record EndpointBitLockerVolumeState(
    string MountPoint,
    string ProtectionStatus,
    string ConversionStatus,
    string EncryptionMethod);

public sealed record EndpointTpmState(
    string Status,
    string Enabled,
    string Activated,
    string Owned,
    string SpecificationVersion);

/// <summary>Local-only WMI/CIM snapshot. Only nonsecret status properties
/// are selected. KeyProtector IDs, recovery data, credentials and blobs
/// are not queried or exported.</summary>
public static class EndpointDeviceStateInspector
{
    public static void Append(EndpointDiagnosticBundle bundle)
    {
        if (!OperatingSystem.IsWindows())
        {
            bundle.Diagnostics.Add("BitLocker/TPM WMI: NotVerified (Windows required).");
            return;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftVolumeEncryption",
                "SELECT DriveLetter, ProtectionStatus, ConversionStatus, EncryptionMethod FROM Win32_EncryptableVolume");
            using var objects = searcher.Get();
            foreach (ManagementObject item in objects)
            {
                using (item)
                {
                    var volume = item["DriveLetter"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(volume))
                        continue;
                    bundle.VolumeStates.Add(new EndpointBitLockerVolumeState(
                        volume,
                        Protection(item["ProtectionStatus"]),
                        Conversion(item["ConversionStatus"]),
                        item["EncryptionMethod"]?.ToString() ?? "NotVerified"));
                    if (bundle.VolumeStates.Count >= 30) break;
                }
            }
            if (bundle.VolumeStates.Count == 0)
                bundle.Diagnostics.Add("BitLocker status WMI: no volumes returned; state NotVerified.");
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException
                                   or System.Runtime.InteropServices.COMException
                                   or PlatformNotSupportedException)
        {
            bundle.Diagnostics.Add("BitLocker status WMI: NotVerified (" +
                DiagnosticRedaction.Sanitize(ex.GetType().Name) + ").");
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftTpm",
                "SELECT IsEnabled_InitialValue, IsActivated_InitialValue, IsOwned_InitialValue, SpecVersion FROM Win32_Tpm");
            using var objects = searcher.Get();
            var found = false;
            foreach (ManagementObject item in objects)
            {
                using (item)
                {
                    bundle.Tpm = new EndpointTpmState(
                        "Observed",
                        State(item["IsEnabled_InitialValue"]),
                        State(item["IsActivated_InitialValue"]),
                        State(item["IsOwned_InitialValue"]),
                        item["SpecVersion"]?.ToString() ?? "NotVerified");
                    found = true;
                    break;
                }
            }
            if (!found)
                bundle.Diagnostics.Add("TPM WMI: no local TPM object returned; state NotVerified.");
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException
                                   or System.Runtime.InteropServices.COMException
                                   or PlatformNotSupportedException)
        {
            bundle.Diagnostics.Add("TPM status WMI: NotVerified (" +
                DiagnosticRedaction.Sanitize(ex.GetType().Name) + ").");
        }
    }

    private static string State(object? value) => value is bool b ? b ? "True" : "False" :
        "NotVerified";

    private static string Protection(object? value) => value?.ToString() switch
    {
        "0" => "Unprotected",
        "1" => "Protected",
        _ => "NotVerified"
    };

    private static string Conversion(object? value) => value?.ToString() switch
    {
        "0" => "FullyDecrypted",
        "1" => "FullyEncrypted",
        "2" => "EncryptionInProgress",
        "3" => "DecryptionInProgress",
        "4" => "EncryptionPaused",
        "5" => "DecryptionPaused",
        _ => "NotVerified"
    };
}
