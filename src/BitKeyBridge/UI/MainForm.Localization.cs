namespace BitKeyBridge;

/// <summary>
/// Localize fixed, non-secret captions without recreating the MainForm tree.
/// Runtime status messages and secret/identity fields are deliberately excluded.
/// </summary>
public sealed partial class MainForm
{
    private static readonly IReadOnlyDictionary<string, string> LocalizedCaptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HomeUseWindowsIdentity"] = "MainWindowsAccount",
            ["HomeUseOtherAccount"] = "MainOtherAdAccount",
            ["HomeConnectAdButton"] = "MainConnectAd",
            ["HomeDisconnectAdButton"] = "MainDisconnectAd",
            ["HomeConnectionToolsButton"] = "MainConnectionTools",
            ["HomeRecoverySteps"] = "MainRecoverySteps",
            ["HomeBitLockerDiagnosticLink"] = "MainViewCommands",
            ["HomeLoadLapsHistoryButton"] = "MainLoadHistory",
            ["RecoveryBackToStart"] = "MainBackToStart",
            ["LapsBackToStart"] = "MainBackToStart",
            ["BitLockerDiagnosticCommandsButton"] = "MainBitLockerCommands",
            ["LapsDiagnosticCommandsButton"] = "MainLapsCommands",
            ["HelpdeskOpenDeviceWorkspaceButton"] = "MainOpenDeviceSearch",
            ["HelpdeskRecoveryStepsButton"] = "MainRecoverySteps",
            ["DiagnosticToolsTab"] = "Tools",
            ["EnterpriseSettingsTab"] = "MainEnterprise",
            ["RunEnvironmentCheckButton"] = "MainEnvironmentCheck",
            ["RunAdvancedDeviceDiagnosticsButton"] = "MainDeviceDiagnostics",
            ["RunBitLockerCommandsButton"] = "MainBitLockerCommands",
            ["RunLapsCommandsButton"] = "MainLapsCommands",
            ["RunProtectedAuditCheckButton"] = "MainCheckProtectedAudit",
            ["RunUiPerformanceDiagnosticsButton"] = "MainUiPerformance",
            ["ResetVisualLayoutButton"] = "MainResetLayout",
            ["CloudEnableRotationPermission"] = "MainCloudRotation"
        };

    private readonly Dictionary<string, Control> _localizedCaptionControls =
        new(StringComparer.Ordinal);
    private bool _localizedCaptionCacheBuilt;

    private void ApplyLocalizedShellCaptions(bool rtl)
    {
        if (!_localizedCaptionCacheBuilt)
        {
            _localizedCaptionCacheBuilt = true;
            var pending = new Stack<Control>();
            pending.Push(_mainTabs);
            while (pending.Count > 0)
            {
                var control = pending.Pop();
                if (!string.IsNullOrEmpty(control.Name) &&
                    LocalizedCaptions.ContainsKey(control.Name))
                    _localizedCaptionControls.TryAdd(control.Name, control);
                foreach (Control child in control.Controls)
                    pending.Push(child);
            }
        }

        var direction = rtl ? RightToLeft.Yes : RightToLeft.No;
        foreach (var (name, control) in _localizedCaptionControls)
        {
            if (control.IsDisposed || control.Disposing)
                continue;
            var translated = UiLanguageService.Translate(
                _uiLanguageCode, LocalizedCaptions[name]);
            if (!string.IsNullOrWhiteSpace(translated) &&
                !string.Equals(control.Text, translated, StringComparison.Ordinal))
                control.Text = translated;
            // Keep text entry and recovery values LTR even for Hebrew.
            if (control is Button or CheckBox or RadioButton or LinkLabel or GroupBox &&
                control.RightToLeft != direction)
                control.RightToLeft = direction;
        }

        SetCloudCaption(_cloudSaveButton, "MainSaveConfig", direction);
        SetCloudCaption(_cloudWizardButton, "MainEntraWizard", direction);
        SetCloudCaption(_cloudConnectButton, "MainConnectTest", direction);
        SetCloudCaption(_cloudConnectCancelButton, "Cancel", direction);
        SetCloudCaption(_cloudSetupButton, "MainFirstRunRepair", direction);
        SetCloudCaption(_cloudBootstrapButton, "MainBootstrap", direction);
        SetCloudCaption(_cloudRolloverButton, "MainRolloverCertificate", direction);
        SetCloudCaption(_cloudLapsPermissions, "MainCloudLapsRead", direction);

        foreach (var (name, key) in new[]
        {
            ("HomeEnvironmentCheckMenuItem", "MainEnvironmentCheck"),
            ("HomeDeviceDiagnosticsMenuItem", "MainDeviceDiagnostics")
        })
        {
            foreach (var item in _homeConnectionToolsMenu.Items.Find(name, false))
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, key);
                if (!string.IsNullOrWhiteSpace(caption) &&
                    !string.Equals(item.Text, caption, StringComparison.Ordinal))
                    item.Text = caption;
            }
        }
        RefreshAdvancedConnectionMenuCaption();
    }

    private void SetCloudCaption(Control control, string key, RightToLeft direction)
    {
        if (control.Parent is null || control.IsDisposed)
            return;
        var caption = UiLanguageService.Translate(_uiLanguageCode, key);
        if (!string.IsNullOrWhiteSpace(caption) &&
            !string.Equals(control.Text, caption, StringComparison.Ordinal))
            control.Text = caption;
        if (control is Button or CheckBox && control.RightToLeft != direction)
            control.RightToLeft = direction;
    }

    private void RefreshAdvancedConnectionMenuCaption()
    {
        var key = _advancedConnectionGroup.Visible
            ? "MainHideAdvancedAd"
            : "MainAdvancedAd";
        var caption = UiLanguageService.Translate(_uiLanguageCode, key);
        if (!string.IsNullOrWhiteSpace(caption) &&
            !string.Equals(_homeAdvancedConnectionMenuItem.Text, caption,
                StringComparison.Ordinal))
            _homeAdvancedConnectionMenuItem.Text = caption;
    }
}
