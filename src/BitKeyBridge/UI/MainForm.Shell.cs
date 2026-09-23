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
    private RecoveryAccessContext? _deviceRecoveryContext;

    private bool AdministrationAllowed =>
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
            Font = new Font("Segoe UI Semibold", 18F),
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

        _startPurposeStatus.AutoSize = true;
        _startPurposeStatus.Font = new Font("Segoe UI Semibold", 10F);
        _startPurposeStatus.Text =
            "Ready — choose the source, connect if required, and search.";
        _startPurposeStatus.Margin = new Padding(0, 0, 0, 10);
        root.Controls.Add(_startPurposeStatus, 0, 1);

        var connection = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        connection.Controls.Add(new Label
        {
            Text = "Source:",
            AutoSize = true,
            Margin = new Padding(0, 9, 6, 0)
        });

        _recoverySource.DropDownStyle = ComboBoxStyle.DropDownList;
        _recoverySource.Width = 145;
        _recoverySource.Items.AddRange([
            "Live AD",
            "Local cache"
        ]);
        connection.Controls.Add(_recoverySource);

        _connectAdButton.Text = "Connect to AD";
        _connectAdButton.AutoSize = true;
        _connectAdButton.Padding = new Padding(8, 3, 8, 3);
        connection.Controls.Add(_connectAdButton);

        _adConnectionStatus.AutoSize = true;
        _adConnectionStatus.MaximumSize = new Size(620, 0);
        _adConnectionStatus.Margin = new Padding(10, 8, 8, 0);
        connection.Controls.Add(_adConnectionStatus);

        _advancedConnectionButton.Text = "Advanced connection settings...";
        _advancedConnectionButton.AutoSize = true;
        _advancedConnectionButton.Padding = new Padding(6, 1, 6, 1);
        connection.Controls.Add(_advancedConnectionButton);

        root.Controls.Add(connection, 0, 2);

        var scopeFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };
        _startSelectOu.Text = "Select / Change OU...";
        _startSelectOu.AutoSize = true;
        _startSelectOu.Padding = new Padding(6, 2, 6, 2);
        scopeFlow.Controls.Add(_startSelectOu);

        _startOuStatus.AutoSize = true;
        _startOuStatus.MaximumSize = new Size(850, 0);
        _startOuStatus.Margin = new Padding(10, 8, 0, 0);
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
            Font = new Font("Segoe UI Semibold", 9.5F),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 10, 0)
        }, 0, 0);

        _startQuery.Dock = DockStyle.Fill;
        _startQuery.Font = new Font("Segoe UI", 11F);
        _startQuery.PlaceholderText = "PC-12345 or Recovery ID";
        searchGrid.Controls.Add(_startQuery, 1, 0);

        _startSearch.Text = "Search BitLocker";
        _startSearch.AutoSize = true;
        _startSearch.Padding = new Padding(12, 3, 12, 3);
        _startSearch.Font = new Font("Segoe UI Semibold", 9.5F);
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
                await TestDirectoryConnectionAsync();

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

        var keyFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        _startKey.Width = 470;
        _startKey.ReadOnly = true;
        _startKey.UseSystemPasswordChar = true;
        _startKey.Font = new Font("Consolas", 10F);
        keyFlow.Controls.Add(_startKey);

        _startShow.Text = "Reveal Recovery Key";
        _startShow.AutoSize = true;
        keyFlow.Controls.Add(_startShow);

        _startCopy.Text = "Copy Key";
        _startCopy.AutoSize = true;
        keyFlow.Controls.Add(_startCopy);

        grid.Controls.Add(new Label
        {
            Text = "Recovery key:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            Margin = new Padding(0, 8, 10, 0)
        }, 0, 5);
        grid.Controls.Add(keyFlow, 1, 5);

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
            Font = new Font("Segoe UI Semibold", 9F),
            Margin = new Padding(0, 3, 10, 3)
        }, 0, row);

        value.AutoSize = true;
        value.MaximumSize = new Size(900, 0);
        value.Margin = new Padding(0, 3, 0, 3);
        grid.Controls.Add(value, 1, row);
    }

    private void ConfigureAdvancedConnectionGroup()
    {
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

        var save = new Button { Text = "Save settings", AutoSize = true };
        var saveCredential = new Button { Text = "Save credential", AutoSize = true };
        var deleteCredential = new Button { Text = "Delete stored credential", AutoSize = true };
        var clearPassword = new Button { Text = "Clear session password", AutoSize = true };
        actions.Controls.AddRange([
            save,
            saveCredential,
            deleteCredential,
            clearPassword
        ]);

        grid.Controls.Add(actions, 1, 5);
        grid.SetColumnSpan(actions, 3);

        _credentialVaultStatus.AutoSize = true;
        _credentialVaultStatus.MaximumSize = new Size(900, 0);
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
        _unifiedQuery.Font = new Font("Segoe UI", 10.5F);
        search.Controls.Add(_unifiedQuery, 1, 0);

        var searchButton = new Button
        {
            Text = "Search AD + Cloud",
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2)
        };
        search.Controls.Add(searchButton, 2, 0);
        root.Controls.Add(search, 0, 0);

        _unifiedStatus.AutoSize = true;
        _unifiedStatus.MaximumSize = new Size(1050, 0);
        _unifiedStatus.Text =
            "One search combines AD, Entra BitLocker metadata, and Intune inventory.";
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
        _unifiedDetails.Font = new Font("Consolas", 9F);
        _unifiedDetails.Dock = DockStyle.Fill;
        root.Controls.Add(_unifiedDetails, 0, 3);

        var keyPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true
        };
        keyPanel.Controls.Add(new Label
        {
            Text = "Entra Recovery ID:",
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0)
        });
        _deviceRecoveryIds.DropDownStyle = ComboBoxStyle.DropDownList;
        _deviceRecoveryIds.Width = 330;
        keyPanel.Controls.Add(_deviceRecoveryIds);

        _deviceRecoveryKey.Width = 410;
        _deviceRecoveryKey.ReadOnly = true;
        _deviceRecoveryKey.UseSystemPasswordChar = true;
        _deviceRecoveryKey.Font = new Font("Consolas", 9.5F);
        keyPanel.Controls.Add(_deviceRecoveryKey);

        _deviceShowKey.Text = "Reveal Key";
        _deviceShowKey.AutoSize = true;
        keyPanel.Controls.Add(_deviceShowKey);

        _deviceCopyKey.Text = "Copy Key";
        _deviceCopyKey.AutoSize = true;
        keyPanel.Controls.Add(_deviceCopyKey);

        root.Controls.Add(keyPanel, 0, 4);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true
        };
        var rotate = new Button
        {
            Text = "Rotate BitLocker Key in Intune",
            AutoSize = true
        };
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
                _deviceRecoveryContext = null;
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
            Font = new Font("Segoe UI Semibold", 16F),
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
        var save = new Button { Text = "Save Config", AutoSize = true };
        var connect = new Button { Text = "Connect / Test", AutoSize = true };
        var setup = new Button { Text = "First-Run / Repair", AutoSize = true };
        var bootstrap = new Button { Text = "Bootstrap...", AutoSize = true };
        var rollover = new Button { Text = "Rollover Certificate...", AutoSize = true };
        buttons.Controls.AddRange([
            save,
            connect,
            setup,
            bootstrap,
            rollover
        ]);

        root.Controls.Add(buttons, 1, 7);

        _cloudStatus.AutoSize = true;
        _cloudStatus.MaximumSize = new Size(900, 0);
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

        _startSearch.Enabled =
            local ||
            _startScope is not null;

        if (local)
        {
            _adConnectionStatus.Text =
                File.Exists(_config.OutputCsv)
                    ? "Local cache is available. AD connection is not required for this search."
                    : "Local cache is selected, but the recovery export CSV does not exist yet.";

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
        _deviceRecoveryContext = null;
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

            _deviceRecoveryContext =
                context;
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
