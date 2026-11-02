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
    internal bool PreviewDevice360SharedSearchForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI self-test only.");

        var record = new UnifiedDeviceInfo
        {
            ComputerName = "UI-EXACT-SESSION-001",
            IdentityKey = "INTUNE:MANAGED:ui-exact-device-001",
            IdentityStatus = "Intune object",
            FoundInIntune = true,
            FoundInEntra = true,
            ManagedDeviceId = "ui-exact-device-001",
            EntraDeviceId = "a9c5e909-23a2-4274-8786-714d6c8ef305",
            Manufacturer = "Synthetic Test Manufacturer",
            Model = "Synthetic Model",
            OperatingSystem = "Windows",
            OsVersion = "11"
        };
        var row = new HomeSearchResult
        {
            ComputerName = record.ComputerName,
            SourceIdentity = record.IdentityKey,
            ComputerId = record.EntraDeviceId,
            IsCloudRecord = true,
            InventoryMetadata = record
        };
        try
        {
            if (!TryReuseHomeInventoryInDevices(row) ||
                _unifiedResults.Items.Count != 1 ||
                !ReferenceEquals(_unifiedResults.Items[0].Tag, record) ||
                _cloudToken is not null)
                return false;

            // An unrelated authoritative ID must never be accepted solely
            // because a hostname matches; stale context must remain intact.
            var forged = new HomeSearchResult
            {
                ComputerName = record.ComputerName,
                IsCloudRecord = true,
                SourceIdentity = "INTUNE:MANAGED:a-different-object",
                InventoryMetadata = record
            };
            if (TryReuseHomeInventoryInDevices(forged) ||
                _unifiedResults.Items.Count != 1 ||
                !ReferenceEquals(_unifiedResults.Items[0].Tag, record))
                return false;

            // AD-only inventory from the same Start search may be
            // DN-qualified here and GUID-qualified in Unified Devices.
            // No Graph session exists and no new search is needed.
            var adRecord = new UnifiedDeviceInfo
            {
                ComputerName = "UI-AD-EXACT-001",
                IdentityKey = "AD:GUID:00000000-0000-0000-0000-000000000099",
                IdentityStatus = "AD object",
                FoundInAd = true,
                AdObjectGuid = "00000000-0000-0000-0000-000000000099",
                AdDistinguishedName = "CN=UI-AD-EXACT-001,OU=Test,DC=example,DC=com",
                AdDnsHostName = "ui-ad-exact-001.example.com",
                OperatingSystem = "Windows 11",
                RecoveryMetadataIncomplete = true
            };
            var adHome = new HomeSearchResult
            {
                ComputerName = adRecord.ComputerName,
                AdComputerDistinguishedName = adRecord.AdDistinguishedName,
                InventoryMetadata = adRecord
            };
            // Simulate an older in-flight Devices search when Start opens
            // this already verified AD object. It must be invalidated
            // without a new LDAP/Graph call or an MFA prompt.
            using var staleSearch = new CancellationTokenSource();
            _unifiedCancellation = staleSearch;
            _unifiedSearch.Enabled = false;
            _unifiedCancel.Enabled = true;
            _unifiedQuery.Enabled = false;
            _unifiedProgress.Visible = true;
            var previousGeneration = _unifiedSearchGeneration;
            if (!TryReuseHomeInventoryInDevices(adHome) ||
                !staleSearch.IsCancellationRequested ||
                _unifiedCancellation is not null ||
                _unifiedSearchGeneration != previousGeneration + 1 ||
                !_unifiedSearch.Enabled || _unifiedCancel.Enabled ||
                !_unifiedQuery.Enabled || _unifiedProgress.Visible ||
                _unifiedResults.Items.Count != 1 ||
                !ReferenceEquals(_unifiedResults.Items[0].Tag, adRecord) ||
                _cloudToken is not null)
                return false;

            // Both a forged AD DN and a conflicting AD GUID must fail
            // without overwriting the already selected record.
            var wrongDn = new HomeSearchResult
            {
                ComputerName = adRecord.ComputerName,
                AdComputerDistinguishedName =
                    "CN=UI-AD-EXACT-001,OU=Foreign,DC=example,DC=com",
                InventoryMetadata = adRecord
            };
            var wrongGuid = new HomeSearchResult
            {
                ComputerName = adRecord.ComputerName,
                ComputerId = "00000000-0000-0000-0000-000000000098",
                AdComputerDistinguishedName = adRecord.AdDistinguishedName,
                InventoryMetadata = adRecord
            };
            return !TryReuseHomeInventoryInDevices(wrongDn) &&
                !TryReuseHomeInventoryInDevices(wrongGuid) &&
                _unifiedResults.Items.Count == 1 &&
                ReferenceEquals(_unifiedResults.Items[0].Tag, adRecord);
        }
        finally
        {
            _unifiedCancellation?.Cancel();
            _unifiedCancellation = null;
            _unifiedSearch.Enabled = true;
            _unifiedCancel.Enabled = false;
            _unifiedQuery.Enabled = true;
            _unifiedProgress.Visible = false;
            _unifiedResults.Items.Clear();
            _unifiedDetails.Clear();
        }
    }

    private bool TryReuseHomeInventoryInDevices(HomeSearchResult selected)
    {
        if (selected is null || selected.IdentityAmbiguous ||
            selected.InventoryMetadata is not { } metadata ||
            metadata.IdentityStatus is "Name collision" or "Ambiguous device ID")
            return false;

        var home = Device360Snapshot.FromHome(selected);
        var unified = Device360Snapshot.FromUnified(metadata);
        // AD can be DN-qualified on Start and GUID-qualified in Devices.
        // Compare proven AD object identifiers, not dissimilar key formats
        // or a matching hostname. Cloud identities still require the same
        // exact managed/Entra device key on both sides.
        var sameSourceIdentity = selected.IsCloudRecord
            ? string.Equals(home.QualifiedIdentity,
                unified.QualifiedIdentity, StringComparison.OrdinalIgnoreCase)
            : Device360AdIdentityMatcher.Matches(selected, metadata);
        if (!home.IdentityVerified || !unified.IdentityVerified ||
            string.IsNullOrWhiteSpace(home.QualifiedIdentity) ||
            !sameSourceIdentity)
            return false;

        // An earlier Devices search may still be resolving AD/Graph.
        // Cancel and invalidate that generation before changing the view.
        // If it later completes or fails, it cannot replace this object,
        // misreport an error or reset the current UI controls.
        if (_unifiedCancellation is { } priorSearch)
        {
            ++_unifiedSearchGeneration;
            _unifiedCancellation = null;
            priorSearch.Cancel();
            _unifiedSearch.Enabled = true;
            _unifiedCancel.Enabled = false;
            _unifiedQuery.Enabled = true;
            _unifiedProgress.Visible = false;
        }

        // Clear previous confidential recovery results before changing device
        // context. No previous secret is retained or displayed in Devices.
        _unifiedDiagnostics.Clear();
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
        _device360UnifiedFailureKinds = default;
        // The Start object is a new exact source context. Do not leave a
        // retry menu pointing at a previous Devices search or stale token.
        _unifiedSourceReport = null;
        RefreshUnifiedSourceRetryButton();
        SetDevicesCloudWarningAction(!HasCloudSearchSession());

        UiStyle.SetStatus(_unifiedStatus,
            "Loaded the exact source-qualified device from Start. " +
            "No new AD/Graph search, MFA prompt or secret retrieval was made. " +
            "Metadata may be stale; press Search for fresh verification.",
            UiStatusKind.Neutral);
        return true;
    }
}
