namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly Button _homeDevice360Button = new();
    private bool _device360LastUnifiedPartial;

    private Device360OperationalStatus CaptureDevice360OperationalStatus(
        Device360Snapshot snapshot, bool searchPartial)
    {
        // The current connection and config flags are NOT access grants.
        // Never retain or log the token itself.
        var validCloudSession = _cloudToken is not null &&
            _cloudToken.ExpiresAt > DateTime.Now.AddMinutes(1) &&
            string.Equals(_cloudTokenContext, BuildCloudTokenContext(),
                StringComparison.Ordinal);
        return Device360OperationalStatus.Capture(snapshot,
            adConnected: _directoryConnected && !_directoryConnecting,
            cloudSessionPresent: validCloudSession,
            rbacEnabled: _config.RbacEnabled,
            ticketRequired: _config.RequireRecoveryAccessReference,
            protectedAuditRequired: _config.RequireProtectedAuditForRecovery,
            auditBrokerConfigured: _config.AuditBrokerEnabled,
            searchPartial: searchPartial);
    }

    // A single read-only card for the EXACT selected Start object. It does
    // not execute an AD/Graph search or trigger a secret-bearing handler.
    private async void ShowHomeDevice360Card()
    {
        var selected = GetSelectedHomeSearchResult();
        if (selected is null)
        {
            UiStyle.SetStatus(_homeSearchStatus,
                "Select an exact computer record before opening Device 360.",
                UiStatusKind.Warning);
            return;
        }

        var snapshot = Device360Snapshot.FromHome(selected);
        using var dialog = new Device360Dialog(snapshot, _uiLanguageCode,
            operational: CaptureDevice360OperationalStatus(snapshot,
                selected.BitLockerLookupFailed || selected.LapsLookupFailed));
        if (dialog.ShowDialog(this) != DialogResult.OK ||
            dialog.SelectedRoute == Device360Route.None)
            return;
        if (dialog.SelectedRoute == Device360Route.AccessHealth)
        {
            OpenHelpdeskAccessHealth();
            return;
        }

        // A dialog may outlive a query, disconnect, cancellation or results
        // refresh. Object-reference AND source identity must still match.
        // Even then, navigation alone never grants RecoveryRead.
        var current = GetSelectedHomeSearchResult();
        if (!ReferenceEquals(current, selected) ||
            !string.Equals(Device360Snapshot.FromHome(current!).QualifiedIdentity,
                snapshot.QualifiedIdentity, StringComparison.OrdinalIgnoreCase) ||
            !snapshot.IdentityVerified || current!.IdentityAmbiguous)
        {
            UiStyle.SetStatus(_homeSearchStatus,
                "Selected device changed. Re-select the exact record.",
                UiStatusKind.Warning);
            return;
        }

        switch (dialog.SelectedRoute)
        {
            case Device360Route.BitLocker when !current.IsCloudRecord:
                await OpenSelectedHomeBitLockerAsync();
                break;
            case Device360Route.Laps when !current.IsCloudRecord:
                await OpenSelectedHomeLapsAsync();
                break;
            case Device360Route.Devices when current.IsCloudRecord:
                // Switch workspaces only. Search, reveal and rotation remain
                // separate operator actions on source-qualified objects.
                var devicesTab = _mainTabs.TabPages.Cast<TabPage>()
                    .FirstOrDefault(tab => tab.Name == "DevicesWorkspace");
                if (devicesTab is not null)
                {
                    _mainTabs.SelectedTab = devicesTab;
                    QueueNavigationViewportReflow();
                }
                break;
        }
    }

    // Reuse the very same card for Devices' exact UnifiedDeviceInfo.
    // It is not another search or a new recovery authorization path.
    private void ShowUnifiedDevice360Card()
    {
        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not UnifiedDeviceInfo row)
        {
            UiStyle.SetStatus(_unifiedStatus,
                "Select an exact device record first.",
                UiStatusKind.Warning);
            return;
        }

        var snapshot = Device360Snapshot.FromUnified(row);
        using var dialog = new Device360Dialog(snapshot, _uiLanguageCode,
            allowNavigation: false,
            operational: CaptureDevice360OperationalStatus(snapshot,
                _device360LastUnifiedPartial));
        if (dialog.ShowDialog(this) == DialogResult.OK &&
            dialog.SelectedRoute == Device360Route.AccessHealth)
            OpenHelpdeskAccessHealth();
    }

    internal Device360Dialog? PreviewDevice360CardForUiSelfTest(bool cloud)
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException(
                "Device 360 preview is available only in UI tests.");
        var row = cloud
            ? new HomeSearchResult
            {
                ComputerName = "UI-TEST-CLOUD",
                ComputerId = "d0c832d9-982c-4aa2-907e-1d9a3130e8c1",
                SourceIdentity = "INTUNE:MANAGED:ui-test-id",
                IsCloudRecord = true,
                BitLockerKeyCount = 1,
                BitLockerLookupFailed = false,
                LapsStatus = "Not verified"
            }
            : new HomeSearchResult
            {
                ComputerName = "UI-TEST-AD",
                AdComputerDistinguishedName =
                    "CN=UI-TEST-AD,OU=Test,DC=example,DC=com",
                BitLockerKeyCount = 1,
                LapsCandidateFound = true,
                LapsStatus = "Detected"
            };
        return new Device360Dialog(Device360Snapshot.FromHome(row),
            _uiLanguageCode);
    }
}
