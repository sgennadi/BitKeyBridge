using System.Diagnostics;

namespace BitKeyBridge;

public sealed class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;
    private readonly AuditService _audit = new();
    private readonly CheckedListBox _scopes = new();
    private readonly RichTextBox _exportLog = new();
    private readonly Label _lastSuccess = new();
    private readonly Label _exportSummary = new();
    private readonly ListView _scopeResults = new();
    private readonly CheckBox _forcePublish = new();
    private readonly Button _runExport = new();
    private readonly Button _dryRun = new();

    private readonly ListView _dcResults = new();
    private readonly RichTextBox _dcDetails = new();
    private readonly Button _dcTest = new();

    private readonly TextBox _localQuery = new();
    private readonly ListView _localResults = new();
    private readonly TextBox _localKey = new();
    private readonly Button _localShow = new();
    private string? _localCurrentKey;

    private readonly TextBox _cloudTenant = new();
    private readonly TextBox _cloudClient = new();
    private readonly TextBox _cloudUsername = new();
    private readonly TextBox _cloudPassword = new();
    private readonly TextBox _cloudThumbprint = new();
    private readonly ComboBox _cloudAuthMode = new();
    private readonly TextBox _cloudQuery = new();
    private readonly ListView _cloudResults = new();
    private readonly TextBox _cloudKey = new();
    private readonly Button _cloudShow = new();
    private readonly Label _cloudStatus = new();
    private CloudAuthConfig _cloudConfig;
    private GraphToken? _cloudToken;
    private string? _cloudCurrentKey;

    private readonly TextBox _unifiedQuery = new();
    private readonly ListView _unifiedResults = new();
    private readonly RichTextBox _unifiedDetails = new();
    private readonly Label _unifiedStatus = new();

    private readonly ListView _auditResults = new();

    private readonly ComboBox _adMode = new();
    private readonly TextBox _adServer = new();
    private readonly TextBox _adDomain = new();
    private readonly TextBox _adUsername = new();
    private readonly TextBox _adPassword = new();
    private readonly NumericUpDown _adPort = new();
    private readonly CheckBox _adUseLdaps = new();
    private readonly CheckBox _adExplicitCredentials = new();
    private readonly ComboBox _adCredentialStorage = new();
    private readonly Label _credentialVaultStatus = new();
    private readonly TextBox _outputRoot = new();
    private readonly TextBox _outputSubdirectory = new();
    private readonly Label _adConnectionStatus = new();

    private readonly Label _dashboardStatus = new();
    private readonly RichTextBox _dashboardDetails = new();
    private readonly NumericUpDown _serviceInterval = new();
    private readonly NumericUpDown _healthPort = new();
    private readonly CheckBox _healthEnabled = new();
    private readonly CheckBox _serviceRunOnStart = new();
    private readonly ComboBox _serviceIdentityMode = new();
    private readonly TextBox _serviceIdentityAccount = new();
    private readonly TextBox _serviceIdentityPassword = new();
    private readonly Label _serviceIdentityStatus = new();

    private readonly Label _updateStatus = new();
    private readonly TextBox _updateRepository = new();
    private readonly CheckBox _checkUpdatesOnStart = new();
    private readonly CheckBox _allowPrereleaseUpdates = new();
    private UpdateInfo? _lastUpdateInfo;

    private readonly Label _remoteApiStatus = new();
    private readonly NumericUpDown _remoteApiPort = new();
    private readonly CheckBox _remoteApiManagement = new();

    private readonly CheckBox _requireRecoveryReference = new();
    private readonly CheckBox _suggestRotationAfterRecovery = new();
    private readonly Dictionary<string, RecoveryAccessContext> _recoveryAccessContexts =
        new(StringComparer.OrdinalIgnoreCase);

    public MainForm(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService(config);
        _cloudConfig = ConfigService.LoadCloudConfig();
        try
        {
            if (SecurityContext.IsAdministrator())
                WindowsEventLogService.EnsureSource();
        }
        catch { }
        Text = "BitKeyBridge 0.6.0 (.NET)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1220, 820);
        MinimumSize = new Size(1000, 700);
        Font = new Font("Segoe UI", 9F);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildDirectoryConnectionTab());
        tabs.TabPages.Add(BuildDashboardTab());
        tabs.TabPages.Add(BuildOperationsTab());
        tabs.TabPages.Add(BuildExportTab());
        tabs.TabPages.Add(BuildDcTab());
        tabs.TabPages.Add(BuildLocalSearchTab());
        tabs.TabPages.Add(BuildUnifiedTab());
        tabs.TabPages.Add(BuildCloudTab());
        tabs.TabPages.Add(BuildAuditTab());
        Controls.Add(tabs);

        LoadDirectorySettings();
        LoadDefaultScopes();
        RefreshLastSuccess();
        LoadCloudFields();
        LoadDashboardSettings();
        LoadOperationsSettings();
        RefreshDashboard();
        FormClosing += (_, _) => ClearSensitiveState();
        Shown += async (_, _) =>
        {
            if (_config.CheckForUpdatesOnStart)
                await CheckForUpdatesGuiAsync(silentWhenCurrent: true);
        };
    }

    private TabPage BuildDirectoryConnectionTab()
    {
        var tab = new TabPage("Directory Connection");

        var header = new Label
        {
            Text = "Active Directory / DC Connection",
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Left = 18,
            Top = 16
        };
        tab.Controls.Add(header);

        var adGroup = new GroupBox
        {
            Text = "Active Directory",
            Left = 20,
            Top = 58,
            Width = 1145,
            Height = 365,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        tab.Controls.Add(adGroup);

        AddLabeled(adGroup, "Mode:", _adMode, 16, 30, 145, 330);
        _adMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _adMode.Items.AddRange([
            "Auto - domain workstation / DC",
            "Explicit DC - standalone / workstation"
        ]);

        AddLabeled(adGroup, "DC host / FQDN:", _adServer, 16, 70, 145, 330);
        AddLabeled(adGroup, "Domain:", _adDomain, 16, 110, 145, 330);

        adGroup.Controls.Add(new Label
        {
            Text = "LDAP port:",
            Left = 16,
            Top = 154,
            Width = 145,
            Height = 24
        });
        _adPort.SetBounds(161, 150, 100, 27);
        _adPort.Minimum = 1;
        _adPort.Maximum = 65535;
        adGroup.Controls.Add(_adPort);

        _adUseLdaps.Text = "Use LDAPS / TLS";
        _adUseLdaps.SetBounds(280, 151, 160, 25);
        adGroup.Controls.Add(_adUseLdaps);

        _adExplicitCredentials.Text =
            "Use explicit AD credentials (password remains in memory only)";
        _adExplicitCredentials.SetBounds(520, 32, 450, 26);
        adGroup.Controls.Add(_adExplicitCredentials);

        AddLabeled(adGroup, "AD user:", _adUsername, 520, 70, 115, 420);
        AddLabeled(adGroup, "Password:", _adPassword, 520, 110, 115, 420);
        _adPassword.UseSystemPasswordChar = true;

        AddLabeled(adGroup, "Storage:", _adCredentialStorage, 520, 150, 115, 245);
        _adCredentialStorage.DropDownStyle = ComboBoxStyle.DropDownList;
        _adCredentialStorage.Items.AddRange([
            "Session only",
            "Current User - Credential Manager",
            "Machine / Service - DPAPI"
        ]);

        var saveCredential = new Button
        {
            Text = "Save Credential",
            Left = 895,
            Top = 147,
            Width = 110,
            Height = 31
        };
        var deleteCredential = new Button
        {
            Text = "Delete Stored",
            Left = 1015,
            Top = 147,
            Width = 105,
            Height = 31
        };
        adGroup.Controls.AddRange([saveCredential, deleteCredential]);

        _credentialVaultStatus.SetBounds(520, 188, 600, 42);
        _credentialVaultStatus.Text = "Credential storage: session only.";
        adGroup.Controls.Add(_credentialVaultStatus);

        var save = new Button
        {
            Text = "Save Settings",
            Left = 16,
            Top = 245,
            Width = 125,
            Height = 34
        };
        var test = new Button
        {
            Text = "Test DC Connection",
            Left = 151,
            Top = 245,
            Width = 155,
            Height = 34
        };
        var clearPassword = new Button
        {
            Text = "Clear Session Password",
            Left = 316,
            Top = 245,
            Width = 175,
            Height = 34
        };
        adGroup.Controls.AddRange([save, test, clearPassword]);

        _adConnectionStatus.SetBounds(16, 295, 1090, 52);
        _adConnectionStatus.Text = "Connection has not been tested in this GUI session.";
        adGroup.Controls.Add(_adConnectionStatus);

        _adMode.SelectedIndexChanged += (_, _) => UpdateDirectoryConnectionUi();
        _adExplicitCredentials.CheckedChanged += (_, _) => UpdateDirectoryConnectionUi();
        _adCredentialStorage.SelectedIndexChanged += (_, _) =>
        {
            UpdateDirectoryConnectionUi();
            RefreshCredentialVaultStatus();
        };
        _adUseLdaps.CheckedChanged += (_, _) =>
        {
            if (_adUseLdaps.Checked && _adPort.Value == 389)
                _adPort.Value = 636;
            else if (!_adUseLdaps.Checked && _adPort.Value == 636)
                _adPort.Value = 389;
        };
        save.Click += (_, _) => SaveDirectorySettings(showConfirmation: true);
        saveCredential.Click += (_, _) => SaveSelectedCredential();
        deleteCredential.Click += (_, _) => DeleteSelectedCredential();
        test.Click += async (_, _) => await TestDirectoryConnectionAsync();
        clearPassword.Click += (_, _) =>
        {
            _adPassword.Clear();
            AdSessionCredentials.Clear();
            _adConnectionStatus.Text = "Session AD password cleared.";
        };

        var outputGroup = new GroupBox
        {
            Text = "Recovery Export Output",
            Left = 20,
            Top = 440,
            Width = 1145,
            Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        tab.Controls.Add(outputGroup);

        AddLabeled(outputGroup, "Output root:", _outputRoot, 16, 34, 145, 760);
        var browse = new Button
        {
            Text = "Browse...",
            Left = 935,
            Top = 31,
            Width = 105,
            Height = 31
        };
        outputGroup.Controls.Add(browse);

        AddLabeled(
            outputGroup,
            "Subdirectory:",
            _outputSubdirectory,
            16,
            76,
            145,
            330);

        outputGroup.Controls.Add(new Label
        {
            Text =
                "Local and UNC paths are supported. If OutputRoot is left on the legacy SYSVOL path, existing DC/WinPE deployments continue to work.",
            Left = 16,
            Top = 118,
            Width = 1085,
            Height = 42
        });

        var saveOutput = new Button
        {
            Text = "Save Output Settings",
            Left = 16,
            Top = 168,
            Width = 160,
            Height = 32
        };
        outputGroup.Controls.Add(saveOutput);

        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog
            {
                Description = "Select BitKeyBridge output root",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(_outputRoot.Text)
                    ? _outputRoot.Text
                    : string.Empty
            };

            if (picker.ShowDialog(this) == DialogResult.OK)
                _outputRoot.Text = picker.SelectedPath;
        };
        saveOutput.Click += (_, _) => SaveDirectorySettings(showConfirmation: true);

        tab.Controls.Add(new Label
        {
            Text =
                "Microsoft 365 / Entra / Intune does not require Windows domain membership. Configure cloud authentication in the 'Entra / Intune Cloud' tab.",
            Left = 20,
            Top = 690,
            Width = 1145,
            Height = 42
        });

        return tab;
    }

    private TabPage BuildDashboardTab()
    {
        var tab = new TabPage("Dashboard");

        var header = new Label
        {
            Text = "BitKeyBridge Health & Service",
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Left = 18,
            Top = 16
        };
        tab.Controls.Add(header);

        _dashboardStatus.SetBounds(20, 54, 1145, 30);
        _dashboardStatus.Font = new Font("Segoe UI Semibold", 11F);
        _dashboardStatus.Text = "Loading health status...";
        tab.Controls.Add(_dashboardStatus);

        var serviceGroup = new GroupBox
        {
            Text = "Native Windows Service",
            Left = 20,
            Top = 90,
            Width = 1145,
            Height = 225,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        tab.Controls.Add(serviceGroup);

        var install = new Button { Text = "Install / Update", Left = 16, Top = 28, Width = 125, Height = 32 };
        var start = new Button { Text = "Start", Left = 151, Top = 28, Width = 80, Height = 32 };
        var stop = new Button { Text = "Stop", Left = 241, Top = 28, Width = 80, Height = 32 };
        var uninstall = new Button { Text = "Uninstall", Left = 331, Top = 28, Width = 95, Height = 32 };
        var refresh = new Button { Text = "Refresh", Left = 436, Top = 28, Width = 90, Height = 32 };
        var openHealth = new Button { Text = "Open /health", Left = 536, Top = 28, Width = 110, Height = 32 };
        var secureOutput = new Button { Text = "Secure Output...", Left = 656, Top = 28, Width = 125, Height = 32 };
        serviceGroup.Controls.AddRange([install, start, stop, uninstall, refresh, openHealth, secureOutput]);

        serviceGroup.Controls.Add(new Label { Text = "Export interval (minutes):", Left = 16, Top = 86, Width = 145, Height = 24 });
        _serviceInterval.SetBounds(165, 82, 90, 27);
        _serviceInterval.Minimum = 1;
        _serviceInterval.Maximum = 10080;
        serviceGroup.Controls.Add(_serviceInterval);

        _healthEnabled.Text = "Enable loopback health endpoint";
        _healthEnabled.SetBounds(285, 84, 220, 25);
        serviceGroup.Controls.Add(_healthEnabled);

        serviceGroup.Controls.Add(new Label { Text = "Port:", Left = 515, Top = 86, Width = 38, Height = 24 });
        _healthPort.SetBounds(555, 82, 90, 27);
        _healthPort.Minimum = 1024;
        _healthPort.Maximum = 65535;
        serviceGroup.Controls.Add(_healthPort);

        _serviceRunOnStart.Text = "Run export when service starts";
        _serviceRunOnStart.SetBounds(675, 84, 220, 25);
        serviceGroup.Controls.Add(_serviceRunOnStart);

        var saveSettings = new Button { Text = "Save Service Settings", Left = 925, Top = 79, Width = 190, Height = 32 };
        serviceGroup.Controls.Add(saveSettings);

        serviceGroup.Controls.Add(new Label
        {
            Text = "Service identity:",
            Left = 16,
            Top = 137,
            Width = 95,
            Height = 24
        });
        _serviceIdentityMode.SetBounds(115, 132, 215, 27);
        _serviceIdentityMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _serviceIdentityMode.Items.AddRange([
            "LocalSystem",
            "gMSA / Managed Account",
            "Domain Account"
        ]);
        serviceGroup.Controls.Add(_serviceIdentityMode);

        serviceGroup.Controls.Add(new Label
        {
            Text = "Account:",
            Left = 350,
            Top = 137,
            Width = 60,
            Height = 24
        });
        _serviceIdentityAccount.SetBounds(412, 132, 255, 27);
        serviceGroup.Controls.Add(_serviceIdentityAccount);

        serviceGroup.Controls.Add(new Label
        {
            Text = "Password:",
            Left = 682,
            Top = 137,
            Width = 65,
            Height = 24
        });
        _serviceIdentityPassword.SetBounds(750, 132, 205, 27);
        _serviceIdentityPassword.UseSystemPasswordChar = true;
        serviceGroup.Controls.Add(_serviceIdentityPassword);

        var applyIdentity = new Button
        {
            Text = "Apply Identity",
            Left = 970,
            Top = 129,
            Width = 145,
            Height = 34
        };
        serviceGroup.Controls.Add(applyIdentity);

        _serviceIdentityStatus.SetBounds(16, 172, 1095, 42);
        _serviceIdentityStatus.Text = "Service identity has not been queried.";
        serviceGroup.Controls.Add(_serviceIdentityStatus);

        _dashboardDetails.ReadOnly = true;
        _dashboardDetails.Font = new Font("Consolas", 9.5F);
        _dashboardDetails.SetBounds(20, 330, 1145, 410);
        _dashboardDetails.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tab.Controls.Add(_dashboardDetails);

        refresh.Click += (_, _) => RefreshDashboard();
        install.Click += (_, _) => InstallOrUpdateService();
        start.Click += (_, _) => StartServiceFromGui();
        stop.Click += (_, _) => StopServiceFromGui();
        uninstall.Click += (_, _) => UninstallServiceFromGui();
        openHealth.Click += (_, _) => OpenHealthEndpoint();
        secureOutput.Click += (_, _) => RunSecureOutputWizard();
        saveSettings.Click += (_, _) => SaveDashboardSettings(restartRunningService: true);
        applyIdentity.Click += (_, _) => ApplyServiceIdentityFromGui();
        _serviceIdentityMode.SelectedIndexChanged += (_, _) => UpdateServiceIdentityUi();

        return tab;
    }

    private TabPage BuildOperationsTab()
    {
        var tab = new TabPage("Operations");

        var updateGroup = new GroupBox
        {
            Text = "Verified Updates",
            Left = 20,
            Top = 20,
            Width = 1145,
            Height = 250,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        tab.Controls.Add(updateGroup);

        updateGroup.Controls.Add(new Label
        {
            Text = "GitHub repository:",
            Left = 16,
            Top = 34,
            Width = 115,
            Height = 24
        });
        _updateRepository.SetBounds(135, 30, 350, 27);
        updateGroup.Controls.Add(_updateRepository);

        _checkUpdatesOnStart.Text = "Check for updates when GUI starts";
        _checkUpdatesOnStart.SetBounds(510, 31, 230, 25);
        updateGroup.Controls.Add(_checkUpdatesOnStart);

        _allowPrereleaseUpdates.Text = "Allow prerelease versions";
        _allowPrereleaseUpdates.SetBounds(760, 31, 190, 25);
        updateGroup.Controls.Add(_allowPrereleaseUpdates);

        var saveUpdateSettings = new Button
        {
            Text = "Save Settings",
            Left = 965,
            Top = 27,
            Width = 150,
            Height = 32
        };
        updateGroup.Controls.Add(saveUpdateSettings);

        var check = new Button { Text = "Check Now", Left = 16, Top = 78, Width = 110, Height = 34 };
        var install = new Button { Text = "Install Verified Update", Left = 136, Top = 78, Width = 170, Height = 34 };
        var openRelease = new Button { Text = "Open Release", Left = 316, Top = 78, Width = 120, Height = 34 };
        updateGroup.Controls.AddRange([check, install, openRelease]);

        _updateStatus.SetBounds(16, 128, 1095, 95);
        _updateStatus.Text = "Update status has not been checked in this GUI session.";
        updateGroup.Controls.Add(_updateStatus);

        saveUpdateSettings.Click += (_, _) => SaveOperationsSettings();
        check.Click += async (_, _) => await CheckForUpdatesGuiAsync(silentWhenCurrent: false);
        install.Click += async (_, _) => await InstallLatestUpdateGuiAsync();
        openRelease.Click += (_, _) => OpenLatestRelease();

        var remoteGroup = new GroupBox
        {
            Text = "Remote Monitoring / Management API (opt-in, TLS + bearer token)",
            Left = 20,
            Top = 290,
            Width = 1145,
            Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        tab.Controls.Add(remoteGroup);

        remoteGroup.Controls.Add(new Label
        {
            Text = "TCP port:",
            Left = 16,
            Top = 34,
            Width = 70,
            Height = 24
        });
        _remoteApiPort.SetBounds(90, 30, 100, 27);
        _remoteApiPort.Minimum = 1024;
        _remoteApiPort.Maximum = 65535;
        remoteGroup.Controls.Add(_remoteApiPort);

        _remoteApiManagement.Text = "Allow remote POST /api/v1/export";
        _remoteApiManagement.SetBounds(220, 31, 255, 25);
        remoteGroup.Controls.Add(_remoteApiManagement);

        var enableRemote = new Button { Text = "Enable / Reconfigure", Left = 500, Top = 27, Width = 150, Height = 32 };
        var rotateToken = new Button { Text = "Rotate Token", Left = 660, Top = 27, Width = 120, Height = 32 };
        var disableRemote = new Button { Text = "Disable", Left = 790, Top = 27, Width = 100, Height = 32 };
        var openEvents = new Button { Text = "Open Event Log", Left = 900, Top = 27, Width = 130, Height = 32 };
        remoteGroup.Controls.AddRange([enableRemote, rotateToken, disableRemote, openEvents]);

        _remoteApiStatus.SetBounds(16, 78, 1095, 118);
        _remoteApiStatus.Text =
            "Remote API is disabled by default. When enabled it requires TLS and bearer-token authentication.";
        remoteGroup.Controls.Add(_remoteApiStatus);

        enableRemote.Click += (_, _) => EnableOrReconfigureRemoteApi();
        rotateToken.Click += (_, _) => RotateRemoteApiToken();
        disableRemote.Click += (_, _) => DisableRemoteApi();
        openEvents.Click += (_, _) => OpenWindowsEventLog();

        var helpdeskGroup = new GroupBox
        {
            Text = "Helpdesk Recovery Workflow",
            Left = 20,
            Top = 530,
            Width = 1145,
            Height = 135,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        tab.Controls.Add(helpdeskGroup);

        _requireRecoveryReference.Text =
            "Require a ticket/reference before recovery-key access";
        _requireRecoveryReference.SetBounds(16, 30, 390, 26);
        helpdeskGroup.Controls.Add(_requireRecoveryReference);

        _suggestRotationAfterRecovery.Text =
            "Suggest Intune key rotation after a cloud recovery password is retrieved";
        _suggestRotationAfterRecovery.SetBounds(16, 64, 510, 26);
        helpdeskGroup.Controls.Add(_suggestRotationAfterRecovery);

        var saveHelpdesk = new Button
        {
            Text = "Save Helpdesk Settings",
            Left = 905,
            Top = 43,
            Width = 205,
            Height = 34
        };
        helpdeskGroup.Controls.Add(saveHelpdesk);
        saveHelpdesk.Click += (_, _) => SaveOperationsSettings();

        var note = new Label
        {
            Text = "Remote API never exposes BitLocker recovery passwords. Helpdesk ticket/reference and reason are audited; the recovery password is never written to the audit.",
            Left = 20,
            Top = 680,
            Width = 1145,
            Height = 44
        };
        tab.Controls.Add(note);

        return tab;
    }

    private TabPage BuildExportTab()
    {
        var tab = new TabPage("Export");
        var header = new Label
        {
            Text = "BitLocker AD Recovery Export",
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Left = 18,
            Top = 16
        };
        tab.Controls.Add(header);

        _lastSuccess.SetBounds(20, 52, 1145, 24);
        tab.Controls.Add(_lastSuccess);

        var scopeGroup = new GroupBox { Text = "Export scopes (no selection = defaults)", Left = 20, Top = 82, Width = 1145, Height = 155, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _scopes.SetBounds(12, 23, 885, 115);
        _scopes.CheckOnClick = true;
        _scopes.HorizontalScrollbar = true;
        scopeGroup.Controls.Add(_scopes);

        var addOu = new Button { Text = "Add OU...", Left = 910, Top = 25, Width = 105, Height = 31 };
        var defaults = new Button { Text = "Use defaults", Left = 1025, Top = 25, Width = 105, Height = 31 };
        var remove = new Button { Text = "Remove", Left = 910, Top = 66, Width = 105, Height = 31 };
        var checkAll = new Button { Text = "Check all", Left = 1025, Top = 66, Width = 105, Height = 31 };
        var saveDefaults = new Button { Text = "Save defaults", Left = 910, Top = 107, Width = 220, Height = 31 };
        scopeGroup.Controls.AddRange([addOu, defaults, remove, checkAll, saveDefaults]);
        tab.Controls.Add(scopeGroup);

        addOu.Click += async (_, _) => await AddOuAsync();
        defaults.Click += (_, _) => LoadDefaultScopes();
        remove.Click += (_, _) => { if (_scopes.SelectedIndex >= 0) _scopes.Items.RemoveAt(_scopes.SelectedIndex); };
        checkAll.Click += (_, _) => { for (var i = 0; i < _scopes.Items.Count; i++) _scopes.SetItemChecked(i, true); };
        saveDefaults.Click += (_, _) => SaveSelectedScopesAsDefaults();

        _runExport.Text = "Run Export";
        _runExport.SetBounds(20, 250, 120, 36);
        _dryRun.Text = "Dry Run";
        _dryRun.SetBounds(150, 250, 105, 36);
        var openCsv = new Button { Text = "Open CSV", Left = 265, Top = 250, Width = 100, Height = 36 };
        var openLog = new Button { Text = "Open Log", Left = 375, Top = 250, Width = 100, Height = 36 };
        var openFolder = new Button { Text = "Open Folder", Left = 485, Top = 250, Width = 110, Height = 36 };
        _forcePublish.Text = "Override publish guards (row drop / scope change)";
        _forcePublish.AutoSize = true;
        _forcePublish.SetBounds(620, 260, 400, 24);
        tab.Controls.AddRange([_runExport, _dryRun, openCsv, openLog, openFolder, _forcePublish]);

        _exportSummary.SetBounds(20, 298, 1145, 42);
        _exportSummary.Text = "No run in this GUI session yet.";
        tab.Controls.Add(_exportSummary);

        _scopeResults.View = View.Details;
        _scopeResults.FullRowSelect = true;
        _scopeResults.GridLines = true;
        _scopeResults.SetBounds(20, 345, 1145, 165);
        _scopeResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_scopeResults, ("Scope", 180), ("Status", 80), ("Objects", 80), ("Valid", 70), ("Duplicates", 85), ("Invalid", 70), ("Error", 520));
        tab.Controls.Add(_scopeResults);

        _exportLog.ReadOnly = true;
        _exportLog.WordWrap = false;
        _exportLog.Font = new Font("Consolas", 9F);
        _exportLog.SetBounds(20, 522, 1145, 220);
        _exportLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tab.Controls.Add(_exportLog);

        _runExport.Click += async (_, _) => await RunExportAsync(false);
        _dryRun.Click += async (_, _) => await RunExportAsync(true);
        openCsv.Click += (_, _) => OpenPath(_config.OutputCsv);
        openLog.Click += (_, _) => OpenPath(_config.ErrorLog, "notepad.exe");
        openFolder.Click += (_, _) => OpenPath(_config.OutputDirectory);
        return tab;
    }

    private TabPage BuildDcTab()
    {
        var tab = new TabPage("DC Comparison");
        _dcTest.Text = "Discover and Test DCs";
        _dcTest.SetBounds(16, 16, 170, 36);
        var hint = new Label
        {
            Text = "Domain controllers are discovered dynamically from Active Directory. Uses the checked OUs from Export; if none are checked, defaults are used.",
            AutoSize = true,
            Left = 205,
            Top = 26
        };
        tab.Controls.AddRange([_dcTest, hint]);

        _dcResults.View = View.Details;
        _dcResults.FullRowSelect = true;
        _dcResults.GridLines = true;
        _dcResults.SetBounds(16, 65, 1155, 315);
        _dcResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_dcResults,
            ("DC", 120), ("Site", 140), ("RODC", 60), ("Reachable", 75), ("Status", 80),
            ("Objects", 75), ("Delta", 60), ("Repl errors", 80), ("Warnings", 75), ("Max age h", 80), ("IPv4", 105), ("OS", 190));
        tab.Controls.Add(_dcResults);

        _dcDetails.ReadOnly = true;
        _dcDetails.Font = new Font("Consolas", 9F);
        _dcDetails.SetBounds(16, 392, 1155, 345);
        _dcDetails.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tab.Controls.Add(_dcDetails);
        _dcTest.Click += async (_, _) => await RunDcTestAsync();
        return tab;
    }

    private TabPage BuildLocalSearchTab()
    {
        var tab = new TabPage("Recovery Search");
        var label = new Label { Text = "Computer name or Recovery ID:", Left = 16, Top = 22, AutoSize = true };
        _localQuery.SetBounds(200, 18, 520, 27);
        var search = new Button { Text = "Search Local CSV", Left = 735, Top = 16, Width = 135, Height = 32 };
        tab.Controls.AddRange([label, _localQuery, search]);

        _localResults.View = View.Details;
        _localResults.FullRowSelect = true;
        _localResults.GridLines = true;
        _localResults.SetBounds(16, 62, 1155, 330);
        _localResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_localResults, ("Computer", 220), ("Recovery ID", 330), ("Last checked", 160), ("Source", 90));
        tab.Controls.Add(_localResults);

        var keyLabel = new Label { Text = "Recovery key:", Left = 16, Top = 415, AutoSize = true };
        _localKey.SetBounds(110, 410, 510, 28);
        _localKey.ReadOnly = true;
        _localKey.UseSystemPasswordChar = true;
        _localShow.Text = "Show Key";
        _localShow.SetBounds(635, 408, 100, 32);
        var copy = new Button { Text = "Copy Key", Left = 745, Top = 408, Width = 100, Height = 32 };
        tab.Controls.AddRange([keyLabel, _localKey, _localShow, copy]);

        var note = new Label
        {
            Text = "The recovery password is hidden until a row is selected. Clipboard is cleared after 60 seconds if it still contains that key.",
            Left = 16,
            Top = 455,
            AutoSize = true
        };
        tab.Controls.Add(note);

        search.Click += (_, _) => SearchLocal();
        _localQuery.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) SearchLocal(); };
        _localResults.SelectedIndexChanged += (_, _) => SelectLocalRecord();
        _localShow.Click += (_, _) =>
        {
            if (!_localKey.UseSystemPasswordChar)
            {
                ToggleKey(_localKey, _localShow);
                return;
            }

            if (_localResults.SelectedItems.Count == 0 ||
                _localResults.SelectedItems[0].Tag is not RecoveryRecord row)
                return;

            var context = GetOrRequestRecoveryAccessContext(
                "AD",
                row.BitLockerId,
                row.ComputerName,
                allowRotationReminder: false);
            if (context is null) return;

            ToggleKey(_localKey, _localShow);
            _audit.Write(
                "RevealLocalRecoveryKey",
                computerName: row.ComputerName,
                recoveryId: row.BitLockerId,
                source: "AD",
                reference: context.Reference,
                reason: context.Reason);
        };
        copy.Click += (_, _) =>
        {
            if (_localResults.SelectedItems.Count == 0 ||
                _localResults.SelectedItems[0].Tag is not RecoveryRecord row)
                return;

            var context = GetOrRequestRecoveryAccessContext(
                "AD",
                row.BitLockerId,
                row.ComputerName,
                allowRotationReminder: false);
            if (context is null) return;

            _audit.Write(
                "CopyLocalRecoveryKey",
                computerName: row.ComputerName,
                recoveryId: row.BitLockerId,
                source: "AD",
                reference: context.Reference,
                reason: context.Reason);
            CopyKeyWithAutoClear(_localCurrentKey);
        };
        return tab;
    }

    private TabPage BuildUnifiedTab()
    {
        var tab = new TabPage("Unified Devices");
        var label = new Label
        {
            Text = "Search name, serial, user/UPN or device ID:",
            Left = 16,
            Top = 23,
            AutoSize = true
        };
        _unifiedQuery.SetBounds(275, 18, 460, 27);
        var search = new Button { Text = "Search AD + Cloud", Left = 750, Top = 16, Width = 145, Height = 32 };
        var rotate = new Button { Text = "Rotate BitLocker Key", Left = 910, Top = 16, Width = 165, Height = 32 };
        tab.Controls.AddRange([label, _unifiedQuery, search, rotate]);

        _unifiedStatus.SetBounds(16, 58, 1155, 24);
        _unifiedStatus.Text = "Search combines on-prem AD computer data with Entra/Intune inventory and BitLocker metadata.";
        tab.Controls.Add(_unifiedStatus);

        _unifiedResults.View = View.Details;
        _unifiedResults.FullRowSelect = true;
        _unifiedResults.GridLines = true;
        _unifiedResults.SetBounds(16, 90, 1155, 390);
        _unifiedResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_unifiedResults,
            ("Computer", 150), ("AD", 45), ("Entra", 50), ("Intune", 52), ("Serial", 120),
            ("User / UPN", 185), ("Model", 135), ("OS", 120), ("Compliance", 90),
            ("Encrypted", 70), ("Last Sync", 140), ("Keys", 45));
        tab.Controls.Add(_unifiedResults);

        _unifiedDetails.ReadOnly = true;
        _unifiedDetails.Font = new Font("Consolas", 9F);
        _unifiedDetails.SetBounds(16, 492, 1155, 245);
        _unifiedDetails.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tab.Controls.Add(_unifiedDetails);

        search.Click += async (_, _) => await SearchUnifiedDevicesAsync();
        _unifiedQuery.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) await SearchUnifiedDevicesAsync(); };
        rotate.Click += async (_, _) => await RotateSelectedUnifiedDeviceAsync();
        _unifiedResults.SelectedIndexChanged += (_, _) => ShowUnifiedDeviceDetails();
        return tab;
    }

    private TabPage BuildCloudTab()
    {
        var tab = new TabPage("Entra / Intune Cloud");
        var authGroup = new GroupBox { Text = "Microsoft Graph authentication", Left = 16, Top = 14, Width = 1155, Height = 205, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        tab.Controls.Add(authGroup);

        AddLabeled(authGroup, "Tenant ID/domain:", _cloudTenant, 14, 28, 135, 510);
        AddLabeled(authGroup, "Client ID:", _cloudClient, 14, 62, 135, 510);
        AddLabeled(authGroup, "Username:", _cloudUsername, 14, 96, 135, 510);
        AddLabeled(authGroup, "Password:", _cloudPassword, 14, 130, 135, 510);
        _cloudPassword.UseSystemPasswordChar = true;
        AddLabeled(authGroup, "Certificate thumbprint:", _cloudThumbprint, 585, 28, 150, 390);
        AddLabeled(authGroup, "Authentication:", _cloudAuthMode, 585, 62, 150, 390);
        _cloudAuthMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cloudAuthMode.Items.AddRange([
            "Device Code (MFA / Conditional Access)",
            "Username + Password (ROPC legacy)",
            "App registration + certificate"
        ]);

        var save = new Button { Text = "Save Config", Left = 585, Top = 103, Width = 115, Height = 32 };
        var connect = new Button { Text = "Connect / Test", Left = 710, Top = 103, Width = 120, Height = 32 };
        var autoSetup = new Button { Text = "First-Run / Repair Setup", Left = 840, Top = 103, Width = 165, Height = 32 };
        var bootstrap = new Button { Text = "Bootstrap...", Left = 1015, Top = 103, Width = 115, Height = 32 };
        authGroup.Controls.AddRange([save, connect, autoSetup, bootstrap]);
        var ropcNote = new Label
        {
            Text = "No pre-created App Registration is required. First-Run Setup uses a Microsoft first-party bootstrap, then creates BitKeyBridge's dedicated Entra app. Device Code supports MFA/Conditional Access.",
            Left = 585,
            Top = 148,
            Width = 545,
            Height = 42
        };
        authGroup.Controls.Add(ropcNote);

        _cloudStatus.SetBounds(16, 228, 1155, 24);
        _cloudStatus.Text = "Not connected.";
        tab.Controls.Add(_cloudStatus);

        var searchLabel = new Label { Text = "Computer name or Recovery ID:", Left = 16, Top = 267, AutoSize = true };
        _cloudQuery.SetBounds(205, 262, 515, 27);
        var search = new Button { Text = "Cloud Search", Left = 735, Top = 260, Width = 115, Height = 32 };
        tab.Controls.AddRange([searchLabel, _cloudQuery, search]);

        _cloudResults.View = View.Details;
        _cloudResults.FullRowSelect = true;
        _cloudResults.GridLines = true;
        _cloudResults.SetBounds(16, 305, 1155, 280);
        _cloudResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_cloudResults, ("Computer", 220), ("Recovery ID", 330), ("Device ID", 290), ("Volume", 100), ("Created", 160));
        tab.Controls.Add(_cloudResults);

        var keyLabel = new Label { Text = "Cloud recovery key:", Left = 16, Top = 607, AutoSize = true };
        _cloudKey.SetBounds(145, 602, 475, 28);
        _cloudKey.ReadOnly = true;
        _cloudKey.UseSystemPasswordChar = true;
        var getKey = new Button { Text = "Get Key from Entra", Left = 635, Top = 600, Width = 135, Height = 32 };
        _cloudShow.Text = "Show Key";
        _cloudShow.SetBounds(780, 600, 95, 32);
        var copy = new Button { Text = "Copy Key", Left = 885, Top = 600, Width = 95, Height = 32 };
        var rotate = new Button { Text = "Rotate Key in Intune", Left = 990, Top = 600, Width = 165, Height = 32 };
        tab.Controls.AddRange([keyLabel, _cloudKey, getKey, _cloudShow, copy, rotate]);

        save.Click += (_, _) => SaveCloudFields();
        connect.Click += async (_, _) => await ConnectCloudAsync();
        search.Click += async (_, _) => await SearchCloudAsync();
        _cloudQuery.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) await SearchCloudAsync(); };
        getKey.Click += async (_, _) => await GetCloudKeyAsync();
        _cloudShow.Click += (_, _) =>
        {
            if (!_cloudKey.UseSystemPasswordChar)
            {
                ToggleKey(_cloudKey, _cloudShow);
                return;
            }

            if (string.IsNullOrWhiteSpace(_cloudCurrentKey) ||
                _cloudResults.SelectedItems.Count == 0 ||
                _cloudResults.SelectedItems[0].Tag is not CloudRecoveryMetadata row)
                return;

            var context = GetOrRequestRecoveryAccessContext(
                "Entra",
                row.RecoveryId,
                row.ComputerName,
                allowRotationReminder: true);
            if (context is null) return;

            ToggleKey(_cloudKey, _cloudShow);
            _audit.Write(
                "RevealCloudRecoveryKey",
                computerName: row.ComputerName,
                recoveryId: row.RecoveryId,
                source: "Entra",
                authMode: _cloudToken?.AuthMode,
                reference: context.Reference,
                reason: context.Reason);
        };
        copy.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_cloudCurrentKey) ||
                _cloudResults.SelectedItems.Count == 0 ||
                _cloudResults.SelectedItems[0].Tag is not CloudRecoveryMetadata row)
                return;

            var context = GetOrRequestRecoveryAccessContext(
                "Entra",
                row.RecoveryId,
                row.ComputerName,
                allowRotationReminder: true);
            if (context is null) return;

            _audit.Write(
                "CopyCloudRecoveryKey",
                computerName: row.ComputerName,
                recoveryId: row.RecoveryId,
                source: "Entra",
                authMode: _cloudToken?.AuthMode,
                reference: context.Reference,
                reason: context.Reason);
            CopyKeyWithAutoClear(_cloudCurrentKey);
        };
        rotate.Click += async (_, _) => await RotateSelectedCloudKeyAsync();
        autoSetup.Click += async (_, _) => await RunNativeAutoSetupAsync();
        bootstrap.Click += (_, _) => ConfigureCustomBootstrap();
        _cloudAuthMode.SelectedIndexChanged += (_, _) => UpdateCloudAuthUi();
        return tab;
    }

    private TabPage BuildAuditTab()
    {
        var tab = new TabPage("Audit");
        var refresh = new Button { Text = "Refresh", Left = 16, Top = 16, Width = 100, Height = 32 };
        var open = new Button { Text = "Open audit.jsonl", Left = 128, Top = 16, Width = 135, Height = 32 };
        var note = new Label
        {
            Text = "Security audit contains actions and IDs only. Recovery passwords are never written to this file.",
            Left = 280,
            Top = 24,
            AutoSize = true
        };
        tab.Controls.AddRange([refresh, open, note]);

        _auditResults.View = View.Details;
        _auditResults.FullRowSelect = true;
        _auditResults.GridLines = true;
        _auditResults.SetBounds(16, 62, 1155, 675);
        _auditResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_auditResults,
            ("Time (UTC)", 155), ("User", 165), ("Host", 110), ("Action", 175), ("Result", 75),
            ("Computer", 135), ("Recovery ID", 230), ("Source", 70), ("Auth", 90),
            ("Reference", 130), ("Reason", 180), ("Details", 260));
        tab.Controls.Add(_auditResults);

        refresh.Click += (_, _) => RefreshAudit();
        open.Click += (_, _) => OpenPath(AppPaths.AuditLogFile, "notepad.exe");
        RefreshAudit();
        return tab;
    }

    private void LoadDefaultScopes()
    {
        _scopes.Items.Clear();
        foreach (var scope in _config.DefaultScopes) _scopes.Items.Add(scope, true);
    }

    private List<BitLockerScope> GetSelectedScopes()
    {
        var selected = _scopes.CheckedItems.OfType<BitLockerScope>().ToList();
        return selected.Count == 0 ? _config.DefaultScopes.ToList() : selected;
    }


    private void SaveSelectedScopesAsDefaults()
    {
        var selected = _scopes.CheckedItems.OfType<BitLockerScope>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Check at least one OU before saving defaults.", "Default Scopes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            _config.DefaultScopes = selected;
            ConfigService.SaveAppConfig(_config);
            MessageBox.Show(this, $"Saved {selected.Count} default OU scope(s) to:{Environment.NewLine}{AppPaths.AppSettingsFile}", "Default Scopes", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Default Scopes", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task AddOuAsync()
    {
        try
        {
            UseWaitCursor = true;
            var dc = _ad.GetPreferredWritableDc();
            var ous = await Task.Run(() => _ad.ListOrganizationalUnits(dc));
            using var browser = new OuBrowserForm();
            browser.LoadScopes(ous);
            if (browser.ShowDialog(this) == DialogResult.OK && browser.SelectedScope is { } scope)
            {
                var existing = _scopes.Items.OfType<BitLockerScope>().Any(x => x.SearchBase.Equals(scope.SearchBase, StringComparison.OrdinalIgnoreCase));
                if (!existing) _scopes.Items.Add(scope, true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "OU Browser Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async Task RunExportAsync(bool dryRun)
    {
        SetExportRunning(true);
        _exportLog.Clear();
        _scopeResults.Items.Clear();
        try
        {
            var progress = new Progress<string>(AppendExportLog);
            var service = new ExportService(_config);
            var result = await service.RunAsync(dryRun, _forcePublish.Checked, GetSelectedScopes(), progress);
            ShowExportResult(result);
        }
        finally
        {
            SetExportRunning(false);
            RefreshLastSuccess();
        }
    }

    private void ShowExportResult(ExportResult result)
    {
        var state = result.Success ? (result.DryRun ? "DRY RUN OK" : "SUCCESS") : "FAILED";
        _exportSummary.Text = $"Status: {state}    DC: {result.AdServer}    Previous: {result.PreviousRows}    New: {result.ValidRows}    Objects: {result.ObjectsFound}    Duplicates: {result.Duplicates}    Invalid: {result.InvalidObjects}    Duration: {result.DurationSeconds:0.00}s";
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) _exportSummary.Text += Environment.NewLine + "Error: " + result.ErrorMessage;
        else if (!string.IsNullOrWhiteSpace(result.RowCountWarning)) _exportSummary.Text += Environment.NewLine + "Warning: " + result.RowCountWarning;
        else if (!string.IsNullOrWhiteSpace(result.ScopeWarning)) _exportSummary.Text += Environment.NewLine + "Warning: " + result.ScopeWarning;

        _scopeResults.Items.Clear();
        foreach (var scope in result.Containers)
        {
            var item = new ListViewItem(scope.Name);
            item.SubItems.Add(scope.Status);
            item.SubItems.Add(scope.ObjectsFound.ToString());
            item.SubItems.Add(scope.ValidRows.ToString());
            item.SubItems.Add(scope.Duplicates.ToString());
            item.SubItems.Add(scope.Invalid.ToString());
            item.SubItems.Add(scope.Error ?? string.Empty);
            _scopeResults.Items.Add(item);
        }
    }

    private void SetExportRunning(bool running)
    {
        _runExport.Enabled = !running;
        _dryRun.Enabled = !running;
        _forcePublish.Enabled = !running;
    }

    private void AppendExportLog(string message)
    {
        _exportLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _exportLog.SelectionStart = _exportLog.TextLength;
        _exportLog.ScrollToCaret();
    }

    private void RefreshLastSuccess()
    {
        try
        {
            var last = JsonStore.Read<LastSuccessInfo>(_config.LastSuccessFile);
            if (last is null)
            {
                _lastSuccess.Text = "Last successful export: not recorded yet.";
                _lastSuccess.ForeColor = SystemColors.ControlText;
                return;
            }
            var age = (DateTime.Now - last.Finished).TotalHours;
            var stale = age > _config.StaleSuccessHours;
            _lastSuccess.Text = $"Last successful export: {last.Finished:yyyy-MM-dd HH:mm:ss} ({age:0.0}h ago) | Rows: {last.ValidRows} | DC: {last.AdServer}" + (stale ? " | STALE" : string.Empty);
            _lastSuccess.ForeColor = stale ? Color.DarkRed : Color.DarkGreen;
        }
        catch { }
    }

    private async Task RunDcTestAsync()
    {
        _dcTest.Enabled = false;
        _dcResults.Items.Clear();
        _dcDetails.Clear();
        try
        {
            var progress = new Progress<string>(m => _dcDetails.AppendText($"[{DateTime.Now:HH:mm:ss}] {m}{Environment.NewLine}"));
            var service = new DomainControllerComparisonService(_config);
            var rows = await service.RunAsync(GetSelectedScopes(), progress);
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.Name);
                item.SubItems.Add(row.Site);
                item.SubItems.Add(row.IsReadOnly ? "Yes" : "No");
                item.SubItems.Add(row.Reachable ? "Yes" : "No");
                item.SubItems.Add(row.Status);
                item.SubItems.Add(row.ObjectsFound.ToString());
                item.SubItems.Add(row.DifferenceFromMax?.ToString() ?? "-");
                item.SubItems.Add(row.ReplicationErrors.ToString());
                item.SubItems.Add(row.ReplicationWarnings.ToString());
                item.SubItems.Add(row.MaxReplicationAgeHours?.ToString("0.00") ?? "-");
                item.SubItems.Add(row.IPv4Address);
                item.SubItems.Add(row.OperatingSystem);
                item.Tag = row;
                _dcResults.Items.Add(item);
            }
            _dcDetails.AppendText(Environment.NewLine + $"Discovered {rows.Count} domain controller(s)." + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _dcDetails.AppendText("ERROR: " + ex + Environment.NewLine);
        }
        finally { _dcTest.Enabled = true; }
    }

    private void SearchLocal()
    {
        try
        {
            var q = _localQuery.Text.Trim();
            var rows = CsvUtility.ReadRecoveryCsv(_config.OutputCsv);
            if (!string.IsNullOrWhiteSpace(q))
                rows = rows.Where(x => x.ComputerName.Contains(q, StringComparison.OrdinalIgnoreCase) || x.BitLockerId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            rows = rows.Take(1000).ToList();
            _localResults.Items.Clear();
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.ComputerName);
                item.SubItems.Add(row.BitLockerId);
                item.SubItems.Add(row.LastChecked.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(row.Source);
                item.Tag = row;
                _localResults.Items.Add(item);
            }
            _localCurrentKey = null;
            _localKey.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Recovery Search", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SelectLocalRecord()
    {
        if (_localResults.SelectedItems.Count == 0 || _localResults.SelectedItems[0].Tag is not RecoveryRecord row) return;
        _localCurrentKey = row.RecoveryKey;
        _localKey.Text = row.RecoveryKey;
        _localKey.UseSystemPasswordChar = true;
        _localShow.Text = "Show Key";
    }

    private void LoadCloudFields()
    {
        _cloudConfig = ConfigService.LoadCloudConfig();
        _cloudTenant.Text = _cloudConfig.TenantId;
        _cloudClient.Text = _cloudConfig.ClientId;
        _cloudUsername.Text = _cloudConfig.Username;
        _cloudThumbprint.Text = _cloudConfig.CertificateThumbprint;
        _cloudAuthMode.SelectedIndex = _cloudConfig.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase)
            ? 2
            : _cloudConfig.AuthMode.Equals("Password", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
        UpdateCloudAuthUi();
        if (string.IsNullOrWhiteSpace(_cloudConfig.ClientId))
        {
            _cloudStatus.Text =
                "No BitKeyBridge App Registration is configured yet. Click First-Run / Repair Setup; no pre-created App Registration is required.";
        }
    }

    private void SaveCloudFields()
    {
        _cloudConfig.TenantId = _cloudTenant.Text.Trim();
        _cloudConfig.ClientId = _cloudClient.Text.Trim();
        _cloudConfig.Username = _cloudUsername.Text.Trim();
        _cloudConfig.CertificateThumbprint = _cloudThumbprint.Text.Trim();
        _cloudConfig.AuthMode = _cloudAuthMode.SelectedIndex switch
        {
            2 => "Certificate",
            1 => "Password",
            _ => "DeviceCode"
        };
        ConfigService.SaveCloudConfig(_cloudConfig);
        _cloudStatus.Text = "Cloud config saved. Password was not saved.";
    }

    private void UpdateCloudAuthUi()
    {
        var deviceCode = _cloudAuthMode.SelectedIndex == 0;
        var password = _cloudAuthMode.SelectedIndex == 1;
        var cert = _cloudAuthMode.SelectedIndex == 2;
        _cloudUsername.Enabled = password;
        _cloudPassword.Enabled = password;
        _cloudThumbprint.Enabled = cert;
        if (deviceCode) _cloudPassword.Clear();
    }

    private async Task<bool> ConnectCloudAsync()
    {
        try
        {
            SaveCloudFields();
            _cloudStatus.Text = "Connecting...";
            using var graph = new CloudGraphService();
            _cloudToken = _cloudAuthMode.SelectedIndex switch
            {
                2 => await graph.AcquireCertificateTokenAsync(_cloudTenant.Text, _cloudClient.Text, _cloudThumbprint.Text),
                1 => await graph.AcquirePasswordTokenAsync(_cloudTenant.Text, _cloudClient.Text, _cloudUsername.Text, _cloudPassword.Text),
                _ => await graph.AcquireDeviceCodeTokenAsync(
                    _cloudTenant.Text,
                    _cloudClient.Text,
                    ShowDeviceCodeAsync)
            };
            var count = await graph.TestAccessAsync(_cloudToken.AccessToken);
            _cloudStatus.Text = $"Connected using {_cloudToken.AuthMode}. First Graph page returned {count} recovery metadata item(s).";
            return true;
        }
        catch (Exception ex)
        {
            _cloudToken = null;
            _cloudStatus.Text = "Connection failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Microsoft Graph", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task<bool> EnsureCloudTokenAsync()
    {
        if (_cloudToken is not null && _cloudToken.ExpiresAt > DateTime.Now.AddMinutes(1)) return true;
        return await ConnectCloudAsync();
    }

    private async Task SearchCloudAsync()
    {
        if (!await EnsureCloudTokenAsync()) return;
        try
        {
            _cloudStatus.Text = "Searching cloud BitLocker metadata...";
            using var graph = new CloudGraphService();
            var rows = await graph.SearchAsync(_cloudToken!.AccessToken, _cloudQuery.Text);
            _cloudResults.Items.Clear();
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.ComputerName);
                item.SubItems.Add(row.RecoveryId);
                item.SubItems.Add(row.DeviceId);
                item.SubItems.Add(row.VolumeType);
                item.SubItems.Add(row.CreatedDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                item.Tag = row;
                _cloudResults.Items.Add(item);
            }
            _cloudCurrentKey = null;
            _cloudKey.Clear();
            _cloudStatus.Text = $"Cloud search returned {rows.Count} metadata record(s). Recovery passwords were not requested.";
        }
        catch (Exception ex)
        {
            _cloudStatus.Text = "Cloud search failed: " + ex.Message;
        }
    }

    private async Task GetCloudKeyAsync()
    {
        if (_cloudResults.SelectedItems.Count == 0 ||
            _cloudResults.SelectedItems[0].Tag is not CloudRecoveryMetadata row)
        {
            MessageBox.Show(
                this,
                "Select a cloud recovery record first.",
                "Cloud Recovery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var context = GetOrRequestRecoveryAccessContext(
            "Entra",
            row.RecoveryId,
            row.ComputerName,
            allowRotationReminder: true);
        if (context is null) return;

        if (!await EnsureCloudTokenAsync()) return;

        try
        {
            _cloudStatus.Text =
                "Retrieving recovery password from Entra (this access is audited)...";
            using var graph = new CloudGraphService();
            _cloudCurrentKey = await graph.GetRecoveryKeyValueAsync(
                _cloudToken!.AccessToken,
                row.RecoveryId);
            _cloudKey.Text = _cloudCurrentKey;
            _cloudKey.UseSystemPasswordChar = true;
            _cloudShow.Text = "Show Key";

            _audit.Write(
                "GetCloudRecoveryKey",
                computerName: row.ComputerName,
                recoveryId: row.RecoveryId,
                source: "Entra",
                authMode: _cloudToken.AuthMode,
                reference: context.Reference,
                reason: context.Reason);

            if (context.RemindRotation)
            {
                _cloudStatus.Text =
                    "Recovery password retrieved. Complete recovery first, then use Rotate Key in Intune.";
                MessageBox.Show(
                    this,
                    "Recovery password retrieved." + Environment.NewLine + Environment.NewLine +
                    "After the recovery operation is complete and the device is back online, use " +
                    "'Rotate Key in Intune' to invalidate the exposed recovery password." +
                    (string.IsNullOrWhiteSpace(context.Reference)
                        ? string.Empty
                        : Environment.NewLine + Environment.NewLine + "Reference: " + context.Reference),
                    "Recovery Rotation Reminder",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _cloudStatus.Text =
                    "Recovery password retrieved. This key-read operation is auditable in Microsoft Entra and BitKeyBridge.";
            }
        }
        catch (Exception ex)
        {
            _audit.Write(
                "GetCloudRecoveryKey",
                "Failed",
                row.ComputerName,
                row.RecoveryId,
                "Entra",
                _cloudToken?.AuthMode,
                ex.Message,
                context.Reference,
                context.Reason);
            _cloudStatus.Text = "Key retrieval failed: " + ex.Message;
        }
    }

    private async Task RunNativeAutoSetupAsync()
    {
        SaveCloudFields();

        var usingDefaultBootstrap = string.IsNullOrWhiteSpace(_cloudConfig.BootstrapClientId) ||
            string.Equals(
                _cloudConfig.BootstrapClientId,
                EntraSetupService.DefaultBootstrapClientId,
                StringComparison.OrdinalIgnoreCase);

        var bootstrapDescription = usingDefaultBootstrap
            ? $"Microsoft first-party '{EntraSetupService.DefaultBootstrapDisplayName}'"
            : $"custom bootstrap Client ID {_cloudConfig.BootstrapClientId}";

        var answer = MessageBox.Show(
            this,
            "BitKeyBridge will create or repair its dedicated Microsoft Entra App Registration automatically." +
            Environment.NewLine + Environment.NewLine +
            $"Bootstrap: {bootstrapDescription}" + Environment.NewLine +
            "You will be asked to sign in with an Entra administrator account using Device Code and approve the requested management permissions." +
            Environment.NewLine + Environment.NewLine +
            "BitKeyBridge will then create/update the application, Enterprise Application, Graph permissions, admin-consent grants and local certificate. No administrator password is stored." +
            Environment.NewLine + Environment.NewLine +
            "Continue?",
            "First-Run / Repair Entra Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1);
        if (answer != DialogResult.Yes) return;

        try
        {
            Enabled = false;
            _cloudStatus.Text = "Starting first-run Entra setup...";
            using var setup = new EntraSetupService();
            var progress = new Progress<string>(m => _cloudStatus.Text = m);
            var result = await setup.RunAsync(
                _cloudTenant.Text,
                _cloudConfig.BootstrapClientId,
                "BitKeyBridge",
                _cloudConfig,
                ShowDeviceCodeAsync,
                progress);
            LoadCloudFields();
            _cloudAuthMode.SelectedIndex = 0;
            SaveCloudFields();
            _audit.Write(
                "EntraAutoSetup",
                computerName: Environment.MachineName,
                source: "Entra",
                authMode: "DeviceCode",
                details: $"ApplicationId={result.ClientId}; ServicePrincipalId={result.ServicePrincipalId}");
            _cloudStatus.Text =
                $"First-run setup complete. BitKeyBridge App Client ID: {result.ClientId}; certificate: {result.CertificateThumbprint}";
            MessageBox.Show(
                this,
                "Microsoft Entra setup completed successfully." + Environment.NewLine + Environment.NewLine +
                $"BitKeyBridge Client ID: {result.ClientId}" + Environment.NewLine +
                $"Certificate: {result.CertificateThumbprint}" + Environment.NewLine +
                $"Certificate expires: {result.CertificateNotAfter:yyyy-MM-dd}" + Environment.NewLine + Environment.NewLine +
                "Authentication was switched to Device Code. You can now use Connect / Test, Cloud Search and Unified Devices.",
                "BitKeyBridge Entra Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "EntraAutoSetup",
                "Failed",
                source: "Entra",
                authMode: "DeviceCode",
                details: ex.Message);
            _cloudStatus.Text = "Auto Setup failed: " + ex.Message;
            MessageBox.Show(
                this,
                ex.Message + Environment.NewLine + Environment.NewLine +
                "If your tenant blocks the Microsoft first-party bootstrap through Conditional Access, use Bootstrap... to configure a tenant-approved public-client Application ID and run setup again.",
                "First-Run / Repair Entra Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally { Enabled = true; }
    }

    private void ConfigureCustomBootstrap()
    {
        var current = string.IsNullOrWhiteSpace(_cloudConfig.BootstrapClientId)
            ? EntraSetupService.DefaultBootstrapClientId
            : _cloudConfig.BootstrapClientId;

        using var input = new InputDialog(
            "Advanced Bootstrap Client",
            "Optional advanced override. Leave the Microsoft default Client ID below, or enter a tenant-approved public-client Application ID. Clear the field to restore the Microsoft default bootstrap:",
            current);

        if (input.ShowDialog(this) != DialogResult.OK) return;

        var value = input.Value.Trim();
        _cloudConfig.BootstrapClientId =
            string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, EntraSetupService.DefaultBootstrapClientId, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
        ConfigService.SaveCloudConfig(_cloudConfig);

        _cloudStatus.Text = string.IsNullOrWhiteSpace(_cloudConfig.BootstrapClientId)
            ? $"Bootstrap reset to Microsoft first-party '{EntraSetupService.DefaultBootstrapDisplayName}'."
            : $"Custom bootstrap saved: {_cloudConfig.BootstrapClientId}";
    }

    private async Task ShowDeviceCodeAsync(DeviceCodeInfo info)
    {
        try { Clipboard.SetText(info.UserCode); } catch { }
        try { Process.Start(new ProcessStartInfo(info.VerificationUri) { UseShellExecute = true }); } catch { }
        MessageBox.Show(this,
            info.Message + Environment.NewLine + Environment.NewLine +
            "The code has been copied to the clipboard. Complete sign-in in the browser, then return to BitKeyBridge.",
            "Microsoft Entra Device Code",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        await Task.CompletedTask;
    }

    private async Task SearchUnifiedDevicesAsync()
    {
        if (!await EnsureCloudTokenAsync()) return;
        try
        {
            _unifiedStatus.Text = "Searching Active Directory, Entra and Intune...";
            _unifiedResults.Items.Clear();
            _unifiedDetails.Clear();
            var service = new UnifiedDeviceService(_config);
            var rows = await service.SearchAsync(_cloudToken!.AccessToken, _unifiedQuery.Text);
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.ComputerName);
                item.SubItems.Add(row.FoundInAd ? "Yes" : "No");
                item.SubItems.Add(row.FoundInEntra ? "Yes" : "No");
                item.SubItems.Add(row.FoundInIntune ? "Yes" : "No");
                item.SubItems.Add(row.SerialNumber);
                item.SubItems.Add(string.IsNullOrWhiteSpace(row.UserPrincipalName) ? row.UserDisplayName : row.UserPrincipalName);
                item.SubItems.Add((row.Manufacturer + " " + row.Model).Trim());
                item.SubItems.Add((row.OperatingSystem + " " + row.OsVersion).Trim());
                item.SubItems.Add(row.ComplianceState);
                item.SubItems.Add(row.IsEncrypted is null ? "-" : row.IsEncrypted.Value ? "Yes" : "No");
                item.SubItems.Add(row.LastSyncDateTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty);
                item.SubItems.Add(row.RecoveryKeyCount.ToString());
                item.Tag = row;
                _unifiedResults.Items.Add(item);
            }
            _unifiedStatus.Text = $"Unified search returned {rows.Count} device(s). Recovery passwords were not requested.";
            _audit.Write("UnifiedDeviceSearch", computerName: _unifiedQuery.Text.Trim(), source: "AD+Entra+Intune", authMode: _cloudToken.AuthMode, details: $"Results={rows.Count}");
        }
        catch (Exception ex)
        {
            _unifiedStatus.Text = "Unified search failed: " + ex.Message;
            _audit.Write("UnifiedDeviceSearch", "Failed", source: "AD+Entra+Intune", authMode: _cloudToken?.AuthMode, details: ex.Message);
        }
    }

    private void ShowUnifiedDeviceDetails()
    {
        _unifiedDetails.Clear();
        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not UnifiedDeviceInfo row) return;

        _unifiedDetails.AppendText($"Computer:          {row.ComputerName}{Environment.NewLine}");
        _unifiedDetails.AppendText($"AD:                {row.FoundInAd}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Entra:             {row.FoundInEntra}  Device ID: {row.EntraDeviceId}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Intune:            {row.FoundInIntune}  Managed Device ID: {row.ManagedDeviceId}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Serial:            {row.SerialNumber}{Environment.NewLine}");
        _unifiedDetails.AppendText($"User:              {row.UserDisplayName}  {row.UserPrincipalName}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Hardware:          {(row.Manufacturer + " " + row.Model).Trim()}{Environment.NewLine}");
        _unifiedDetails.AppendText($"OS:                {(row.OperatingSystem + " " + row.OsVersion).Trim()}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Compliance:        {row.ComplianceState}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Encrypted:         {(row.IsEncrypted is null ? "-" : row.IsEncrypted.Value ? "Yes" : "No")}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Intune last sync:  {row.LastSyncDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
        _unifiedDetails.AppendText($"AD last logon:     {row.AdLastLogonTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
        _unifiedDetails.AppendText($"AD DN:             {row.AdDistinguishedName}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Recovery IDs:      {row.RecoveryKeyCount}{Environment.NewLine}");
        foreach (var recoveryId in row.RecoveryIds)
            _unifiedDetails.AppendText($"  {recoveryId}{Environment.NewLine}");
    }

    private async Task RotateSelectedUnifiedDeviceAsync()
    {
        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not UnifiedDeviceInfo row)
        {
            MessageBox.Show(
                this,
                "Select an Intune-managed device first.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(row.ManagedDeviceId))
        {
            MessageBox.Show(
                this,
                "The selected device is not linked to an Intune managedDevice object.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        await RotateManagedDeviceAsync(
            row.ManagedDeviceId,
            row.ComputerName,
            row.RecoveryIds.FirstOrDefault());
    }

    private async Task RotateSelectedCloudKeyAsync()
    {
        if (_cloudResults.SelectedItems.Count == 0 ||
            _cloudResults.SelectedItems[0].Tag is not CloudRecoveryMetadata row)
        {
            MessageBox.Show(
                this,
                "Select a cloud recovery record first.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var context = GetOrRequestRecoveryAccessContext(
            "Entra",
            row.RecoveryId,
            row.ComputerName,
            allowRotationReminder: false);
        if (context is null) return;

        if (!await EnsureCloudTokenAsync()) return;

        try
        {
            _cloudStatus.Text = "Resolving Intune managed device...";
            using var graph = new CloudGraphService();
            var managed = await graph.FindManagedDeviceByEntraDeviceIdAsync(
                _cloudToken!.AccessToken,
                row.DeviceId);
            if (managed is null)
                throw new InvalidOperationException(
                    "No matching Intune managedDevice was found for this Entra device.");

            await RotateManagedDeviceAsync(
                managed.ManagedDeviceId,
                row.ComputerName,
                row.RecoveryId,
                context);
        }
        catch (Exception ex)
        {
            _cloudStatus.Text = "Rotation failed: " + ex.Message;
            _audit.Write(
                "RotateBitLockerKey",
                "Failed",
                row.ComputerName,
                row.RecoveryId,
                "Intune",
                _cloudToken?.AuthMode,
                ex.Message,
                context.Reference,
                context.Reason);
            MessageBox.Show(
                this,
                ex.Message,
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task RotateManagedDeviceAsync(
        string managedDeviceId,
        string computerName,
        string? recoveryId,
        RecoveryAccessContext? existingContext = null)
    {
        var auditId = string.IsNullOrWhiteSpace(recoveryId)
            ? managedDeviceId
            : recoveryId;

        var context = existingContext ?? GetOrRequestRecoveryAccessContext(
            "Entra",
            auditId,
            computerName,
            allowRotationReminder: false);
        if (context is null) return;

        if (!await EnsureCloudTokenAsync()) return;

        var answer = MessageBox.Show(
            this,
            $"Request BitLocker recovery-key rotation for {computerName}?{Environment.NewLine}{Environment.NewLine}" +
            "Only rotate after the recovery operation is complete and the device can process the Intune action." +
            (string.IsNullOrWhiteSpace(context.Reference)
                ? string.Empty
                : Environment.NewLine + Environment.NewLine + "Reference: " + context.Reference),
            "Confirm BitLocker Key Rotation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            using var graph = new CloudGraphService();
            await graph.RotateBitLockerKeysAsync(
                _cloudToken!.AccessToken,
                managedDeviceId);

            _audit.Write(
                "RotateBitLockerKey",
                computerName: computerName,
                recoveryId: recoveryId,
                source: "Intune",
                authMode: _cloudToken.AuthMode,
                details: $"ManagedDeviceId={managedDeviceId}",
                reference: context.Reference,
                reason: context.Reason);

            _cloudStatus.Text =
                $"Intune accepted the BitLocker key-rotation request for {computerName}.";
            _unifiedStatus.Text = _cloudStatus.Text;
            MessageBox.Show(
                this,
                "Intune accepted the rotation request. The new recovery key appears after the device processes the action and backs up the new key.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "RotateBitLockerKey",
                "Failed",
                computerName,
                recoveryId,
                "Intune",
                _cloudToken?.AuthMode,
                ex.Message,
                context.Reference,
                context.Reason);
            throw;
        }
    }

    private void LoadOperationsSettings()
    {
        _updateRepository.Text = _config.UpdateRepository;
        _checkUpdatesOnStart.Checked = _config.CheckForUpdatesOnStart;
        _allowPrereleaseUpdates.Checked = _config.AllowPrereleaseUpdates;
        _remoteApiPort.Value = Math.Clamp(_config.RemoteApiPort, 1024, 65535);
        _remoteApiManagement.Checked = _config.RemoteApiAllowManagement;
        _requireRecoveryReference.Checked = _config.RequireRecoveryAccessReference;
        _suggestRotationAfterRecovery.Checked =
            _config.SuggestRotationAfterCloudKeyRetrieval;
        RefreshRemoteApiStatus();

        try
        {
            _lastUpdateInfo = JsonStore.Read<UpdateInfo>(AppPaths.UpdateStatusFile);
            if (_lastUpdateInfo is not null)
                DisplayUpdateInfo(_lastUpdateInfo);
        }
        catch { }
    }

    private void SaveOperationsSettings()
    {
        var repository = _updateRepository.Text.Trim();
        if (string.IsNullOrWhiteSpace(repository) || repository.Split('/').Length != 2)
        {
            MessageBox.Show(
                this,
                "Update repository must use owner/repository format.",
                "Update Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _config.UpdateRepository = repository;
        _config.CheckForUpdatesOnStart = _checkUpdatesOnStart.Checked;
        _config.AllowPrereleaseUpdates = _allowPrereleaseUpdates.Checked;
        _config.RemoteApiPort = (int)_remoteApiPort.Value;
        _config.RemoteApiAllowManagement = _remoteApiManagement.Checked;
        _config.RequireRecoveryAccessReference = _requireRecoveryReference.Checked;
        _config.SuggestRotationAfterCloudKeyRetrieval =
            _suggestRotationAfterRecovery.Checked;
        ConfigService.SaveAppConfig(_config);
        _audit.Write(
            "SaveOperationsSettings",
            source: "Local",
            details:
                $"UpdateRepository={_config.UpdateRepository}; CheckOnStart={_config.CheckForUpdatesOnStart}; AllowPrerelease={_config.AllowPrereleaseUpdates}; RemotePort={_config.RemoteApiPort}; RemoteManagement={_config.RemoteApiAllowManagement}; RequireReference={_config.RequireRecoveryAccessReference}; SuggestRotation={_config.SuggestRotationAfterCloudKeyRetrieval}");
        RefreshRemoteApiStatus();
    }

    private async Task CheckForUpdatesGuiAsync(bool silentWhenCurrent)
    {
        SaveOperationsSettings();
        try
        {
            _updateStatus.Text = "Checking GitHub Releases...";
            using var updater = new UpdateService(_config);
            _lastUpdateInfo = await updater.CheckAsync();
            DisplayUpdateInfo(_lastUpdateInfo);

            if (!string.IsNullOrWhiteSpace(_lastUpdateInfo.Error))
            {
                if (!silentWhenCurrent)
                    MessageBox.Show(
                        this,
                        _lastUpdateInfo.Error,
                        "BitKeyBridge Update",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                return;
            }

            if (!_lastUpdateInfo.UpdateAvailable && !silentWhenCurrent)
            {
                MessageBox.Show(
                    this,
                    $"BitKeyBridge {_lastUpdateInfo.CurrentVersion} is current.",
                    "BitKeyBridge Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
            _updateStatus.Text = "Update check failed: " + ex.Message;
        }
    }

    private void DisplayUpdateInfo(UpdateInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.Error))
        {
            _updateStatus.Text =
                $"Current: {info.CurrentVersion}{Environment.NewLine}" +
                $"Update check failed: {info.Error}";
            return;
        }

        _updateStatus.Text =
            $"Current: {info.CurrentVersion}    Latest: {info.LatestVersion}    Architecture: {info.Architecture}{Environment.NewLine}" +
            (info.UpdateAvailable
                ? $"UPDATE AVAILABLE: {info.LatestVersion}    Asset: {info.AssetName}"
                : "No newer release is available.") +
            Environment.NewLine +
            $"Checked: {info.CheckedAtUtc:u}";
    }

    private async Task InstallLatestUpdateGuiAsync()
    {
        if (_lastUpdateInfo is null ||
            !string.IsNullOrWhiteSpace(_lastUpdateInfo.Error) ||
            !_lastUpdateInfo.UpdateAvailable)
        {
            await CheckForUpdatesGuiAsync(silentWhenCurrent: false);
        }

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
            "The GUI will close during replacement and reopen automatically.",
            "Install Verified Update",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            Enabled = false;
            _updateStatus.Text = $"Downloading and verifying BitKeyBridge {info.LatestVersion}...";
            using var updater = new UpdateService(_config);
            var prepared = await updater.PrepareAsync(info);
            _audit.Write(
                "PrepareVerifiedUpdate",
                source: "GitHub",
                details: $"Version={info.LatestVersion}; Asset={info.AssetName}; SHA256={info.ExpectedSha256}");
            updater.LaunchApplyHelper(prepared, restartGui: true);
            _audit.Write(
                "LaunchUpdateHelper",
                source: "Local",
                details: $"Version={info.LatestVersion}");
            Close();
        }
        catch (Exception ex)
        {
            Enabled = true;
            _updateStatus.Text = "Update preparation failed: " + ex.Message;
            _audit.Write(
                "PrepareVerifiedUpdate",
                "Failed",
                source: "GitHub",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Install Verified Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
            MessageBox.Show(this, ex.Message, "GitHub Release", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnableOrReconfigureRemoteApi()
    {
        SaveOperationsSettings();

        var answer = MessageBox.Show(
            this,
            $"Enable the TLS Remote API on TCP {_config.RemoteApiPort}?{Environment.NewLine}{Environment.NewLine}" +
            $"Remote export management: {_config.RemoteApiAllowManagement}{Environment.NewLine}" +
            "A self-signed server certificate will be created in LocalMachine\\My if needed. " +
            "A new random bearer token will be generated and shown once.",
            "Enable Remote API",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            var setup = new RemoteApiSetupService();
            var result = setup.Enable(
                _config,
                (int)_remoteApiPort.Value,
                _remoteApiManagement.Checked);
            RestartServiceIfRunning();
            _audit.Write(
                "EnableRemoteApi",
                source: "RemoteAPI",
                details: $"Port={result.Port}; Management={_config.RemoteApiAllowManagement}; Certificate={result.CertificateThumbprint}");
            RefreshRemoteApiStatus();

            using var secret = new SecretDisplayDialog(
                "BitKeyBridge Remote API Token",
                "Save this bearer token in your monitoring/management system now. BitKeyBridge stores only its SHA-256 hash and cannot display the token again.",
                result.Token,
                $"TLS certificate thumbprint: {result.CertificateThumbprint}{Environment.NewLine}" +
                $"Certificate expires: {result.CertificateExpires:yyyy-MM-dd}{Environment.NewLine}" +
                $"API base URL: https://{Environment.MachineName}:{result.Port}/api/v1/");
            secret.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _audit.Write("EnableRemoteApi", "Failed", source: "RemoteAPI", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RotateRemoteApiToken()
    {
        if (!_config.RemoteApiEnabled)
        {
            MessageBox.Show(this, "Remote API is not enabled.", "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var setup = new RemoteApiSetupService();
            var result = setup.RegenerateToken(_config);
            RestartServiceIfRunning();
            _audit.Write(
                "RotateRemoteApiToken",
                source: "RemoteAPI",
                details: $"Port={result.Port}; Certificate={result.CertificateThumbprint}");
            RefreshRemoteApiStatus();

            using var secret = new SecretDisplayDialog(
                "New BitKeyBridge Remote API Token",
                "The previous bearer token is now invalid. Save this new token now.",
                result.Token,
                $"API base URL: https://{Environment.MachineName}:{result.Port}/api/v1/");
            secret.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _audit.Write("RotateRemoteApiToken", "Failed", source: "RemoteAPI", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisableRemoteApi()
    {
        if (!_config.RemoteApiEnabled) return;

        var answer = MessageBox.Show(
            this,
            "Disable the Remote API and invalidate the current bearer token?",
            "Disable Remote API",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            new RemoteApiSetupService().Disable(_config);
            RestartServiceIfRunning();
            _audit.Write("DisableRemoteApi", source: "RemoteAPI");
            RefreshRemoteApiStatus();
        }
        catch (Exception ex)
        {
            _audit.Write("DisableRemoteApi", "Failed", source: "RemoteAPI", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshRemoteApiStatus()
    {
        _remoteApiStatus.Text = _config.RemoteApiEnabled
            ? $"ENABLED: https://{Environment.MachineName}:{_config.RemoteApiPort}/api/v1/{Environment.NewLine}" +
              $"Management: {_config.RemoteApiAllowManagement}    Certificate: {_config.RemoteApiCertificateThumbprint}{Environment.NewLine}" +
              "Bearer token is stored only as SHA-256. Use Rotate Token if the original token is no longer available."
            : "DISABLED. Remote API does not listen on the network until explicitly enabled.";
    }

    private void RestartServiceIfRunning()
    {
        var service = WindowsServiceHost.GetInfo();
        if (!service.Installed ||
            !string.Equals(service.State, "Running", StringComparison.OrdinalIgnoreCase))
            return;
        WindowsServiceHost.Stop();
        WindowsServiceHost.Start();
    }

    private void OpenWindowsEventLog()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "eventvwr.msc",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Windows Event Log", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadDirectorySettings()
    {
        _adMode.SelectedIndex = string.Equals(
            _config.AdConnectionMode,
            "Explicit",
            StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        _adServer.Text = _config.AdServer;
        _adDomain.Text = _config.AdDomain;
        _adUsername.Text = _config.AdUsername;
        _adPassword.Clear();
        _adPort.Value = Math.Clamp(_config.AdPort, 1, 65535);
        _adUseLdaps.Checked = _config.AdUseLdaps || _config.AdPort == 636;
        _adExplicitCredentials.Checked = _config.AdUseExplicitCredentials;
        _adCredentialStorage.SelectedIndex =
            _config.AdCredentialStorageMode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase)
                ? 1
                : _config.AdCredentialStorageMode.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 0;
        _outputRoot.Text = string.IsNullOrWhiteSpace(_config.OutputRoot)
            ? _config.SysvolScriptsRoot
            : _config.OutputRoot;
        _outputSubdirectory.Text = _config.OutputSubdirectory;

        UpdateDirectoryConnectionUi();
        RefreshCredentialVaultStatus();
    }

    private void UpdateDirectoryConnectionUi()
    {
        var explicitServer = _adMode.SelectedIndex == 1;
        _adServer.Enabled = explicitServer;
        _adDomain.Enabled = explicitServer || _adExplicitCredentials.Checked;
        _adPort.Enabled = explicitServer;
        _adUsername.Enabled = _adExplicitCredentials.Checked;
        _adPassword.Enabled = _adExplicitCredentials.Checked;
        _adCredentialStorage.Enabled = _adExplicitCredentials.Checked;
    }

    private void SaveDirectorySettings(bool showConfirmation)
    {
        _config.AdConnectionMode = _adMode.SelectedIndex == 1
            ? "Explicit"
            : "Auto";
        _config.AdServer = _adServer.Text.Trim();
        _config.AdDomain = _adDomain.Text.Trim();
        _config.AdUsername = _adUsername.Text.Trim();
        _config.AdPort = (int)_adPort.Value;
        _config.AdUseLdaps = _adUseLdaps.Checked;
        _config.AdUseExplicitCredentials = _adExplicitCredentials.Checked;
        _config.AdCredentialStorageMode = GetSelectedCredentialStorageMode();

        if (_config.AdUseExplicitCredentials)
        {
            if (_config.AdCredentialStorageMode.Equals("Session", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(_adPassword.Text))
                    AdSessionCredentials.SetPassword(_adPassword.Text);
            }
            else
            {
                AdSessionCredentials.Clear();
            }
        }
        else
        {
            AdSessionCredentials.Clear();
            _adPassword.Clear();
        }

        var outputRoot = _outputRoot.Text.Trim();
        _config.OutputRoot = string.Equals(
            outputRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            _config.SysvolScriptsRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : outputRoot;

        _config.OutputSubdirectory =
            string.IsNullOrWhiteSpace(_outputSubdirectory.Text)
                ? "BL"
                : _outputSubdirectory.Text.Trim();

        ConfigService.SaveAppConfig(_config);
        _audit.Write(
            "SaveDirectorySettings",
            source: "Local",
            details:
                $"Mode={_config.AdConnectionMode}; Server={_config.AdServer}; Domain={_config.AdDomain}; User={_config.AdUsername}; ExplicitCredentials={_config.AdUseExplicitCredentials}; CredentialStorage={_config.AdCredentialStorageMode}; LDAPS={_config.AdUseLdaps}; Port={_config.AdPort}; OutputRoot={_config.EffectiveOutputRoot}; OutputSubdirectory={_config.OutputSubdirectory}");

        if (showConfirmation)
        {
            MessageBox.Show(
                this,
                _config.AdCredentialStorageMode.Equals("Session", StringComparison.OrdinalIgnoreCase)
                    ? "Directory/output settings saved. Session-mode AD password remains only in this process memory."
                    : "Directory/output settings saved. Stored credentials remain protected by the selected Windows vault; no plaintext password is written to appsettings.",
                "Directory Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private string GetSelectedCredentialStorageMode() =>
        _adCredentialStorage.SelectedIndex switch
        {
            1 => "CurrentUser",
            2 => "LocalMachine",
            _ => "Session"
        };

    private void SaveSelectedCredential()
    {
        if (!_adExplicitCredentials.Checked)
        {
            MessageBox.Show(
                this,
                "Enable explicit AD credentials first.",
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var username = _adUsername.Text.Trim();
        var password = _adPassword.Text;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            MessageBox.Show(
                this,
                "Enter the AD user and password before saving the credential.",
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SaveDirectorySettings(showConfirmation: false);
            var mode = GetSelectedCredentialStorageMode();
            var vault = new CredentialVaultService();

            if (mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                vault.SaveUserCredential(
                    _config.AdCredentialTarget,
                    username,
                    password);
                AdSessionCredentials.Clear();
                _audit.Write(
                    "SaveAdCredential",
                    source: "CredentialManager",
                    details: $"Storage=CurrentUser; User={username}; Target={_config.AdCredentialTarget}");
            }
            else if (mode.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase))
            {
                vault.SaveMachineCredential(
                    username,
                    _adDomain.Text.Trim(),
                    password);
                AdSessionCredentials.Clear();
                _audit.Write(
                    "SaveAdCredential",
                    source: "DPAPI",
                    details: $"Storage=LocalMachine; User={username}; File={AppPaths.MachineAdCredentialFile}");
            }
            else
            {
                AdSessionCredentials.SetPassword(password);
                _audit.Write(
                    "LoadAdSessionCredential",
                    source: "Memory",
                    details: $"Storage=Session; User={username}");
            }

            _config.AdCredentialStorageMode = mode;
            ConfigService.SaveAppConfig(_config);

            if (!mode.Equals("Session", StringComparison.OrdinalIgnoreCase))
                _adPassword.Clear();

            RefreshCredentialVaultStatus();
            MessageBox.Show(
                this,
                mode.Equals("Session", StringComparison.OrdinalIgnoreCase)
                    ? "Credential loaded for this BitKeyBridge process only."
                    : "Credential saved successfully in the selected protected Windows vault.",
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "SaveAdCredential",
                "Failed",
                source: "CredentialVault",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void DeleteSelectedCredential()
    {
        try
        {
            var mode = GetSelectedCredentialStorageMode();
            var vault = new CredentialVaultService();

            if (mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                vault.DeleteUserCredential(_config.AdCredentialTarget);
                _audit.Write(
                    "DeleteAdCredential",
                    source: "CredentialManager",
                    details: $"Storage=CurrentUser; Target={_config.AdCredentialTarget}");
            }
            else if (mode.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase))
            {
                vault.DeleteMachineCredential();
                _audit.Write(
                    "DeleteAdCredential",
                    source: "DPAPI",
                    details: $"Storage=LocalMachine; File={AppPaths.MachineAdCredentialFile}");
            }
            else
            {
                AdSessionCredentials.Clear();
                _audit.Write(
                    "DeleteAdCredential",
                    source: "Memory",
                    details: "Storage=Session");
            }

            _adPassword.Clear();
            RefreshCredentialVaultStatus();
        }
        catch (Exception ex)
        {
            _audit.Write(
                "DeleteAdCredential",
                "Failed",
                source: "CredentialVault",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshCredentialVaultStatus()
    {
        try
        {
            if (!_adExplicitCredentials.Checked)
            {
                _credentialVaultStatus.Text =
                    "Using current Windows identity; no explicit AD credential is required.";
                return;
            }

            var mode = GetSelectedCredentialStorageMode();
            if (mode.Equals("Session", StringComparison.OrdinalIgnoreCase))
            {
                _credentialVaultStatus.Text = AdSessionCredentials.HasPassword
                    ? $"Session credential loaded for {_adUsername.Text.Trim()}."
                    : "Session credential is not loaded.";
                return;
            }

            var vault = new CredentialVaultService();
            var metadata = mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase)
                ? vault.GetUserMetadata(_config.AdCredentialTarget)
                : vault.GetMachineMetadata();

            _credentialVaultStatus.Text = metadata.Exists
                ? $"Stored: {metadata.Storage}; User={metadata.Username}; Protected by {metadata.ProtectedBy}."
                : $"No stored {metadata.Storage} credential. Protected by {metadata.ProtectedBy}.";
        }
        catch (Exception ex)
        {
            _credentialVaultStatus.Text = "Credential status error: " + ex.Message;
        }
    }

    private async Task TestDirectoryConnectionAsync()
    {
        try
        {
            SaveDirectorySettings(showConfirmation: false);
            _adConnectionStatus.Text = "Testing Active Directory connection...";
            UseWaitCursor = true;

            var result = await Task.Run(() =>
            {
                var service = new ActiveDirectoryService(_config);
                var server = service.GetPreferredWritableDc();
                var root = service.TestConnection(server);
                return (Server: server, Root: root);
            });

            _adConnectionStatus.Text =
                $"OK: {result.Server}:{_config.AdPort}    " +
                $"Protocol: {(_config.AdUseLdaps || _config.AdPort == 636 ? "LDAPS" : "LDAP signed/sealed")}    " +
                $"Domain DN: {result.Root.GetValueOrDefault("defaultNamingContext", "-")}    " +
                $"RODC: {result.Root.GetValueOrDefault("isRODC", "Unknown")}";
        }
        catch (Exception ex)
        {
            _adConnectionStatus.Text = "FAILED: " + ex.Message;
            MessageBox.Show(
                this,
                ex.Message,
                "Directory Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void LoadDashboardSettings()
    {
        _serviceInterval.Value = Math.Clamp(_config.ServiceIntervalMinutes, 1, 10080);
        _healthPort.Value = Math.Clamp(_config.HealthEndpointPort, 1024, 65535);
        _healthEnabled.Checked = _config.HealthEndpointEnabled;
        _serviceRunOnStart.Checked = _config.ServiceRunExportOnStart;
        _serviceIdentityMode.SelectedIndex =
            _config.ServiceIdentityMode.Equals("gMSA", StringComparison.OrdinalIgnoreCase)
                ? 1
                : _config.ServiceIdentityMode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 0;
        _serviceIdentityAccount.Text = _config.ServiceIdentityAccount;
        _serviceIdentityPassword.Clear();
        UpdateServiceIdentityUi();
        RefreshServiceIdentityStatus();
    }

    private void SaveDashboardSettings(bool restartRunningService)
    {
        try
        {
            var serviceBefore = WindowsServiceHost.GetInfo();
            _config.ServiceIntervalMinutes = (int)_serviceInterval.Value;
            _config.HealthEndpointPort = (int)_healthPort.Value;
            _config.HealthEndpointEnabled = _healthEnabled.Checked;
            _config.ServiceRunExportOnStart = _serviceRunOnStart.Checked;
            _config.ServiceIdentityMode = GetSelectedServiceIdentityMode();
            _config.ServiceIdentityAccount = _serviceIdentityAccount.Text.Trim();
            ConfigService.SaveAppConfig(_config);
            _audit.Write(
                "SaveServiceSettings",
                source: "Local",
                details: $"Interval={_config.ServiceIntervalMinutes}; HealthEnabled={_config.HealthEndpointEnabled}; Port={_config.HealthEndpointPort}; RunOnStart={_config.ServiceRunExportOnStart}; IdentityMode={_config.ServiceIdentityMode}; IdentityAccount={_config.ServiceIdentityAccount}");

            if (restartRunningService && serviceBefore.Installed &&
                string.Equals(serviceBefore.State, "Running", StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            RefreshDashboard();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Service Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void InstallOrUpdateService()
    {
        try
        {
            SaveDashboardSettings(restartRunningService: false);
            var before = WindowsServiceHost.GetInfo();
            if (before.Installed && string.Equals(before.State, "Running", StringComparison.OrdinalIgnoreCase))
                WindowsServiceHost.Stop();

            WindowsServiceHost.InstallOrUpdate();
            ApplyConfiguredServiceIdentity(restartIfRunning: false, allowExistingDomainPassword: true);
            WindowsServiceHost.Start();
            _audit.Write(
                "InstallOrUpdateService",
                source: "WindowsService",
                details: $"Executable={AppPaths.ServiceExecutable}; Identity={WindowsServiceHost.GetInfo().Identity}");
            RefreshDashboard();
            MessageBox.Show(
                this,
                $"BitKeyBridge service is installed and running.{Environment.NewLine}{Environment.NewLine}" +
                $"Executable: {AppPaths.ServiceExecutable}",
                "BitKeyBridge Service",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write("InstallOrUpdateService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshDashboard();
        }
    }

    private string GetSelectedServiceIdentityMode() =>
        _serviceIdentityMode.SelectedIndex switch
        {
            1 => "gMSA",
            2 => "DomainAccount",
            _ => "LocalSystem"
        };

    private void UpdateServiceIdentityUi()
    {
        var mode = GetSelectedServiceIdentityMode();
        var localSystem = mode.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase);
        var domainAccount = mode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase);

        _serviceIdentityAccount.Enabled = !localSystem;
        _serviceIdentityPassword.Enabled = domainAccount;

        if (localSystem)
        {
            _serviceIdentityAccount.Clear();
            _serviceIdentityPassword.Clear();
        }
        else if (!domainAccount)
        {
            _serviceIdentityPassword.Clear();
        }
    }

    private void RefreshServiceIdentityStatus()
    {
        try
        {
            var info = WindowsServiceHost.GetInfo();
            _serviceIdentityStatus.Text = info.Installed
                ? $"Installed service identity: {(string.IsNullOrWhiteSpace(info.Identity) ? "Unknown" : info.Identity)}"
                : "Windows Service is not installed.";
        }
        catch (Exception ex)
        {
            _serviceIdentityStatus.Text = "Service identity status error: " + ex.Message;
        }
    }

    private void ApplyConfiguredServiceIdentity(
        bool restartIfRunning,
        bool allowExistingDomainPassword)
    {
        var mode = GetSelectedServiceIdentityMode();
        var account = _serviceIdentityAccount.Text.Trim();
        var password = _serviceIdentityPassword.Text;
        var current = WindowsServiceHost.GetInfo();

        if (!current.Installed)
            throw new InvalidOperationException(
                "Install the BitKeyBridge Windows Service before applying its identity.");

        if (mode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(password) &&
            allowExistingDomainPassword &&
            string.Equals(current.Identity, account, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WindowsServiceHost.ConfigureIdentity(
            mode,
            account,
            mode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase)
                ? password
                : null,
            restartIfRunning);
    }

    private void ApplyServiceIdentityFromGui()
    {
        try
        {
            SaveDashboardSettings(restartRunningService: false);
            ApplyConfiguredServiceIdentity(
                restartIfRunning: true,
                allowExistingDomainPassword: false);

            _audit.Write(
                "ConfigureServiceIdentity",
                source: "WindowsService",
                details: $"Mode={_config.ServiceIdentityMode}; Account={_config.ServiceIdentityAccount}; EffectiveIdentity={WindowsServiceHost.GetInfo().Identity}");

            _serviceIdentityPassword.Clear();
            RefreshServiceIdentityStatus();
            RefreshDashboard();

            MessageBox.Show(
                this,
                "Windows Service identity updated successfully." +
                Environment.NewLine + Environment.NewLine +
                (GetSelectedServiceIdentityMode().Equals("gMSA", StringComparison.OrdinalIgnoreCase)
                    ? "The gMSA password is managed by Active Directory and is not stored by BitKeyBridge."
                    : "BitKeyBridge does not store the Windows Service account password."),
                "Service Identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _serviceIdentityPassword.Clear();
            _audit.Write(
                "ConfigureServiceIdentity",
                "Failed",
                source: "WindowsService",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Service Identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshServiceIdentityStatus();
        }
    }

    private void StartServiceFromGui()
    {
        try
        {
            WindowsServiceHost.Start();
            _audit.Write("StartService", source: "WindowsService");
        }
        catch (Exception ex)
        {
            _audit.Write("StartService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshDashboard();
    }

    private void StopServiceFromGui()
    {
        try
        {
            WindowsServiceHost.Stop();
            _audit.Write("StopService", source: "WindowsService");
        }
        catch (Exception ex)
        {
            _audit.Write("StopService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshDashboard();
    }

    private void UninstallServiceFromGui()
    {
        var answer = MessageBox.Show(
            this,
            "Stop and remove the BitKeyBridge Windows Service? Configuration, audit logs and exported recovery data will be preserved.",
            "Uninstall BitKeyBridge Service",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            WindowsServiceHost.Uninstall();
            _audit.Write("UninstallService", source: "WindowsService");
        }
        catch (Exception ex)
        {
            _audit.Write("UninstallService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshDashboard();
    }

    private void OpenHealthEndpoint()
    {
        try
        {
            var url = $"http://127.0.0.1:{Math.Clamp(_config.HealthEndpointPort, 1024, 65535)}/health";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Health Endpoint", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshDashboard()
    {
        try
        {
            var health = new HealthService(_config).GetSnapshot();
            _dashboardStatus.Text =
                $"Overall: {health.OverallStatus}    Service: {health.ServiceState}    Last rows: {health.LastRunRows}    DC: {(string.IsNullOrWhiteSpace(health.LastRunDc) ? "-" : health.LastRunDc)}";

            _dashboardDetails.Clear();
            _dashboardDetails.AppendText($"Version:                  {health.Version}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Machine:                  {health.MachineName}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service installed:        {health.ServiceInstalled}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service state:            {health.ServiceState}{Environment.NewLine}");
            var serviceInfo = WindowsServiceHost.GetInfo();
            _dashboardDetails.AppendText($"Service identity:         {(serviceInfo.Installed ? serviceInfo.Identity : "-")}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service executable:       {AppPaths.ServiceExecutable}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Health endpoint:          {health.HealthEndpoint}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service interval:         {_config.ServiceIntervalMinutes} minute(s){Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Output directory:         {health.OutputDirectory}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Output exists:            {health.OutputDirectoryExists}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Recovery CSV exists:      {health.CsvExists}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Recovery CSV rows:        {health.CsvRows}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Last run success:         {health.LastRunSuccess?.ToString() ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run finished:        {health.LastRunFinished?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run published:       {health.LastRunPublished}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run rows:            {health.LastRunRows}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run DC:              {health.LastRunDc}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Replication healthy:      {health.ReplicationHealthy?.ToString() ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Replication errors:       {health.ReplicationErrors}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Replication warnings:     {health.ReplicationWarnings}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last successful export:   {health.LastSuccessfulExport?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last success age (hours): {health.LastSuccessfulExportAgeHours?.ToString("0.0") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Cloud configured:         {health.CloudConfigured}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Cloud auth mode:          {health.CloudAuthMode}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Certificate status:       {health.CertificateStatus}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Certificate expires:      {health.CertificateExpires?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Certificate days left:    {health.CertificateDaysRemaining?.ToString("0.0") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Remote API enabled:       {health.RemoteApiEnabled}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Remote API port:          {(health.RemoteApiEnabled ? health.RemoteApiPort.ToString() : "-")}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Remote management:        {health.RemoteApiManagementEnabled}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Remote API certificate:   {health.RemoteApiCertificateThumbprint}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Update checked (UTC):     {health.UpdateCheckedAtUtc?.ToString("u") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Latest release:           {health.LatestVersion}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Update available:         {health.UpdateAvailable}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Update error:             {health.UpdateError}{Environment.NewLine}");

            if (health.Warnings.Count > 0)
            {
                _dashboardDetails.AppendText(Environment.NewLine + "WARNINGS" + Environment.NewLine);
                foreach (var warning in health.Warnings)
                    _dashboardDetails.AppendText("  - " + warning + Environment.NewLine);
            }

            if (health.Errors.Count > 0)
            {
                _dashboardDetails.AppendText(Environment.NewLine + "ERRORS" + Environment.NewLine);
                foreach (var error in health.Errors)
                    _dashboardDetails.AppendText("  - " + error + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _dashboardStatus.Text = "Dashboard error: " + ex.Message;
        }
    }

    private void RunSecureOutputWizard()
    {
        using var dialog = new SecureOutputDialog(_config.OutputDirectory);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var service = new SecureOutputService();
            var result = service.Create(dialog.DirectoryPath, dialog.ReaderPrincipals, dialog.ShareName);

            if (dialog.UpdateApplicationConfig)
            {
                var full = Path.GetFullPath(result.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar);
                var parent = Path.GetDirectoryName(full)
                    ?? throw new InvalidOperationException("The selected output directory has no parent directory.");
                var name = Path.GetFileName(full);
                _config.OutputRoot = parent;
                _config.OutputSubdirectory = name;
                _outputRoot.Text = parent;
                _outputSubdirectory.Text = name;
                ConfigService.SaveAppConfig(_config);

                var svc = WindowsServiceHost.GetInfo();
                if (svc.Installed && string.Equals(svc.State, "Running", StringComparison.OrdinalIgnoreCase))
                {
                    WindowsServiceHost.Stop();
                    WindowsServiceHost.Start();
                }
            }

            _audit.Write(
                "CreateSecureOutput",
                source: "WindowsACL",
                details: $"Path={result.DirectoryPath}; Share={result.ShareName}; Readers={string.Join(",", result.ReaderPrincipals)}; ConfigUpdated={dialog.UpdateApplicationConfig}");

            RefreshLastSuccess();
            RefreshDashboard();

            var shareText = result.ShareCreated
                ? $"SMB share: \\{Environment.MachineName}\\{result.ShareName}"
                : "SMB share: not created";
            MessageBox.Show(
                this,
                $"Protected output created successfully.{Environment.NewLine}{Environment.NewLine}" +
                $"Directory: {result.DirectoryPath}{Environment.NewLine}{shareText}{Environment.NewLine}{Environment.NewLine}" +
                (dialog.UpdateApplicationConfig
                    ? "BitKeyBridge export configuration was updated. If an existing WinPE workflow still reads NETLOGON/SYSVOL, update that consumer before switching production export."
                    : "BitKeyBridge export configuration was not changed."),
                "Secure BitLocker Output",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write("CreateSecureOutput", "Failed", source: "WindowsACL", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Secure BitLocker Output", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private RecoveryAccessContext? GetOrRequestRecoveryAccessContext(
        string source,
        string recoveryId,
        string computerName,
        bool allowRotationReminder)
    {
        var normalizedId = string.IsNullOrWhiteSpace(recoveryId)
            ? computerName
            : recoveryId;
        var cacheKey = source + ":" + normalizedId;

        if (_recoveryAccessContexts.TryGetValue(cacheKey, out var existing))
            return existing;

        var prompt =
            _config.RequireRecoveryAccessReference ||
            (allowRotationReminder &&
             _config.SuggestRotationAfterCloudKeyRetrieval);

        if (!prompt)
        {
            var empty = new RecoveryAccessContext();
            _recoveryAccessContexts[cacheKey] = empty;
            return empty;
        }

        using var dialog = new RecoveryAccessDialog(
            computerName,
            normalizedId,
            _config.RequireRecoveryAccessReference,
            allowRotationReminder,
            allowRotationReminder &&
            _config.SuggestRotationAfterCloudKeyRetrieval);

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return null;

        var context = dialog.Context;
        _recoveryAccessContexts[cacheKey] = context;
        return context;
    }

    private void ClearSensitiveState()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if ((!string.IsNullOrWhiteSpace(_localCurrentKey) && string.Equals(text, _localCurrentKey, StringComparison.Ordinal)) ||
                    (!string.IsNullOrWhiteSpace(_cloudCurrentKey) && string.Equals(text, _cloudCurrentKey, StringComparison.Ordinal)))
                    Clipboard.Clear();
            }
        }
        catch { }

        _localCurrentKey = null;
        _cloudCurrentKey = null;
        _cloudToken = null;
        AdSessionCredentials.Clear();
        _recoveryAccessContexts.Clear();
        _localKey.Clear();
        _cloudKey.Clear();
        _cloudPassword.Clear();
        _adPassword.Clear();
        _serviceIdentityPassword.Clear();
    }

    private void RefreshAudit()
    {
        _auditResults.Items.Clear();
        foreach (var row in _audit.ReadRecent(1000))
        {
            var item = new ListViewItem(row.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(row.User);
            item.SubItems.Add(row.Host);
            item.SubItems.Add(row.Action);
            item.SubItems.Add(row.Result);
            item.SubItems.Add(row.ComputerName);
            item.SubItems.Add(row.RecoveryId);
            item.SubItems.Add(row.Source);
            item.SubItems.Add(row.AuthMode);
            item.SubItems.Add(row.Reference);
            item.SubItems.Add(row.Reason);
            item.SubItems.Add(row.Details);
            _auditResults.Items.Add(item);
        }
    }

    private static void ToggleKey(TextBox box, Button button)
    {
        box.UseSystemPasswordChar = !box.UseSystemPasswordChar;
        button.Text = box.UseSystemPasswordChar ? "Show Key" : "Hide Key";
    }

    private void CopyKeyWithAutoClear(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        Clipboard.SetText(key);
        var timer = new System.Windows.Forms.Timer { Interval = 60000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                if (Clipboard.ContainsText() && string.Equals(Clipboard.GetText(), key, StringComparison.Ordinal)) Clipboard.Clear();
            }
            catch { }
            timer.Dispose();
        };
        timer.Start();
    }

    private static void AddColumns(ListView view, params (string Name, int Width)[] columns)
    {
        foreach (var (name, width) in columns) view.Columns.Add(name, width);
    }

    private static void AddLabeled(Control parent, string labelText, Control control, int x, int y, int labelWidth, int controlWidth)
    {
        var label = new Label { Text = labelText, Left = x, Top = y + 4, Width = labelWidth, Height = 23 };
        control.SetBounds(x + labelWidth, y, controlWidth, 27);
        parent.Controls.Add(label);
        parent.Controls.Add(control);
    }

    private void OpenPath(string path, string? executable = null)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show(this, $"Path does not exist:{Environment.NewLine}{path}", "Open", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (executable is null)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(executable, $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
