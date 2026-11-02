namespace BitKeyBridge;

/// <summary>
/// A single source-qualified device record can move from Start -> Devices
/// with no Graph call, device-code prompt, hostname correlation or secret read.
/// The same renderer is used for a fresh Unified Devices search.
/// </summary>
public sealed partial class MainForm
{
    private ListViewItem AddUnifiedDeviceSearchResultRow(UnifiedDeviceInfo row)
    {
        var item = new ListViewItem(row.ComputerName);
        item.SubItems.Add(row.IdentityStatus);
        item.SubItems.Add(row.FoundInAd ? "Yes" : "No");
        item.SubItems.Add(row.FoundInEntra ? "Yes" : "No");
        item.SubItems.Add(row.FoundInIntune ? "Yes" : "No");
        item.SubItems.Add(row.SerialNumber);
        item.SubItems.Add(string.IsNullOrWhiteSpace(row.UserPrincipalName)
            ? row.UserDisplayName : row.UserPrincipalName);
        item.SubItems.Add((row.Manufacturer + " " + row.Model).Trim());
        item.SubItems.Add((row.OperatingSystem + " " + row.OsVersion).Trim());
        item.SubItems.Add(row.ComplianceState);
        item.SubItems.Add(row.IsEncrypted is null ? "-" :
            row.IsEncrypted.Value ? "Yes" : "No");
        item.SubItems.Add(row.LastSyncDateTime?.ToString("yyyy-MM-dd HH:mm")
            ?? string.Empty);
        item.SubItems.Add(row.RecoveryKeyCount == 0 &&
            row.RecoveryMetadataIncomplete ? "?" :
            row.RecoveryKeyCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        item.Tag = row;
        _unifiedResults.Items.Add(item);
        return item;
    }

    /// <summary>
    /// No hostname-only matching. The Start record must carry a complete
    /// source-qualified snapshot obtained in the already completed search.
    /// </summary>
    private bool TryReuseHomeInventoryInDevices(HomeSearchResult selected)
    {
        if (selected is null || selected.IdentityAmbiguous ||
            selected.InventoryMetadata is not { } metadata ||
            metadata.IdentityStatus is "Name collision" or "Ambiguous device ID")
            return false;

        var home = Device360Snapshot.FromHome(selected);
        var unified = Device360Snapshot.FromUnified(metadata);
        if (!home.IdentityVerified || !unified.IdentityVerified ||
            string.IsNullOrWhiteSpace(home.QualifiedIdentity) ||
            !string.Equals(home.QualifiedIdentity, unified.QualifiedIdentity,
                StringComparison.OrdinalIgnoreCase))
            return false;

        // Clear previous confidential recovery results before changing device
        // context. No previous secret is retained or displayed in Devices.
        _deviceCurrentKey = null;
        _deviceRecoveryKey.Clear();
        _deviceRecoveryIds.Items.Clear();

        _unifiedQuery.Text = selected.ComputerName;
        _unifiedResults.Items.Clear();
        _unifiedDetails.Clear();
        var item = AddUnifiedDeviceSearchResultRow(metadata);
        item.Selected = true;
        item.Focused = true;
        _unifiedDetails.Text = Device360Formatter.Format(metadata);
        _device360LastUnifiedPartial = metadata.RecoveryMetadataIncomplete;
        _device360UnifiedLookupFailed = default;
        SetDevicesCloudWarningAction(!HasCloudSearchSession());

        UiStyle.SetStatus(_unifiedStatus,
            "Loaded the exact source-qualified device from Start. " +
            "No new AD/Graph search, MFA prompt or secret retrieval was made. " +
            "Metadata may be stale; press Search for fresh verification.",
            UiStatusKind.Neutral);
        return true;
    }
}
