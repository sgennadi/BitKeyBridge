using System.Diagnostics;

namespace BitKeyBridge;

/// <summary>
/// Helpdesk updater commands are kept separate from device recovery,
/// connection and administrative settings in the main window.
/// </summary>
public sealed partial class MainForm
{
    private void LoadUpdateSettings()
    {
        _updateRepository.Text =
            _config.UpdateRepository;
        _checkUpdatesOnStart.Checked =
            _config.CheckForUpdatesOnStart;
        _autoInstallUpdatesOnStart.Checked =
            _config.AutoInstallUpdatesOnStart;
        _allowUnsignedAutoUpdates.Checked =
            _config.AllowUnsignedUnattendedUpdates;
        _allowPrereleaseUpdates.Checked =
            _config.AllowPrereleaseUpdates;
        _requireTrustedUpdateSignature.Checked =
            _config.RequireTrustedUpdateSignature;
        _trustedUpdatePublisher.Text =
            _config.TrustedUpdatePublisher;

        try
        {
            _lastUpdateInfo =
                JsonStore.Read<UpdateInfo>(
                    AppPaths.UpdateStatusFile);

            if (_lastUpdateInfo is not null)
            {
                DisplayUpdateInfo(
                    _lastUpdateInfo, cached: true);
            }
            else
            {
                UiStyle.SetStatus(
                    _updateStatus,
                    _config.CheckForUpdatesOnStart
                        ? _config.AutoInstallUpdatesOnStart
                            ? UpdateTrustPolicy.CanInstallUnattended(_config)
                                ? "Auto-updates enabled: " + UpdateTrustPolicy.DescribeMode(_config) + "."
                                : "Saved auto-install setting blocked: configure trusted signing or explicit unsigned consent."
                            : "Automatic startup update checks are enabled. Installation remains manual."
                        : "Automatic startup update checks are disabled. Manual Check for Updates and Update Now remain available.",
                    UiStatusKind.Neutral);
            }
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Update status read failed: " +
                ex.Message,
                EventLogSeverity.Warning,
                4545,
                "Update");

            UiStyle.SetStatus(
                _updateStatus,
                "Previous update status could not be loaded. Manual update actions are still available.",
                UiStatusKind.Warning);
        }

        if (!_layoutSelfTest)
            RefreshUpdateHistory();
    }

    private bool SaveUpdateSettings(
        bool showConfirmation)
    {
        string repository;
        try
        {
            repository =
                UpdateService.NormalizeRepository(
                    _updateRepository.Text);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _updateStatus,
                "Update repository is invalid: " +
                ex.Message,
                UiStatusKind.Error);
            return false;
        }

        var untrustedLegacyAutoinstall =
            _config.AutoInstallUpdatesOnStart &&
            !UpdateTrustPolicy.CanInstallUnattended(_config);
        var downgradedLegacyAutoinstall = false;

        // Never migrate AutoInstall=true from an old install into unsigned
        // consent. A user must acknowledge the risk in this UI explicitly.
        if (_allowUnsignedAutoUpdates.Checked &&
            !_config.AllowUnsignedUnattendedUpdates)
        {
            var approval = MessageBox.Show(
                this,
                "Unsigned update packages are verified against GitHub release metadata and SHA-256, " +
                "but do NOT authenticate a publisher. If the GitHub publishing account is compromised, " +
                "a malicious update could still pass these checks.\n\n" +
                "Allow unsigned automatic updates ONLY from sgennadi/BitKeyBridge on this computer?",
                "Unsigned Automatic Update Risk",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (approval != DialogResult.Yes)
                return false;
        }

        if (_autoInstallUpdatesOnStart.Checked &&
            !UpdateTrustPolicy.CanEnableUnattended(
                _requireTrustedUpdateSignature.Checked,
                _trustedUpdatePublisher.Text,
                _allowUnsignedAutoUpdates.Checked,
                repository))
        {
            if (untrustedLegacyAutoinstall)
            {
                // Existing 0.29.x installations may remember an unsafe
                // auto-install flag. Safely migrate it to checks-only so that
                // Check for Updates and manual installs remain usable.
                _autoInstallUpdatesOnStart.Checked = false;
                downgradedLegacyAutoinstall = true;
            }
            else
            {
                UiStyle.SetStatus(_updateStatus,
                    "Auto-install requires a trusted Authenticode publisher OR explicit " +
                    "unsigned-update consent with the official BitKeyBridge GitHub repository.",
                    UiStatusKind.Warning);
                return false;
            }
        }

        try
        {
            _config.UpdateRepository =
                repository;
            _config.CheckForUpdatesOnStart =
                _checkUpdatesOnStart.Checked;
            _config.AutoInstallUpdatesOnStart =
                _autoInstallUpdatesOnStart.Checked;
            _config.AllowUnsignedUnattendedUpdates =
                _allowUnsignedAutoUpdates.Checked;
            _config.AllowPrereleaseUpdates =
                _allowPrereleaseUpdates.Checked;
            _config.RequireTrustedUpdateSignature =
                _requireTrustedUpdateSignature.Checked;
            _config.TrustedUpdatePublisher =
                _trustedUpdatePublisher.Text.Trim();

            ConfigService.SaveAppConfig(
                _config);

            if (_config.AutoInstallUpdatesOnStart &&
                UpdateRollbackHoldService.IsActiveForCurrentBuild())
            {
                UpdateRollbackHoldService.Clear();
            }

            _audit.Write(
                "SaveUpdateSettings",
                source: "Local",
                details:
                    $"UpdateRepository={_config.UpdateRepository}; CheckOnStart={_config.CheckForUpdatesOnStart}; AutoInstallOnStart={_config.AutoInstallUpdatesOnStart}; AllowUnsignedConsent={_config.AllowUnsignedUnattendedUpdates}; AllowPrerelease={_config.AllowPrereleaseUpdates}; RequireTrustedSignature={_config.RequireTrustedUpdateSignature}; TrustedPublisher={_config.TrustedUpdatePublisher}");

            if (showConfirmation)
            {
                UiStyle.SetStatus(
                    _updateStatus,
                    downgradedLegacyAutoinstall
                        ? "Previous unsigned/unpinned auto-install setting was safely disabled. " +
                          "Startup checks and manual updates remain available."
                        : _config.CheckForUpdatesOnStart
                        ? _config.AutoInstallUpdatesOnStart
                            ? "Auto-install enabled: " + UpdateTrustPolicy.DescribeMode(_config) + "."
                            : "Update settings saved. Startup checks are enabled; installation remains manual."
                        : "Update settings saved. Automatic startup checks are disabled; manual checks remain available.",
                    UiStatusKind.Success);
            }

            return true;
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _updateStatus,
                "Could not save update settings: " +
                ex.Message,
                UiStatusKind.Error);

            ShowAppError(
                "Saving update settings failed.",
                "SaveUpdateSettings",
                ex,
                ("Repository", repository));
            return false;
        }
    }

    private async Task<bool> TryAutomaticUpdateOnStartAsync()
    {
        if (!_config.CheckForUpdatesOnStart)
            return false;

        try
        {
            UiStyle.SetStatus(
                _updateStatus,
                _config.AutoInstallUpdatesOnStart
                    ? "Checking for automatic BitKeyBridge updates..."
                    : "Checking GitHub Releases...",
                UiStatusKind.Busy);

            using var updater =
                new UpdateService(
                    _config);
            using var startupCheckTimeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(
                        20));

            _lastUpdateInfo =
                await updater.CheckAsync(
                    startupCheckTimeout.Token);

            DisplayUpdateInfo(
                _lastUpdateInfo);

            if (!string.IsNullOrWhiteSpace(
                    _lastUpdateInfo.Error))
            {
                WindowsEventLogService.TryWrite(
                    "Automatic update check failed: " +
                    _lastUpdateInfo.Error,
                    EventLogSeverity.Warning,
                    4102,
                    "Update");
                return false;
            }

            if (!_lastUpdateInfo.UpdateAvailable)
                return false;

            WindowsEventLogService.TryWrite(
                $"BitKeyBridge update {_lastUpdateInfo.LatestVersion} is available.",
                EventLogSeverity.Information,
                4101,
                "Update");

            // Unattended replacement must be opt-in AND restricted to a
            // trusted, explicitly pinned publisher. SHA-256 alone is not
            // a publisher identity check for an elevated update helper.
            if (_config.AutoInstallUpdatesOnStart &&
                !UpdateTrustPolicy.CanInstallUnattended(_config))
            {
                UiStyle.SetStatus(_updateStatus,
                    "Auto-install requires trusted publisher signing OR your explicit " +
                    "unsigned-update consent restricted to the official GitHub repository.",
                    UiStatusKind.Warning);
                return false;
            }

            if (!_config.AutoInstallUpdatesOnStart)
            {
                if (UpdateRollbackHoldService.IsActiveForCurrentBuild())
                {
                    UiStyle.SetStatus(
                        _updateStatus,
                        "Automatic installation was paused after a deliberate rollback. " +
                        "Use Update Now or Resume Auto-Install to return to newer releases.",
                        UiStatusKind.Warning);
                }
                return false;
            }

            if (UpdateRollbackHoldService.IsActiveForCurrentBuild())
            {
                UiStyle.SetStatus(
                    _updateStatus,
                    $"Update { _lastUpdateInfo.LatestVersion } is available, but automatic installation is paused after a manual rollback. " +
                    "Use Update Now or Resume Auto-Install to return to newer releases.",
                    UiStatusKind.Warning);
                return false;
            }

            return await InstallVerifiedUpdateGuiAsync(
                _lastUpdateInfo,
                automatic:
                    true);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _updateStatus,
                "Automatic update failed: " +
                ex.Message,
                UiStatusKind.Error);

            WindowsEventLogService.TryWrite(
                "Automatic updater failed: " +
                ex.Message,
                EventLogSeverity.Warning,
                4103,
                "Update");

            return false;
        }
    }

    private async Task CheckForUpdatesGuiAsync(bool silentWhenCurrent)
    {
        if (!SaveUpdateSettings(
                showConfirmation: false))
        {
            return;
        }

        try
        {
            UiStyle.SetStatus(
                _updateStatus,
                "Checking GitHub Releases...",
                UiStatusKind.Busy);
            using var updater = new UpdateService(_config);
            _lastUpdateInfo = await updater.CheckAsync();
            DisplayUpdateInfo(_lastUpdateInfo);

            if (!string.IsNullOrWhiteSpace(_lastUpdateInfo.Error))
            {
                if (!silentWhenCurrent)
                    ShowAppMessage(
                "BitKeyBridge Update",
                _lastUpdateInfo.Error);
                return;
            }

            if (!_lastUpdateInfo.UpdateAvailable && !silentWhenCurrent)
            {
                ShowAppMessage(
                "BitKeyBridge Update",
                $"BitKeyBridge {_lastUpdateInfo.CurrentVersion} is current.");
            }

            if (_lastUpdateInfo.UpdateAvailable)
            {
                WindowsEventLogService.TryWrite(
                    $"BitKeyBridge update {_lastUpdateInfo.LatestVersion} is available.",
                    EventLogSeverity.Information,
                    4101,
                    "Update");
            }
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _updateStatus,
                "Update check failed: " + ex.Message,
                UiStatusKind.Error);
        }
    }

    private void DisplayUpdateInfo(UpdateInfo info, bool cached = false)
    {
        if (!string.IsNullOrWhiteSpace(info.Error))
        {
            UiStyle.SetStatus(
                _updateStatus,
                $"Current: {info.CurrentVersion}{Environment.NewLine}" +
                $"Update check failed: {info.Error}",
                UiStatusKind.Error);
            return;
        }

        UiStyle.SetStatus(
            _updateStatus,
            (cached
                ? "Previously saved update result (not verified today). Use Check for Updates or Update Now to refresh." + Environment.NewLine
                : string.Empty) +
            $"Repository: {_config.UpdateRepository}    Release: {info.ReleaseTag}{Environment.NewLine}" +
            $"Current: {info.CurrentVersion}    Latest: {info.LatestVersion}    Architecture: {info.Architecture}{Environment.NewLine}" +
            (info.UpdateAvailable
                ? $"UPDATE AVAILABLE: {info.LatestVersion}    Asset: {info.AssetName}"
                : "No newer release is available.") +
            Environment.NewLine +
            $"Checked: {info.CheckedAtUtc:u}" +
            (string.IsNullOrWhiteSpace(info.ExpectedSha256)
                ? string.Empty
                : Environment.NewLine + $"SHA-256: {info.ExpectedSha256}") +
            Environment.NewLine +
            $"Automatic startup checks: {(_config.CheckForUpdatesOnStart ? "Enabled" : "Disabled")}" +
            Environment.NewLine +
            $"Automatic install: {(UpdateRollbackHoldService.IsActiveForCurrentBuild() ? "Paused after rollback" : _config.CheckForUpdatesOnStart && _config.AutoInstallUpdatesOnStart ? UpdateTrustPolicy.CanInstallUnattended(_config) ? "Enabled" : "Blocked: trusted publisher required" : "Disabled")}" +
            Environment.NewLine +
            $"Update trust mode: {UpdateTrustPolicy.DescribeMode(_config)}" + Environment.NewLine +
            $"Authenticode trust required: {(_config.RequireTrustedUpdateSignature ? "Yes" : "No")}" +
            (string.IsNullOrWhiteSpace(_config.TrustedUpdatePublisher)
                ? string.Empty
                : $" • Publisher: {_config.TrustedUpdatePublisher}"),
            cached || info.UpdateAvailable
                ? UiStatusKind.Warning
                : UiStatusKind.Success);
    }

    private async Task InstallLatestUpdateGuiAsync()
    {
        // Never install the release recorded by an earlier run. It might
        // point to v0.26.0 even after a newer release was published.
        _lastUpdateInfo = null;
        await CheckForUpdatesGuiAsync(silentWhenCurrent: false);

        var info = _lastUpdateInfo;
        if (info is null ||
            !string.IsNullOrWhiteSpace(info.Error) ||
            !info.UpdateAvailable)
            return;

        var answer = MessageBox.Show(
            this,
            $"Install BitKeyBridge {info.LatestVersion} for {info.Architecture}?{Environment.NewLine}{Environment.NewLine}" +
            "The ZIP SHA-256 will be verified against SHA256SUMS.txt and GitHub's asset digest when available. " +
            "The downloaded EXE must also pass --self-test before installation. " +
            (_config.RequireTrustedUpdateSignature
                ? "A trusted Authenticode signature is required by policy. "
                : string.Empty) +
            "The GUI will close during replacement and reopen automatically.",
            "Install Verified Update",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        await InstallVerifiedUpdateGuiAsync(
            info,
            automatic:
                false);
    }

    private async Task<bool> InstallVerifiedUpdateGuiAsync(
        UpdateInfo info,
        bool automatic)
    {
        try
        {
            Enabled =
                false;

            UiStyle.SetStatus(
                _updateStatus,
                automatic
                    ? $"Automatic update: downloading and verifying BitKeyBridge {info.LatestVersion}..."
                    : $"Downloading and verifying BitKeyBridge {info.LatestVersion}...",
                UiStatusKind.Busy);

            using var updater =
                new UpdateService(
                    _config);

            var prepared =
                await updater.PrepareAsync(
                    info);

            _audit.Write(
                "PrepareVerifiedUpdate",
                source: "GitHub",
                details:
                    $"Version={info.LatestVersion}; Asset={info.AssetName}; SHA256={info.ExpectedSha256}; Automatic={automatic}");

            updater.LaunchApplyHelper(
                prepared,
                restartGui:
                    true,
                automatic:
                    automatic);

            _audit.Write(
                "LaunchUpdateHelper",
                source: "Local",
                details:
                    $"Version={info.LatestVersion}; Automatic={automatic}");

            Close();
            return true;
        }
        catch (Exception ex)
        {
            Enabled =
                true;

            UiStyle.SetStatus(
                _updateStatus,
                (automatic
                    ? "Automatic update failed: "
                    : "Update preparation failed: ") +
                ex.Message,
                UiStatusKind.Error);

            _audit.Write(
                "PrepareVerifiedUpdate",
                "Failed",
                source: "GitHub",
                details:
                    $"Automatic={automatic}; {ex.Message}");

            if (automatic)
            {
                WindowsEventLogService.TryWrite(
                    "Automatic update preparation failed: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4104,
                    "Update");
            }
            else
            {
                ShowAppError(
                    "Preparing the verified update failed.",
                    "PrepareVerifiedUpdate",
                    ex,
                    ("Repository", _config.UpdateRepository));
            }

            return false;
        }
    }

    private void OpenLatestRelease()
    {
        var url = _lastUpdateInfo?.ReleaseUrl;
        if (string.IsNullOrWhiteSpace(url))
            url = $"https://github.com/{_config.UpdateRepository}/releases";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowAppError(
                "Opening the GitHub release page failed.",
                "OpenLatestRelease",
                ex,
                ("Repository", _config.UpdateRepository));
        }
    }

}
