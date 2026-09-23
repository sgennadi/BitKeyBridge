namespace BitKeyBridge;

public sealed partial class MainForm
{
    private TabPage BuildSecuritySettingsResponsiveTab()
    {
        var page = new TabPage("Security & Settings");
        var root = CreateVerticalWorkspace();
        page.Controls.Add(root);

        root.Controls.Add(
            CreateSectionTitle(
                "Verified Updates",
                "Update checks use the configured GitHub repository and verified release workflow."));

        var updateGrid = CreateTwoColumnGrid();
        AddGridField(updateGrid, 0, "GitHub repository:", _updateRepository);

        _checkUpdatesOnStart.Text =
            "Check for updates when GUI starts";
        _checkUpdatesOnStart.AutoSize = true;
        updateGrid.Controls.Add(_checkUpdatesOnStart, 1, 1);

        _allowPrereleaseUpdates.Text =
            "Allow prerelease versions";
        _allowPrereleaseUpdates.AutoSize = true;
        updateGrid.Controls.Add(_allowPrereleaseUpdates, 1, 2);

        var updateActions = CreateActionFlow();
        var saveUpdate = NewActionButton("Save Settings");
        var check = NewActionButton("Check Now");
        var install = NewActionButton("Install Verified Update");
        updateActions.Controls.AddRange([
            saveUpdate,
            check,
            install
        ]);
        updateGrid.Controls.Add(updateActions, 1, 3);

        _updateStatus.AutoSize = true;
        _updateStatus.MaximumSize = new Size(900, 0);
        _updateStatus.Text =
            "Update status has not been checked in this GUI session.";
        updateGrid.Controls.Add(_updateStatus, 1, 4);
        root.Controls.Add(updateGrid);

        saveUpdate.Click +=
            (_, _) => SaveOperationsSettings();
        check.Click +=
            async (_, _) =>
                await CheckForUpdatesGuiAsync(
                    silentWhenCurrent: false);
        install.Click +=
            async (_, _) =>
                await InstallLatestUpdateGuiAsync();

        root.Controls.Add(
            CreateSectionTitle(
                "Remote Monitoring / Management API",
                "Optional TLS + bearer-token API. Recovery passwords are never exposed by the API."));

        var remoteGrid = CreateTwoColumnGrid();

        _remoteApiPort.Minimum = 1024;
        _remoteApiPort.Maximum = 65535;
        AddGridField(
            remoteGrid,
            0,
            "TCP port:",
            _remoteApiPort);

        _remoteApiManagement.Text =
            "Allow remote export + Coverage run";
        _remoteApiManagement.AutoSize = true;
        remoteGrid.Controls.Add(
            _remoteApiManagement,
            1,
            1);

        var remoteActions =
            CreateActionFlow();
        var enableRemote =
            NewActionButton(
                "Enable / Reconfigure");
        var rotateToken =
            NewActionButton(
                "Rotate Admin Token");
        var scopedTokens =
            NewActionButton(
                "Scoped Tokens...");
        var disableRemote =
            NewActionButton(
                "Disable");
        remoteActions.Controls.AddRange([
            enableRemote,
            rotateToken,
            scopedTokens,
            disableRemote
        ]);
        remoteGrid.Controls.Add(
            remoteActions,
            1,
            2);

        _remoteApiStatus.AutoSize = true;
        _remoteApiStatus.MaximumSize =
            new Size(900, 0);
        _remoteApiStatus.Text =
            "Remote API is disabled by default.";
        remoteGrid.Controls.Add(
            _remoteApiStatus,
            1,
            3);

        root.Controls.Add(
            remoteGrid);

        enableRemote.Click +=
            (_, _) =>
                EnableOrReconfigureRemoteApi();
        rotateToken.Click +=
            (_, _) =>
                RotateRemoteApiToken();
        scopedTokens.Click +=
            (_, _) =>
                ConfigureRemoteApiScopedTokens();
        disableRemote.Click +=
            (_, _) =>
                DisableRemoteApi();

        root.Controls.Add(
            CreateSectionTitle(
                "Helpdesk Recovery Policy",
                "RBAC, JIT recovery and two-person approval stay optional and disabled unless explicitly configured."));

        var helpdeskGrid =
            CreateTwoColumnGrid();

        _requireRecoveryReference.Text =
            "Require a ticket/reference before recovery-key access";
        _requireRecoveryReference.AutoSize =
            true;
        helpdeskGrid.Controls.Add(
            _requireRecoveryReference,
            1,
            0);

        _suggestRotationAfterRecovery.Text =
            "Suggest Intune key rotation after cloud recovery-key access";
        _suggestRotationAfterRecovery.AutoSize =
            true;
        helpdeskGrid.Controls.Add(
            _suggestRotationAfterRecovery,
            1,
            1);

        var helpdeskActions =
            CreateActionFlow();
        var rbac =
            NewActionButton(
                "RBAC...");
        var privileged =
            NewActionButton(
                "Privileged Access...");
        var saveHelpdesk =
            NewActionButton(
                "Save Helpdesk Settings");
        helpdeskActions.Controls.AddRange([
            rbac,
            privileged,
            saveHelpdesk
        ]);
        helpdeskGrid.Controls.Add(
            helpdeskActions,
            1,
            2);

        root.Controls.Add(
            helpdeskGrid);

        rbac.Click +=
            (_, _) =>
                ConfigureRbacFromGui();
        privileged.Click +=
            (_, _) =>
                ConfigurePrivilegedAccessFromGui();
        saveHelpdesk.Click +=
            (_, _) =>
                SaveOperationsSettings();

        root.Controls.Add(
            CreateSectionTitle(
                "Configuration / Diagnostics / Storage",
                "Backup, restore, diagnostics, incident metadata and storage housekeeping."));

        var maintenance =
            CreateActionFlow();
        var backup =
            NewActionButton(
                "Backup Config");
        var restore =
            NewActionButton(
                "Restore Config");
        var diagnostics =
            NewActionButton(
                "Diagnostics ZIP");
        var incidents =
            NewActionButton(
                "Open Incidents");
        var housekeeping =
            NewActionButton(
                "Housekeeping...");
        maintenance.Controls.AddRange([
            backup,
            restore,
            diagnostics,
            incidents,
            housekeeping
        ]);
        root.Controls.Add(
            maintenance);

        backup.Click +=
            (_, _) =>
                BackupConfigurationGui();
        restore.Click +=
            (_, _) =>
                RestoreConfigurationGui();
        diagnostics.Click +=
            (_, _) =>
                CreateDiagnosticsBundleGui();
        incidents.Click +=
            (_, _) =>
                OpenPath(
                    AppPaths.IncidentsDirectory);
        housekeeping.Click +=
            (_, _) =>
                ConfigureStorageMaintenanceGui();

        return page;
    }

    private TabPage BuildExportAutomationResponsiveTab()
    {
        var page =
            new TabPage(
                "Export & Automation");
        var root =
            CreateVerticalWorkspace();
        page.Controls.Add(
            root);

        root.Controls.Add(
            CreateSectionTitle(
                "Recovery Export Storage",
                "Export is an administrative/offline-cache function and is intentionally separate from the normal Recovery screen."));

        var outputGrid =
            CreateTwoColumnGrid();
        AddGridField(
            outputGrid,
            0,
            "Output root:",
            _outputRoot);
        AddGridField(
            outputGrid,
            1,
            "Subdirectory:",
            _outputSubdirectory);

        var outputActions =
            CreateActionFlow();
        var saveOutput =
            NewActionButton(
                "Save Export Settings");
        var secureOutput =
            NewActionButton(
                "Secure Output...");
        outputActions.Controls.AddRange([
            saveOutput,
            secureOutput
        ]);
        outputGrid.Controls.Add(
            outputActions,
            1,
            2);

        root.Controls.Add(
            outputGrid);

        saveOutput.Click +=
            (_, _) =>
                SaveDirectorySettings(
                    showConfirmation: true);
        secureOutput.Click +=
            (_, _) =>
                RunSecureOutputWizard();

        root.Controls.Add(
            CreateSectionTitle(
                "Export Scopes",
                "Checked OUs are used by export and DC comparison. With no checked scope, configured defaults are used."));

        _scopes.CheckOnClick = true;
        _scopes.HorizontalScrollbar = true;
        _scopes.Dock = DockStyle.Top;
        _scopes.Height = 145;
        root.Controls.Add(
            _scopes);

        var scopeActions =
            CreateActionFlow();
        var addOu =
            NewActionButton(
                "Add OU...");
        var defaults =
            NewActionButton(
                "Use Defaults");
        var remove =
            NewActionButton(
                "Remove Selected");
        var checkAll =
            NewActionButton(
                "Check All");
        var saveDefaults =
            NewActionButton(
                "Save as Defaults");
        scopeActions.Controls.AddRange([
            addOu,
            defaults,
            remove,
            checkAll,
            saveDefaults
        ]);
        root.Controls.Add(
            scopeActions);

        addOu.Click +=
            async (_, _) =>
                await AddOuAsync();
        defaults.Click +=
            (_, _) =>
                LoadDefaultScopes();
        remove.Click +=
            (_, _) =>
            {
                if (_scopes.SelectedIndex >= 0)
                {
                    _scopes.Items.RemoveAt(
                        _scopes.SelectedIndex);
                }
            };
        checkAll.Click +=
            (_, _) =>
            {
                for (var i = 0;
                     i < _scopes.Items.Count;
                     i++)
                {
                    _scopes.SetItemChecked(
                        i,
                        true);
                }
            };
        saveDefaults.Click +=
            (_, _) =>
                SaveSelectedScopesAsDefaults();

        root.Controls.Add(
            CreateSectionTitle(
                "Run Export",
                "The normal helpdesk Recovery flow does not require this export. Use it for offline cache / automation only."));

        var runActions =
            CreateActionFlow();

        _runExport.Text =
            "Run Export";
        _runExport.AutoSize =
            true;
        _runExport.Padding =
            new Padding(
                8,
                2,
                8,
                2);

        _dryRun.Text =
            "Dry Run";
        _dryRun.AutoSize =
            true;
        _dryRun.Padding =
            new Padding(
                8,
                2,
                8,
                2);

        _forcePublish.Text =
            "Override publish guards (row drop / scope change)";
        _forcePublish.AutoSize =
            true;

        runActions.Controls.AddRange([
            _runExport,
            _dryRun,
            _forcePublish
        ]);
        root.Controls.Add(
            runActions);

        _lastSuccess.AutoSize =
            true;
        _lastSuccess.MaximumSize =
            new Size(1000, 0);
        root.Controls.Add(
            _lastSuccess);

        _exportSummary.AutoSize =
            true;
        _exportSummary.MaximumSize =
            new Size(1000, 0);
        _exportSummary.Text =
            "No run in this GUI session yet.";
        root.Controls.Add(
            _exportSummary);

        _scopeResults.View =
            View.Details;
        _scopeResults.FullRowSelect =
            true;
        _scopeResults.GridLines =
            true;
        _scopeResults.Dock =
            DockStyle.Top;
        _scopeResults.Height =
            180;
        AddColumns(
            _scopeResults,
            ("Scope", 180),
            ("Status", 80),
            ("Objects", 80),
            ("Valid", 70),
            ("Duplicates", 85),
            ("Invalid", 70),
            ("Error", 520));
        root.Controls.Add(
            _scopeResults);

        _exportLog.ReadOnly =
            true;
        _exportLog.WordWrap =
            false;
        _exportLog.Font =
            new Font(
                "Consolas",
                9F);
        _exportLog.Dock =
            DockStyle.Top;
        _exportLog.Height =
            220;
        root.Controls.Add(
            _exportLog);

        _runExport.Click +=
            async (_, _) =>
                await RunExportAsync(
                    false);
        _dryRun.Click +=
            async (_, _) =>
                await RunExportAsync(
                    true);

        return page;
    }

    private TabPage BuildCoverageResponsiveTab()
    {
        var page =
            new TabPage(
                "Coverage");
        var root =
            CreateVerticalWorkspace();
        page.Controls.Add(
            root);

        root.Controls.Add(
            CreateSectionTitle(
                "BitLocker Coverage",
                "AD + Entra + Intune metadata only. Recovery passwords are never requested by this report."));

        var controls =
            CreateActionFlow();

        var run =
            NewActionButton(
                "Run Coverage");
        var export =
            NewActionButton(
                "Export Visible CSV");
        var policy =
            NewActionButton(
                "Policy...");

        controls.Controls.AddRange([
            run,
            export
        ]);

        controls.Controls.Add(
            NewInlineLabel(
                "Filter:"));

        _coverageFilter.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _coverageFilter.Width =
            180;
        _coverageFilter.Items.AddRange([
            "All devices",
            "No recovery key",
            "AD only",
            "Entra only",
            "AD + Entra",
            "Multiple recovery keys",
            "Intune not encrypted",
            "Intune stale",
            "Old cloud key"
        ]);
        _coverageFilter.SelectedIndex =
            0;
        controls.Controls.Add(
            _coverageFilter);

        controls.Controls.Add(
            NewInlineLabel(
                "Intune stale days:"));
        _coverageStaleDays.Minimum =
            1;
        _coverageStaleDays.Maximum =
            3650;
        _coverageStaleDays.Width =
            72;
        _coverageStaleDays.Value =
            Math.Clamp(
                _config.CoverageStaleIntuneDays,
                1,
                3650);
        controls.Controls.Add(
            _coverageStaleDays);

        controls.Controls.Add(
            NewInlineLabel(
                "Old key days:"));
        _coverageOldKeyDays.Minimum =
            1;
        _coverageOldKeyDays.Maximum =
            3650;
        _coverageOldKeyDays.Width =
            72;
        _coverageOldKeyDays.Value =
            Math.Clamp(
                _config.CoverageOldCloudKeyDays,
                1,
                3650);
        controls.Controls.Add(
            _coverageOldKeyDays);
        controls.Controls.Add(
            policy);

        root.Controls.Add(
            controls);

        _coverageSummary.AutoSize =
            true;
        _coverageSummary.Font =
            new Font(
                "Segoe UI Semibold",
                9.5F);
        _coverageSummary.MaximumSize =
            new Size(1000, 0);
        _coverageSummary.Text =
            "Coverage has not been generated yet.";
        root.Controls.Add(
            _coverageSummary);

        _coverageResults.View =
            View.Details;
        _coverageResults.FullRowSelect =
            true;
        _coverageResults.GridLines =
            true;
        _coverageResults.Dock =
            DockStyle.Top;
        _coverageResults.Height =
            430;
        AddColumns(
            _coverageResults,
            ("Computer", 150),
            ("Coverage", 95),
            ("AD Keys", 60),
            ("Entra Keys", 70),
            ("Intune", 52),
            ("Encrypted", 70),
            ("Compliance", 85),
            ("Last Sync", 130),
            ("AD Last Logon", 130),
            ("Newest Cloud Key", 135),
            ("Serial", 105),
            ("User / UPN", 155),
            ("Model", 125));
        root.Controls.Add(
            _coverageResults);

        _coverageStatus.AutoSize =
            true;
        _coverageStatus.MaximumSize =
            new Size(1000, 0);
        _coverageStatus.Text =
            "Metadata-only report: recovery passwords are never requested.";
        root.Controls.Add(
            _coverageStatus);

        run.Click +=
            async (_, _) =>
                await RunCoverageAsync();
        export.Click +=
            (_, _) =>
                ExportCoverageCsv();
        policy.Click +=
            (_, _) =>
                ConfigureCoveragePolicyFromGui();
        _coverageFilter.SelectedIndexChanged +=
            (_, _) =>
                RenderCoverageRows();

        return page;
    }

    private TabPage BuildServiceHealthResponsiveTab()
    {
        var page =
            new TabPage(
                "Service & Health");
        var root =
            CreateVerticalWorkspace();
        page.Controls.Add(
            root);

        root.Controls.Add(
            CreateSectionTitle(
                "BitKeyBridge Health & Service",
                "Service automation, health endpoint and scheduled coverage."));

        _dashboardStatus.AutoSize =
            true;
        _dashboardStatus.Font =
            new Font(
                "Segoe UI Semibold",
                11F);
        _dashboardStatus.MaximumSize =
            new Size(1000, 0);
        _dashboardStatus.Text =
            "Loading health status...";
        root.Controls.Add(
            _dashboardStatus);

        var serviceActions =
            CreateActionFlow();
        var install =
            NewActionButton(
                "Install / Update");
        var start =
            NewActionButton(
                "Start");
        var stop =
            NewActionButton(
                "Stop");
        var uninstall =
            NewActionButton(
                "Uninstall");
        var refresh =
            NewActionButton(
                "Refresh");
        var openHealth =
            NewActionButton(
                "Open /health");
        serviceActions.Controls.AddRange([
            install,
            start,
            stop,
            uninstall,
            refresh,
            openHealth
        ]);
        root.Controls.Add(
            serviceActions);

        var settings =
            CreateTwoColumnGrid();

        _serviceInterval.Minimum =
            1;
        _serviceInterval.Maximum =
            10080;
        AddGridField(
            settings,
            0,
            "Export interval (minutes):",
            _serviceInterval);

        _healthPort.Minimum =
            1024;
        _healthPort.Maximum =
            65535;
        AddGridField(
            settings,
            1,
            "Health endpoint port:",
            _healthPort);

        _healthEnabled.Text =
            "Enable loopback health endpoint";
        _healthEnabled.AutoSize =
            true;
        settings.Controls.Add(
            _healthEnabled,
            1,
            2);

        _serviceRunOnStart.Text =
            "Run export when service starts";
        _serviceRunOnStart.AutoSize =
            true;
        settings.Controls.Add(
            _serviceRunOnStart,
            1,
            3);

        _serviceCoverageEnabled.Text =
            "Enable scheduled Coverage";
        _serviceCoverageEnabled.AutoSize =
            true;
        settings.Controls.Add(
            _serviceCoverageEnabled,
            1,
            4);

        _serviceCoverageInterval.Minimum =
            15;
        _serviceCoverageInterval.Maximum =
            10080;
        AddGridField(
            settings,
            5,
            "Coverage interval (minutes):",
            _serviceCoverageInterval);

        _serviceCoverageRunOnStart.Text =
            "Run Coverage when service starts";
        _serviceCoverageRunOnStart.AutoSize =
            true;
        settings.Controls.Add(
            _serviceCoverageRunOnStart,
            1,
            6);

        var serviceSettingsActions =
            CreateActionFlow();
        var saveSettings =
            NewActionButton(
                "Save Service Settings");
        var saveMachineCloud =
            NewActionButton(
                "Save Cloud for Service");
        var deleteMachineCloud =
            NewActionButton(
                "Delete Machine Cloud");
        var repairMachineKey =
            NewActionButton(
                "Repair Cert Access");
        serviceSettingsActions.Controls.AddRange([
            saveSettings,
            saveMachineCloud,
            deleteMachineCloud,
            repairMachineKey
        ]);
        settings.Controls.Add(
            serviceSettingsActions,
            1,
            7);

        _machineCloudStatus.AutoSize =
            true;
        _machineCloudStatus.MaximumSize =
            new Size(900, 0);
        _machineCloudStatus.Text =
            "Machine cloud config has not been checked.";
        settings.Controls.Add(
            _machineCloudStatus,
            1,
            8);

        root.Controls.Add(
            settings);

        root.Controls.Add(
            CreateSectionTitle(
                "Service Identity",
                "Use LocalSystem, gMSA/managed account, or a domain account."));

        var identity =
            CreateTwoColumnGrid();

        _serviceIdentityMode.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _serviceIdentityMode.Items.AddRange([
            "LocalSystem",
            "gMSA / Managed Account",
            "Domain Account"
        ]);
        AddGridField(
            identity,
            0,
            "Identity:",
            _serviceIdentityMode);
        AddGridField(
            identity,
            1,
            "Account:",
            _serviceIdentityAccount);

        _serviceIdentityPassword.UseSystemPasswordChar =
            true;
        AddGridField(
            identity,
            2,
            "Password:",
            _serviceIdentityPassword);

        var applyIdentity =
            NewActionButton(
                "Apply Identity");
        identity.Controls.Add(
            applyIdentity,
            1,
            3);

        _serviceIdentityStatus.AutoSize =
            true;
        _serviceIdentityStatus.MaximumSize =
            new Size(900, 0);
        _serviceIdentityStatus.Text =
            "Service identity has not been queried.";
        identity.Controls.Add(
            _serviceIdentityStatus,
            1,
            4);

        root.Controls.Add(
            identity);

        _dashboardDetails.ReadOnly =
            true;
        _dashboardDetails.Font =
            new Font(
                "Consolas",
                9.5F);
        _dashboardDetails.Dock =
            DockStyle.Top;
        _dashboardDetails.Height =
            300;
        root.Controls.Add(
            _dashboardDetails);

        refresh.Click +=
            (_, _) =>
                RefreshDashboard();
        install.Click +=
            (_, _) =>
                InstallOrUpdateService();
        start.Click +=
            (_, _) =>
                StartServiceFromGui();
        stop.Click +=
            (_, _) =>
                StopServiceFromGui();
        uninstall.Click +=
            (_, _) =>
                UninstallServiceFromGui();
        openHealth.Click +=
            (_, _) =>
                OpenHealthEndpoint();
        saveSettings.Click +=
            (_, _) =>
                SaveDashboardSettings(
                    restartRunningService: true);
        saveMachineCloud.Click +=
            (_, _) =>
                SaveMachineCloudFromGui();
        deleteMachineCloud.Click +=
            (_, _) =>
                DeleteMachineCloudFromGui();
        repairMachineKey.Click +=
            (_, _) =>
                RepairMachineCertificateAccessFromGui();
        applyIdentity.Click +=
            (_, _) =>
                ApplyServiceIdentityFromGui();
        _serviceIdentityMode.SelectedIndexChanged +=
            (_, _) =>
                UpdateServiceIdentityUi();

        return page;
    }

    private TabPage BuildDomainControllersResponsiveTab()
    {
        var page =
            new TabPage(
                "Domain Controllers");
        var root =
            new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 4
            };
        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                48F));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                52F));
        page.Controls.Add(
            root);

        root.Controls.Add(
            CreateSectionTitle(
                "Domain Controller Comparison",
                "Discovers domain controllers dynamically and compares BitLocker object counts / replication health."));

        var actions =
            CreateActionFlow();

        _dcTest.Text =
            "Discover and Test DCs";
        _dcTest.AutoSize =
            true;
        _dcTest.Padding =
            new Padding(
                8,
                2,
                8,
                2);
        actions.Controls.Add(
            _dcTest);
        root.Controls.Add(
            actions);

        _dcResults.View =
            View.Details;
        _dcResults.FullRowSelect =
            true;
        _dcResults.GridLines =
            true;
        _dcResults.Dock =
            DockStyle.Fill;
        AddColumns(
            _dcResults,
            ("DC", 120),
            ("Site", 140),
            ("RODC", 60),
            ("Reachable", 75),
            ("Status", 80),
            ("Objects", 75),
            ("Delta", 60),
            ("Repl errors", 80),
            ("Warnings", 75),
            ("Max age h", 80),
            ("IPv4", 105),
            ("OS", 190));
        root.Controls.Add(
            _dcResults);

        _dcDetails.ReadOnly =
            true;
        _dcDetails.Font =
            new Font(
                "Consolas",
                9F);
        _dcDetails.Dock =
            DockStyle.Fill;
        root.Controls.Add(
            _dcDetails);

        _dcTest.Click +=
            async (_, _) =>
                await RunDcTestAsync();

        return page;
    }

    private TabPage BuildAuditResponsiveTab()
    {
        var page =
            new TabPage(
                "Audit");
        var root =
            new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 4
            };
        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));
        page.Controls.Add(
            root);

        var actions =
            CreateActionFlow();
        var refresh =
            NewActionButton(
                "Refresh");
        var open =
            NewActionButton(
                "Open audit.jsonl");
        var verify =
            NewActionButton(
                "Verify Chain");
        var setupSigning =
            NewActionButton(
                "Setup Signing");
        var signNow =
            NewActionButton(
                "Sign Now");
        var verifySignature =
            NewActionButton(
                "Verify Signature");
        var rolloverSigning =
            NewActionButton(
                "Rollover Signing");
        var disableSigning =
            NewActionButton(
                "Disable Signing");
        var verifyIncident =
            NewActionButton(
                "Incident...");
        actions.Controls.AddRange([
            refresh,
            open,
            verify,
            setupSigning,
            signNow,
            verifySignature,
            rolloverSigning,
            disableSigning,
            verifyIncident
        ]);
        root.Controls.Add(
            actions);

        root.Controls.Add(
            CreateSectionTitle(
                "Tamper-Evident Recovery Audit",
                "Recovery passwords are never written to audit. Entries are SHA-256 chained and can use signed checkpoints."));

        _auditSigningStatus.AutoSize =
            true;
        _auditSigningStatus.MaximumSize =
            new Size(1000, 0);
        _auditSigningStatus.Text =
            "Audit signing status has not been checked.";
        root.Controls.Add(
            _auditSigningStatus);

        _auditResults.View =
            View.Details;
        _auditResults.FullRowSelect =
            true;
        _auditResults.GridLines =
            true;
        _auditResults.Dock =
            DockStyle.Fill;
        AddColumns(
            _auditResults,
            ("Time (UTC)", 155),
            ("User", 165),
            ("Host", 110),
            ("Action", 175),
            ("Result", 75),
            ("Computer", 135),
            ("Recovery ID", 230),
            ("Source", 70),
            ("Auth", 90),
            ("Reference", 130),
            ("Session", 230),
            ("Reason", 180),
            ("Details", 260));
        root.Controls.Add(
            _auditResults);

        refresh.Click +=
            (_, _) =>
            {
                RefreshAudit();
                RefreshAuditSigningStatus();
            };
        open.Click +=
            (_, _) =>
                OpenPath(
                    AppPaths.AuditLogFile,
                    "notepad.exe");
        verify.Click +=
            (_, _) =>
                VerifyAuditIntegrityGui();
        setupSigning.Click +=
            (_, _) =>
                SetupAuditSigningGui();
        signNow.Click +=
            (_, _) =>
                SignAuditCheckpointGui();
        verifySignature.Click +=
            (_, _) =>
                VerifyAuditSignatureGui();
        rolloverSigning.Click +=
            (_, _) =>
                RolloverAuditSigningGui();
        disableSigning.Click +=
            (_, _) =>
                DisableAuditSigningGui();
        verifyIncident.Click +=
            (_, _) =>
                VerifyRecoveryIncidentGui();

        RefreshAudit();
        RefreshAuditSigningStatus();

        return page;
    }

    private static FlowLayoutPanel CreateVerticalWorkspace()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18)
        };
    }

    private static Control CreateSectionTitle(
        string title,
        string description)
    {
        var panel =
            new TableLayoutPanel
            {
                AutoSize = true,
                Width = 1040,
                ColumnCount = 1,
                Margin = new Padding(
                    0,
                    4,
                    0,
                    8)
            };
        panel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        panel.Controls.Add(
            new Label
            {
                Text = title,
                Font =
                    new Font(
                        "Segoe UI Semibold",
                        14F),
                AutoSize = true,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        2)
            },
            0,
            0);

        panel.Controls.Add(
            new Label
            {
                Text = description,
                AutoSize = true,
                MaximumSize =
                    new Size(
                        1000,
                        0)
            },
            0,
            1);

        return panel;
    }

    private static TableLayoutPanel CreateTwoColumnGrid()
    {
        var grid =
            new TableLayoutPanel
            {
                AutoSize = true,
                Width = 1040,
                ColumnCount = 2,
                Margin = new Padding(
                    0,
                    0,
                    0,
                    12)
            };

        grid.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        grid.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        return grid;
    }

    private static void AddGridField(
        TableLayoutPanel grid,
        int row,
        string caption,
        Control control)
    {
        grid.Controls.Add(
            new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor =
                    AnchorStyles.Left,
                Margin =
                    new Padding(
                        0,
                        8,
                        12,
                        4)
            },
            0,
            row);

        control.Dock =
            DockStyle.Top;
        control.Margin =
            new Padding(
                0,
                3,
                0,
                5);
        grid.Controls.Add(
            control,
            1,
            row);
    }

    private static FlowLayoutPanel CreateActionFlow()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection =
                FlowDirection.LeftToRight,
            WrapContents = true,
            Margin =
                new Padding(
                    0,
                    4,
                    0,
                    8)
        };
    }

    private static Button NewActionButton(
        string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Padding =
                new Padding(
                    7,
                    2,
                    7,
                    2),
            Margin =
                new Padding(
                    0,
                    0,
                    8,
                    6)
        };
    }

    private static Label NewInlineLabel(
        string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin =
                new Padding(
                    8,
                    8,
                    4,
                    0)
        };
    }
}
