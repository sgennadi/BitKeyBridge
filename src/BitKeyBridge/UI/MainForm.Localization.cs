using System.Runtime.CompilerServices;

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

    // Strong references are kept only to controls with fixed captions.
    // Removed/disposed controls are deregistered; the observer uses weak keys.
    // Multiple controls with the same Name must all receive translations.
    private readonly Dictionary<Control, string> _localizedCaptionControls =
        new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<Control, object> _localizedCaptionObserved = new();
    private static readonly object LocalizedCaptionObserverMarker = new();
    private bool _localizedCaptionCacheBuilt;

    private void ObserveLocalizedCaptionTree(Control root)
    {
        var pending = new Stack<Control>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var control = pending.Pop();
            if (control.IsDisposed || control.Disposing ||
                _localizedCaptionObserved.TryGetValue(control, out _))
                continue;

            _localizedCaptionObserved.Add(control, LocalizedCaptionObserverMarker);
            // A late-created tab, wizard section, or diagnostics panel should
            // be translated without scanning the entire WinForms tree again.
            control.ControlAdded += OnLocalizedCaptionControlAdded;

            if (LocalizedCaptions.TryGetValue(control.Name, out var key))
            {
                _localizedCaptionControls[control] = key;
                control.Disposed += OnLocalizedCaptionControlDisposed;
                ApplyLocalizedCaption(control, key,
                    UiLanguageService.IsRightToLeft(_uiLanguageCode)
                        ? RightToLeft.Yes : RightToLeft.No);
            }

            foreach (Control child in control.Controls)
                pending.Push(child);
        }
    }

    private void OnLocalizedCaptionControlAdded(object? sender, ControlEventArgs args)
    {
        if (_localizedCaptionCacheBuilt && !IsDisposed && !Disposing)
            ObserveLocalizedCaptionTree(args.Control);
    }

    private void OnLocalizedCaptionControlDisposed(object? sender, EventArgs args)
    {
        if (sender is Control control)
            _localizedCaptionControls.Remove(control);
    }

    private void ApplyLocalizedCaption(
        Control control, string key, RightToLeft direction)
    {
        if (control.IsDisposed || control.Disposing)
            return;

        var translated = UiLanguageService.Translate(_uiLanguageCode, key);
        if (!string.IsNullOrWhiteSpace(translated) &&
            !string.Equals(control.Text, translated, StringComparison.Ordinal))
            control.Text = translated;

        // Only captions follow RTL. Device identity, recovery keys and typed
        // input remain LTR; changing Form.RightToLeft recreates HWNDs.
        if (control is Button or CheckBox or RadioButton or LinkLabel or GroupBox &&
            control.RightToLeft != direction)
            control.RightToLeft = direction;
    }

    private void ApplyLocalizedShellCaptions(bool rtl)
    {
        if (!_localizedCaptionCacheBuilt)
        {
            _localizedCaptionCacheBuilt = true;
            ObserveLocalizedCaptionTree(_mainTabs);
        }

        var direction = rtl ? RightToLeft.Yes : RightToLeft.No;
        foreach (var (control, key) in _localizedCaptionControls.ToArray())
            ApplyLocalizedCaption(control, key, direction);

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
        if ((control is Button or CheckBox) && control.RightToLeft != direction)
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
