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
            _unifiedResults.Enabled = false;
            _deviceShowKey.Enabled = false;
            _deviceCopyKey.Enabled = false;
            _unifiedProgress.Visible = true;
            var previousGeneration = _unifiedSearchGeneration;
            var previousRecoveryGeneration = _deviceRecoverySelectionGeneration;
            // A prior view could have displayed a sensitive value. A new
            // source-qualified Start selection must re-mask and clear it.
            _deviceRecoveryKey.Text = "SYNTHETIC-NONSECRET-PLACEHOLDER";
            _deviceCurrentKey = "SYNTHETIC-NONSECRET-PLACEHOLDER";
            _deviceRecoveryKey.UseSystemPasswordChar = false;
            _deviceShowKey.Text = "Hide Key";
            if (!TryReuseHomeInventoryInDevices(adHome) ||
                _deviceRecoverySelectionGeneration <= previousRecoveryGeneration ||
                _deviceCurrentKey is not null ||
                _deviceRecoveryKey.Text.Length != 0 ||
                !_deviceRecoveryKey.UseSystemPasswordChar ||
                _deviceShowKey.Text != "Reveal Key" ||
                !staleSearch.IsCancellationRequested ||
                _unifiedCancellation is not null ||
                _unifiedSearchGeneration != previousGeneration + 1 ||
                !_unifiedSearch.Enabled || _unifiedCancel.Enabled ||
                !_unifiedQuery.Enabled || !_unifiedResults.Enabled ||
                !_deviceShowKey.Enabled || !_deviceCopyKey.Enabled ||
                _unifiedProgress.Visible ||
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
            _unifiedResults.Enabled = true;
            _deviceShowKey.Enabled = true;
            _deviceCopyKey.Enabled = true;
            _unifiedProgress.Visible = false;
            _unifiedResults.Items.Clear();
            _unifiedDetails.Clear();
        }
    }

    /// <summary>
    /// Native WinForms fixture for the exact Start -> Devices transfer while
    /// a memory-only Graph session is valid, then after its identity context
    /// is invalidated. No token request, Graph call, secret read or clipboard.
    /// </summary>
    internal bool PreviewStartToDevicesCloudSessionForUiSelfTest()
    {
        if (!_layoutSelfTest || _cloudConnecting)
            return false;

        var previousToken = _cloudToken;
        var previousContext = _cloudTokenContext;
        var previousRecovery = _cloudTokenHasRecovery;
        var previousLaps = _cloudTokenHasLaps;
        var previousQuery = _unifiedQuery.Text;
        try
        {
            var token = new GraphToken
            {
                AccessToken = "SYNTHETIC-NOT-A-CREDENTIAL",
                AuthMode = "DeviceCode",
                ExpiresAt = DateTime.Now.AddMinutes(30)
            };
            _cloudToken = token;
            _cloudTokenHasRecovery = true;
            _cloudTokenHasLaps = false;
            _cloudTokenContext = BuildCloudTokenContext();
            if (!HasCloudSearchSession())
                return false;

            var device = new UnifiedDeviceInfo
            {
                ComputerName = "UI-TEST-SESSION-TRANSFER",
                IdentityKey = "INTUNE:MANAGED:ui-session-device-001",
                IdentityStatus = "Intune object",
                FoundInIntune = true,
                FoundInEntra = true,
                ManagedDeviceId = "ui-session-device-001",
                EntraDeviceId = "e4963b97-64db-4119-a55c-1f90da27f250",
                Manufacturer = "Synthetic Vendor",
                Model = "Synthetic Laptop",
                OperatingSystem = "Windows",
                OsVersion = "11",
                RecoveryMetadataIncomplete = true
            };
            var fromStart = new HomeSearchResult
            {
                ComputerName = device.ComputerName,
                SourceIdentity = device.IdentityKey,
                ComputerId = device.EntraDeviceId,
                IsCloudRecord = true,
                InventoryMetadata = device
            };
            var searchGeneration = _unifiedSearchGeneration;
            var selectionGeneration = _deviceRecoverySelectionGeneration;
            if (!TryReuseHomeInventoryInDevices(fromStart) ||
                !ReferenceEquals(_cloudToken, token) ||
                !HasCloudSearchSession() ||
                _unifiedSearchGeneration != searchGeneration ||
                _deviceRecoverySelectionGeneration <= selectionGeneration ||
                _unifiedResults.Items.Count != 1 ||
                !ReferenceEquals(_unifiedResults.Items[0].Tag, device) ||
                !_unifiedDetails.Text.Contains("Synthetic Laptop",
                    StringComparison.Ordinal) ||
                _deviceCurrentKey is not null ||
                _deviceRecoveryKey.Text.Length != 0 ||
                !_deviceRecoveryKey.UseSystemPasswordChar)
                return false;

            // A changed tenant/client/scope context disables recovery reuse.
            // Navigation may still display already collected non-secret
            // metadata, but must never create a new MFA prompt by itself.
            _cloudTokenContext = "UI-TEST-INVALIDATED-CONTEXT";
            if (HasCloudSearchSession() ||
                !TryReuseHomeInventoryInDevices(fromStart) ||
                !ReferenceEquals(_cloudToken, token) ||
                HasCloudSearchSession() ||
                _unifiedResults.Items.Count != 1 ||
                !ReferenceEquals(_unifiedResults.Items[0].Tag, device) ||
                _deviceCurrentKey is not null ||
                _deviceRecoveryKey.Text.Length != 0 ||
                !_deviceRecoveryKey.UseSystemPasswordChar ||
                _deviceRecoveryIds.Items.Count != 0)
                return false;

            return true;
        }
        finally
        {
            _unifiedResults.Items.Clear();
            _unifiedDetails.Clear();
            _deviceRecoveryIds.Items.Clear();
            _unifiedQuery.Text = previousQuery;
            _cloudToken = previousToken;
            _cloudTokenContext = previousContext;
            _cloudTokenHasRecovery = previousRecovery;
            _cloudTokenHasLaps = previousLaps;
            SetDevicesCloudWarningAction(!HasCloudSearchSession());
        }
    }

    /// <summary>
    /// Offline native WinForms regression for explicit Entra Recovery ID
    /// selection after Start -> Devices navigation. Two IDs must NOT
    /// silently choose the first; one exact ID remains actionable.
    /// The test uses synthetic metadata only and never invokes Reveal.
    /// </summary>
    internal bool PreviewDeviceRecoveryIdSelectionForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI self-test only.");

        var previousQuery = _unifiedQuery.Text;
        try
        {
            var device = new UnifiedDeviceInfo
            {
                ComputerName = "UI-TEST-RECOVERY-CHOICE",
                IdentityKey = "INTUNE:MANAGED:ui-recovery-choice-001",
                IdentityStatus = "Intune object",
                FoundInIntune = true,
                FoundInEntra = true,
                ManagedDeviceId = "ui-recovery-choice-001",
                EntraDeviceId = "a4caf67e-538b-4e36-9763-4b7e22e8b00e",
                RecoveryIds = ["UI-TEST-RECOVERY-ONE", "UI-TEST-RECOVERY-TWO"]
            };
            var selected = new HomeSearchResult
            {
                ComputerName = device.ComputerName,
                SourceIdentity = device.IdentityKey,
                ComputerId = device.EntraDeviceId,
                IsCloudRecord = true,
                InventoryMetadata = device
            };
            if (!TryReuseHomeInventoryInDevices(selected) ||
                _deviceRecoveryIds.Items.Count != 2 ||
                _deviceRecoveryIds.SelectedIndex != -1 ||
                TryGetSelectedDeviceRecovery(out _, out _))
                return false;

            // Deliberate dropdown selection must resolve only that ID.
            _deviceRecoveryIds.SelectedIndex = 1;
            if (!TryGetSelectedDeviceRecovery(out var chosen, out var id) ||
                !ReferenceEquals(chosen, device) ||
                id != "UI-TEST-RECOVERY-TWO")
                return false;

            // Simulate an old unmasked field before the next exact
            // navigation; the same presenter must clear it immediately.
            _deviceCurrentKey = "SYNTHETIC-NONSECRET-PLACEHOLDER";
            _deviceRecoveryKey.Text = _deviceCurrentKey;
            _deviceRecoveryKey.UseSystemPasswordChar = false;
            _deviceShowKey.Text = "Hide Key";

            device.RecoveryIds = ["UI-TEST-RECOVERY-ONE"];
            if (!TryReuseHomeInventoryInDevices(selected) ||
                _deviceRecoveryIds.Items.Count != 1 ||
                _deviceRecoveryIds.SelectedIndex != 0 ||
                !TryGetSelectedDeviceRecovery(out chosen, out id) ||
                !ReferenceEquals(chosen, device) ||
                id != "UI-TEST-RECOVERY-ONE" ||
                _deviceCurrentKey is not null ||
                _deviceRecoveryKey.Text.Length != 0 ||
                !_deviceRecoveryKey.UseSystemPasswordChar ||
                _deviceShowKey.Text != "Reveal Key")
                return false;

            return true;
        }
        finally
        {
            _unifiedResults.Items.Clear();
            _unifiedDetails.Clear();
            _deviceRecoveryIds.Items.Clear();
            _unifiedQuery.Text = previousQuery;
            InvalidateDeviceCloudSecretOnSessionChange();
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
            // A targeted source retry may have disabled metadata/secret
            // controls. This new exact Start selection now owns the view;
            // restore only these controls, while authorization still gates
            // every Reveal/Copy action separately.
            _unifiedResults.Enabled = true;
            _deviceShowKey.Enabled = true;
            _deviceCopyKey.Enabled = true;
            _unifiedProgress.Visible = false;
        }

        // Clear previous confidential recovery results before changing device
        // context. No previous secret is retained or displayed in Devices.
        _unifiedDiagnostics.Clear();
        ++_deviceRecoverySelectionGeneration;
        _deviceCurrentKey = null;
        _deviceCurrentKeySession = null;
        _deviceRecoveryKey.Clear();
        _deviceRecoveryKey.UseSystemPasswordChar = true;
        _deviceShowKey.Text = "Reveal Key";
        _deviceRecoveryIds.Items.Clear();

        _unifiedQuery.Text = selected.ComputerName;
        _unifiedResults.Items.Clear();
        _unifiedDetails.Clear();
        var item = AddUnifiedDeviceSearchResultRow(metadata);
        item.Selected = true;
        item.Focused = true;
        // Inactive WinForms pages may not fire SelectedIndexChanged until
        // shown. Populate metadata deterministically after Start navigation.
        // This does not query Graph or retrieve a recovery password.
        PopulateDeviceRecoveryIds();
        _unifiedDetails.Text = Device360Formatter.Format(metadata);
        _device360LastUnifiedPartial = metadata.RecoveryMetadataIncomplete;
        _device360UnifiedLookupFailed = default;
        _device360UnifiedFailureKinds = default;
        // The Start object is a new exact source context. Do not leave a
        // retry menu pointing at a previous Devices search or stale token.
        _unifiedSourceReport = null;
        _unifiedSourceCloudSession = null;
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
