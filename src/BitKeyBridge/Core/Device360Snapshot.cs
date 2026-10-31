namespace BitKeyBridge;

/// <summary>
/// The common, metadata-only projection used by the Start and Devices
/// computer card. It intentionally has NO recovery ID, recovery password,
/// LAPS password, token or credential property. No network lookup is performed.
/// </summary>
internal sealed class Device360Snapshot
{
    internal string Computer { get; init; } = string.Empty;
    internal string QualifiedIdentity { get; init; } = string.Empty;
    internal bool IdentityVerified { get; init; }
    internal bool IdentityAmbiguous { get; init; }
    internal string IdentityWarning { get; init; } = string.Empty;

    // Null means the source was not verified, NOT that the device is absent.
    internal bool? InAd { get; init; }
    internal bool? InEntra { get; init; }
    internal bool? InIntune { get; init; }

    internal int BitLockerMetadataCount { get; init; }
    internal bool BitLockerMetadataIncomplete { get; init; } = true;
    internal bool LapsMetadataDetected { get; init; }
    internal bool LapsMetadataExpired { get; init; }
    internal DateTime? LapsExpirationUtc { get; init; }

    internal string AdObjectGuid { get; init; } = string.Empty;
    internal string AdDistinguishedName { get; init; } = string.Empty;
    internal string EntraDeviceId { get; init; } = string.Empty;
    internal string IntuneManagedDeviceId { get; init; } = string.Empty;
    internal string OperatingSystem { get; init; } = string.Empty;
    internal string Hardware { get; init; } = string.Empty;
    internal string SerialNumber { get; init; } = string.Empty;
    internal string AssignedUser { get; init; } = string.Empty;
    internal string Compliance { get; init; } = string.Empty;
    internal bool? Encrypted { get; init; }
    internal DateTime? LastSyncUtc { get; init; }
    internal DateTime? AdLastLogonUtc { get; init; }

    internal static Device360Snapshot FromHome(HomeSearchResult row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var cloud = row.IsCloudRecord;
        var sourceId = cloud ? row.SourceIdentity : string.Empty;
        var adGuid = !cloud && Guid.TryParse(row.ComputerId, out var guid)
            ? guid.ToString("D") : string.Empty;
        var adDn = cloud ? string.Empty : row.AdComputerDistinguishedName;
        var qualifiedId = cloud
            ? SafeQualifiedIdentity(sourceId)
            : !string.IsNullOrWhiteSpace(adGuid)
                ? "AD:GUID:" + adGuid
                : !string.IsNullOrWhiteSpace(adDn)
                    ? "AD:DN:" + adDn
                    : string.Empty;
        var lapsDetected = !cloud && !row.LapsLookupFailed &&
            row.LapsCandidateFound && row.LapsStatus is ("Detected" or "Expired");
        return new Device360Snapshot
        {
            Computer = row.ComputerName,
            QualifiedIdentity = qualifiedId,
            IdentityAmbiguous = row.IdentityAmbiguous,
            IdentityVerified = !row.IdentityAmbiguous &&
                !string.IsNullOrWhiteSpace(qualifiedId),
            InAd = cloud ? null : true,
            InEntra = cloud && sourceId.StartsWith("ENTRA:",
                StringComparison.OrdinalIgnoreCase) ? true :
                cloud && Guid.TryParse(row.ComputerId, out _) ? true : null,
            InIntune = cloud && sourceId.StartsWith("INTUNE:",
                StringComparison.OrdinalIgnoreCase) ? true : null,
            BitLockerMetadataCount = Math.Max(0, row.BitLockerKeyCount),
            BitLockerMetadataIncomplete = row.BitLockerLookupFailed ||
                row.BitLockerKeyCount <= 0,
            LapsMetadataDetected = lapsDetected,
            LapsMetadataExpired = lapsDetected && row.LapsStatus == "Expired",
            LapsExpirationUtc = lapsDetected ? row.LapsExpiresAtUtc : null,
            AdObjectGuid = adGuid,
            AdDistinguishedName = adDn,
            EntraDeviceId = cloud && Guid.TryParse(row.ComputerId, out _)
                ? row.ComputerId : string.Empty,
            AdLastLogonUtc = null
        };
    }

    internal static Device360Snapshot FromUnified(UnifiedDeviceInfo row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var qualifiedId = SafeQualifiedIdentity(row.IdentityKey);
        var ambiguous = row.IdentityStatus is
            "Name collision" or "Ambiguous device ID";
        return new Device360Snapshot
        {
            Computer = row.ComputerName,
            QualifiedIdentity = qualifiedId,
            IdentityAmbiguous = ambiguous,
            IdentityVerified = !ambiguous &&
                !string.IsNullOrWhiteSpace(qualifiedId),
            IdentityWarning = row.IdentityWarning,
            InAd = row.FoundInAd ? true : null,
            InEntra = row.FoundInEntra ? true : null,
            InIntune = row.FoundInIntune ? true : null,
            BitLockerMetadataCount = row.RecoveryKeyCount,
            BitLockerMetadataIncomplete = row.RecoveryMetadataIncomplete ||
                row.RecoveryKeyCount <= 0,
            // Unified Devices does not query LAPS. This stays unverified.
            LapsMetadataDetected = false,
            AdObjectGuid = row.AdObjectGuid,
            AdDistinguishedName = row.AdDistinguishedName,
            EntraDeviceId = row.EntraDeviceId,
            IntuneManagedDeviceId = row.ManagedDeviceId,
            OperatingSystem = (row.OperatingSystem + " " + row.OsVersion).Trim(),
            Hardware = (row.Manufacturer + " " + row.Model).Trim(),
            SerialNumber = row.SerialNumber,
            AssignedUser = string.IsNullOrWhiteSpace(row.UserPrincipalName)
                ? row.UserDisplayName : row.UserPrincipalName,
            Compliance = row.ComplianceState,
            Encrypted = row.IsEncrypted,
            LastSyncUtc = row.LastSyncDateTime,
            AdLastLogonUtc = row.AdLastLogonTimestamp
        };
    }

    // A recovery-ID-only ENTRA:KEY record is NOT an exact device identity.
    // Never display a recovery identifier disguised as a qualified device ID.
    private static string SafeQualifiedIdentity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var candidate = raw.Trim();
        if (!candidate.StartsWith("AD:", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("ENTRA:DEVICE:",
                StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("INTUNE:MANAGED:",
                StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("INTUNE:DEVICE:",
                StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (candidate.Contains(":KEY:", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains(":UNRESOLVED:",
                StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return candidate;
    }
}
