namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Local context action; changes only the selected workspace, never
    // triggers Reveal, Copy, LAPS read or automatic Intune key rotation.
    private readonly Button _homeRecoveryStepsButton = new();

    private async void ShowRecoveryWorkflowSteps()
    {
        var selectedBeforeDialog = GetSelectedHomeSearchResult();
        RecoveryWorkflowReadinessService.Snapshot snapshot;
        try
        {
            AuthorizationDecision? authorization = _layoutSelfTest ? null :
                new AuthorizationService(_config).Check(BitKeyBridgePermission.RecoveryRead);
            snapshot = RecoveryWorkflowReadinessService.Build(
                _config, selectedBeforeDialog,
                _directoryConnected && !_directoryConnecting,
                HasCloudSearchSession(), authorization);
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Recovery preparation could not inspect the Windows token.",
                DiagnosticRedaction.Sanitize(ex.Message) +
                " No authorization has been granted.");
            return;
        }

        using var dialog = new RecoveryWorkflowDialog(snapshot, _uiLanguageCode);
        if (dialog.ShowDialog(this) != DialogResult.OK || IsDisposed)
            return;

        var route = dialog.SelectedRoute;
        if (route == RecoveryWorkflowRoute.Start)
        {
            if (_homeWorkspaceTab is not null)
            {
                _mainTabs.SelectedTab = _homeWorkspaceTab;
                _homeQuery.Focus();
            }
            return;
        }
        if (route == RecoveryWorkflowRoute.AccessHealth)
        {
            OpenHelpdeskAccessHealth();
            return;
        }

        // The modal dialog freezes ordinary UI input, but network status,
        // token revocation, search cancellation or async refresh may still
        // invalidate its snapshot. Never route by the old hostname alone.
        try
        {
            var freshAuthorization = new AuthorizationService(_config)
                .Check(BitKeyBridgePermission.RecoveryRead);
            // Strict audit and broker configuration may change while the
            // modal guide is open; re-check these settings before routing.
            var freshPreflight = RecoveryWorkflowReadinessService.Build(
                _config, GetSelectedHomeSearchResult(),
                _directoryConnected && !_directoryConnecting,
                HasCloudSearchSession(), freshAuthorization);
            if (!freshPreflight.CanNavigateToRecoveryScreen ||
                !RecoveryWorkflowNavigationPolicy.CanOpen(
                    selectedBeforeDialog, GetSelectedHomeSearchResult(), route,
                    _directoryConnected && !_directoryConnecting,
                    HasCloudSearchSession(), freshAuthorization.Allowed))
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Recovery context changed or access is denied. Select the exact device in Start and recheck Recovery Steps.",
                    UiStatusKind.Warning);
                if (_homeWorkspaceTab is not null)
                    _mainTabs.SelectedTab = _homeWorkspaceTab;
                return;
            }

            switch (route)
            {
                case RecoveryWorkflowRoute.BitLocker:
                    await OpenSelectedHomeBitLockerAsync();
                    break;
                case RecoveryWorkflowRoute.Laps:
                    await OpenSelectedHomeLapsAsync();
                    break;
                case RecoveryWorkflowRoute.Devices:
                    // This performs metadata search only. The operator must
                    // verify the exact managedDeviceId before any cloud
                    // recovery or explicit Intune rotation.
                    OpenHomeDeviceDetails();
                    UiStyle.SetStatus(_unifiedStatus,
                        "Select the exact Entra/Intune device ID. The guided recovery step has not read a secret.",
                        UiStatusKind.Warning);
                    await SearchUnifiedDevicesAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Recovery navigation failed; no secret was requested by this guide.",
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }
}
