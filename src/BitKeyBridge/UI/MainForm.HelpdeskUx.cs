namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly HashSet<string> _rotationSuggestionSessions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly UiStatusLabel _homeSelectionSummary = new();
    private readonly Button _homeRevealBitLockerButton = new();
    private readonly Button _homeCopyBitLockerButton = new();
    private readonly Button _homeRotateBitLockerButton = new();
    private readonly Button _homeDeviceDetailsButton = new();
    private readonly UiStatusLabel _secretLifetimeStatus = new();
    private readonly UiStatusLabel _setupStatus = new();
    private readonly UiStatusLabel _startSetupStatus = new();
    private readonly Button _clearSecretNow = new();
    private System.Windows.Forms.Timer? _secretLifetimeTimer;
    private DateTime? _secretVisibleUntilUtc;
    private DateTime? _clipboardClearAtUtc;

    private readonly TextBox _auditFilter = new();
    private readonly ComboBox _auditFilterField = new();
    private List<AuditEntry> _auditRows = [];

    private readonly ListView _accessHealthResults = new();
    private readonly UiStatusLabel _accessHealthStatus = new();
    private readonly Button _accessHealthRun = new();
    private readonly ProgressBar _accessHealthProgress = new();

    private readonly ComboBox _helpdeskProfile = new();
    private readonly UiStatusLabel _helpdeskProfileStatus = new();
    private readonly TextBox _recoveryReferencePatternSetting = new();
    private readonly TextBox _recoveryReferenceExampleSetting = new();
    private readonly NumericUpDown _secretDisplaySecondsSetting = new();
    private readonly NumericUpDown _clipboardSecondsSetting = new();
    private readonly TextBox _accessHealthComputer = new();
    private readonly DateTimePicker _auditFrom = new();
    private readonly DateTimePicker _auditTo = new();
    private readonly ListView _updateHistoryResults = new();
    private readonly Button _rollbackUpdateButton = new();
    private readonly CheckBox _requireTrustedUpdateSignature = new();
    private readonly TextBox _trustedUpdatePublisher = new();

    private void RefreshUpdateHistory()
    {
        if (_updateHistoryResults.IsDisposed)
            return;

        _updateHistoryResults.BeginUpdate();
        try
        {
            _updateHistoryResults.Items.Clear();

            foreach (var row in
                     new UpdateHistoryService()
                         .Read(
                             100))
            {
                var item =
                    new ListViewItem(
                        row.TimestampUtc.ToString(
                            "yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(
                    row.Action);
                item.SubItems.Add(
                    row.FromVersion);
                item.SubItems.Add(
                    row.ToVersion);
                item.SubItems.Add(
                    row.Result);
                item.SubItems.Add(
                    row.Automatic
                        ? "Automatic"
                        : "Manual");
                item.SubItems.Add(
                    row.Publisher);
                item.SubItems.Add(
                    row.Details);
                _updateHistoryResults.Items.Add(
                    item);
            }
        }
        finally
        {
            _updateHistoryResults.EndUpdate();
        }

        var current =
            Environment.ProcessPath;
        _rollbackUpdateButton.Enabled =
            !string.IsNullOrWhiteSpace(
                current) &&
            File.Exists(
                current + ".bak");
    }

    private void RollbackPreviousUpdateGui()
    {
        var current =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(
                current) ||
            !File.Exists(
                current + ".bak"))
        {
            UiStyle.SetStatus(
                _updateStatus,
                "No retained previous executable is available for rollback.",
                UiStatusKind.Warning);
            RefreshUpdateHistory();
            return;
        }

        var previousVersion =
            System.Diagnostics.FileVersionInfo
                .GetVersionInfo(
                    current + ".bak")
                .FileVersion ??
            "unknown";

        var answer =
            MessageBox.Show(
                this,
                $"Rollback BitKeyBridge to {previousVersion}?{Environment.NewLine}{Environment.NewLine}" +
                "The current executable will become the next rollback backup and BitKeyBridge will restart.",
                "Rollback BitKeyBridge",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        try
        {
            using var updater =
                new UpdateService(
                    _config);

            updater.LaunchRollbackHelper(
                restartGui:
                    true);

            _audit.Write(
                "LaunchUpdateRollback",
                source:
                    "Local",
                details:
                    $"TargetVersion={previousVersion}");

            Close();
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _updateStatus,
                "Rollback could not be started: " +
                ex.Message,
                UiStatusKind.Error);

            new UpdateHistoryService()
                .Append(
                    new UpdateHistoryEntry
                    {
                        Action =
                            "Rollback",
                        FromVersion =
                            GetType().Assembly.GetName().Version?.ToString(3) ??
                            string.Empty,
                        ToVersion =
                            previousVersion,
                        Result =
                            "FailedToStart",
                        Details =
                            DiagnosticRedaction.Sanitize(
                                ex.Message)
                    });
        }
    }

    private TabPage BuildHelpdeskBasicsResponsiveTab()
    {
        var page =
            new TabPage(
                "Helpdesk")
            {
                Name =
                    "HelpdeskBasicsTab"
            };

        var root =
            CreateVerticalWorkspace();
        page.Controls.Add(
            root);

        root.Controls.Add(
            CreateSectionTitle(
                "Helpdesk Basics",
                "Normal recovery work stays simple. Enterprise policy gates are opt-in and live under Advanced."));

        var setupActions =
            CreateActionFlow();
        var refreshSetup =
            NewActionButton(
                "Refresh Setup Status");
        var quickDiagnostics =
            NewActionButton(
                "Create Support Bundle");
        var openSupport =
            NewActionButton(
                "Open Support Bundles");
        setupActions.Controls.AddRange([
            refreshSetup,
            quickDiagnostics,
            openSupport
        ]);
        root.Controls.Add(
            setupActions);

        UiStyle.ConfigureStatusLabel(
            _setupStatus);
        _setupStatus.Name =
            "SetupStatus";
        _setupStatus.AccessibleName =
            "BitKeyBridge setup status";
        _setupStatus.MaximumSize =
            new Size(
                1000,
                0);
        root.Controls.Add(
            _setupStatus);

        root.Controls.Add(
            CreateSectionTitle(
                "Environment Profile",
                "Profiles store non-secret AD/Cloud endpoints and preferences only. Passwords, tokens and recovery secrets are never saved in a profile."));

        var profileGrid =
            CreateTwoColumnGrid();

        _helpdeskProfile.DropDownStyle =
            ComboBoxStyle.DropDownList;
        AddGridField(
            profileGrid,
            0,
            "Profile:",
            _helpdeskProfile);

        var profileActions =
            CreateActionFlow();
        var applyProfile =
            NewActionButton(
                "Apply Profile");
        var saveProfile =
            NewActionButton(
                "Save Current As...");
        var deleteProfile =
            NewActionButton(
                "Delete Profile");
        profileActions.Controls.AddRange([
            applyProfile,
            saveProfile,
            deleteProfile
        ]);
        profileGrid.Controls.Add(
            profileActions,
            1,
            1);

        UiStyle.ConfigureStatusLabel(
            _helpdeskProfileStatus);
        _helpdeskProfileStatus.MaximumSize =
            new Size(
                900,
                0);
        profileGrid.Controls.Add(
            _helpdeskProfileStatus,
            1,
            2);
        root.Controls.Add(
            profileGrid);

        root.Controls.Add(
            CreateSectionTitle(
                "Recovery Access",
                "Ticket/reference is optional by default. Turn it on only when your helpdesk process requires a case number."));

        var recoveryGrid =
            CreateTwoColumnGrid();

        _requireRecoveryReference.Text =
            "Require a ticket/reference before recovery-key access";
        _requireRecoveryReference.AutoSize =
            true;
        recoveryGrid.Controls.Add(
            _requireRecoveryReference,
            1,
            0);

        _suggestRotationAfterRecovery.Text =
            "Suggest Intune key rotation after cloud recovery-key access";
        _suggestRotationAfterRecovery.AutoSize =
            true;
        recoveryGrid.Controls.Add(
            _suggestRotationAfterRecovery,
            1,
            1);

        _recoveryReferencePatternSetting.PlaceholderText =
            "Optional regex, e.g. ^INC-[0-9]+$";
        AddGridField(
            recoveryGrid,
            2,
            "Ticket format:",
            _recoveryReferencePatternSetting);

        _recoveryReferenceExampleSetting.PlaceholderText =
            "INC-12345";
        AddGridField(
            recoveryGrid,
            3,
            "Ticket example:",
            _recoveryReferenceExampleSetting);

        _secretDisplaySecondsSetting.Minimum =
            15;
        _secretDisplaySecondsSetting.Maximum =
            3600;
        AddGridField(
            recoveryGrid,
            4,
            "Secret lifetime (sec):",
            _secretDisplaySecondsSetting);

        _clipboardSecondsSetting.Minimum =
            5;
        _clipboardSecondsSetting.Maximum =
            600;
        AddGridField(
            recoveryGrid,
            5,
            "Clipboard lifetime (sec):",
            _clipboardSecondsSetting);

        var save =
            NewActionButton(
                "Save Helpdesk Settings");
        recoveryGrid.Controls.Add(
            save,
            1,
            6);
        root.Controls.Add(
            recoveryGrid);

        refreshSetup.Click +=
            (_, _) =>
                RefreshSetupStatus();
        quickDiagnostics.Click +=
            (_, _) =>
                CreateQuickDiagnosticsBundleGui();
        openSupport.Click +=
            (_, _) =>
                OpenSupportBundlesFolder();
        applyProfile.Click +=
            (_, _) =>
                ApplySelectedHelpdeskProfile();
        saveProfile.Click +=
            (_, _) =>
                SaveCurrentHelpdeskProfile();
        deleteProfile.Click +=
            (_, _) =>
                DeleteSelectedHelpdeskProfile();
        save.Click +=
            (_, _) =>
                SaveHelpdeskBasics();

        if (!_layoutSelfTest)
        {
            RefreshHelpdeskProfiles();
            RefreshSetupStatus();
        }

        return page;
    }

    private TabPage BuildAccessHealthResponsiveTab()
    {
        var page =
            new TabPage(
                "Access Health")
            {
                Name =
                    "AccessHealthTab"
            };

        var root =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Fill,
                Padding =
                    new Padding(
                        UiStyle.PagePadding),
                ColumnCount =
                    1,
                RowCount =
                    5
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
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));
        page.Controls.Add(
            root);

        root.Controls.Add(
            CreateSectionTitle(
                "Security / Access Health Center",
                "Runs metadata-only checks for AD, BitLocker, LAPS, Graph and Intune. It never requests a BitLocker recovery password or LAPS password."),
            0,
            0);

        var query =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    3
            };
        query.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        query.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        query.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        query.Controls.Add(
            new Label
            {
                Text =
                    "Test computer (optional):",
                AutoSize =
                    true,
                Anchor =
                    AnchorStyles.Left,
                Margin =
                    new Padding(
                        0,
                        7,
                        8,
                        0)
            },
            0,
            0);

        _accessHealthComputer.Dock =
            DockStyle.Fill;
        _accessHealthComputer.PlaceholderText =
            "CLINIC-300489";
        query.Controls.Add(
            _accessHealthComputer,
            1,
            0);

        _accessHealthRun.Text =
            "Run Access Checks";
        UiStyle.ConfigureActionButton(
            _accessHealthRun);
        query.Controls.Add(
            _accessHealthRun,
            2,
            0);
        root.Controls.Add(
            query,
            0,
            1);

        _accessHealthProgress.Dock =
            DockStyle.Top;
        _accessHealthProgress.Style =
            ProgressBarStyle.Marquee;
        _accessHealthProgress.Visible =
            false;
        root.Controls.Add(
            _accessHealthProgress,
            0,
            2);

        UiStyle.ConfigureStatusLabel(
            _accessHealthStatus);
        _accessHealthStatus.MaximumSize =
            new Size(
                1000,
                0);
        UiStyle.SetStatus(
            _accessHealthStatus,
            "Access checks have not been run.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _accessHealthStatus,
            0,
            3);

        _accessHealthResults.View =
            View.Details;
        _accessHealthResults.FullRowSelect =
            true;
        _accessHealthResults.GridLines =
            true;
        _accessHealthResults.Dock =
            DockStyle.Fill;
        AddColumns(
            _accessHealthResults,
            ("Source", 100),
            ("Check", 210),
            ("State", 95),
            ("Detail / How to fix", 620));
        root.Controls.Add(
            _accessHealthResults,
            0,
            4);

        _accessHealthRun.Click +=
            async (_, _) =>
                await RunAccessHealthAsync();

        return page;
    }

    private void AddAccessHealthRow(
        string source,
        string check,
        string state,
        string detail)
    {
        var item =
            new ListViewItem(
                source);
        item.SubItems.Add(
            check);
        item.SubItems.Add(
            state);
        item.SubItems.Add(
            detail);
        _accessHealthResults.Items.Add(
            item);
    }

    private async Task RunAccessHealthAsync()
    {
        if (_accessHealthRun.Enabled ==
            false)
        {
            return;
        }

        _accessHealthRun.Enabled =
            false;
        _accessHealthProgress.Visible =
            true;
        _accessHealthResults.Items.Clear();

        UiStyle.SetStatus(
            _accessHealthStatus,
            "Running metadata-only access checks...",
            UiStatusKind.Busy);

        var errors =
            0;
        var warnings =
            0;
        var computer =
            _accessHealthComputer.Text.Trim();

        try
        {
            string? dc =
                null;

            try
            {
                dc =
                    await Task.Run(
                        () =>
                            _ad.GetPreferredWritableDc());

                var root =
                    await Task.Run(
                        () =>
                            _ad.TestConnection(
                                dc));

                AddAccessHealthRow(
                    "AD",
                    "LDAP bind",
                    "OK",
                    $"Connected to {dc}; naming context {root.GetValueOrDefault("defaultNamingContext", "-")}.");
            }
            catch (Exception ex)
            {
                errors++;
                AddAccessHealthRow(
                    "AD",
                    "LDAP bind",
                    "Failed",
                    DiagnosticRedaction.Sanitize(
                        ex.Message) +
                    " Check Start > AD connection and the selected credentials.");
            }

            if (!string.IsNullOrWhiteSpace(
                    computer) &&
                !string.IsNullOrWhiteSpace(
                    dc))
            {
                try
                {
                    var adComputer =
                        await Task.Run(
                            () =>
                                _ad.FindComputerByName(
                                    dc,
                                    computer));

                    var metadata =
                        adComputer is null
                            ? []
                            : await Task.Run(
                                () =>
                                    _ad.GetRecoveryMetadataForComputer(
                                        dc,
                                        adComputer.DistinguishedName));

                    AddAccessHealthRow(
                        "AD",
                        "BitLocker metadata",
                        metadata.Count > 0
                            ? "OK"
                            : "Warning",
                        metadata.Count > 0
                            ? $"{metadata.Count} recovery object(s) visible. Password attributes were not read."
                            : "No recovery object is visible. Check backup policy, computer name and msFVE recovery-object read permissions.");

                    if (metadata.Count == 0)
                        warnings++;
                }
                catch (Exception ex)
                {
                    errors++;
                    AddAccessHealthRow(
                        "AD",
                        "BitLocker metadata",
                        "Failed",
                        DiagnosticRedaction.Sanitize(
                            ex.Message) +
                        " Check computer-object/recovery-object read permissions.");
                }

                try
                {
                    var credential =
                        AdSessionCredentials
                            .CreateNetworkCredential(
                                _config);
                    var laps =
                        await Task.Run(
                            () =>
                                new LapsDirectoryService(
                                    _config,
                                    credential)
                                .CheckAccess(
                                    computer));

                    foreach (var check in
                             laps.Checks)
                    {
                        var state =
                            check.State.ToString();

                        AddAccessHealthRow(
                            "AD LAPS",
                            check.Name,
                            state,
                            check.Detail);

                        if (check.State ==
                            LapsAccessState.Failed)
                        {
                            errors++;
                        }
                        else if (check.State is
                                 LapsAccessState.NotDetected or
                                 LapsAccessState.NotProbed)
                        {
                            warnings++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    AddAccessHealthRow(
                        "AD LAPS",
                        "Metadata access",
                        "Failed",
                        DiagnosticRedaction.Sanitize(
                            ex.Message));
                }
            }

            var cloudConfigured =
                !string.IsNullOrWhiteSpace(
                    _cloudConfig.TenantId) &&
                !string.IsNullOrWhiteSpace(
                    _cloudConfig.ClientId);

            if (!cloudConfigured)
            {
                warnings++;
                AddAccessHealthRow(
                    "Cloud",
                    "Configuration",
                    "Not configured",
                    "Use Administration > Cloud > Intune / Entra Setup Wizard when cloud access is required.");
            }
            else
            {
                if (await EnsureCloudTokenAsync())
                {
                    using var graph =
                        new CloudGraphService();

                    try
                    {
                        var count =
                            await graph.TestAccessAsync(
                                _cloudToken!.AccessToken);

                        AddAccessHealthRow(
                            "Entra",
                            "BitLocker metadata",
                            "OK",
                            $"Microsoft Graph metadata request succeeded ({count} object(s) sampled).");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        AddAccessHealthRow(
                            "Entra",
                            "BitLocker metadata",
                            "Failed",
                            DiagnosticRedaction.Sanitize(
                                ex.Message) +
                            " Verify Graph application permissions/admin consent.");
                    }

                    if (!string.IsNullOrWhiteSpace(
                            computer))
                    {
                        try
                        {
                            var devices =
                                await graph.SearchManagedDevicesAsync(
                                    _cloudToken!.AccessToken,
                                    computer,
                                    10);

                            AddAccessHealthRow(
                                "Intune",
                                "Managed device",
                                devices.Count > 0
                                    ? "OK"
                                    : "Warning",
                                devices.Count > 0
                                    ? $"{devices.Count} matching managed device(s) visible."
                                    : "No matching Intune managed device is visible. Check enrollment and DeviceManagementManagedDevices permissions.");

                            if (devices.Count == 0)
                                warnings++;
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            AddAccessHealthRow(
                                "Intune",
                                "Managed device",
                                "Failed",
                                DiagnosticRedaction.Sanitize(
                                    ex.Message));
                        }

                        try
                        {
                            var laps =
                                await graph.CheckLapsAccessAsync(
                                    _cloudToken!.AccessToken,
                                    computer);

                            foreach (var check in
                                     laps.Checks)
                            {
                                AddAccessHealthRow(
                                    "Entra LAPS",
                                    check.Name,
                                    check.State.ToString(),
                                    check.Detail);

                                if (check.State ==
                                    LapsAccessState.Failed)
                                {
                                    errors++;
                                }
                                else if (check.State is
                                         LapsAccessState.NotDetected or
                                         LapsAccessState.NotProbed)
                                {
                                    warnings++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            warnings++;
                            AddAccessHealthRow(
                                "Entra LAPS",
                                "Metadata access",
                                "Warning",
                                DiagnosticRedaction.Sanitize(
                                    ex.Message) +
                                " This is expected when Entra LAPS is not enabled for the tenant/device.");
                        }
                    }
                }
                else
                {
                    errors++;
                    AddAccessHealthRow(
                        "Cloud",
                        "Authentication",
                        "Failed",
                        "Microsoft Graph authentication did not complete. Review Administration > Cloud diagnostics.");
                }
            }

            UiStyle.SetStatus(
                _accessHealthStatus,
                errors > 0
                    ? $"Access health completed with {errors} error(s) and {warnings} warning(s)."
                    : warnings > 0
                        ? $"Access health completed with {warnings} warning(s)."
                        : "Access health checks completed successfully.",
                errors > 0
                    ? UiStatusKind.Error
                    : warnings > 0
                        ? UiStatusKind.Warning
                        : UiStatusKind.Success);
        }
        finally
        {
            _accessHealthProgress.Visible =
                false;
            _accessHealthRun.Enabled =
                true;
        }
    }

    private void RefreshHelpdeskProfiles()
    {
        var selected =
            _config.ActiveHelpdeskProfile;

        _helpdeskProfile.Items.Clear();

        foreach (var profile in
                 new HelpdeskProfileService()
                     .Load())
        {
            _helpdeskProfile.Items.Add(
                profile.Name);
        }

        if (_helpdeskProfile.Items.Count ==
            0)
        {
            UiStyle.SetStatus(
                _helpdeskProfileStatus,
                "No saved environment profiles. The current configuration remains active.",
                UiStatusKind.Neutral);
            return;
        }

        var match =
            _helpdeskProfile.Items
                .Cast<string>()
                .FirstOrDefault(
                    x =>
                        x.Equals(
                            selected,
                            StringComparison.OrdinalIgnoreCase));

        _helpdeskProfile.SelectedItem =
            match ??
            _helpdeskProfile.Items[0];

        UiStyle.SetStatus(
            _helpdeskProfileStatus,
            $"Active profile: {_config.ActiveHelpdeskProfile}. Profiles contain no passwords or secrets.",
            UiStatusKind.Success);
    }

    private void SaveCurrentHelpdeskProfile()
    {
        using var dialog =
            new InputDialog(
                "Save Environment Profile",
                "Profile name:",
                _config.ActiveHelpdeskProfile);

        if (dialog.ShowDialog(this) !=
                DialogResult.OK ||
            string.IsNullOrWhiteSpace(
                dialog.Value))
        {
            return;
        }

        var profile =
            new HelpdeskProfile
            {
                Name =
                    dialog.Value,
                AdConnectionMode =
                    _adMode.SelectedIndex == 1
                        ? "Explicit"
                        : "Auto",
                AdServer =
                    _adServer.Text.Trim(),
                AdDomain =
                    _adDomain.Text.Trim(),
                AdPort =
                    (int)_adPort.Value,
                AdUseLdaps =
                    _adUseLdaps.Checked,
                AdUseExplicitCredentials =
                    _adExplicitCredentials.Checked,
                AdUsername =
                    _adUsername.Text.Trim(),
                RecoverySearchSource =
                    _recoverySource.SelectedIndex == 1
                        ? "LocalCache"
                        : "LiveAD",
                CloudTenant =
                    _cloudTenant.Text.Trim(),
                CloudClientId =
                    _cloudClient.Text.Trim(),
                CloudUsername =
                    _cloudUsername.Text.Trim(),
                CloudAuthMode =
                    _cloudAuthMode.SelectedIndex switch
                    {
                        2 => "Certificate",
                        1 => "Password",
                        _ => "DeviceCode"
                    },
                CloudCertificateThumbprint =
                    _cloudThumbprint.Text.Trim()
            };

        new HelpdeskProfileService()
            .Save(
                profile);

        _config.ActiveHelpdeskProfile =
            profile.Name;
        ConfigService.SaveAppConfig(
            _config);

        RefreshHelpdeskProfiles();

        _helpdeskProfile.SelectedItem =
            profile.Name;
    }

    private void ApplySelectedHelpdeskProfile()
    {
        if (_helpdeskProfile.SelectedItem is not
                string name)
        {
            return;
        }

        var profile =
            new HelpdeskProfileService()
                .Load()
                .FirstOrDefault(
                    x =>
                        x.Name.Equals(
                            name,
                            StringComparison.OrdinalIgnoreCase));

        if (profile is null)
            return;

        DisconnectDirectorySession();

        _adMode.SelectedIndex =
            profile.AdConnectionMode.Equals(
                "Explicit",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
        _adServer.Text =
            profile.AdServer;
        _adDomain.Text =
            profile.AdDomain;
        _adPort.Value =
            Math.Clamp(
                profile.AdPort,
                1,
                65535);
        _adUseLdaps.Checked =
            profile.AdUseLdaps;
        _adExplicitCredentials.Checked =
            profile.AdUseExplicitCredentials;
        _adUsername.Text =
            profile.AdUsername;
        _recoverySource.SelectedIndex =
            profile.RecoverySearchSource.Equals(
                "LocalCache",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        _cloudTenant.Text =
            profile.CloudTenant;
        _cloudClient.Text =
            profile.CloudClientId;
        _cloudUsername.Text =
            profile.CloudUsername;
        _cloudThumbprint.Text =
            profile.CloudCertificateThumbprint;
        _cloudAuthMode.SelectedIndex =
            profile.CloudAuthMode.Equals(
                "Certificate",
                StringComparison.OrdinalIgnoreCase)
                ? 2
                : profile.CloudAuthMode.Equals(
                    "Password",
                    StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0;
        UpdateCloudAuthUi();

        _config.ActiveHelpdeskProfile =
            profile.Name;

        SaveDirectorySettings(
            showConfirmation:
                false);
        SaveCloudFields();
        ConfigService.SaveAppConfig(
            _config);

        UiStyle.SetStatus(
            _helpdeskProfileStatus,
            $"Applied profile {profile.Name}. Credentials were not changed.",
            UiStatusKind.Success);

        RefreshSetupStatus();
    }

    private void DeleteSelectedHelpdeskProfile()
    {
        if (_helpdeskProfile.SelectedItem is not
                string name)
        {
            return;
        }

        new HelpdeskProfileService()
            .Delete(
                name);

        if (_config.ActiveHelpdeskProfile.Equals(
                name,
                StringComparison.OrdinalIgnoreCase))
        {
            _config.ActiveHelpdeskProfile =
                "Default";
            ConfigService.SaveAppConfig(
                _config);
        }

        RefreshHelpdeskProfiles();
    }

    private void SaveHelpdeskBasics()
    {
        _config.RequireRecoveryAccessReference =
            _requireRecoveryReference.Checked;
        _config.SuggestRotationAfterCloudKeyRetrieval =
            _suggestRotationAfterRecovery.Checked;
        _config.RecoveryReferencePattern =
            _recoveryReferencePatternSetting.Text.Trim();
        _config.RecoveryReferenceExample =
            _recoveryReferenceExampleSetting.Text.Trim();
        _config.SecretDisplaySeconds =
            (int)_secretDisplaySecondsSetting.Value;
        _config.SensitiveClipboardSeconds =
            (int)_clipboardSecondsSetting.Value;

        ConfigService.SaveAppConfig(
            _config);

        _recoveryAccessContexts.Clear();

        _audit.Write(
            "SaveHelpdeskSettings",
            source:
                "Local",
            details:
                $"RequireReference={_config.RequireRecoveryAccessReference}; " +
                $"SuggestRotation={_config.SuggestRotationAfterCloudKeyRetrieval}; " +
                $"SecretSeconds={_config.SecretDisplaySeconds}; " +
                $"ClipboardSeconds={_config.SensitiveClipboardSeconds}");

        RefreshSetupStatus();
    }

    private void LoadHelpdeskBasics()
    {
        _recoveryReferencePatternSetting.Text =
            _config.RecoveryReferencePattern;
        _recoveryReferenceExampleSetting.Text =
            _config.RecoveryReferenceExample;

        _secretDisplaySecondsSetting.Value =
            Math.Clamp(
                _config.SecretDisplaySeconds,
                15,
                3600);
        _clipboardSecondsSetting.Value =
            Math.Clamp(
                _config.SensitiveClipboardSeconds,
                5,
                600);

        RefreshHelpdeskProfiles();
        RefreshSetupStatus();
    }

    private void RefreshSetupStatus()
    {
        var cloudConfigured =
            !string.IsNullOrWhiteSpace(
                _cloudConfig.TenantId) &&
            !string.IsNullOrWhiteSpace(
                _cloudConfig.ClientId);

        var enterpriseEnabled =
            _config.RbacEnabled ||
            _config.JitRecoveryEnabled ||
            _config.TwoPersonApprovalEnabled ||
            _config.SiemEnabled ||
            _config.RequireRecoveryAccessReference;

        var text =
            $"AD: {(_directoryConnected ? "Connected" : "Not connected")} • " +
            $"Cloud: {(cloudConfigured ? "Configured" : "Not configured")} • " +
            $"Auto update: {(_config.CheckForUpdatesOnStart && _config.AutoInstallUpdatesOnStart ? "On" : "Off")} • " +
            $"Enterprise recovery policies: {(enterpriseEnabled ? "Configured" : "Off by default")}";

        var kind =
            _directoryConnected
                ? UiStatusKind.Success
                : UiStatusKind.Neutral;

        UiStyle.SetStatus(
            _setupStatus,
            text,
            kind);

        UiStyle.SetStatus(
            _startSetupStatus,
            text,
            kind);
    }

    private void OpenSupportBundlesFolder()
    {
        try
        {
            Directory.CreateDirectory(
                AppPaths.SupportBundlesDirectory);

            OpenPath(
                AppPaths.SupportBundlesDirectory);
        }
        catch (Exception ex)
        {
            ShowAppError(
                "Opening the support-bundle folder failed.",
                "OpenSupportBundles",
                ex);
        }
    }

    private void CreateQuickDiagnosticsBundleGui()
    {
        try
        {
            Directory.CreateDirectory(
                AppPaths.SupportBundlesDirectory);

            var path =
                Path.Combine(
                    AppPaths.SupportBundlesDirectory,
                    "BitKeyBridge-Support-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss") +
                    ".zip");

            var result =
                new ConfigurationMaintenanceService()
                    .CreateDiagnosticsBundle(
                        path);

            _audit.Write(
                "CreateQuickDiagnosticsBundle",
                source:
                    "Diagnostics",
                details:
                    $"Path={result.ZipPath}; Files={result.IncludedFiles.Count}");

            try
            {
                Clipboard.SetText(
                    result.ZipPath);
            }
            catch (Exception ex)
            {
                WindowsEventLogService.TryWrite(
                    "Support bundle path copy failed: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4660,
                    "Diagnostics");
            }

            ShowAppMessage(
                "Support bundle created.",
                result.ZipPath +
                Environment.NewLine +
                Environment.NewLine +
                "The path was copied to the clipboard. Recovery passwords, LAPS passwords, tokens, credential blobs and private keys are excluded.");
        }
        catch (Exception ex)
        {
            ShowAppError(
                "Creating the support bundle failed.",
                "CreateQuickDiagnosticsBundle",
                ex);
        }
    }

    private void ApplyAuditFilter()
    {
        if (_auditResults.IsDisposed)
            return;

        IEnumerable<AuditEntry> rows =
            _auditRows;

        if (_auditFrom.Checked)
        {
            var fromUtc =
                _auditFrom.Value.Date.ToUniversalTime();
            rows =
                rows.Where(
                    x =>
                        x.TimestampUtc >=
                        fromUtc);
        }

        if (_auditTo.Checked)
        {
            var toUtc =
                _auditTo.Value.Date
                    .AddDays(1)
                    .ToUniversalTime();
            rows =
                rows.Where(
                    x =>
                        x.TimestampUtc <
                        toUtc);
        }

        var query =
            _auditFilter.Text.Trim();

        if (!string.IsNullOrWhiteSpace(
                query))
        {
            var field =
                _auditFilterField.SelectedItem as
                    string ??
                "All fields";

            bool Match(
                string? value) =>
                !string.IsNullOrWhiteSpace(
                    value) &&
                value.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase);

            rows =
                rows.Where(
                    entry =>
                        field switch
                        {
                            "Computer" =>
                                Match(
                                    entry.ComputerName),
                            "Ticket / Reference" =>
                                Match(
                                    entry.Reference),
                            "User" =>
                                Match(
                                    entry.User),
                            "Action" =>
                                Match(
                                    entry.Action),
                            "Recovery ID" =>
                                Match(
                                    entry.RecoveryId),
                            "Source" =>
                                Match(
                                    entry.Source),
                            _ =>
                                Match(
                                    entry.ComputerName) ||
                                Match(
                                    entry.Reference) ||
                                Match(
                                    entry.User) ||
                                Match(
                                    entry.Action) ||
                                Match(
                                    entry.RecoveryId) ||
                                Match(
                                    entry.Source) ||
                                Match(
                                    entry.Reason) ||
                                Match(
                                    entry.Details) ||
                                Match(
                                    entry.CorrelationId)
                        });
        }

        _auditResults.BeginUpdate();
        try
        {
            _auditResults.Items.Clear();

            foreach (var entry in
                     rows.Take(
                         5000))
            {
                var item =
                    new ListViewItem(
                        entry.TimestampUtc.ToString(
                            "yyyy-MM-dd HH:mm:ss"));

                item.SubItems.Add(
                    entry.User);
                item.SubItems.Add(
                    entry.Host);
                item.SubItems.Add(
                    entry.Action);
                item.SubItems.Add(
                    entry.Result);
                item.SubItems.Add(
                    entry.ComputerName);
                item.SubItems.Add(
                    entry.RecoveryId);
                item.SubItems.Add(
                    entry.Source);
                item.SubItems.Add(
                    entry.AuthMode);
                item.SubItems.Add(
                    entry.Reference);
                item.SubItems.Add(
                    entry.CorrelationId);
                item.SubItems.Add(
                    entry.Reason);
                item.SubItems.Add(
                    entry.Details);

                item.Tag =
                    entry;
                _auditResults.Items.Add(
                    item);
            }
        }
        finally
        {
            _auditResults.EndUpdate();
        }
    }

    private Task MaybeSuggestCloudRotationAfterRecoveryAsync(
        RecoveryAccessContext context,
        UnifiedDeviceInfo row,
        string recoveryId)
    {
        _ = recoveryId;

        if (!_config.SuggestRotationAfterCloudKeyRetrieval ||
            string.IsNullOrWhiteSpace(
                row.ManagedDeviceId) ||
            !_rotationSuggestionSessions.Add(
                context.SessionId))
        {
            return Task.CompletedTask;
        }

        var message =
            $"Recovery key accessed for {row.ComputerName}. " +
            "After recovery is complete, use Rotate BitLocker Key in Intune.";

        UiStyle.SetStatus(
            _unifiedStatus,
            message,
            UiStatusKind.Warning);

        UiStyle.SetStatus(
            _homeSearchStatus,
            message,
            UiStatusKind.Warning);

        return Task.CompletedTask;
    }

    private void StartSecretLifetimeCountdown(
        string label,
        int seconds)
    {
        _secretVisibleUntilUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    15,
                    seconds));

        EnsureSecretLifetimeTimer(
            label);
    }

    private void MarkClipboardLifetime()
    {
        _clipboardClearAtUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    5,
                    _config.SensitiveClipboardSeconds));

        EnsureSecretLifetimeTimer(
            "Recovery secret");
    }

    private void EnsureSecretLifetimeTimer(
        string label)
    {
        _secretLifetimeTimer?.Stop();
        _secretLifetimeTimer?.Dispose();

        _secretLifetimeTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

        void Refresh()
        {
            var secretSeconds =
                _secretVisibleUntilUtc is null
                    ? 0
                    : Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (_secretVisibleUntilUtc.Value -
                             DateTime.UtcNow)
                            .TotalSeconds));

            var clipboardSeconds =
                _clipboardClearAtUtc is null
                    ? 0
                    : Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (_clipboardClearAtUtc.Value -
                             DateTime.UtcNow)
                            .TotalSeconds));

            if (_secretVisibleUntilUtc is not null &&
                secretSeconds <= 0)
            {
                ClearVisibleSecretsNow();
                return;
            }

            if (_clipboardClearAtUtc is not null &&
                clipboardSeconds <= 0)
            {
                ClearTrackedRecoveryClipboard();
                _clipboardClearAtUtc =
                    null;
                clipboardSeconds =
                    0;
            }

            if (secretSeconds <= 0 &&
                clipboardSeconds <= 0)
            {
                UiStyle.SetStatus(
                    _secretLifetimeStatus,
                    "No recovery secret is currently retained.",
                    UiStatusKind.Neutral);
                _secretLifetimeTimer?.Stop();
                return;
            }

            var parts =
                new List<string>();

            if (secretSeconds > 0)
            {
                parts.Add(
                    $"{label} clears in {TimeSpan.FromSeconds(secretSeconds):mm\\:ss}");
            }

            if (clipboardSeconds > 0)
            {
                parts.Add(
                    $"clipboard clears in {TimeSpan.FromSeconds(clipboardSeconds):mm\\:ss}");
            }

            UiStyle.SetStatus(
                _secretLifetimeStatus,
                string.Join(" • ", parts),
                UiStatusKind.Warning);
        }

        _secretLifetimeTimer.Tick +=
            (_, _) =>
                Refresh();

        Refresh();
        _secretLifetimeTimer.Start();
    }

    private void ClearVisibleSecretsNow()
    {
        ClearTrackedRecoveryClipboard();
        _clipboardClearAtUtc = null;

        _startCurrentKey = null;
        _startKey.Clear();
        _startKey.UseSystemPasswordChar = true;
        _startShow.Text = "Reveal Recovery Key";

        _deviceCurrentKey = null;
        _deviceRecoveryKey.Clear();
        _deviceRecoveryKey.UseSystemPasswordChar = true;
        _deviceShowKey.Text = "Reveal Key";

        ClearLapsResult();

        _secretVisibleUntilUtc = null;
        _secretLifetimeTimer?.Stop();

        UiStyle.SetStatus(
            _secretLifetimeStatus,
            "Recovery secrets cleared.",
            UiStatusKind.Success);
    }

    private async Task<RecoverySearchResult?>
        GetLatestHomeRecoveryRowAsync(
            HomeSearchResult selected)
    {
        if (!_directoryConnected ||
            _startScope is null)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Connect to Active Directory before reading a BitLocker recovery key.",
                UiStatusKind.Warning);
            return null;
        }

        var rows =
            await SearchLiveAdRecoveryMetadataAsync(
                selected.ComputerName,
                _startScope,
                CancellationToken.None);

        return rows
            .Where(
                row =>
                    row.ComputerName.Equals(
                        selected.ComputerName,
                        StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(
                row =>
                    row.IsLatest == true)
            .ThenByDescending(
                row =>
                    row.KeyDate)
            .FirstOrDefault();
    }

    private async Task<(string Key, RecoverySearchResult Row, RecoveryAccessContext Context)?>
        GetHomeBitLockerSecretAsync(
            string action)
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null ||
            selected.BitLockerKeyCount <= 0)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Select a computer with BitLocker recovery metadata.",
                UiStatusKind.Warning);
            return null;
        }

        var row =
            await GetLatestHomeRecoveryRowAsync(
                selected);

        if (row is null)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "The selected BitLocker recovery record could not be resolved.",
                UiStatusKind.Warning);
            return null;
        }

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                action,
                row.ComputerName,
                row.RecoveryId,
                "AD"))
        {
            return null;
        }

        var context =
            GetOrRequestRecoveryAccessContext(
                "AD",
                row.RecoveryId,
                row.ComputerName,
                allowRotationReminder:
                    false);

        if (context is null ||
            !AuthorizePrivilegedRecoveryAccess(
                context,
                action,
                row.ComputerName,
                row.RecoveryId,
                "AD"))
        {
            return null;
        }

        _startCurrentKey =
            null;

        var key =
            await EnsureStartRecoveryKeyAsync(
                row);

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return null;
        }

        return (
            key,
            row,
            context);
    }

    private async Task RevealHomeBitLockerAsync()
    {
        var access =
            await GetHomeBitLockerSecretAsync(
                "RevealHomeRecoveryKey");

        if (access is null)
            return;

        WriteRecoveryAudit(
            "RevealHomeRecoveryKey",
            access.Value.Context,
            computerName:
                access.Value.Row.ComputerName,
            recoveryId:
                access.Value.Row.RecoveryId,
            source:
                "AD");

        RecordRecentComputer(
            access.Value.Row.ComputerName,
            string.Empty,
            "BitLocker reveal");

        using var dialog =
            new SecretDisplayDialog(
                "BitLocker Recovery Key",
                $"{access.Value.Row.ComputerName} • Recovery ID {access.Value.Row.RecoveryId}",
                access.Value.Key,
                $"The secret is kept only in memory and is cleared when this window closes. Clipboard copies clear after {_config.SensitiveClipboardSeconds} seconds.",
                _config.SensitiveClipboardSeconds);

        dialog.ShowDialog(this);

        _startCurrentKey =
            null;

        UiStyle.SetStatus(
            _homeSearchStatus,
            "BitLocker recovery key view closed and the in-memory copy was cleared.",
            UiStatusKind.Success);
    }

    private async Task CopyHomeBitLockerAsync()
    {
        var access =
            await GetHomeBitLockerSecretAsync(
                "CopyHomeRecoveryKey");

        if (access is null)
            return;

        WriteRecoveryAudit(
            "CopyHomeRecoveryKey",
            access.Value.Context,
            computerName:
                access.Value.Row.ComputerName,
            recoveryId:
                access.Value.Row.RecoveryId,
            source:
                "AD");

        RecordRecentComputer(
            access.Value.Row.ComputerName,
            string.Empty,
            "BitLocker copy");

        CopyKeyWithAutoClear(
            access.Value.Key);

        _startCurrentKey =
            null;

        UiStyle.SetStatus(
            _homeSearchStatus,
            $"Recovery key copied. Clipboard clears in {_config.SensitiveClipboardSeconds} seconds.",
            UiStatusKind.Success);
    }

    private async Task RotateHomeBitLockerAsync()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
            return;

        if (string.IsNullOrWhiteSpace(
                _cloudConfig.TenantId) ||
            string.IsNullOrWhiteSpace(
                _cloudConfig.ClientId))
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Intune rotation is unavailable because Cloud is not configured.",
                UiStatusKind.Warning);
            return;
        }

        if (!await EnsureCloudTokenAsync())
            return;

        try
        {
            using var graph =
                new CloudGraphService();

            var devices =
                await graph.SearchManagedDevicesAsync(
                    _cloudToken!.AccessToken,
                    selected.ComputerName,
                    20);

            var device =
                devices.FirstOrDefault(
                    x =>
                        x.DeviceName.Equals(
                            selected.ComputerName,
                            StringComparison.OrdinalIgnoreCase)) ??
                devices.FirstOrDefault();

            if (device is null ||
                string.IsNullOrWhiteSpace(
                    device.ManagedDeviceId))
            {
                UiStyle.SetStatus(
                    _homeSearchStatus,
                    $"No Intune managed device was found for {selected.ComputerName}.",
                    UiStatusKind.Warning);
                return;
            }

            await RotateManagedDeviceAsync(
                device.ManagedDeviceId,
                selected.ComputerName,
                selected.LatestRecoveryId);

            RecordRecentComputer(
                selected,
                "Intune rotate");
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Intune key rotation failed. Review diagnostics.",
                UiStatusKind.Error);

            _appDiagnostics.ShowError(
                "Intune key rotation failed.",
                "RotateHomeBitLocker",
                ex,
                ("Computer", selected.ComputerName));
        }
    }

    private void OpenHomeDeviceDetails()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
            return;

        _unifiedQuery.Text =
            selected.ComputerName;

        foreach (TabPage page in
                 _mainTabs.TabPages)
        {
            if (page.Text.Equals(
                    "Devices",
                    StringComparison.OrdinalIgnoreCase))
            {
                _mainTabs.SelectedTab =
                    page;
                break;
            }
        }
    }

    private void RefreshHomeSelectionSummary()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
        {
            UiStyle.SetStatus(
                _homeSelectionSummary,
                "Select a result to see the helpdesk actions and source summary.",
                UiStatusKind.Neutral);
            return;
        }

        var source =
            string.IsNullOrWhiteSpace(
                selected.SourceSummary)
                ? "AD"
                : selected.SourceSummary;

        UiStyle.SetStatus(
            _homeSelectionSummary,
            $"{selected.ComputerName} • Source: {source} • BitLocker: {selected.BitLockerStatus} • LAPS: {selected.LapsStatus}",
            selected.BitLockerKeyCount > 0 ||
            selected.LapsDetected
                ? UiStatusKind.Success
                : UiStatusKind.Warning);
    }
}
