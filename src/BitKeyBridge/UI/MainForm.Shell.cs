namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly TabControl _mainTabs = new();
    private ToolStripMenuItem? _toolsTopMenu;
    private ToolStripMenuItem? _recoveryStepsMenu;
    private ToolStripMenuItem? _accessHealthMenu;
    private ToolStripMenuItem? _languageTopMenu;
    private ToolStripMenuItem? _sectionExplanationsMenuItem;
    private readonly List<ToolStripMenuItem> _languageChoices = [];
    private string _uiLanguageCode = "en";
    private bool _shellLanguageApplied;
    private readonly UiDiagnosticPanel _appDiagnostics = new();
    private readonly ComboBox _recoverySource = new();
    private readonly Button _connectAdButton = new();
    private readonly Button _connectAdCancelButton = new();
    private readonly Button _disconnectAdButton = new();
    private readonly ProgressBar _adConnectionProgress = new();
    private readonly UiDiagnosticPanel _recoveryDiagnostics = new();
    private readonly TableLayoutPanel _bitLockerZeroWarningPanel = new();
    private readonly UiStatusLabel _bitLockerZeroWarning = new();
    private readonly Button _bitLockerDiagnosticCommands = new();
    private readonly GroupBox _advancedConnectionGroup = new();
    private readonly GroupBox _recoveryCard = new();
    private readonly Label _recoveryCardComputer = new();
    private readonly Label _recoveryCardOu = new();
    private readonly Label _recoveryCardId = new();
    private readonly Label _recoveryCardTime = new();
    private readonly Label _recoveryCardSource = new();
    private readonly Button _startCopy = new();
    private readonly Button _recoveryBackToStart = new();
    private readonly Button _lapsBackToStart = new();

    private readonly UiStatusLabel _homeConnectionStatus = new();
    private readonly UiStatusLabel _homeAdAccountStatus = new();
    private readonly Button _homeConnectAdButton = new();
    private readonly Button _homeDisconnectAdButton = new();
    private readonly Button _homeConnectionToolsButton = new();
    private readonly ContextMenuStrip _homeConnectionToolsMenu = new();
    private readonly ToolStripMenuItem _homeAdvancedConnectionMenuItem = new();
    private readonly ProgressBar _homeConnectionProgress = new();
    private readonly TextBox _homeQuery = new();
    private readonly Button _homeSearchButton = new();
    private readonly Button _homeSearchCancelButton = new();
    private readonly ProgressBar _homeSearchProgress = new();
    private readonly UiStatusLabel _homeSearchStatus = new();
    private readonly DataGridView _homeSearchResults = new();
    private readonly TableLayoutPanel _homeResultsArea = new();
    private TableLayoutPanel? _homeConnectionsRecentLayout;
    private TableLayoutPanel? _homeAdConnectionLayout;
    private TableLayoutPanel? _homeRecentQuickPanel;
    private readonly DataGridView _homeRecentResults = new();
    private readonly RadioButton _homeUseWindowsIdentity = new();
    private readonly RadioButton _homeUseOtherAccount = new();
    private readonly ToolStripStatusLabel _globalConnectionStatus = new();
    private readonly ToolStripStatusLabel _globalAdAccountStatus = new();
    private readonly ToolStripStatusLabel _globalVersionStatus = new();
    private readonly ToolStripStatusLabel _globalCopyDiagnostics = new();
    private System.Windows.Forms.Timer? _homeSearchDebounceTimer;
    private CancellationTokenSource? _homeSearchCancellation;
    private int _homeSearchGeneration;
    private TabPage? _homeWorkspaceTab;
    private TabPage? _recoveryWorkspaceTab;
    private TabPage? _lapsWorkspaceTab;

    private readonly ComboBox _deviceRecoveryIds = new();
    private readonly TextBox _deviceRecoveryKey = new();
    private readonly Button _deviceShowKey = new();
    private readonly Button _deviceCopyKey = new();
    private string? _deviceCurrentKey;

    // A restricted endpoint-management token may fail an identity lookup.
    // Treat that as NOT authorized to administer settings; never let it prevent
    // a standard-user Helpdesk session from constructing its main window.
    private bool AdministrationAllowed
    {
        get
        {
            if (_layoutSelfTest)
                return true;
            try
            {
                return new AuthorizationService(_config)
                    .Check(BitKeyBridgePermission.Administrator)
                    .Allowed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Administrator tab visibility check unavailable: " +
                    ex.GetType().Name);
                return false;
            }
        }
    }

    private Control BuildMainShell()
    {
        var root = new TableLayoutPanel
        {
            Name = "MainLayoutRoot",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var menu = BuildMainMenu();
        root.Controls.Add(menu, 0, 0);

        _appDiagnostics.Name =
            "ApplicationDiagnostics";
        _appDiagnostics.AccessibleName =
            "Application diagnostics";
        root.Controls.Add(
            _appDiagnostics,
            0,
            1);

        _mainTabs.Name =
            "MainWorkspaceTabs";
        _mainTabs.Dock =
            DockStyle.Fill;
        // Track native HWND creation/destruction counts, never their values.
        _mainTabs.HandleCreated += (_, _) =>
            UiLayoutTelemetry.RecordMainTabHandleCreated();
        _mainTabs.HandleDestroyed += (_, _) =>
            UiLayoutTelemetry.RecordMainTabHandleDestroyed();

        _homeWorkspaceTab =
            BuildHomeWorkspaceTab();
        _recoveryWorkspaceTab =
            BuildRecoveryWorkspaceTab();
        _lapsWorkspaceTab =
            BuildLapsWorkspaceTab();

        // Keep primary recovery tasks in the technician's actual work order:
        // Start -> BitLocker -> LAPS -> Devices / Microsoft 365 -> Helpdesk.
        // Never reconstruct pages when switching, so search state and
        // authorization checks remain bound to their original instances.
        _mainTabs.TabPages.Add(_homeWorkspaceTab);
        _mainTabs.TabPages.Add(_recoveryWorkspaceTab);
        _mainTabs.TabPages.Add(_lapsWorkspaceTab);
        _mainTabs.TabPages.Add(BuildDevicesWorkspaceTab());
        _mainTabs.TabPages.Add(BuildHelpdeskDashboardTab());

        // Read-only access diagnostics are useful to non-admin Helpdesk staff.
        // Privileged service, DC and signed-audit controls remain gated.
        _mainTabs.TabPages.Add(
            BuildHealthAuditWorkspaceTab());
        if (AdministrationAllowed)
            _mainTabs.TabPages.Add(BuildAdministrationWorkspaceTab());

        // Scrolling belongs to each workspace's inner viewport. An
        // AutoScroll TabPage can leave Dock=Fill nested tabs at an obsolete
        // virtual width after DPI or RDP resolution changes.
        foreach (TabPage page in _mainTabs.TabPages)
        {
            page.AutoScroll = false;
            // Set an EXPLICIT LTR value before the parent ever mirrors.
            // An inherited LTR value looks the same but flips to RTL during
            // the first Hebrew switch, forcing every full-page subtree to
            // reflow/recreate native handles before it is reset to LTR.
            // Recovery passwords, device IDs and content remain LTR forever.
            page.RightToLeft = RightToLeft.No;
        }

        _mainTabs.SelectedIndexChanged +=
            (_, _) =>
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                ApplyComputerQueryToSelectedWorkspace();
                QueueNavigationViewportReflow(started);

                if (ReferenceEquals(
                        _mainTabs.SelectedTab,
                        _recoveryWorkspaceTab))
                {
                    _recoveryBackToStart.Visible =
                        false;
                }
                else if (ReferenceEquals(
                             _mainTabs.SelectedTab,
                             _lapsWorkspaceTab))
                {
                    _lapsBackToStart.Visible =
                        false;
                }
            };

        // Keyboard navigation uses the same authorized existing TabPages.
        // Admin-only Settings cannot be opened when its tab is absent.
        KeyDown += (_, e) =>
        {
            // Global, non-secret Helpdesk shortcuts; they never invoke
            // Reveal, Copy, rotation or an audit-broker probe.
            if (e.Control && e.Shift && !e.Alt)
            {
                if (e.KeyCode == Keys.H)
                {
                    OpenHelpdeskAccessHealth();
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.R)
                {
                    ShowRecoveryWorkflowSteps();
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                }
                // Ctrl+Shift+P uses the actual ToolStrip ShortcutKeys
                // routing and stays usable when a TextBox or grid has focus.
                return;
            }
            if (!e.Control || e.Alt || e.Shift)
                return;

            // Ctrl+F focuses the existing metadata search input in the
            // currently selected workspace; it never invokes secret reveal.
            if (e.KeyCode == Keys.F)
            {
                TextBox? query = _mainTabs.SelectedIndex switch
                {
                    0 => _homeQuery,
                    1 => _startQuery,
                    2 => _lapsQuery,
                    3 => _unifiedQuery,
                    // Helpdesk reuses the one Start query, never a second
                    // textbox or a second metadata request.
                    4 => _homeQuery,
                    _ => null
                };
                if (_mainTabs.SelectedTab?.Name == "HelpdeskDashboardTab")
                    OpenHelpdeskDeviceWorkspace();
                if (query is { Visible: true, Enabled: true })
                {
                    query.Focus();
                    query.SelectAll();
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                }
                return;
            }

            int index = e.KeyCode switch
            {
                Keys.D1 or Keys.NumPad1 => 0,
                Keys.D2 or Keys.NumPad2 => 1,
                Keys.D3 or Keys.NumPad3 => 2,
                Keys.D4 or Keys.NumPad4 => 3,
                Keys.D5 or Keys.NumPad5 => 4,
                Keys.D6 or Keys.NumPad6 => 5,
                Keys.D7 or Keys.NumPad7 => 6,
                _ => -1
            };
            if (index < 0 || index >= _mainTabs.TabPages.Count)
                return;

            _mainTabs.SelectedIndex = index;
            e.SuppressKeyPress = true;
            e.Handled = true;
        };

        // The technician explicitly requested Start as the landing page.
        // Do not change selection in Shown (including after the connection
        // wizard), and never silently switch to Helpdesk on normal launch.
        _mainTabs.SelectedTab = _homeWorkspaceTab;
        ApplyShellLanguage();

        root.Controls.Add(_mainTabs, 0, 2);
        root.Controls.Add(
            BuildGlobalStatusBar(),
            0,
            3);
        MainMenuStrip = menu;
        return root;
    }

    private StatusStrip BuildGlobalStatusBar()
    {
        var status =
            new StatusStrip
            {
                Name =
                    "GlobalStatusBar",
                SizingGrip =
                    false,
                // Status links are screen-positioned, never mirrored with
                // Hebrew navigation; copy must remain at a stable edge.
                RightToLeft = RightToLeft.No
            };

        _globalConnectionStatus.Name =
            "GlobalConnectionStatus";
        _globalConnectionStatus.Text =
            "AD: disconnected";
        _globalConnectionStatus.IsLink =
            true;
        _globalConnectionStatus.Spring =
            true;
        _globalConnectionStatus.TextAlign =
            ContentAlignment.MiddleLeft;
        _globalConnectionStatus.ToolTipText =
            "Open Start / connection settings";

        _globalAdAccountStatus.Name =
            "GlobalAdAccountStatus";
        _globalAdAccountStatus.Text =
            "AD user: not connected";
        _globalAdAccountStatus.ToolTipText =
            "No validated Active Directory account is connected.";

        _globalVersionStatus.Name =
            "GlobalVersionStatus";
        _globalVersionStatus.Text =
            "v" +
            (GetType().Assembly.GetName().Version?.ToString(3) ??
             "unknown");

        _globalCopyDiagnostics.Name = "GlobalCopyDiagnostics";
        _globalCopyDiagnostics.IsLink = true;
        _globalCopyDiagnostics.Text =
            UiStaticCaptionCatalog.TranslateOrOriginal(
                _uiLanguageCode, "Copy diagnostics");
        _globalCopyDiagnostics.ToolTipText =
            "Copy current redacted WinForms layout timings (Ctrl+Shift+P)";
        _globalCopyDiagnostics.Margin = new Padding(
            UiStyle.ControlGap, 0, UiStyle.ControlGap, 0);
        _globalCopyDiagnostics.Click += (_, _) =>
            ShowUiPerformanceReport(copyImmediately: true);

        status.Items.Add(_globalConnectionStatus);
        status.Items.Add(_globalAdAccountStatus);
        // The bottom bar is outside the main TabControl and diagnostics
        // AutoSize row; this copy link remains accessible even if a native
        // RTL tab transition temporarily hides the inline action row.
        status.Items.Add(_globalCopyDiagnostics);
        status.Items.Add(_globalVersionStatus);

        _globalConnectionStatus.Click +=
            (_, _) =>
            {
                if (_homeWorkspaceTab is not null)
                {
                    _mainTabs.SelectedTab =
                        _homeWorkspaceTab;
                    _advancedConnectionGroup.Visible =
                        true;
                    _homeAdvancedConnectionMenuItem.Text =
                        "Hide advanced AD settings";
                }
            };

        return status;
    }

    private MenuStrip BuildMainMenu()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top
        };

        var tools = new ToolStripMenuItem("Tools")
        {
            AutoSize = true,
            Padding = new Padding(10, 0, 10, 0)
        };

        if (AdministrationAllowed)
        {
            tools.DropDownItems.Add(
                "Open recovery export",
                null,
                (_, _) => OpenPath(_config.OutputCsv));
            tools.DropDownItems.Add(
                "Open export log",
                null,
                (_, _) => OpenPath(_config.ErrorLog));
            tools.DropDownItems.Add(
                "Open export folder",
                null,
                (_, _) => OpenPath(_config.OutputDirectory));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(
                "Windows Event Log",
                null,
                (_, _) => OpenWindowsEventLog());
            tools.DropDownItems.Add(new ToolStripSeparator());
        }

        var recoverySteps = new ToolStripMenuItem(
            "Recovery Steps (Ctrl+Shift+R)")
        {
            Name = "RecoveryStepsMenu",
            ToolTipText = "Read-only helpdesk recovery checklist; never reveals a password."
        };
        recoverySteps.Click += (_, _) => ShowRecoveryWorkflowSteps();
        _recoveryStepsMenu = recoverySteps;
        tools.DropDownItems.Add(recoverySteps);

        var accessHealth = new ToolStripMenuItem(
            "Access Health (Ctrl+Shift+H)")
        {
            Name = "AccessHealthMenu",
            ToolTipText = "Metadata-only permissions diagnosis; no secret reads or elevation."
        };
        accessHealth.Click += (_, _) => OpenHelpdeskAccessHealth();
        _accessHealthMenu = accessHealth;
        tools.DropDownItems.Add(accessHealth);

        // The global diagnostic banner can be temporarily obscured while
        // WinForms rebuilds a mirrored tab handle. This persistent Tools
        // action and Ctrl+Shift+P always copy a FRESH non-secret report.
        var copyUiReport = new ToolStripMenuItem("Copy UI diagnostics")
        {
            Name = "CopyUiPerformanceReportMenuItem",
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.P,
            ShowShortcutKeys = true,
            ToolTipText = "Copy current non-secret WinForms layout timings."
        };
        copyUiReport.Click += (_, _) => ShowUiPerformanceReport(copyImmediately: true);
        tools.DropDownItems.Add(copyUiReport);
        var latestRelease = tools.DropDownItems.Add(
            "Latest GitHub release",
            null,
            (_, _) => OpenLatestRelease());
        latestRelease.Name = "LatestGithubReleaseMenuItem";

        // Compact by default. Only noninteractive explanatory paragraphs
        // are hidden; all controls, security checks and status remain.
        tools.DropDownItems.Add(new ToolStripSeparator());
        var explanations = new ToolStripMenuItem("Show explanations")
        {
            Name = "UiShowSectionExplanations",
            CheckOnClick = true,
            Checked = _showSectionExplanations,
            ToolTipText = "Show or hide section descriptions; no settings or permissions change."
        };
        explanations.CheckedChanged += (_, _) =>
            SetSectionExplanationsVisible(explanations.Checked);
        _sectionExplanationsMenuItem = explanations;
        tools.DropDownItems.Add(explanations);

        _toolsTopMenu = tools;
        menu.Items.Add(tools);

        // Language selection is per user. Never change machine RBAC, cloud
        // credentials, system locale or the saved search/recovery context.
        var languages = new ToolStripMenuItem("Language")
        {
            Name = "UiLanguageMenu",
            AutoSize = true,
            // Hebrew שפה must retain all glyphs after menu RTL/reflow.
            Padding = new Padding(10, 0, 10, 0)
        };
        _languageTopMenu = languages;
        foreach (var language in UiLanguageService.SupportedLanguages)
        {
            var option = new ToolStripMenuItem(language.DisplayName)
            {
                Name = "UiLanguage-" + language.Code,
                Tag = language.Code,
                CheckOnClick = false
            };
            option.Click += (_, _) => SetShellLanguage(language.Code);
            languages.DropDownItems.Add(option);
            _languageChoices.Add(option);
        }
        menu.Items.Add(languages);
        return menu;
    }

    private void SetShellLanguage(string code)
    {
        var next = UiLanguageService.Normalize(code);
        if (string.Equals(next, _uiLanguageCode, StringComparison.Ordinal))
            return;
        try
        {
            UiLanguageService.SavePreferred(next);
            _uiLanguageCode = next;
            ApplyShellLanguage();
            QueueNavigationViewportReflow();
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Cannot save the selected interface language.",
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private void ApplyShellLanguage()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var initialApply = !_shellLanguageApplied;
        var rtl = UiLanguageService.IsRightToLeft(_uiLanguageCode);
        var direction = rtl ? RightToLeft.Yes : RightToLeft.No;
        // Mirroring the entire Form HWND can recreate native child controls
        // and stall during Hebrew switches, especially under RDP.
        _mainTabs.SuspendLayout();
        MainMenuStrip?.SuspendLayout();
        try
        {
            var chromeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            if (RightToLeft != RightToLeft.No)
                RightToLeft = RightToLeft.No;
            if (RightToLeftLayout)
                RightToLeftLayout = false;
            // Preserve the same native main-tab paint mode for EN, RU and HE.
            // This avoids a second expensive HWND recreation from switching
            // OwnerDrawFixed <-> Normal, while real Hebrew tab mirroring and
            // tab order continue to work through RightToLeftLayout.
            if (_mainTabs.DrawMode != TabDrawMode.Normal)
                _mainTabs.DrawMode = TabDrawMode.Normal;
            var directionStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var directionChanged = _mainTabs.RightToLeft != direction;
            if (directionChanged)
                _mainTabs.RightToLeft = direction;
            UiLayoutTelemetry.RecordLanguageTabDirection(
                directionStarted, initialApply, directionChanged);
            var mirroringStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var mirroringChanged = _mainTabs.RightToLeftLayout != rtl;
            if (mirroringChanged)
                _mainTabs.RightToLeftLayout = rtl;
            UiLayoutTelemetry.RecordLanguageTabMirroring(
                mirroringStarted, initialApply, mirroringChanged);
            // Each page has been explicitly LTR since construction. Do not
            // reapply RightToLeft here: repeating this operation may cause
            // multi-second WinForms native control-tree rebuilds.
            var menuStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            if (MainMenuStrip is { } menu && menu.RightToLeft != direction)
                menu.RightToLeft = direction;
            UiLayoutTelemetry.RecordLanguageMenuDirection(menuStarted);
            UiLayoutTelemetry.RecordLanguageChrome(chromeStarted);

            // Recovery-key strings, passwords, GUIDs and account/device IDs
            // must stay visually LTR even under mirrored Hebrew navigation.
            foreach (var literal in new[]
            {
                _homeQuery, _startQuery, _lapsQuery, _unifiedQuery,
                _startKey, _lapsPassword, _deviceRecoveryKey,
                _adServer, _adDomain, _adUsername,
                _cloudTenant, _cloudClient, _cloudThumbprint
            })
                literal.RightToLeft = RightToLeft.No;

            var keys = new[]
            {
                "Start", "BitLocker", "LAPS", "Devices", "Helpdesk",
                "Diagnostics", "Settings"
            };
            for (var i = 0; i < Math.Min(keys.Length, _mainTabs.TabPages.Count); i++)
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, keys[i]);
                if (!string.Equals(_mainTabs.TabPages[i].Text, caption,
                        StringComparison.Ordinal))
                    _mainTabs.TabPages[i].Text = caption;
            }

            if (_toolsTopMenu is not null)
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, "Tools");
                if (_toolsTopMenu.Text != caption)
                    _toolsTopMenu.Text = caption;
            }
            if (_recoveryStepsMenu is not null)
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, "RecoveryStepsShortcut");
                if (_recoveryStepsMenu.Text != caption)
                    _recoveryStepsMenu.Text = caption;
            }
            if (_accessHealthMenu is not null)
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, "AccessHealthShortcut");
                if (_accessHealthMenu.Text != caption)
                    _accessHealthMenu.Text = caption;
            }
            if (_languageTopMenu is not null)
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, "Language");
                if (_languageTopMenu.Text != caption)
                    _languageTopMenu.Text = caption;
            }
            foreach (var choice in _languageChoices)
                choice.Checked = string.Equals(choice.Tag as string,
                    _uiLanguageCode, StringComparison.Ordinal);

            _homeSearchButton.Text =
                UiLanguageService.Translate(_uiLanguageCode, "Search");
            _homeSearchCancelButton.Text =
                UiLanguageService.Translate(_uiLanguageCode, "Cancel");
            _globalConnectionStatus.ToolTipText =
                UiLanguageService.Translate(_uiLanguageCode, "ConnectionSettings");
            var captionsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            ApplyUpdateCenterLanguage();
            ApplyLocalizedShellCaptions(rtl);
            // The application-wide diagnostic banner lives outside
            // _mainTabs and is not part of the shell-caption observer.
            // Never clear its active report during a language change.
            _appDiagnostics.ApplyLanguage(_uiLanguageCode);
            var copyCaption = UiStaticCaptionCatalog.TranslateOrOriginal(
                _uiLanguageCode, "Copy diagnostics");
            if (!string.Equals(_globalCopyDiagnostics.Text, copyCaption,
                    StringComparison.Ordinal))
                _globalCopyDiagnostics.Text = copyCaption;
            UiLayoutTelemetry.RecordLanguageCaptions(captionsStarted);
        }
        finally
        {
            var resumeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            // Initial/show and headless UI tests require an immediate pass.
            // Warm user-initiated changes schedule a single selected-viewport
            // reflow from SetShellLanguage; forcing the entire TabControl tree
            // here was a redundant synchronous layout during RTL transitions.
            _mainTabs.ResumeLayout(performLayout: initialApply || _layoutSelfTest);
            // false reused the old menu layout after switching between
            // Language / Язык / שפה, sometimes clipping the final Hebrew glyph.
            MainMenuStrip?.ResumeLayout(performLayout: true);
            // Native TabControl RTL transitions can change Win32 z-order.
            // Keep the non-modal diagnostics accessible until Dismiss.
            if (_appDiagnostics.HasActiveDiagnostics)
                _appDiagnostics.BringToFront();
            UiLayoutTelemetry.RecordLanguageResume(resumeStarted);
            _shellLanguageApplied = true;
            UiLayoutTelemetry.RecordLanguageSwitch(started, initialApply);
        }
    }

    internal void PreviewShellLanguageForLayoutTest(string code)
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Language preview is restricted to UI tests.");
        _uiLanguageCode = UiLanguageService.Normalize(code);
        ApplyShellLanguage();
    }

    private TabPage BuildHomeWorkspaceTab()
    {
        var tab =
            new TabPage("Start")
            {
                Name =
                    "StartWorkspace"
            };

        var root =
            new TableLayoutPanel
            {
                Name = "HomeLayoutRoot",
                Dock =
                    DockStyle.Fill,
                AutoScroll =
                    true,
                Padding =
                    new Padding(
                        UiStyle.PagePadding),
                ColumnCount =
                    1,
                RowCount =
                    6
            };

        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        // Results and recovery cards are intrinsically sized. Giving the
        // search grid a percentage of the whole window previously pushed
        // the BitLocker/LAPS cards below a giant empty grid viewport.
        // Keep Connections, search and optional selection status compact.
        // All remaining vertical space is shared by the three equal panes.
        for (var index = 0; index < 6; index++)
            root.RowStyles.Add(new RowStyle(
                index == 5 ? SizeType.Percent : SizeType.AutoSize,
                index == 5 ? 100F : 0F));

        tab.Controls.Add(
            root);

        root.Controls.Add(
            new Label
            {
                Text =
                    "BitKeyBridge",
                Font =
                    UiStyle.CreatePageTitleFont(),
                AutoSize =
                    true,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            },
            0,
            0);

        root.Controls.Add(
            new Label
            {
                Text =
                    "Connect once, search once, then view BitLocker and LAPS side by side. " +
                    "Search reads metadata only — secrets are still read on demand.",
                AutoSize =
                    true,
                MaximumSize =
                    new Size(
                        1100,
                        0),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            },
            0,
            1);

        var connectionGroup =
            new GroupBox
            {
                Text =
                    "Connections",
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                Padding =
                    new Padding(14),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            };

        var connection =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    1,
                RowCount =
                    8
            };

        connection.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        for (var index = 0;
             index < 8;
             index++)
        {
            connection.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
        }

        _homeConnectionStatus.Name =
            "HomeConnectionStatus";
        _homeConnectionStatus.AccessibleName =
            "Start page Active Directory connection status";
        UiStyle.ConfigureStatusLabel(
            _homeConnectionStatus);
        _homeConnectionStatus.MaximumSize =
            new Size(
                1100,
                0);
        UiStyle.SetStatus(
            _homeConnectionStatus,
            "Not connected. BitKeyBridge will try the configured AD connection when the GUI starts.",
            UiStatusKind.Neutral);
        connection.Controls.Add(
            _homeConnectionStatus,
            0,
            0);

        _homeAdAccountStatus.Name =
            "HomeAdAccountStatus";
        _homeAdAccountStatus.AccessibleName =
            "Connected Active Directory account";
        UiStyle.ConfigureInlineStatusLabel(
            _homeAdAccountStatus);
        // Space the account identity away from the main connection status.
        // Logical UiStyle spacing scales with Windows DPI.
        _homeAdAccountStatus.Margin =
            new Padding(0, UiStyle.SectionGap, 0, UiStyle.ControlGap);
        _homeAdAccountStatus.MaximumSize =
            new Size(1100, 0);
        UiStyle.SetStatus(
            _homeAdAccountStatus,
            "Connected as: not connected",
            UiStatusKind.Neutral);
        connection.Controls.Add(
            _homeAdAccountStatus,
            0,
            1);

        var identity =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                WrapContents =
                    true,
                FlowDirection =
                    FlowDirection.LeftToRight,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };

        _homeUseWindowsIdentity.Name =
            "HomeUseWindowsIdentity";
        _homeUseWindowsIdentity.Text =
            "Use current Windows account";
        _homeUseWindowsIdentity.AutoSize =
            true;

        _homeUseOtherAccount.Name =
            "HomeUseOtherAccount";
        _homeUseOtherAccount.Text =
            "Use another AD account";
        _homeUseOtherAccount.AutoSize =
            true;

        _startAdIdentityPanel = identity;
        identity.Controls.Add(
            _homeUseWindowsIdentity);
        identity.Controls.Add(
            _homeUseOtherAccount);
        connection.Controls.Add(
            identity,
            0,
            2);

        _homeConnectionProgress.Name =
            "HomeConnectionProgress";
        _homeConnectionProgress.AccessibleName =
            "Start page Active Directory connection progress";
        _homeConnectionProgress.Dock =
            DockStyle.Top;
        _homeConnectionProgress.Style =
            ProgressBarStyle.Marquee;
        _homeConnectionProgress.MarqueeAnimationSpeed =
            25;
        _homeConnectionProgress.Visible =
            false;
        _homeConnectionProgress.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                UiStyle.ControlGap);
        connection.Controls.Add(
            _homeConnectionProgress,
            0,
            3);

        var connectionActions =
            new FlowLayoutPanel
            {
                Name = "HomeConnectionDirectActions",
                Visible = false,
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                WrapContents =
                    true,
                FlowDirection =
                    FlowDirection.LeftToRight,
                Margin =
                    new Padding(0)
            };

        _homeConnectAdButton.Name =
            "HomeConnectAdButton";
        _homeConnectAdButton.Text =
            "Connect to AD";
        UiStyle.ConfigureActionButton(
            _homeConnectAdButton);

        _homeDisconnectAdButton.Name =
            "HomeDisconnectAdButton";
        _homeDisconnectAdButton.Text =
            "Disconnect";
        UiStyle.ConfigureActionButton(
            _homeDisconnectAdButton);
        _homeDisconnectAdButton.Enabled =
            false;

        // Common connection actions remain visible. Secondary tasks share
        // one discoverable menu instead of three competing toolbar buttons.
        _homeConnectionToolsButton.Name = "HomeConnectionToolsButton";
        _homeConnectionToolsButton.Text = "Connection tools...";
        _homeConnectionToolsButton.AccessibleName =
            "Open environment checks, device diagnostics and advanced AD settings";
        UiStyle.ConfigureActionButton(_homeConnectionToolsButton);
        _homeConnectionToolsButton.ContextMenuStrip = _homeConnectionToolsMenu;
        _homeConnectionToolsButton.Click += (_, _) =>
            _homeConnectionToolsMenu.Show(
                _homeConnectionToolsButton, 0, _homeConnectionToolsButton.Height);
        _homeConnectionToolsButton.Disposed +=
            (_, _) => _homeConnectionToolsMenu.Dispose();

        var environmentCheck = new ToolStripMenuItem("Environment Check...")
        {
            Name = "HomeEnvironmentCheckMenuItem"
        };
        environmentCheck.Click += (_, _) => ShowEnvironmentDiagnosticDialog();
        var deviceDiagnostics = new ToolStripMenuItem("Device Diagnostics...")
        {
            Name = "HomeDeviceDiagnosticsMenuItem"
        };
        deviceDiagnostics.Click += (_, _) => ShowAdvancedDiagnosticCenter();

        _homeAdvancedConnectionMenuItem.Name = "HomeAdvancedConnectionMenuItem";
        _homeAdvancedConnectionMenuItem.Text = "Advanced AD settings...";
        _homeAdvancedConnectionMenuItem.Click += (_, _) =>
        {
            _advancedConnectionGroup.Visible = !_advancedConnectionGroup.Visible;
            RefreshAdvancedConnectionMenuCaption();
        };

        _homeConnectionToolsMenu.Items.AddRange([
            environmentCheck,
            deviceDiagnostics,
            new ToolStripSeparator(),
            _homeAdvancedConnectionMenuItem
        ]);
        connectionActions.Controls.AddRange([
            _homeConnectAdButton,
            _homeDisconnectAdButton,
            _homeConnectionToolsButton
        ]);
        _startDirectActionsPanel = connectionActions;
        connection.Controls.Add(
            connectionActions,
            0,
            4);

        var directConnectionHint = new Label
            {
                Name = "HomeConnectionDirectHint",
                Visible = false,
                Text =
                    "Direct AD actions and account selection are optional. Connect / Change opens the normal connection wizard.",
                AutoSize =
                    true,
                MaximumSize =
                    new Size(
                        1100,
                        0),
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };
        _startDirectHint = directConnectionHint;
        connection.Controls.Add(directConnectionHint, 0, 5);

        UiStyle.ConfigureStatusLabel(
            _startSetupStatus);
        _startSetupStatus.Name =
            "StartSetupStatus";
        _startSetupStatus.AccessibleName =
            "Start setup status";
        _startSetupStatus.MaximumSize =
            new Size(
                1100,
                0);
        UiStyle.SetStatus(
            _startSetupStatus,
            "Setup status is loading...",
            UiStatusKind.Neutral);
        connection.Controls.Add(
            _startSetupStatus,
            0,
            6);

        connection.Controls.Add(
            _recoveryDiagnostics,
            0,
            7);

        // Insert a single source chooser ABOVE the existing AD controls,
        // preserving all their original row coordinates and dependencies.
        connection.RowCount = 9;
        connection.RowStyles.Insert(0, new RowStyle(SizeType.AutoSize));
        foreach (Control existing in connection.Controls)
            connection.SetRow(existing, connection.GetRow(existing) + 1);
        connection.Controls.Add(BuildStartConnectionSourceChooser(), 0, 0);

        // Utilize the formerly empty upper-right part of Connections.
        // Recent computers contain names/action metadata only, not secrets.
        var connectionSplit = new TableLayoutPanel
        {
            Name = "HomeConnectionsAndRecent",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        connectionSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        connectionSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        connectionSplit.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        connection.Margin = new Padding(0, 0, UiStyle.SectionGap, 0);
        connectionSplit.Controls.Add(connection, 0, 0);

        var recentPanel = new TableLayoutPanel
        {
            Name = "HomeRecentQuickPanel",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(UiStyle.ControlGap, 0, 0, 0)
        };
        recentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        recentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,
            UiStyle.HomeRecentQuickListHeight *
            DeviceDpi / (float)UiStyle.BaselineDpi));
        recentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recentPanel.Controls.Add(new Label
        {
            Text = "Recent computers",
            Font = UiStyle.CreateSectionTitleFont(),
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        }, 0, 0);

        _homeRecentResults.Name = "HomeRecentComputers";
        _homeRecentResults.AccessibleName = "Recent computers without stored secrets";
        _homeRecentResults.Dock = DockStyle.Fill;
        _homeRecentResults.MinimumSize = new Size(0, 100);
        UiStyle.ConfigureDataGridView(_homeRecentResults);
        _homeRecentResults.Columns.AddRange([
            UiStyle.CreateSortableTextColumn("Computer", "Computer", 170),
            UiStyle.CreateSortableTextColumn("LastAction", "Last action", 120),
            UiStyle.CreateSortableTextColumn("LastUsed", "Last used", 130)
        ]);
        _homeRecentResults.Columns["LastUsed"]!
            .DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
        recentPanel.Controls.Add(_homeRecentResults, 0, 1);
        var recentExplanation = new Label
        {
            Text = "Stores only device names and last action. No recovery passwords.",
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
        };
        UiStyle.ConfigureWrappingLabel(recentExplanation);
        recentPanel.Controls.Add(recentExplanation, 0, 2);
        connectionSplit.Controls.Add(recentPanel, 1, 0);
        _homeConnectionsRecentLayout = connectionSplit;
        _homeAdConnectionLayout = connection;
        _homeRecentQuickPanel = recentPanel;
        connectionGroup.Controls.Add(connectionSplit);
        root.Controls.Add(connectionGroup, 0, 2);

        ConfigureAdvancedConnectionGroup();
        root.Controls.Add(
            _advancedConnectionGroup,
            0,
            3);

        var search =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    4,
                RowCount =
                    3,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };

        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        search.Controls.Add(
            new Label
            {
                Text =
                    "Computer / Recovery ID / device ID:",
                AutoSize =
                    true,
                Font =
                    UiStyle.CreateEmphasisFont(
                        9.5F),
                Anchor =
                    AnchorStyles.Left,
                Margin =
                    new Padding(
                        0,
                        7,
                        UiStyle.ControlGap,
                        0)
            },
            0,
            0);

        _homeQuery.Name =
            "HomeUnifiedQuery";
        _homeQuery.Dock =
            DockStyle.Fill;
        _homeQuery.Font =
            UiStyle.CreateBodyFont(
                11F);
        _homeQuery.PlaceholderText =
            "ACA-HADASWI, CLINIC-300418B, Recovery ID...";
        search.Controls.Add(
            _homeQuery,
            1,
            0);

        _homeSearchButton.Name =
            "HomeUnifiedSearchButton";
        _homeSearchButton.Text =
            "Search";
        UiStyle.ConfigureActionButton(
            _homeSearchButton);
        search.Controls.Add(
            _homeSearchButton,
            2,
            0);

        _homeSearchCancelButton.Name =
            "HomeUnifiedSearchCancelButton";
        _homeSearchCancelButton.Text =
            "Cancel";
        UiStyle.ConfigureActionButton(
            _homeSearchCancelButton);
        _homeSearchCancelButton.Enabled =
            false;
        search.Controls.Add(
            _homeSearchCancelButton,
            3,
            0);

        _homeSearchProgress.Name =
            "HomeUnifiedSearchProgress";
        _homeSearchProgress.Dock =
            DockStyle.Top;
        _homeSearchProgress.Style =
            ProgressBarStyle.Marquee;
        _homeSearchProgress.MarqueeAnimationSpeed =
            25;
        _homeSearchProgress.Visible =
            false;
        _homeSearchProgress.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        search.Controls.Add(
            _homeSearchProgress,
            0,
            1);
        search.SetColumnSpan(
            _homeSearchProgress,
            4);

        UiStyle.ConfigureStatusLabel(
            _homeSearchStatus);
        _homeSearchStatus.Name =
            "HomeUnifiedSearchStatus";
        _homeSearchStatus.MaximumSize =
            new Size(
                1100,
                0);
        UiStyle.SetStatus(
            _homeSearchStatus,
            "Enter at least two characters. Live search starts after a short pause; Enter searches immediately.",
            UiStatusKind.Neutral);
        search.Controls.Add(
            _homeSearchStatus,
            0,
            2);
        search.SetColumnSpan(
            _homeSearchStatus,
            4);

        root.Controls.Add(
            search,
            0,
            4);

        _homeSearchResults.Name =
            "HomeUnifiedSearchResults";
        _homeSearchResults.AccessibleName =
            "Unified BitLocker and LAPS search results";
        _homeSearchResults.Dock =
            DockStyle.Fill;
        _homeSearchResults.MinimumSize =
            new Size(
                0,
                150);
        UiStyle.ConfigureDataGridView(
            _homeSearchResults);
        _homeSearchResults.Columns.AddRange([
            UiStyle.CreateSortableTextColumn(
                "Computer",
                "Computer",
                150),
            UiStyle.CreateSortableTextColumn(
                "Source",
                "Source",
                90),
            UiStyle.CreateSortableTextColumn(
                "BitLocker",
                "BitLocker",
                105),
            UiStyle.CreateSortableTextColumn(
                "BitLockerDate",
                "BitLocker key date",
                130),
            UiStyle.CreateSortableTextColumn(
                "Laps",
                "LAPS",
                105),
            UiStyle.CreateSortableTextColumn(
                "LapsDate",
                "LAPS metadata date",
                130)
        ]);
        _homeSearchResults.Columns["BitLockerDate"]!
            .DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        _homeSearchResults.Columns["LapsDate"]!
            .DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        _homeResultsArea.Name = "HomeSearchResultsArea";
        _homeResultsArea.Dock = DockStyle.Fill;
        _homeResultsArea.AutoSize = false;
        _homeResultsArea.ColumnCount = 1;
        _homeResultsArea.RowCount = 2;
        _homeResultsArea.Margin = Padding.Empty;
        _homeResultsArea.ColumnStyles.Clear();
        _homeResultsArea.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));
        _homeResultsArea.RowStyles.Clear();
        _homeResultsArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _homeResultsArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Recovery buttons are owned by their cards; no navigation-only toolbar.
        _homeRevealBitLockerButton.Name =
            "HomeRevealBitLockerButton";
        _homeRevealBitLockerButton.Text =
            "Reveal BitLocker Key";
        UiStyle.ConfigureActionButton(
            _homeRevealBitLockerButton);
        _homeRevealBitLockerButton.Enabled =
            false;

        _homeCopyBitLockerButton.Name =
            "HomeCopyBitLockerButton";
        _homeCopyBitLockerButton.Text =
            "Copy BitLocker Key";
        UiStyle.ConfigureActionButton(
            _homeCopyBitLockerButton);
        _homeCopyBitLockerButton.Enabled =
            false;

        _homeSelectionPanel.Name =
            "HomeSelectionPanel";
        _homeSelectionPanel.Dock =
            DockStyle.Top;
        _homeSelectionPanel.AutoSize =
            true;
        _homeSelectionPanel.ColumnCount =
            1;
        _homeSelectionPanel.RowCount =
            2;
        _homeSelectionPanel.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        _homeSelectionPanel.Visible =
            false;
        _homeSelectionPanel.ColumnStyles.Clear();
        _homeSelectionPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));
        _homeSelectionPanel.RowStyles.Clear();
        for (var row = 0; row < 2; row++)
            _homeSelectionPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        UiStyle.ConfigureStatusLabel(
            _homeSelectionSummary);
        _homeSelectionSummary.Name =
            "HomeSelectionSummary";
        _homeSelectionSummary.AccessibleName =
            "Selected helpdesk result summary";
        _homeSelectionSummary.MaximumSize =
            new Size(
                1100,
                0);
        UiStyle.SetStatus(
            _homeSelectionSummary,
            "Select a result to see the helpdesk actions and source summary.",
            UiStatusKind.Neutral);
        _homeSelectionPanel.Controls.Add(
            _homeSelectionSummary,
            0,
            0);

        BuildHomeServiceCards();


        var secretLifetime =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                WrapContents =
                    true,
                FlowDirection =
                    FlowDirection.LeftToRight,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };

        UiStyle.ConfigureStatusLabel(
            _secretLifetimeStatus);
        _secretLifetimeStatus.Name =
            "SecretLifetimeStatus";
        _secretLifetimeStatus.AccessibleName =
            "Recovery secret lifetime status";
        _secretLifetimeStatus.MaximumSize =
            new Size(
                760,
                0);
        UiStyle.SetStatus(
            _secretLifetimeStatus,
            "No recovery secret is currently retained.",
            UiStatusKind.Neutral);

        _clearSecretNow.Name =
            "ClearRecoverySecretsNow";
        _clearSecretNow.Text =
            "Clear secrets now";
        UiStyle.ConfigureActionButton(
            _clearSecretNow);

        secretLifetime.Controls.Add(
            _secretLifetimeStatus);
        secretLifetime.Controls.Add(
            _clearSecretNow);
        _homeRecoveryStepsButton.Name = "HomeRecoverySteps";
        _homeRecoveryStepsButton.Text = "Recovery Steps";
        _homeRecoveryStepsButton.AccessibleName =
            "Review selected computer recovery steps without revealing secrets";
        _homeRecoveryStepsButton.Enabled = false;
        UiStyle.ConfigureActionButton(_homeRecoveryStepsButton);
        _homeRecoveryStepsButton.Click += (_, _) => ShowRecoveryWorkflowSteps();
        secretLifetime.Controls.Add(_homeRecoveryStepsButton);
        _homeSelectionPanel.Controls.Add(
            secretLifetime,
            0,
            1);

        // Three independently draggable, equally sized cards occupy the
        // shared workspace; selection info is a compact bar above them.
        BuildHomePaneHosts();
        _homeResultsArea.Controls.Add(_homeSelectionPanel, 0, 0);
        _homeResultsArea.Controls.Add(_homeServiceCards, 0, 1);

        root.Controls.Add(
            _homeResultsArea,
            0,
            5);

        _homeRevealBitLockerButton.Click +=
            async (_, _) =>
                await RevealHomeBitLockerAsync();

        _homeCopyBitLockerButton.Click +=
            async (_, _) =>
                await CopyHomeBitLockerAsync();

        _clearSecretNow.Click +=
            (_, _) =>
                ClearVisibleSecretsNow();

        _homeConnectAdButton.Click +=
            async (_, _) =>
                await TestDirectoryConnectionAsync(
                    promptForOu: false,
                    promptForSessionCredentials: true,
                    forceManualCredentials: _lastAdConnectionFailed &&
                        _homeUseOtherAccount.Checked);

        _homeDisconnectAdButton.Click +=
            (_, _) =>
                DisconnectDirectorySession();

        _homeUseWindowsIdentity.CheckedChanged +=
            (_, _) =>
            {
                if (!_homeUseWindowsIdentity.Checked)
                    return;

                _adExplicitCredentials.Checked =
                    false;
                UpdateDirectoryConnectionUi();
            };

        _homeUseOtherAccount.CheckedChanged +=
            (_, _) =>
            {
                if (!_homeUseOtherAccount.Checked)
                    return;

                _adExplicitCredentials.Checked =
                    true;
                UpdateDirectoryConnectionUi();
            };

        _homeSearchButton.Click +=
            async (_, _) =>
                await SearchHomeAsync(
                    immediate: true);

        _homeSearchCancelButton.Click +=
            (_, _) =>
                CancelHomeSearch();

        _homeQuery.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode !=
                    Keys.Enter)
                {
                    return;
                }

                e.SuppressKeyPress =
                    true;
                _homeSearchDebounceTimer?.Stop();
                await SearchHomeAsync(
                    immediate: true);
            };

        _homeQuery.TextChanged +=
            (_, _) =>
            {
                if (_applyingComputerQuery)
                    return;

                RememberComputerQuery(_homeQuery.Text);
                InvalidateHomeSelectionOnQueryEdit();
                QueueHomeSearch();
            };

        _homeSearchDebounceTimer ??=
            new System.Windows.Forms.Timer
            {
                Interval =
                    SearchText.DebounceMilliseconds
            };

        _homeSearchDebounceTimer.Tick +=
            async (_, _) =>
            {
                _homeSearchDebounceTimer.Stop();

                if (_homeQuery.Text.Trim().Length >=
                    SearchText.MinimumLiveSearchCharacters)
                {
                    await SearchHomeAsync(
                        immediate: false);
                }
            };

        _homeSearchResults.SelectionChanged +=
            (_, _) =>
                UpdateHomeSearchSelection();

        _homeSearchResults.CellDoubleClick +=
            (_, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    OpenSelectedHomeDefaultAction();
                }
            };

        _homeSearchResults.KeyDown +=
            (_, e) =>
            {
                if (e.KeyCode ==
                    Keys.Enter)
                {
                    e.SuppressKeyPress =
                        true;
                    OpenSelectedHomeDefaultAction();
                }
            };

        _homeRecentResults.CellDoubleClick +=
            async (_, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    await SearchRecentComputerAsync();
                }
            };

        _homeRecentResults.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode ==
                    Keys.Enter)
                {
                    e.SuppressKeyPress =
                        true;
                    await SearchRecentComputerAsync();
                }
            };

        return tab;
    }

    private void NavigateToRecoveryWorkspace(
        bool fromStart = false)
    {
        if (_recoveryWorkspaceTab is null)
            return;

        if (_recoverySource.SelectedIndex != 0)
        {
            _recoverySource.SelectedIndex =
                0;
        }

        _mainTabs.SelectedTab =
            _recoveryWorkspaceTab;
        _recoveryBackToStart.Visible =
            fromStart;
        _startQuery.Focus();
    }

    private void NavigateToLapsWorkspace(
        bool fromStart = false)
    {
        if (_lapsWorkspaceTab is null)
            return;

        if (_lapsSource.SelectedIndex != 0)
        {
            _lapsSource.SelectedIndex =
                0;
        }

        _mainTabs.SelectedTab =
            _lapsWorkspaceTab;
        _lapsBackToStart.Visible =
            fromStart;
        _lapsQuery.Focus();
    }

    private TabPage BuildRecoveryWorkspaceTab()
    {
        var tab = new TabPage("BitLocker")
        {
            Name = "RecoveryWorkspace"
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(root);

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        titlePanel.Controls.Add(new Label
        {
            Text = "Find BitLocker Recovery Key",
            Font = UiStyle.CreatePageTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        _recoveryBackToStart.Name =
            "RecoveryBackToStart";
        _recoveryBackToStart.Text =
            "Back to Start";
        _recoveryBackToStart.Visible =
            false;
        UiStyle.ConfigureActionButton(
            _recoveryBackToStart);
        titlePanel.Controls.Add(
            _recoveryBackToStart,
            1,
            0);

        var recoveryIntro =
            new Label
            {
                Text =
                    "Search by computer name or Recovery ID. BitKeyBridge searches metadata first; " +
                    "the recovery password is retrieved only when you explicitly reveal or copy it.",
                AutoSize = true,
                MaximumSize = new Size(1050, 0)
            };
        titlePanel.Controls.Add(
            recoveryIntro,
            0,
            1);
        titlePanel.SetColumnSpan(
            recoveryIntro,
            2);
        root.Controls.Add(titlePanel, 0, 0);

        _recoveryBackToStart.Click +=
            (_, _) =>
            {
                _recoveryBackToStart.Visible =
                    false;
                if (_homeWorkspaceTab is not null)
                {
                    _mainTabs.SelectedTab =
                        _homeWorkspaceTab;
                    _homeQuery.Focus();
                }
            };

        UiStyle.ConfigureInlineStatusLabel(
            _startPurposeStatus);
        _startPurposeStatus.AccessibleName =
            "Recovery workflow status";
        _startPurposeStatus.Font =
            UiStyle.CreateEmphasisFont(
                10F);
        _startPurposeStatus.MaximumSize =
            new Size(
                1050,
                0);
        _startPurposeStatus.Margin =
            new Padding(
                0,
                0,
                0,
                10);
        UiStyle.SetStatus(
            _startPurposeStatus,
            "Ready — choose the source, connect if required, and search.",
            UiStatusKind.Neutral);
        root.Controls.Add(_startPurposeStatus, 0, 1);

        var sourceFlow =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                WrapContents =
                    true,
                FlowDirection =
                    FlowDirection.LeftToRight,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };

        sourceFlow.Controls.Add(
            new Label
            {
                Text =
                    "Source:",
                AutoSize =
                    true,
                Margin =
                    new Padding(
                        0,
                        8,
                        UiStyle.ControlGap,
                        0)
            });

        _recoverySource.Name =
            "RecoverySourceSelector";
        _recoverySource.AccessibleName =
            "Recovery source";
        _recoverySource.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _recoverySource.MinimumSize =
            new Size(
                180,
                0);
        _recoverySource.Items.AddRange([
            "Live AD",
            "Local cache",
            "Protected cache"
        ]);
        sourceFlow.Controls.Add(
            _recoverySource);

        var startConnection =
            UiStyle.CreateActionButton(
                "Connection on Start");
        startConnection.Name =
            "RecoveryOpenStartButton";
        sourceFlow.Controls.Add(
            startConnection);

        root.Controls.Add(
            sourceFlow,
            0,
            2);

        startConnection.Click +=
            (_, _) =>
            {
                if (_homeWorkspaceTab is not null)
                {
                    _mainTabs.SelectedTab =
                        _homeWorkspaceTab;
                }
            };

        var scopeFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };
        _startSelectOu.Text =
            "Select / Change OU...";
        UiStyle.ConfigureActionButton(
            _startSelectOu);
        scopeFlow.Controls.Add(_startSelectOu);

        var recoveryAccessHelp =
            UiStyle.CreateActionButton(
                "AD access help...");
        scopeFlow.Controls.Add(
            recoveryAccessHelp);
        recoveryAccessHelp.Click +=
            (_, _) =>
                ShowAdAccessTroubleshooting(
                    "BitLocker recovery in Active Directory");

        UiStyle.ConfigureInlineStatusLabel(
            _startOuStatus);
        _startOuStatus.AccessibleName =
            "Recovery OU status";
        _startOuStatus.MaximumSize =
            new Size(
                850,
                0);
        _startOuStatus.Margin =
            new Padding(
                10,
                8,
                0,
                0);
        scopeFlow.Controls.Add(_startOuStatus);
        root.Controls.Add(scopeFlow, 0, 3);

        var searchGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 10)
        };
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        searchGrid.Controls.Add(new Label
        {
            Text = "Computer / Recovery ID:",
            AutoSize = true,
            Font = UiStyle.CreateEmphasisFont(9.5F),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 10, 0)
        }, 0, 0);

        _startQuery.Name = "BitLockerSearchQuery";
        _startQuery.Dock = DockStyle.Fill;
        _startQuery.Font = UiStyle.CreateBodyFont(11F);
        _startQuery.PlaceholderText = "PC-12345 or Recovery ID";
        searchGrid.Controls.Add(_startQuery, 1, 0);

        _startSearch.Text =
            "Search BitLocker";
        UiStyle.ConfigureActionButton(
            _startSearch);
        _startSearch.Font =
            UiStyle.CreateEmphasisFont(
                9.5F);
        searchGrid.Controls.Add(_startSearch, 2, 0);

        _startCancel.Text =
            "Cancel";
        UiStyle.ConfigureActionButton(
            _startCancel);
        _startCancel.Enabled =
            false;
        searchGrid.Controls.Add(
            _startCancel,
            3,
            0);

        _startProgress.Name =
            "RecoverySearchProgress";
        _startProgress.AccessibleName =
            "BitLocker recovery search progress";
        _startProgress.Dock =
            DockStyle.Top;
        _startProgress.Style =
            ProgressBarStyle.Marquee;
        _startProgress.MarqueeAnimationSpeed =
            25;
        _startProgress.Visible =
            false;
        _startProgress.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        searchGrid.Controls.Add(
            _startProgress,
            0,
            1);
        searchGrid.SetColumnSpan(
            _startProgress,
            4);

        searchGrid.Controls.Add(
            _startDiagnostics,
            0,
            2);
        searchGrid.SetColumnSpan(
            _startDiagnostics,
            4);

        root.Controls.Add(searchGrid, 0, 4);

        // Place the actionable zero-result/read-permission warning BEFORE the
        // scrolling results grid so that it is not hidden below the fold.
        _bitLockerZeroWarningPanel.Name =
            "BitLockerZeroRecordWarningPanel";
        _bitLockerZeroWarningPanel.Dock = DockStyle.Top;
        _bitLockerZeroWarningPanel.AutoSize = true;
        _bitLockerZeroWarningPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _bitLockerZeroWarningPanel.ColumnCount = 1;
        _bitLockerZeroWarningPanel.RowCount = 2;
        _bitLockerZeroWarningPanel.Margin =
            new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap);
        _bitLockerZeroWarningPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));
        _bitLockerZeroWarningPanel.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));
        _bitLockerZeroWarningPanel.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));
        _bitLockerZeroWarning.Name = "BitLockerZeroRecordWarning";
        _bitLockerZeroWarning.AccessibleName =
            "BitLocker recovery diagnostics warning";
        UiStyle.ConfigureStatusLabel(_bitLockerZeroWarning);
        UiStyle.SetStatus(
            _bitLockerZeroWarning,
            "No BitLocker recovery metadata was returned. The backup, search scope and permissions have not been independently verified.",
            UiStatusKind.Warning);
        _bitLockerZeroWarningPanel.Controls.Add(_bitLockerZeroWarning, 0, 0);

        _bitLockerDiagnosticCommands.Name =
            "BitLockerDiagnosticCommandsButton";
        _bitLockerDiagnosticCommands.Text =
            "Diagnostic steps and commands...";
        UiStyle.ConfigureActionButton(_bitLockerDiagnosticCommands);
        _bitLockerDiagnosticCommands.Anchor = AnchorStyles.Left;
        _bitLockerDiagnosticCommands.Margin =
            new Padding(0, UiStyle.ControlGap, 0, 0);
        _bitLockerDiagnosticCommands.Click +=
            (_, _) => ShowBitLockerDiagnosticCommands();
        _bitLockerZeroWarningPanel.Controls.Add(
            _bitLockerDiagnosticCommands, 0, 1);
        _bitLockerZeroWarningPanel.Visible = false;
        root.Controls.Add(_bitLockerZeroWarningPanel, 0, 5);

        _startResults.Name =
            "BitLockerSearchResults";
        _startResults.AccessibleName =
            "BitLocker recovery search results";
        _startResults.Dock =
            DockStyle.Fill;
        _startResults.MinimumSize =
            new Size(0, 180);
        UiStyle.ConfigureDataGridView(
            _startResults);
        _startResults.Columns.AddRange([
            UiStyle.CreateSortableTextColumn(
                "Computer",
                "Computer",
                150),
            UiStyle.CreateSortableTextColumn(
                "RecoveryId",
                "Recovery ID",
                230),
            UiStyle.CreateSortableTextColumn(
                "KeyDate",
                "Key date",
                140),
            UiStyle.CreateSortableTextColumn(
                "Latest",
                "Latest",
                70),
            UiStyle.CreateSortableTextColumn(
                "Source",
                "Source",
                90)
        ]);
        _startResults.Columns["KeyDate"]!
            .DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        root.Controls.Add(
            _startResults,
            0,
            6);

        ConfigureRecoveryCard();
        root.Controls.Add(_recoveryCard, 0, 7);

        _connectAdButton.Click +=
            async (_, _) =>
                await TestDirectoryConnectionAsync(
                    promptForOu: true,
                    promptForSessionCredentials: true,
                    forceManualCredentials: true);

        _connectAdCancelButton.Click +=
            (_, _) => CancelDirectoryConnection();

        _disconnectAdButton.Click +=
            (_, _) => DisconnectDirectorySession();

        _startSelectOu.Click +=
            async (_, _) =>
                await SelectStartOuAsync();

        _startSearch.Click +=
            async (_, _) =>
                await SearchStartRecoveryAsync();

        _startCancel.Click +=
            (_, _) =>
                CancelStartRecoverySearch();

        _startQuery.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter &&
                    _startSearch.Enabled)
                {
                    e.SuppressKeyPress =
                        true;
                    _startSearchDebounceTimer?.Stop();
                    await SearchStartRecoveryAsync();
                }
            };

        _startQuery.TextChanged +=
            (_, _) =>
            {
                ClearBitLockerWarning();
                if (_applyingComputerQuery)
                    return;

                RememberComputerQuery(_startQuery.Text);
                if (_layoutSelfTest)
                    return;

                QueueStartRecoveryLiveSearch();
            };

        _startSearchDebounceTimer ??=
            new System.Windows.Forms.Timer
            {
                Interval =
                    SearchText.DebounceMilliseconds
            };
        _startSearchDebounceTimer.Tick +=
            async (_, _) =>
            {
                _startSearchDebounceTimer.Stop();

                if (_startQuery.Text.Trim().Length >=
                    SearchText.MinimumLiveSearchCharacters)
                {
                    await SearchStartRecoveryAsync();
                }
            };

        _startResults.SelectionChanged +=
            (_, _) =>
                SelectStartRecoveryRecord();

        _startResults.KeyDown +=
            (_, e) =>
            {
                if (e.KeyCode !=
                        Keys.Enter ||
                    _startResults.SelectedRows.Count ==
                        0)
                {
                    return;
                }

                e.SuppressKeyPress =
                    true;
                SelectStartRecoveryRecord();
            };

        _startShow.Click +=
            async (_, _) =>
                await RevealStartRecoveryKeyAsync();

        _startCopy.Click +=
            async (_, _) =>
                await CopyStartRecoveryKeyAsync();

        _recoverySource.SelectedIndexChanged +=
            (_, _) =>
            {
                ClearBitLockerWarning();
                ResetRecoverySearchState();
                UpdateRecoverySourceUi();
                TrySaveRecoveryUiState();
            };

        return tab;
    }

    private void ConfigureRecoveryCard()
    {
        _recoveryCard.Text = "Selected recovery record";
        _recoveryCard.Dock = DockStyle.Top;
        _recoveryCard.AutoSize = true;
        _recoveryCard.Padding = new Padding(12);
        _recoveryCard.Margin = new Padding(0, 10, 0, 10);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 7
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        AddRecoveryCardRow(grid, 0, "Computer:", _recoveryCardComputer);
        AddRecoveryCardRow(grid, 1, "OU / scope:", _recoveryCardOu);
        AddRecoveryCardRow(grid, 2, "Recovery ID:", _recoveryCardId);
        AddRecoveryCardRow(grid, 3, "Created / checked:", _recoveryCardTime);
        AddRecoveryCardRow(grid, 4, "Source:", _recoveryCardSource);

        var keyLayout =
            new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
        keyLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        keyLayout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        keyLayout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        _startKey.Name = "BitLockerRecoverySecretField";
        _startKey.Dock =
            DockStyle.Top;
        _startKey.ReadOnly = true;
        _startKey.UseSystemPasswordChar = true;
        _startKey.Font =
            UiStyle.CreateMonospaceFont(
                10F);
        _startKey.Margin =
            new Padding(
                0,
                0,
                0,
                UiStyle.ControlGap);
        keyLayout.Controls.Add(
            _startKey,
            0,
            0);

        var keyActions =
            new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                FlowDirection =
                    FlowDirection.LeftToRight,
                Margin =
                    new Padding(0)
            };

        _startShow.Text =
            "Reveal Recovery Key";
        UiStyle.ConfigureActionButton(
            _startShow);
        keyActions.Controls.Add(
            _startShow);

        _startCopy.Text =
            "Copy Key";
        UiStyle.ConfigureActionButton(
            _startCopy);
        keyActions.Controls.Add(
            _startCopy);

        keyLayout.Controls.Add(
            keyActions,
            0,
            1);

        grid.Controls.Add(new Label
        {
            Text = "Recovery key:",
            AutoSize = true,
            Font =
                UiStyle.CreateEmphasisFont(),
            Margin = new Padding(0, 8, 10, 0)
        }, 0, 5);
        grid.Controls.Add(
            keyLayout,
            1,
            5);

        grid.Controls.Add(new Label
        {
            Text =
                "The secret is not loaded by Search. Reveal/Copy performs an audited, policy-checked on-demand read.",
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0, 6, 0, 0)
        }, 1, 6);

        _recoveryCard.Controls.Add(grid);
    }

    private static void AddRecoveryCardRow(
        TableLayoutPanel grid,
        int row,
        string caption,
        Label value)
    {
        grid.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Font = UiStyle.CreateEmphasisFont(9F),
            Margin = new Padding(0, 3, 10, 3)
        }, 0, row);

        value.AutoSize = true;
        value.MaximumSize = new Size(900, 0);
        value.Margin = new Padding(0, 3, 0, 3);
        grid.Controls.Add(value, 1, row);
    }

    private void ConfigureAdvancedConnectionGroup()
    {
        _advancedConnectionGroup.Name =
            "AdvancedConnectionGroup";
        _advancedConnectionGroup.Text = "Advanced connection settings";
        _advancedConnectionGroup.Dock = DockStyle.Top;
        _advancedConnectionGroup.AutoSize = true;
        _advancedConnectionGroup.Visible = false;
        _advancedConnectionGroup.Padding = new Padding(12);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 7
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        _adMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _adMode.Items.AddRange([
            "Auto - domain workstation / DC",
            "Explicit DC - standalone / workstation"
        ]);

        _adCredentialStorage.DropDownStyle = ComboBoxStyle.DropDownList;
        _adCredentialStorage.Items.AddRange([
            "Session only",
            "Current User - Credential Manager",
            "Machine / Service - DPAPI"
        ]);

        _adPassword.UseSystemPasswordChar = true;
        _adPort.Minimum = 1;
        _adPort.Maximum = 65535;

        AddAdvancedField(grid, 0, 0, "Mode:", _adMode);
        AddAdvancedField(grid, 0, 2, "DC / FQDN:", _adServer);
        AddAdvancedField(grid, 1, 0, "Domain:", _adDomain);
        AddAdvancedField(grid, 1, 2, "LDAP port:", _adPort);
        AddAdvancedField(grid, 2, 0, "AD user:", _adUsername);
        AddAdvancedField(grid, 2, 2, "Password:", _adPassword);
        AddAdvancedField(grid, 3, 0, "Credential storage:", _adCredentialStorage);

        _adUseLdaps.Text = "Use LDAPS / TLS";
        _adUseLdaps.AutoSize = true;
        grid.Controls.Add(_adUseLdaps, 2, 3);

        _adExplicitCredentials.Text = "Use explicit AD credentials";
        _adExplicitCredentials.AutoSize = true;
        grid.Controls.Add(_adExplicitCredentials, 3, 3);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };

        var save =
            UiStyle.CreateActionButton(
                "Save settings");
        var saveCredential =
            UiStyle.CreateActionButton(
                "Save credential");
        var deleteCredential =
            UiStyle.CreateActionButton(
                "Delete stored credential");
        var clearPassword =
            UiStyle.CreateActionButton(
                "Clear session password");
        actions.Controls.AddRange([
            save,
            saveCredential,
            deleteCredential,
            clearPassword
        ]);

        grid.Controls.Add(actions, 1, 5);
        grid.SetColumnSpan(actions, 3);

        _credentialVaultStatus.Name =
            "AdCredentialStatus";
        UiStyle.ConfigureStatusLabel(
            _credentialVaultStatus);
        _credentialVaultStatus.AccessibleName =
            "Active Directory credential status";
        _credentialVaultStatus.MaximumSize =
            new Size(900, 0);
        grid.Controls.Add(_credentialVaultStatus, 1, 6);
        grid.SetColumnSpan(_credentialVaultStatus, 3);

        _advancedConnectionGroup.Controls.Add(grid);

        _adMode.SelectedIndexChanged +=
            (_, _) => UpdateDirectoryConnectionUi();
        _adExplicitCredentials.CheckedChanged +=
            (_, _) =>
            {
                _homeUseOtherAccount.Checked =
                    _adExplicitCredentials.Checked;
                _homeUseWindowsIdentity.Checked =
                    !_adExplicitCredentials.Checked;
                UpdateDirectoryConnectionUi();
            };
        _adCredentialStorage.SelectedIndexChanged +=
            (_, _) =>
            {
                UpdateDirectoryConnectionUi();
                if (!_layoutSelfTest)
                    RefreshCredentialVaultStatus();
            };
        _adUseLdaps.CheckedChanged +=
            (_, _) =>
            {
                if (_adUseLdaps.Checked && _adPort.Value == 389)
                    _adPort.Value = 636;
                else if (!_adUseLdaps.Checked && _adPort.Value == 636)
                    _adPort.Value = 389;
            };

        save.Click +=
            (_, _) => SaveDirectorySettings(showConfirmation: true);
        saveCredential.Click +=
            (_, _) => SaveSelectedCredential();
        deleteCredential.Click +=
            (_, _) => DeleteSelectedCredential();
        clearPassword.Click +=
            (_, _) =>
            {
                _adPassword.Clear();
                AdSessionCredentials.Clear();
                if (!_layoutSelfTest)
                    RefreshCredentialVaultStatus();
            };
    }

    private static void AddAdvancedField(
        TableLayoutPanel grid,
        int row,
        int column,
        string caption,
        Control control)
    {
        grid.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 8, 3)
        }, column, row);

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 14, 3);
        grid.Controls.Add(control, column + 1, row);
    }

    private TabPage BuildDevicesWorkspaceTab()
    {
        var tab = new TabPage("Devices") { Name = "DevicesWorkspace" };
        var nested = new TabControl
        {
            Dock = DockStyle.Fill
        };
        // Selection can activate a page created at WinForms' 200x100
        // placeholder size. Coalesce its reflow with ordinary WM_SIZE.
        nested.SelectedIndexChanged += (_, _) => QueueNavigationViewportReflow();

        nested.TabPages.Add(BuildDeviceSearchPage());

        if (AdministrationAllowed)
        {
            nested.TabPages.Add(BuildCoverageResponsiveTab());
            nested.TabPages.Add(BuildLapsCoverageTab());
        }

        tab.Controls.Add(nested);
        return tab;
    }

    private TabPage BuildDeviceSearchPage()
    {
        var page = new TabPage("Search");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(root);

        var search = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4
        };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        search.Controls.Add(new Label
        {
            Text = "Name / serial / user / device ID:",
            AutoSize = true,
            Margin = new Padding(0, 7, 10, 0)
        }, 0, 0);

        _unifiedQuery.Name = "UnifiedDeviceSearchQuery";
        _unifiedQuery.AccessibleName = "Search device metadata across AD, Entra and Intune";
        _unifiedQuery.Dock = DockStyle.Fill;
        _unifiedQuery.Font = UiStyle.CreateBodyFont(10.5F);
        search.Controls.Add(_unifiedQuery, 1, 0);

        _unifiedSearch.Text =
            "Search AD + Cloud";
        UiStyle.ConfigureActionButton(
            _unifiedSearch);
        search.Controls.Add(
            _unifiedSearch,
            2,
            0);

        _unifiedCancel.Text =
            "Cancel";
        UiStyle.ConfigureActionButton(
            _unifiedCancel);
        _unifiedCancel.Enabled =
            false;
        search.Controls.Add(
            _unifiedCancel,
            3,
            0);
        root.Controls.Add(search, 0, 0);

        UiStyle.ConfigureStatusLabel(
            _unifiedStatus);
        _unifiedStatus.AccessibleName =
            "Unified device search status";
        _unifiedStatus.MaximumSize =
            new Size(1050, 0);
        _unifiedStatus.Text =
            "One search combines AD, Entra BitLocker metadata, and Intune inventory.";
        UiStyle.ApplyStatusLabel(
            _unifiedStatus,
            UiStatusKind.Neutral);
        root.Controls.Add(_unifiedStatus, 0, 1);

        _unifiedProgress.Name =
            "UnifiedSearchProgress";
        _unifiedProgress.AccessibleName =
            "Device search progress";
        _unifiedProgress.Dock =
            DockStyle.Top;
        _unifiedProgress.Style =
            ProgressBarStyle.Marquee;
        _unifiedProgress.MarqueeAnimationSpeed =
            25;
        _unifiedProgress.Visible =
            false;
        root.Controls.Add(
            _unifiedProgress,
            0,
            2);

        root.Controls.Add(
            _unifiedDiagnostics,
            0,
            3);

        _unifiedResults.View = View.Details;
        _unifiedResults.FullRowSelect = true;
        _unifiedResults.GridLines = true;
        _unifiedResults.HideSelection = false;
        _unifiedResults.Dock = DockStyle.Fill;
        AddColumns(
            _unifiedResults,
            ("Computer", 150),
            ("Identity", 145),
            ("AD", 45),
            ("Entra", 50),
            ("Intune", 52),
            ("Serial", 120),
            ("User / UPN", 185),
            ("Model", 135),
            ("OS", 120),
            ("Compliance", 90),
            ("Encrypted", 70),
            ("Last Sync", 140),
            ("Cloud Keys", 80));
        root.Controls.Add(_unifiedResults, 0, 4);

        _unifiedDetails.ReadOnly = true;
        _unifiedDetails.Font = UiStyle.CreateMonospaceFont(9F);
        _unifiedDetails.Dock = DockStyle.Fill;
        root.Controls.Add(_unifiedDetails, 0, 5);

        var keyPanel =
            new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };
        keyPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        keyPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        keyPanel.Controls.Add(new Label
        {
            Text = "Entra Recovery ID:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin =
                new Padding(
                    0,
                    7,
                    UiStyle.ControlGap,
                    4)
        }, 0, 0);

        _deviceRecoveryIds.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _deviceRecoveryIds.Dock =
            DockStyle.Top;
        keyPanel.Controls.Add(
            _deviceRecoveryIds,
            1,
            0);

        keyPanel.Controls.Add(new Label
        {
            Text = "Recovery key:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin =
                new Padding(
                    0,
                    7,
                    UiStyle.ControlGap,
                    4)
        }, 0, 1);

        _deviceRecoveryKey.Name = "EntraBitLockerSecretField";
        _deviceRecoveryKey.Dock =
            DockStyle.Top;
        _deviceRecoveryKey.ReadOnly = true;
        _deviceRecoveryKey.UseSystemPasswordChar = true;
        _deviceRecoveryKey.Font =
            UiStyle.CreateMonospaceFont();
        keyPanel.Controls.Add(
            _deviceRecoveryKey,
            1,
            1);

        var deviceKeyActions =
            new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection =
                    FlowDirection.LeftToRight,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };

        _deviceShowKey.Text =
            "Reveal Key";
        UiStyle.ConfigureActionButton(
            _deviceShowKey);
        deviceKeyActions.Controls.Add(
            _deviceShowKey);

        _deviceCopyKey.Text =
            "Copy Key";
        UiStyle.ConfigureActionButton(
            _deviceCopyKey);
        deviceKeyActions.Controls.Add(
            _deviceCopyKey);

        keyPanel.SetColumnSpan(
            deviceKeyActions,
            2);
        keyPanel.Controls.Add(
            deviceKeyActions,
            0,
            2);

        root.Controls.Add(
            keyPanel,
            0,
            6);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true
        };
        var rotate =
            UiStyle.CreateActionButton(
                "Rotate BitLocker Key in Intune");
        actions.Controls.Add(rotate);
        var copySummary = UiStyle.CreateActionButton(
            "Copy Device 360 Summary");
        copySummary.Name = "CopyDevice360Summary";
        actions.Controls.Add(copySummary);
        root.Controls.Add(actions, 0, 7);

        _unifiedSearch.Click +=
            async (_, _) => await SearchUnifiedDevicesAsync();
        _unifiedCancel.Click +=
            (_, _) => CancelUnifiedDeviceSearch();
        _unifiedQuery.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                    await SearchUnifiedDevicesAsync();
            };

        _unifiedResults.SelectedIndexChanged +=
            (_, _) =>
            {
                ShowUnifiedDeviceDetails();
                PopulateDeviceRecoveryIds();
            };

        _deviceRecoveryIds.SelectedIndexChanged +=
            (_, _) =>
            {
                _deviceCurrentKey = null;
                _deviceRecoveryKey.Clear();
                _deviceRecoveryKey.UseSystemPasswordChar = true;
                _deviceShowKey.Text = "Reveal Key";
            };

        _deviceShowKey.Click +=
            async (_, _) =>
                await RevealSelectedDeviceCloudKeyAsync();

        _deviceCopyKey.Click +=
            async (_, _) =>
                await CopySelectedDeviceCloudKeyAsync();

        rotate.Click +=
            async (_, _) => await RotateSelectedUnifiedDeviceAsync();
        copySummary.Click +=
            (_, _) => CopyUnifiedDeviceSummary();

        return page;
    }

    private TabPage BuildAdministrationWorkspaceTab()
    {
        var tab = new TabPage("Settings") { Name = "SettingsWorkspace" };
        var nested = new TabControl
        {
            Dock = DockStyle.Fill
        };
        // Selection can activate a page created at WinForms' 200x100
        // placeholder size. Coalesce its reflow with ordinary WM_SIZE.
        nested.SelectedIndexChanged += (_, _) => QueueNavigationViewportReflow();

        // Everyday settings are immediately visible; enterprise features live
        // under a single explicit group instead of competing at the top level.
        nested.TabPages.Add(BuildHelpdeskBasicsResponsiveTab());
        nested.TabPages.Add(BuildCloudAdministrationTab());
        nested.TabPages.Add(BuildUpdatesResponsiveTab());

        var enterprise = new TabPage("Enterprise")
        {
            Name = "EnterpriseSettingsTab"
        };
        var enterpriseSections = new TabControl
        {
            Name = "EnterpriseSettingsSections",
            Dock = DockStyle.Fill
        };
        enterpriseSections.SelectedIndexChanged += (_, _) => QueueNavigationViewportReflow();
        enterpriseSections.TabPages.Add(BuildSecuritySettingsResponsiveTab());
        enterpriseSections.TabPages.Add(BuildExportAutomationResponsiveTab());
        enterprise.Controls.Add(enterpriseSections);
        nested.TabPages.Add(enterprise);

        tab.Controls.Add(nested);
        return tab;
    }

    private TabPage BuildHealthAuditWorkspaceTab()
    {
        var tab = new TabPage("Diagnostics") { Name = "DiagnosticsWorkspace" };
        var nested = new TabControl
        {
            Dock = DockStyle.Fill
        };
        // Selection can activate a page created at WinForms' 200x100
        // placeholder size. Coalesce its reflow with ordinary WM_SIZE.
        nested.SelectedIndexChanged += (_, _) => QueueNavigationViewportReflow();

        nested.TabPages.Add(BuildDiagnosticToolsPage());
        nested.TabPages.Add(BuildAccessHealthResponsiveTab());
        if (AdministrationAllowed)
        {
            nested.TabPages.Add(BuildServiceHealthResponsiveTab());
            nested.TabPages.Add(BuildDomainControllersResponsiveTab());
            nested.TabPages.Add(BuildAuditResponsiveTab());
        }

        tab.Controls.Add(nested);
        return tab;
    }

    private TabPage BuildCloudAdministrationTab()
    {
        var page = new TabPage("Cloud");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 11
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Microsoft Graph / Entra / Intune",
            Font = UiStyle.CreateEmphasisFont(16F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);
        root.SetColumnSpan(root.GetControlFromPosition(0, 0)!, 2);

        AddCloudAdminRow(root, 1, "Tenant ID / domain:", _cloudTenant);
        AddCloudAdminRow(root, 2, "Client ID:", _cloudClient);
        AddCloudAdminRow(root, 3, "Certificate thumbprint:", _cloudThumbprint);

        _cloudAuthMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cloudAuthMode.Items.AddRange([
            "Device Code (MFA / Conditional Access)",
            "App registration + certificate"
        ]);
        _cloudAuthMode.Name = "CloudAuthenticationMode";
        AddCloudAdminRow(root, 4, "Authentication:", _cloudAuthMode);

        _cloudLapsPermissions.Text = "Include LAPS read permissions in setup";
        _cloudLapsPermissions.AutoSize = true;
        root.Controls.Add(_cloudLapsPermissions, 1, 5);

        _cloudRotationPermissions.Name = "CloudEnableRotationPermission";
        _cloudRotationPermissions.Text = "Allow Intune BitLocker rotation (write permission, optional)";
        _cloudRotationPermissions.AutoSize = true;
        root.Controls.Add(_cloudRotationPermissions, 1, 6);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        _cloudSaveButton.Text =
            "Save Config";
        UiStyle.ConfigureActionButton(
            _cloudSaveButton);
        _cloudWizardButton.Text =
            "Intune / Entra Setup Wizard...";
        UiStyle.ConfigureActionButton(
            _cloudWizardButton);
        _cloudConnectButton.Text =
            "Connect / Test";
        UiStyle.ConfigureActionButton(
            _cloudConnectButton);
        _cloudConnectCancelButton.Text =
            "Cancel";
        UiStyle.ConfigureActionButton(
            _cloudConnectCancelButton);
        _cloudConnectCancelButton.Enabled =
            false;
        _cloudSetupButton.Text =
            "First-Run / Repair";
        UiStyle.ConfigureActionButton(
            _cloudSetupButton);
        _cloudBootstrapButton.Text =
            "Bootstrap...";
        UiStyle.ConfigureActionButton(
            _cloudBootstrapButton);
        _cloudRolloverButton.Text =
            "Rollover Certificate...";
        UiStyle.ConfigureActionButton(
            _cloudRolloverButton);
        buttons.Controls.AddRange([
            _cloudWizardButton,
            _cloudSaveButton,
            _cloudConnectButton,
            _cloudConnectCancelButton,
            _cloudSetupButton,
            _cloudBootstrapButton,
            _cloudRolloverButton
        ]);

        root.Controls.Add(buttons, 1, 7);

        UiStyle.ConfigureStatusLabel(
            _cloudStatus);
        _cloudStatus.AccessibleName =
            "Microsoft Graph status";
        _cloudStatus.MaximumSize =
            new Size(900, 0);
        root.Controls.Add(_cloudStatus, 1, 8);

        _cloudProgress.Name =
            "CloudConnectionProgress";
        _cloudProgress.AccessibleName =
            "Microsoft Graph connection progress";
        _cloudProgress.Dock =
            DockStyle.Top;
        _cloudProgress.Style =
            ProgressBarStyle.Marquee;
        _cloudProgress.MarqueeAnimationSpeed =
            25;
        _cloudProgress.Visible =
            false;
        _cloudProgress.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        root.Controls.Add(
            _cloudProgress,
            1,
            9);

        root.Controls.Add(
            _cloudDiagnostics,
            1,
            10);

        _cloudWizardButton.Click += async (_, _) => await OpenIntuneSetupWizardAsync();
        _cloudSaveButton.Click += (_, _) => SaveCloudFields();
        _cloudConnectButton.Click += async (_, _) => await ConnectCloudFromUiAsync();
        _cloudConnectCancelButton.Click += (_, _) => CancelCloudConnection();
        _cloudSetupButton.Click += async (_, _) => await RunNativeAutoSetupAsync();
        _cloudBootstrapButton.Click += (_, _) => ConfigureCustomBootstrap();
        _cloudRolloverButton.Click += async (_, _) => await RolloverEntraCertificateGuiAsync();
        _cloudAuthMode.SelectedIndexChanged += (_, _) => UpdateCloudAuthUi();

        return page;
    }

    private static void AddCloudAdminRow(
        TableLayoutPanel grid,
        int row,
        string caption,
        Control control)
    {
        grid.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Margin = new Padding(0, 7, 12, 4)
        }, 0, row);

        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 3, 0, 4);
        grid.Controls.Add(control, 1, row);
    }

    private void RestoreRecoveryUiState()
    {
        var recoverySource = string.IsNullOrWhiteSpace(_startConnectionPreferences.RecoverySearchSource)
            ? _config.RecoverySearchSource
            : _startConnectionPreferences.RecoverySearchSource;
        _recoverySource.SelectedIndex =
            recoverySource.Equals("ProtectedCache", StringComparison.OrdinalIgnoreCase)
                ? 2
                : recoverySource.Equals("LocalCache", StringComparison.OrdinalIgnoreCase)
                    ? 1 : 0;

        var scopeDn = string.IsNullOrWhiteSpace(_startConnectionPreferences.LastRecoveryScopeSearchBase)
            ? _config.LastRecoveryScopeSearchBase
            : _startConnectionPreferences.LastRecoveryScopeSearchBase;
        var scopeName = string.IsNullOrWhiteSpace(_startConnectionPreferences.LastRecoveryScopeSearchBase)
            ? _config.LastRecoveryScopeName
            : _startConnectionPreferences.LastRecoveryScopeName;
        if (!string.IsNullOrWhiteSpace(scopeDn))
        {
            _startScope = new BitLockerScope(
                string.IsNullOrWhiteSpace(scopeName) ? "Last OU" : scopeName,
                scopeDn);

            UiStyle.SetStatus(
                _startOuStatus,
                $"Selected: {_startScope.Name}    {_startScope.SearchBase}",
                UiStatusKind.Success);
        }

        UpdateRecoverySourceUi();
    }

    private void UpdateRecoverySourceUi()
    {
        var local =
            _recoverySource.SelectedIndex != 0;
        var selectedCache = _recoverySource.SelectedIndex == 2
            ? ProtectedRecoveryCacheService.DefaultFile : _config.OutputCsv;

        _connectAdButton.Enabled = !local;
        _startSelectOu.Enabled =
            !local &&
            !string.IsNullOrWhiteSpace(
                _startDomainDn);

        var localCacheAvailable =
            File.Exists(selectedCache);

        _startSearch.Enabled =
            local
                ? localCacheAvailable
                : _startScope is not null;

        if (local)
        {
            SetDirectoryConnectionStatus(
                localCacheAvailable
                    ? (_recoverySource.SelectedIndex == 2
                        ? "DPAPI CurrentUser protected cache is available. Recovery keys are decrypted only in memory."
                        : "Local CSV cache is available. AD connection is not required.")
                    : (_recoverySource.SelectedIndex == 2
                        ? "Protected cache does not exist. Open Start > Device Diagnostics to protect an existing CSV."
                        : "Local CSV cache is selected, but the recovery export file is absent."),
                localCacheAvailable ? UiStatusKind.Neutral : UiStatusKind.Warning);

            UiStyle.SetStatus(
                _startOuStatus,
                "OU selection is not used for Local cache search.",
                UiStatusKind.Neutral);
        }
        else if (_startScope is not null)
        {
            UiStyle.SetStatus(
                _startOuStatus,
                $"Selected: {_startScope.Name}    {_startScope.SearchBase}",
                UiStatusKind.Success);
        }
    }

    private void TrySaveRecoveryUiState()
    {
        try
        {
            _startConnectionPreferences.RecoverySearchSource =
                _recoverySource.SelectedIndex == 2 ? "ProtectedCache" :
                _recoverySource.SelectedIndex == 1 ? "LocalCache" : "LiveAD";
            if (_startScope is not null)
            {
                _startConnectionPreferences.LastRecoveryScopeName = _startScope.Name;
                _startConnectionPreferences.LastRecoveryScopeSearchBase = _startScope.SearchBase;
            }
            ConfigService.SaveStartConnectionPreferences(_startConnectionPreferences);
        }
        catch
        {
            // Non-admin helpdesk sessions keep the selection in memory even
            // when machine-wide configuration cannot be persisted.
        }
    }

    private void PopulateDeviceRecoveryIds()
    {
        _deviceRecoveryIds.Items.Clear();
        _deviceCurrentKey = null;
        _deviceRecoveryKey.Clear();

        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not
                UnifiedDeviceInfo row)
        {
            return;
        }

        if (row.IdentityStatus == "Ambiguous device ID")
            return;

        foreach (var id in row.RecoveryIds)
            _deviceRecoveryIds.Items.Add(id);

        // When names collide, force the operator to pick the exact Entra
        // Recovery ID before requesting a secret. Do not auto-select it.
        if (_deviceRecoveryIds.Items.Count > 0 &&
            row.IdentityStatus != "Name collision")
        {
            _deviceRecoveryIds.SelectedIndex = 0;
        }
    }

    private bool TryGetSelectedDeviceRecovery(
        out UnifiedDeviceInfo? row,
        out string recoveryId)
    {
        row = null;
        recoveryId = string.Empty;

        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not
                UnifiedDeviceInfo selected ||
            _deviceRecoveryIds.SelectedItem is not
                string selectedRecoveryId)
        {
            return false;
        }

        if (selected.IdentityStatus == "Ambiguous device ID" ||
            !selected.RecoveryIds.Contains(selectedRecoveryId,
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        row = selected;
        recoveryId = selectedRecoveryId;
        return true;
    }

    private async Task<(string Key, RecoveryAccessContext Context)?>
        EnsureSelectedDeviceCloudKeyAsync(
            string action)
    {
        if (!TryGetSelectedDeviceRecovery(
                out var row,
                out var recoveryId) ||
            row is null)
        {
            UiStyle.SetStatus(
                _unifiedStatus,
                "Select a device and an Entra Recovery ID first.",
                UiStatusKind.Warning);
            return null;
        }

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                action,
                row.ComputerName,
                recoveryId,
                "Entra"))
        {
            return null;
        }

        var context =
            GetOrRequestRecoveryAccessContext(
                "Entra",
                recoveryId,
                row.ComputerName,
                allowRotationReminder: true);

        if (context is null)
            return null;

        if (!AuthorizePrivilegedRecoveryAccess(
                context,
                action,
                row.ComputerName,
                recoveryId,
                "Entra"))
        {
            return null;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(
                    _deviceCurrentKey))
            {
                if (!await EnsureCloudTokenAsync())
                    return null;

                using var graph =
                    new CloudGraphService();

                _deviceCurrentKey =
                    await graph.GetRecoveryKeyValueAsync(
                        _cloudToken!.AccessToken,
                        recoveryId);
            }
            _deviceRecoveryKey.Text =
                _deviceCurrentKey;
            _deviceRecoveryKey.UseSystemPasswordChar =
                true;

            return (
                _deviceCurrentKey!,
                context);
        }
        catch (Exception ex)
        {
            WriteRecoveryAudit(
                action,
                context,
                result: "Failed",
                computerName: row.ComputerName,
                recoveryId: recoveryId,
                source: "Entra",
                authMode: _cloudToken?.AuthMode,
                details: ex.Message);

            _unifiedDiagnostics.ShowError(
                "Entra recovery-key retrieval failed.",
                "GetEntraRecoveryKey",
                ex,
                ("RecoveryId", recoveryId),
                ("Computer", row.ComputerName));

            UiStyle.SetStatus(
                _unifiedStatus,
                "Recovery-key retrieval failed. Review diagnostics above.",
                UiStatusKind.Error);

            return null;
        }
    }

    private async Task RevealSelectedDeviceCloudKeyAsync()
    {
        if (!_deviceRecoveryKey.UseSystemPasswordChar &&
            !string.IsNullOrWhiteSpace(
                _deviceCurrentKey))
        {
            _deviceRecoveryKey.UseSystemPasswordChar =
                true;
            _deviceShowKey.Text =
                "Reveal Key";
            return;
        }

        var access =
            await EnsureSelectedDeviceCloudKeyAsync(
                "RevealUnifiedCloudRecoveryKey");

        if (access is null ||
            !TryGetSelectedDeviceRecovery(
                out var row,
                out var recoveryId) ||
            row is null)
        {
            return;
        }

        _deviceRecoveryKey.Text =
            access.Value.Key;
        _deviceRecoveryKey.UseSystemPasswordChar =
            false;
        _deviceShowKey.Text =
            "Hide Key";

        WriteRecoveryAudit(
            "RevealUnifiedCloudRecoveryKey",
            access.Value.Context,
            computerName: row.ComputerName,
            recoveryId: recoveryId,
            source: "Entra",
            authMode: _cloudToken?.AuthMode);

        StartSecretLifetimeCountdown(
            "BitLocker recovery key",
            _config.SecretDisplaySeconds);

        await MaybeSuggestCloudRotationAfterRecoveryAsync(
            access.Value.Context,
            row,
            recoveryId);
    }

    private async Task CopySelectedDeviceCloudKeyAsync()
    {
        var access =
            await EnsureSelectedDeviceCloudKeyAsync(
                "CopyUnifiedCloudRecoveryKey");

        if (access is null ||
            !TryGetSelectedDeviceRecovery(
                out var row,
                out var recoveryId) ||
            row is null)
        {
            return;
        }

        WriteRecoveryAudit(
            "CopyUnifiedCloudRecoveryKey",
            access.Value.Context,
            computerName: row.ComputerName,
            recoveryId: recoveryId,
            source: "Entra",
            authMode: _cloudToken?.AuthMode);

        CopyKeyWithAutoClear(
            access.Value.Key);

        await MaybeSuggestCloudRotationAfterRecoveryAsync(
            access.Value.Context,
            row,
            recoveryId);
    }

}
