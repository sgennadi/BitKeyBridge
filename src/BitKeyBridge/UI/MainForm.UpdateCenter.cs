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
        pause.Click += (_, _) =>
        {
            var original = _autoInstallUpdatesOnStart.Checked;
            _autoInstallUpdatesOnStart.Checked = false;
            if (!SaveUpdateSettings(showConfirmation: true))
                _autoInstallUpdatesOnStart.Checked = original;
            RefreshUpdateCenter();
        };
        enable.Click += (_, _) =>
        {
            // Never use this short path to bypass a failed-update hold.
            if (UpdateRollbackHoldService.HasRecordedHold())
            {
                UiStyle.SetStatus(_updateStatus,
                    "An update hold exists. Investigate the rollback and use " +
                    "the separate Resume Auto-Install control.",
                    UiStatusKind.Warning);
                return;
            }

            var oldChecks = _checkUpdatesOnStart.Checked;
            var oldInstall = _autoInstallUpdatesOnStart.Checked;
            _checkUpdatesOnStart.Checked = true;
            _autoInstallUpdatesOnStart.Checked = true;
            if (!SaveUpdateSettings(showConfirmation: true))
            {
                _autoInstallUpdatesOnStart.Checked = oldInstall;
                _checkUpdatesOnStart.Checked = oldChecks;
            }
            RefreshUpdateCenter();
        };
        _compactUpdateCenter.CheckedChanged += (_, _) =>
            ApplyUpdateCenterDensity(_compactUpdateCenter.Checked);

        return grid;
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
        if (!_updateCenterBuilt || _layoutSelfTest || IsDisposed)
            return;

        try
        {
            var status = UpdateCenterStatusService.Capture(_config);
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
            _updateCenterMain.Text = "Update Center status unavailable: " +
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
