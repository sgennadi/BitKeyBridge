namespace BitKeyBridge;

/// <summary>
/// Compact, read-only-at-rest update dashboard. Controls are explicit:
/// pause changes only the installation setting; retry only checks metadata;
/// a rollback/failure hold can be cleared ONLY by the existing separately
/// confirmed Resume Auto-Install action.
/// </summary>
public sealed partial class MainForm
{
    private readonly Label _updateCenterMain = new();
    private Label? _updateCenterHeading;
    private Label? _updateCenterDescription;
    private Button? _updateCenterRefreshAction;
    private Button? _updateCenterRetryAction;
    private Button? _updateCenterPauseAction;
    private Button? _updateCenterEnableAction;
    private readonly Label _updateCenterExtra = new();
    private readonly CheckBox _compactUpdateCenter = new();
    private bool _updateCenterBuilt;
    private int _updateCenterStatusRequestId;

    private Control BuildUpdateCenter()
    {
        string T(string key) => UiLanguageService.Translate(_uiLanguageCode, key);

        var grid = new TableLayoutPanel
        {
            Name = "UpdateCenterV2",
            AccessibleName = "BitKeyBridge Update Center status and controls",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        var header = CreateSectionTitle(
            T("UpdateCenterTitle"), T("UpdateCenterSubtitle"));
        _updateCenterHeading = header.Controls.OfType<Label>().FirstOrDefault();
        _updateCenterDescription = header.Controls.OfType<Label>().Skip(1).FirstOrDefault();
        grid.Controls.Add(header, 0, 0);

        _updateCenterMain.Name = "UpdateCenterSummary";
        _updateCenterMain.AccessibleName = "Installed and available versions, update state";
        _updateCenterMain.AutoSize = true;
        _updateCenterMain.MaximumSize = new Size(960, 0);
        _updateCenterMain.Margin = new Padding(0, 0, 0, UiStyle.ControlGap);
        UiStyle.ConfigureWrappingLabel(_updateCenterMain);
        grid.Controls.Add(_updateCenterMain, 0, 1);

        _updateCenterExtra.Name = "UpdateCenterTaskDetails";
        _updateCenterExtra.AccessibleName =
            "Last update, next scheduled run and task result";
        _updateCenterExtra.AutoSize = true;
        _updateCenterExtra.MaximumSize = new Size(960, 0);
        UiStyle.ConfigureWrappingLabel(_updateCenterExtra);
        grid.Controls.Add(_updateCenterExtra, 0, 2);

        var buttons = CreateActionFlow();
        var refresh = NewActionButton(T("UpdateCenterRefresh"));
        refresh.Name = "UpdateCenterRefreshButton";
        var retry = NewActionButton(T("UpdateCenterRetry"));
        retry.Name = "UpdateCenterRetryCheckButton";
        var pause = NewActionButton(T("UpdateCenterPause"));
        pause.Name = "UpdateCenterPauseButton";
        var enable = NewActionButton(T("UpdateCenterEnable"));
        enable.Name = "UpdateCenterEnableButton";
        _updateCenterRefreshAction = refresh;
        _updateCenterRetryAction = retry;
        _updateCenterPauseAction = pause;
        _updateCenterEnableAction = enable;
        _compactUpdateCenter.Name = "UpdateCenterCompact";
        _compactUpdateCenter.AccessibleName = "Compact Updates layout preference";
        _compactUpdateCenter.Text = T("UpdateCenterCompact");
        _compactUpdateCenter.AutoSize = true;
        _compactUpdateCenter.Checked = true;
        _compactUpdateCenter.Margin = new Padding(8, 7, 0, 0);

        buttons.Controls.AddRange([refresh, retry, pause, enable, _compactUpdateCenter]);
        grid.Controls.Add(buttons, 0, 3);
        _updateCenterBuilt = true;

        refresh.Click += (_, _) => RefreshUpdateCenter();
        retry.Click += async (_, _) =>
        {
            await CheckForUpdatesGuiAsync(silentWhenCurrent: false);
            RefreshUpdateCenter();
        };
        pause.Click += (_, _) => TrySetUpdateCenterAutoInstall(enabled: false);
        enable.Click += (_, _) => TrySetUpdateCenterAutoInstall(enabled: true);
        _compactUpdateCenter.CheckedChanged += (_, _) =>
            ApplyUpdateCenterDensity(_compactUpdateCenter.Checked);

        return grid;
    }

    private void TrySetUpdateCenterAutoInstall(bool enabled)
    {
        // Auto-install is a MACHINE configuration, not a per-user GUI toggle.
        // Never save unrelated, possibly unsaved controls (publisher pin,
        // repository, unsigned consent, prerelease channel) from this shortcut.
        if (!SecurityContext.IsAdministrator())
        {
            UiStyle.SetStatus(_updateStatus,
                "Changing machine automatic updates requires administrator rights.",
                UiStatusKind.Warning);
            return;
        }

        try
        {
            var active = ConfigService.LoadAppConfig();
            if (enabled)
            {
                if (UpdateRollbackHoldService.HasRecordedHold())
                {
                    UiStyle.SetStatus(_updateStatus,
                        "A rollback/failure hold exists. Review the failure and use " +
                        "the separate confirmed Resume Auto-Install action.",
                        UiStatusKind.Warning);
                    return;
                }
                if (!UpdateTrustPolicy.CanInstallUnattended(active))
                {
                    UiStyle.SetStatus(_updateStatus,
                        "Cannot enable auto-install: current trusted signing, " +
                        "explicit unsigned consent or managed GPO policy blocks it. " +
                        "Review and Save Update Settings first.",
                        UiStatusKind.Warning);
                    return;
                }

                var confirmation = MessageBox.Show(
                    this,
                    "Enable automatic BitKeyBridge installation using the currently " +
                    "saved and approved update channel and trust settings?\n\n" +
                    "This can install an updated executable during the next " +
                    "GUI start or scheduled SYSTEM run. No unsigned consent " +
                    "or prerelease access will be granted by this action.",
                    "Enable Automatic Updates",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                    return;

                // Re-read after the human confirmation: policy or consent may
                // have been revoked while the dialog was open.
                active = ConfigService.LoadAppConfig();
                if (UpdateRollbackHoldService.HasRecordedHold() ||
                    !UpdateTrustPolicy.CanInstallUnattended(active))
                    throw new InvalidOperationException(
                        "Update authorization changed. Automatic installation remains disabled.");

                active.CheckForUpdatesOnStart = true;
            }

            active.AutoInstallUpdatesOnStart = enabled;
            ConfigService.SaveAppConfig(active);

            // Keep in-memory configuration and GUI controls in sync only AFTER
            // the atomic file save succeeded; never lift an update hold here.
            _config.CheckForUpdatesOnStart = active.CheckForUpdatesOnStart;
            _config.AutoInstallUpdatesOnStart = active.AutoInstallUpdatesOnStart;
            _checkUpdatesOnStart.Checked = active.CheckForUpdatesOnStart;
            _autoInstallUpdatesOnStart.Checked = active.AutoInstallUpdatesOnStart;
            UiStyle.SetStatus(_updateStatus,
                enabled
                    ? "Automatic installation enabled under the existing signed/unsigned and GPO policy."
                    : "Automatic installation paused. Manual checks and the configured task remain available.",
                UiStatusKind.Success);
            _audit.Write(enabled ? "UpdateCenterEnableAutomatic" : "UpdateCenterPauseAutomatic",
                source: "Local");
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_updateStatus,
                "Could not change automatic installation: " +
                DiagnosticRedaction.Sanitize(ex.Message),
                UiStatusKind.Warning);
        }
        finally
        {
            RefreshUpdateCenter();
        }
    }

    private void ApplyUpdateCenterLanguage()
    {
        if (!_updateCenterBuilt)
            return;

        string T(string key) => UiLanguageService.Translate(_uiLanguageCode, key);
        if (_updateCenterHeading is not null)
            _updateCenterHeading.Text = T("UpdateCenterTitle");
        if (_updateCenterDescription is not null)
            _updateCenterDescription.Text = T("UpdateCenterSubtitle");
        if (_updateCenterRefreshAction is not null)
            _updateCenterRefreshAction.Text = T("UpdateCenterRefresh");
        if (_updateCenterRetryAction is not null)
            _updateCenterRetryAction.Text = T("UpdateCenterRetry");
        if (_updateCenterPauseAction is not null)
            _updateCenterPauseAction.Text = T("UpdateCenterPause");
        if (_updateCenterEnableAction is not null)
            _updateCenterEnableAction.Text = T("UpdateCenterEnable");
        _compactUpdateCenter.Text = T("UpdateCenterCompact");

        if (!_layoutSelfTest && IsHandleCreated)
            RefreshUpdateCenter();
    }

    private void RefreshUpdateCenter()
    {
        if (!_updateCenterBuilt || _layoutSelfTest ||
            IsDisposed || !IsHandleCreated)
            return;

        // The Task Scheduler COM API and ProgramData may stall when the
        // scheduler service is slow or a network profile is redirected.
        // Never perform those reads on the WinForms message-pump thread.
        var requestId = Interlocked.Increment(ref _updateCenterStatusRequestId);
        _ = RefreshUpdateCenterAsync(requestId);
    }

    private async Task RefreshUpdateCenterAsync(int requestId)
    {
        try
        {
            var status = await Task.Run(() =>
            {
                // Re-read effective machine settings: another process or
                // central policy can change them while this GUI is open.
                var config = ConfigService.LoadAppConfig();
                return UpdateCenterStatusService.Capture(config);
            });

            // Two refreshes may overlap (Retry, Save, task registration).
            // Only the last request may paint the screen.
            if (IsDisposed || !IsHandleCreated ||
                requestId != Volatile.Read(ref _updateCenterStatusRequestId))
                return;

            RenderUpdateCenter(status);
        }
        catch (Exception ex)
        {
            if (IsDisposed || !IsHandleCreated ||
                requestId != Volatile.Read(ref _updateCenterStatusRequestId))
                return;

            _updateCenterMain.Text = "Update Center status unavailable: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name);
        }
    }

    private void RenderUpdateCenter(UpdateCenterSnapshot status)
    {
        try
        {
            string T(string key) => UiLanguageService.Translate(_uiLanguageCode, key);
            var checkTime = status.LastCheckUtc.HasValue
                ? status.LastCheckUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : T("UpdateCenterNever");
            _updateCenterMain.Text =
                T("UpdateCenterVersion") + ": " + status.CurrentVersion +
                "    " + T("UpdateCenterChannel") + ": " + status.Channel +
                Environment.NewLine +
                T("UpdateCenterLatest") + ": " + status.LatestCheckedVersion +
                "    " + T("UpdateCenterLastCheck") + ": " + checkTime +
                Environment.NewLine +
                status.InstallationState +
                (status.RollbackOrFailureHold
                    ? Environment.NewLine + "Rollback/failure hold is active." : string.Empty) +
                (string.IsNullOrWhiteSpace(status.CheckError)
                    ? string.Empty : Environment.NewLine + status.CheckError);

            var last = status.LastUpdateUtc.HasValue
                ? status.LastUpdateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : T("UpdateCenterNever");
            _updateCenterExtra.Text =
                T("UpdateCenterLastResult") + ": " + status.LastUpdateAction +
                "  " + status.LastUpdateResult + "  (" + last + ")" +
                Environment.NewLine +
                T("UpdateCenterTask") + ": " + status.Task.Status +
                "    " + T("UpdateCenterNextRun") + ": " +
                (string.IsNullOrEmpty(status.Task.NextRunLocal)
                    ? T("UpdateCenterNever") : status.Task.NextRunLocal) +
                Environment.NewLine +
                "Last task run: " +
                (string.IsNullOrEmpty(status.Task.LastRunLocal)
                    ? T("UpdateCenterNever") : status.Task.LastRunLocal) +
                "    Result: " +
                (string.IsNullOrEmpty(status.Task.LastResult)
                    ? "N/A" : status.Task.LastResult) +
                Environment.NewLine +
                "Trust: " + status.TrustMode;

            _updateCenterMain.ForeColor = status.RollbackOrFailureHold ||
                status.CheckError.Length > 0
                    ? UiStyle.RecoveryCardMutedText
                    : SystemColors.ControlText;
        }
        catch (Exception ex)
        {
            _updateCenterMain.Text = "Update Center display unavailable: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name);
        }
    }

    private void ApplyUpdateCenterDensity(bool compact)
    {
        if (!_updateCenterBuilt)
            return;

        if (_compactUpdateCenter.Checked != compact)
            _compactUpdateCenter.Checked = compact;

        _updateCenterExtra.Visible = !compact;
        _updateHistoryResults.Height = compact ? 148 : 215;
        _updateHistoryResults.MinimumSize = new Size(0, compact ? 110 : 160);
    }
}
