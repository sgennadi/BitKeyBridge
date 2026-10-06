namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly TabControl _mainTabs = new();
    private readonly ComboBox _recoverySource = new();
    private readonly CheckBox _autoConnectOnStart = new();
    private readonly Button _connectAdButton = new();
    private readonly Button _advancedConnectionButton = new();
    private readonly GroupBox _advancedConnectionGroup = new();
    private readonly GroupBox _recoveryCard = new();
    private readonly Label _recoveryCardComputer = new();
    private readonly Label _recoveryCardOu = new();
    private readonly Label _recoveryCardId = new();
    private readonly Label _recoveryCardTime = new();
    private readonly Label _recoveryCardSource = new();
    private readonly Button _startCopy = new();

    private readonly ComboBox _deviceRecoveryIds = new();
    private readonly TextBox _deviceRecoveryKey = new();
    private readonly Button _deviceShowKey = new();
    private readonly Button _deviceCopyKey = new();
    private string? _deviceCurrentKey;

    private bool AdministrationAllowed =>
        _layoutSelfTest ||
        new AuthorizationService(_config)
            .Check(BitKeyBridgePermission.Administrator)
            .Allowed;

    private Control BuildMainShell()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var menu = BuildMainMenu();
        root.Controls.Add(menu, 0, 0);

        _mainTabs.Dock = DockStyle.Fill;
        _mainTabs.TabPages.Add(BuildRecoveryWorkspaceTab());
        _mainTabs.TabPages.Add(BuildDevicesWorkspaceTab());

        if (AdministrationAllowed)
        {
            _mainTabs.TabPages.Add(BuildAdministrationWorkspaceTab());
            _mainTabs.TabPages.Add(BuildHealthAuditWorkspaceTab());
        }

        foreach (TabPage page in _mainTabs.TabPages)
            page.AutoScroll = true;

        root.Controls.Add(_mainTabs, 0, 1);
        MainMenuStrip = menu;
        return root;
    }

    private MenuStrip BuildMainMenu()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top
        };

        var tools = new ToolStripMenuItem("Tools");

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

        tools.DropDownItems.Add(
            "Latest GitHub release",
            null,
            (_, _) => OpenLatestRelease());

        menu.Items.Add(tools);
        return menu;
    }

    private TabPage BuildRecoveryWorkspaceTab()
    {
        var tab = new TabPage("Recovery");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 9
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(root);

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 10)
        };
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titlePanel.Controls.Add(new Label
        {
            Text = "Find BitLocker Recovery Key",
            Font = UiStyle.CreatePageTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        titlePanel.Controls.Add(new Label
        {
            Text =
                "Search by computer name or Recovery ID. BitKeyBridge searches metadata first; " +
                "the recovery password is retrieved only when you explicitly reveal or copy it.",
            AutoSize = true,
            MaximumSize = new Size(1050, 0)
        }, 0, 1);
        root.Controls.Add(titlePanel, 0, 0);

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

        var connection =
            new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 2,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };

        connection.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        connection.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        connection.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        connection.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        connection.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        connection.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        connection.Controls.Add(
            new Label
            {
                Text = "Source:",
                AutoSize = true,
                Anchor =
                    AnchorStyles.Left,
                Margin =
                    new Padding(
                        0,
                        8,
                        UiStyle.ControlGap,
                        4)
            },
            0,
            0);

        _recoverySource.Name =
            "RecoverySourceSelector";
        _recoverySource.AccessibleName =
            "Recovery source";
        _recoverySource.TabIndex =
            0;
        _recoverySource.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _recoverySource.MinimumSize =
            new Size(
                160,
                0);
        _recoverySource.Anchor =
            AnchorStyles.Left |
            AnchorStyles.Right;
        _recoverySource.Margin =
            new Padding(
                0,
                3,
                UiStyle.ControlGap,
                3);
        _recoverySource.Items.AddRange([
            "Live AD",
            "Local cache"
        ]);
        connection.Controls.Add(
            _recoverySource,
            1,
            0);

        _connectAdButton.Name =
            "ConnectAdButton";
        _connectAdButton.AccessibleName =
            "Connect to Active Directory";
        _connectAdButton.TabIndex =
            1;
        _connectAdButton.Text =
            "Connect to AD";
        UiStyle.ConfigureActionButton(
            _connectAdButton);
        _connectAdButton.MinimumSize =
            new Size(
                160,
                UiStyle.MinimumButtonHeight);
        _connectAdButton.Anchor =
            AnchorStyles.Left |
            AnchorStyles.Right;
        _connectAdButton.Margin =
            new Padding(
                0,
                0,
                UiStyle.ControlGap,
                0);
        connection.Controls.Add(
            _connectAdButton,
            2,
            0);

        UiStyle.BindBalancedWidths(
            _recoverySource,
            _connectAdButton);

        _advancedConnectionButton.Name =
            "AdvancedConnectionButton";
        _advancedConnectionButton.AccessibleName =
            "Advanced Active Directory connection settings";
        _advancedConnectionButton.TabIndex =
            2;
        _advancedConnectionButton.Text =
            "Advanced connection settings...";
        UiStyle.ConfigureActionButton(
            _advancedConnectionButton);
        _advancedConnectionButton.Anchor =
            AnchorStyles.Left;
        _advancedConnectionButton.Margin =
            new Padding(
                0);
        connection.Controls.Add(
            _advancedConnectionButton,
            3,
            0);

        _adConnectionStatus.Name =
            "AdConnectionStatus";
        UiStyle.ConfigureStatusLabel(
            _adConnectionStatus);
        _adConnectionStatus.Text =
            "Ready to connect to Active Directory.";
        UiStyle.ApplyStatusLabel(
            _adConnectionStatus,
            UiStatusKind.Neutral);
        connection.Controls.Add(
            _adConnectionStatus,
            0,
            1);
        connection.SetColumnSpan(
            _adConnectionStatus,
            4);

        root.Controls.Add(
            connection,
            0,
            2);

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
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 10)
        };
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        searchGrid.Controls.Add(new Label
        {
            Text = "Computer / Recovery ID:",
            AutoSize = true,
            Font = UiStyle.CreateEmphasisFont(9.5F),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 10, 0)
        }, 0, 0);

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
        root.Controls.Add(searchGrid, 0, 4);

        _startResults.View = View.Details;
        _startResults.FullRowSelect = true;
        _startResults.GridLines = true;
        _startResults.HideSelection = false;
        _startResults.Dock = DockStyle.Fill;
        _startResults.MinimumSize = new Size(0, 180);
        AddColumns(
            _startResults,
            ("Computer", 220),
            ("Recovery ID", 330),
            ("Source", 120),
            ("Created / checked", 180));
        root.Controls.Add(_startResults, 0, 5);

        ConfigureRecoveryCard();
        root.Controls.Add(_recoveryCard, 0, 6);

        ConfigureAdvancedConnectionGroup();
        root.Controls.Add(_advancedConnectionGroup, 0, 7);

        _connectAdButton.Click +=
            async (_, _) =>
                await TestDirectoryConnectionAsync(
                    promptForOu: true,
                    promptForSessionCredentials: true);

        _startSelectOu.Click +=
            async (_, _) =>
                await SelectStartOuAsync();

        _startSearch.Click +=
            async (_, _) =>
                await SearchStartRecoveryAsync();

        _startQuery.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter &&
                    _startSearch.Enabled)
                {
                    await SearchStartRecoveryAsync();
                }
            };

        _startResults.SelectedIndexChanged +=
            (_, _) =>
                SelectStartRecoveryRecord();

        _startShow.Click +=
            async (_, _) =>
                await RevealStartRecoveryKeyAsync();

        _startCopy.Click +=
            async (_, _) =>
                await CopyStartRecoveryKeyAsync();

        _recoverySource.SelectedIndexChanged +=
            (_, _) =>
            {
                ResetRecoverySearchState();
                UpdateRecoverySourceUi();
                TrySaveRecoveryUiState();
            };

        _advancedConnectionButton.Click +=
            (_, _) =>
            {
                _advancedConnectionGroup.Visible =
                    !_advancedConnectionGroup.Visible;

                _advancedConnectionButton.Text =
                    _advancedConnectionGroup.Visible
                        ? "Hide advanced settings"
                        : "Advanced connection settings...";
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

        _autoConnectOnStart.Text = "Auto-connect to AD when the GUI starts";
        _autoConnectOnStart.AutoSize = true;
        grid.Controls.Add(_autoConnectOnStart, 1, 4);
        grid.SetColumnSpan(_autoConnectOnStart, 3);

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
            (_, _) => UpdateDirectoryConnectionUi();
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
        var tab = new TabPage("Devices");
        var nested = new TabControl
        {
            Dock = DockStyle.Fill
        };

        nested.TabPages.Add(BuildDeviceSearchPage());

        if (AdministrationAllowed)
            nested.TabPages.Add(BuildCoverageResponsiveTab());

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
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
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
            ColumnCount = 3
        };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        search.Controls.Add(new Label
        {
            Text = "Name / serial / user / device ID:",
            AutoSize = true,
            Margin = new Padding(0, 7, 10, 0)
        }, 0, 0);

        _unifiedQuery.Dock = DockStyle.Fill;
        _unifiedQuery.Font = UiStyle.CreateBodyFont(10.5F);
        search.Controls.Add(_unifiedQuery, 1, 0);

        var searchButton =
            UiStyle.CreateActionButton(
                "Search AD + Cloud");
        search.Controls.Add(searchButton, 2, 0);
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

        _unifiedResults.View = View.Details;
        _unifiedResults.FullRowSelect = true;
        _unifiedResults.GridLines = true;
        _unifiedResults.HideSelection = false;
        _unifiedResults.Dock = DockStyle.Fill;
        AddColumns(
            _unifiedResults,
            ("Computer", 150),
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
            ("Keys", 45));
        root.Controls.Add(_unifiedResults, 0, 2);

        _unifiedDetails.ReadOnly = true;
        _unifiedDetails.Font = UiStyle.CreateMonospaceFont(9F);
        _unifiedDetails.Dock = DockStyle.Fill;
        root.Controls.Add(_unifiedDetails, 0, 3);

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
            4);

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
        root.Controls.Add(actions, 0, 5);

        searchButton.Click +=
            async (_, _) => await SearchUnifiedDevicesAsync();
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

        return page;
    }

    private TabPage BuildAdministrationWorkspaceTab()
    {
        var tab = new TabPage("Administration");
        var nested = new TabControl
        {
            Dock = DockStyle.Fill
        };

        nested.TabPages.Add(BuildCloudAdministrationTab());
        nested.TabPages.Add(BuildSecuritySettingsResponsiveTab());
        nested.TabPages.Add(BuildExportAutomationResponsiveTab());

        tab.Controls.Add(nested);
        return tab;
    }

    private TabPage BuildHealthAuditWorkspaceTab()
    {
        var tab = new TabPage("Health & Audit");
        var nested = new TabControl
        {
            Dock = DockStyle.Fill
        };

        nested.TabPages.Add(BuildServiceHealthResponsiveTab());
        nested.TabPages.Add(BuildDomainControllersResponsiveTab());
        nested.TabPages.Add(BuildAuditResponsiveTab());

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
            RowCount = 8
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
        AddCloudAdminRow(root, 3, "Username:", _cloudUsername);
        AddCloudAdminRow(root, 4, "Password:", _cloudPassword);
        _cloudPassword.UseSystemPasswordChar = true;
        AddCloudAdminRow(root, 5, "Certificate thumbprint:", _cloudThumbprint);

        _cloudAuthMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cloudAuthMode.Items.AddRange([
            "Device Code (MFA / Conditional Access)",
            "Username + Password (ROPC legacy)",
            "App registration + certificate"
        ]);
        AddCloudAdminRow(root, 6, "Authentication:", _cloudAuthMode);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        var save =
            UiStyle.CreateActionButton(
                "Save Config");
        var connect =
            UiStyle.CreateActionButton(
                "Connect / Test");
        var setup =
            UiStyle.CreateActionButton(
                "First-Run / Repair");
        var bootstrap =
            UiStyle.CreateActionButton(
                "Bootstrap...");
        var rollover =
            UiStyle.CreateActionButton(
                "Rollover Certificate...");
        buttons.Controls.AddRange([
            save,
            connect,
            setup,
            bootstrap,
            rollover
        ]);

        root.Controls.Add(buttons, 1, 7);

        UiStyle.ConfigureStatusLabel(
            _cloudStatus);
        _cloudStatus.AccessibleName =
            "Microsoft Graph status";
        _cloudStatus.MaximumSize =
            new Size(900, 0);
        root.Controls.Add(_cloudStatus, 1, 8);

        save.Click += (_, _) => SaveCloudFields();
        connect.Click += async (_, _) => await ConnectCloudAsync();
        setup.Click += async (_, _) => await RunNativeAutoSetupAsync();
        bootstrap.Click += (_, _) => ConfigureCustomBootstrap();
        rollover.Click += async (_, _) => await RolloverEntraCertificateGuiAsync();
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

    private static void HideToolButtons(
        Control root,
        params string[] labels)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button &&
                labels.Contains(
                    button.Text,
                    StringComparer.OrdinalIgnoreCase))
            {
                button.Visible = false;
            }

            if (child.HasChildren)
                HideToolButtons(child, labels);
        }
    }

    private async Task InitializeRecoveryWorkspaceAsync()
    {
        RestoreRecoveryUiState();

        if (_recoverySource.SelectedIndex == 1)
        {
            UpdateRecoverySourceUi();
            return;
        }

        if (!_config.AutoConnectOnStart)
        {
            UpdateRecoverySourceUi();
            return;
        }

        await TestDirectoryConnectionAsync(
            promptForOu:
                _startScope is null);
    }

    private void RestoreRecoveryUiState()
    {
        _autoConnectOnStart.Checked =
            _config.AutoConnectOnStart;

        _recoverySource.SelectedIndex =
            _config.RecoverySearchSource.Equals(
                "LocalCache",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        if (!string.IsNullOrWhiteSpace(
                _config.LastRecoveryScopeSearchBase))
        {
            _startScope =
                new BitLockerScope(
                    string.IsNullOrWhiteSpace(
                        _config.LastRecoveryScopeName)
                        ? "Last OU"
                        : _config.LastRecoveryScopeName,
                    _config.LastRecoveryScopeSearchBase);

            _startOuStatus.Text =
                $"Selected: {_startScope.Name}    {_startScope.SearchBase}";
        }

        UpdateRecoverySourceUi();
    }

    private void UpdateRecoverySourceUi()
    {
        var local =
            _recoverySource.SelectedIndex == 1;

        _connectAdButton.Enabled = !local;
        _startSelectOu.Enabled =
            !local &&
            !string.IsNullOrWhiteSpace(
                _startDomainDn);

        var localCacheAvailable =
            File.Exists(
                _config.OutputCsv);

        _startSearch.Enabled =
            local
                ? localCacheAvailable
                : _startScope is not null;

        if (local)
        {
            SetDirectoryConnectionStatus(
                File.Exists(_config.OutputCsv)
                    ? "Local cache is available. AD connection is not required for this search."
                    : "Local cache is selected, but the recovery export CSV does not exist yet.",
                File.Exists(_config.OutputCsv)
                    ? UiStatusKind.Neutral
                    : UiStatusKind.Warning);

            _startOuStatus.Text =
                "OU selection is not used for Local cache search.";
        }
        else if (_startScope is not null)
        {
            _startOuStatus.Text =
                $"Selected: {_startScope.Name}    {_startScope.SearchBase}";
        }
    }

    private void TrySaveRecoveryUiState()
    {
        try
        {
            _config.AutoConnectOnStart =
                _autoConnectOnStart.Checked;
            _config.RecoverySearchSource =
                _recoverySource.SelectedIndex == 1
                    ? "LocalCache"
                    : "LiveAD";

            if (_startScope is not null)
            {
                _config.LastRecoveryScopeName =
                    _startScope.Name;
                _config.LastRecoveryScopeSearchBase =
                    _startScope.SearchBase;
            }

            ConfigService.SaveAppConfig(_config);
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

        foreach (var id in row.RecoveryIds)
            _deviceRecoveryIds.Items.Add(id);

        if (_deviceRecoveryIds.Items.Count > 0)
            _deviceRecoveryIds.SelectedIndex = 0;
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
            MessageBox.Show(
                this,
                "Select a device and an Entra Recovery ID first.",
                "Devices",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

            MessageBox.Show(
                this,
                ex.Message,
                "Devices",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

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
    }

}
