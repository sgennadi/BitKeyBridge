namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Localizing displayed choices must never change their security-relevant
    // SelectedIndex (0=choose, 1=AD, 2=cloud, 3=both).
    private static readonly string[] ConnectionSourceChoiceCaptions =
    [
        "Choose a source",
        "Active Directory",
        "Microsoft Entra / Intune (Microsoft 365)",
        "Both: Active Directory + Microsoft Entra"
    ];

    private Button? _homeConnectionOptionsToggle;
    private readonly ComboBox _homeConnectionSource = new();
    private readonly Button _homeConnectCloudButton = new();
    private readonly Button _homeReconnectWizardButton = new();
    private readonly Button _homeConfigureCloudButton = new();
    private readonly UiStatusLabel _homeCloudConnectionStatus = new();

    private readonly StartConnectionPreferences _startConnectionPreferences =
        ConfigService.LoadStartConnectionPreferences();

    private Control? _startAdIdentityPanel;
    private Control? _startDirectActionsPanel;
    private Control? _startDirectHint;
    private bool _connectionOptionsExpanded;
    private Action<bool>? _setConnectionOptionsExpanded;
    private bool _initializingStartSource;

    internal void PreviewSetConnectionOptionsExpandedForUiTest(bool expanded)
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI tests only.");
        _setConnectionOptionsExpanded?.Invoke(expanded);
    }
    private bool _cloudWizardOpening;

    private Control BuildStartConnectionSourceChooser()
    {
        var layout = new TableLayoutPanel
        {
            Name = "HomeConnectionSourceChooser",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 3; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Everyday connection has one primary action. Direct source/cloud
        // maintenance stays discoverable, but does not crowd the first screen.
        var primary = new FlowLayoutPanel
        {
            Name = "HomeConnectionPrimaryActions",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = Padding.Empty,
            FlowDirection = FlowDirection.LeftToRight
        };
        var showOptions = NewActionButton("More connection options...");
        showOptions.Name = "HomeConnectionOptionsToggle";
        _homeConnectionOptionsToggle = showOptions;
        showOptions.AccessibleName =
            "Show or hide direct connection source and Microsoft 365 setup options";

        var row = new FlowLayoutPanel
        {
            Name = "HomeConnectionExtraActions",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            WrapContents = true,
            Visible = false,
            Margin = Padding.Empty,
            FlowDirection = FlowDirection.LeftToRight
        };
        row.Controls.Add(new Label
        {
            Text = "Connect using:",
            AutoSize = true,
            Font = UiStyle.CreateEmphasisFont(10F),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, UiStyle.ControlGap, UiStyle.SectionGap, 0)
        });

        _homeConnectionSource.Name = "HomeConnectionSource";
        _homeConnectionSource.AccessibleName =
            "Select Active Directory, Microsoft Entra/Intune or both";
        _homeConnectionSource.DropDownStyle = ComboBoxStyle.DropDownList;
        _homeConnectionSource.MinimumSize = new Size(310, UiStyle.MinimumButtonHeight);
        _homeConnectionSource.Width = 340;
        _homeConnectionSource.Items.AddRange(ConnectionSourceChoiceCaptions);
        _initializingStartSource = true;
        try
        {
            _homeConnectionSource.SelectedIndex =
                StartConnectionPolicy.Normalize(_startConnectionPreferences.Source) switch
                {
                    StartConnectionPolicy.ActiveDirectory => 1,
                    StartConnectionPolicy.MicrosoftCloud => 2,
                    StartConnectionPolicy.Both => 3,
                    _ => 0
                };
        }
        finally
        {
            _initializingStartSource = false;
        }
        row.Controls.Add(_homeConnectionSource);

        _homeReconnectWizardButton.Name = "HomeReconnectWizardButton";
        _homeReconnectWizardButton.Text = "Connect / Change...";
        _homeReconnectWizardButton.AccessibleName =
            "Reopen the AD or Microsoft 365 connection wizard to change source, " +
            "account or retry after a failed password";
        UiStyle.ConfigureActionButton(_homeReconnectWizardButton);
        primary.Controls.Add(_homeReconnectWizardButton);
        primary.Controls.Add(showOptions);
        layout.Controls.Add(primary, 0, 0);
        void SetConnectionOptionsExpanded(bool expanded)
        {
            if (_connectionOptionsExpanded == expanded)
                return;
            _connectionOptionsExpanded = expanded;
            row.Visible = expanded;
            var caption = expanded
                ? "Hide connection options" : "More connection options...";
            showOptions.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
                _uiLanguageCode, caption);
            UpdateStartConnectionSourceControls();
            QueueMainViewportReflow();
        }
        _setConnectionOptionsExpanded = SetConnectionOptionsExpanded;
        showOptions.Click += (_, _) =>
            SetConnectionOptionsExpanded(!_connectionOptionsExpanded);
        _homeReconnectWizardButton.Click += async (_, _) =>
            await ShowConnectionWizardAsync(
                preferOtherAdAccount: _lastAdConnectionFailed &&
                    StartConnectionPolicy.NeedsActiveDirectory(SelectedStartSource));

        _homeConnectCloudButton.Name = "HomeConnectCloudButton";
        _homeConnectCloudButton.Text = "Connect to Microsoft 365";
        _homeConnectCloudButton.AccessibleName =
            "Authenticate to Microsoft Graph using delegated permissions and MFA";
        UiStyle.ConfigureActionButton(_homeConnectCloudButton);
        row.Controls.Add(_homeConnectCloudButton);

        _homeConfigureCloudButton.Name = "HomeConfigureCloudButton";
        _homeConfigureCloudButton.Text = "Cloud Setup...";
        _homeConfigureCloudButton.AccessibleName =
            "Set up an approved application or request administrator setup";
        UiStyle.ConfigureActionButton(_homeConfigureCloudButton);
        row.Controls.Add(_homeConfigureCloudButton);
        layout.Controls.Add(row, 0, 1);

        _homeCloudConnectionStatus.Name = "HomeCloudConnectionStatus";
        _homeCloudConnectionStatus.AccessibleName =
            "Microsoft Entra / Intune connection status";
        UiStyle.ConfigureStatusLabel(_homeCloudConnectionStatus);
        _homeCloudConnectionStatus.MaximumSize = new Size(1100, 0);
        UiStyle.SetStatus(_homeCloudConnectionStatus,
            "Use Connect / Change to select AD, Microsoft 365 or both.",
            UiStatusKind.Neutral);
        layout.Controls.Add(_homeCloudConnectionStatus, 0, 2);

        _homeConnectionSource.SelectedIndexChanged += async (_, _) =>
        {
            if (_initializingStartSource)
                return;

            // A quick source switch must not silently re-target the remembered
            // unattended connection. Re-enable it deliberately in the wizard.
            _startConnectionPreferences.RememberLastConnectionOnStart = false;
            _startConnectionPreferences.PreferOtherAdAccountOnStart = false;
            await OnStartConnectionSourceSelectedAsync();
        };
        _homeConfigureCloudButton.Click += async (_, _) =>
            await ShowStartCloudWizardAsync(connectAfterSave: true);
        _homeConnectCloudButton.Click += async (_, _) =>
            await ConnectStartCloudAsync();

        UpdateStartConnectionSourceControls();
        return layout;
    }

    private void RefreshStartConnectionLocalization()
    {
        // The toggle has two distinct runtime captions. Do not retranslate
        // only the initial English caption from the generic control cache.
        if (_homeConnectionOptionsToggle is { IsDisposed: false } toggle)
        {
            var original = _connectionOptionsExpanded
                ? "Hide connection options" : "More connection options...";
            var translated = UiStaticCaptionCatalog.TranslateOrOriginal(
                _uiLanguageCode, original);
            if (!string.Equals(toggle.Text, translated, StringComparison.Ordinal))
                toggle.Text = translated;
        }

        if (_homeConnectionSource.IsDisposed ||
            _homeConnectionSource.Items.Count != ConnectionSourceChoiceCaptions.Length)
            return;

        // ComboBox Items are not WinForms Controls. Translate them in place
        // without triggering source changes, clearing recovery metadata, or
        // writing connection preferences.
        var selectedIndex = _homeConnectionSource.SelectedIndex;
        var previousGuard = _initializingStartSource;
        _initializingStartSource = true;
        _homeConnectionSource.BeginUpdate();
        try
        {
            for (var i = 0; i < ConnectionSourceChoiceCaptions.Length; i++)
            {
                var translated = UiStaticCaptionCatalog.TranslateOrOriginal(
                    _uiLanguageCode, ConnectionSourceChoiceCaptions[i]);
                if (!string.Equals(_homeConnectionSource.Items[i] as string,
                        translated, StringComparison.Ordinal))
                    _homeConnectionSource.Items[i] = translated;
            }
            if (_homeConnectionSource.SelectedIndex != selectedIndex)
                _homeConnectionSource.SelectedIndex = selectedIndex;
        }
        finally
        {
            _homeConnectionSource.EndUpdate();
            _initializingStartSource = previousGuard;
        }
    }

    private string SelectedStartSource => _startConnectionPreferences.Source;

    private async Task OnStartConnectionSourceSelectedAsync(bool openCloudWizardIfNeeded = true)
    {
        _startConnectionPreferences.Source = _homeConnectionSource.SelectedIndex switch
        {
            1 => StartConnectionPolicy.ActiveDirectory,
            2 => StartConnectionPolicy.MicrosoftCloud,
            3 => StartConnectionPolicy.Both,
            _ => StartConnectionPolicy.Choose
        };

        // Discard previous-source results and any revealed secrets immediately.
        // In-flight AD/cloud searches must never populate another source's view.
        _homeSearchCancellation?.Cancel();
        _homeSearchGeneration++;
        ClearVisibleSecretsNow();
        _homeSearchResults.Rows.Clear();
        UpdateHomeSearchSelection();

        try
        {
            ConfigService.SaveStartConnectionPreferences(_startConnectionPreferences);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                "Source selected for this session. Unable to save user preferences.",
                UiStatusKind.Warning);
        }

        UpdateStartConnectionSourceControls();
        RefreshHomeConnectionUi();

        if (openCloudWizardIfNeeded &&
            StartConnectionPolicy.NeedsMicrosoftCloud(SelectedStartSource) &&
            !StartCloudAppConfigured())
            await ShowStartCloudWizardAsync(connectAfterSave: true);
    }

    private bool StartCloudAppConfigured() =>
        !string.IsNullOrWhiteSpace(_cloudConfig.TenantId) &&
        Guid.TryParse(_cloudConfig.ClientId, out _);

    private void UpdateStartConnectionSourceControls()
    {
        var ad = StartConnectionPolicy.NeedsActiveDirectory(SelectedStartSource);
        var cloud = StartConnectionPolicy.NeedsMicrosoftCloud(SelectedStartSource);
        _homeConnectAdButton.Visible = ad;
        _homeDisconnectAdButton.Visible = ad;
        _homeUseWindowsIdentity.Visible = ad;
        _homeUseOtherAccount.Visible = ad;
        if (_startAdIdentityPanel is not null)
            _startAdIdentityPanel.Visible = ad && _connectionOptionsExpanded;
        // Direct operations are opt-in visual details only. Permission and
        // source checks on the underlying actions remain unchanged.
        if (_startDirectActionsPanel is not null)
            _startDirectActionsPanel.Visible = _connectionOptionsExpanded;
        if (_startDirectHint is not null)
            _startDirectHint.Visible = _connectionOptionsExpanded;
        // Diagnostic tools remain discoverable even when disconnected or
        // cloud-only: reveal the direct actions through More options.
        _homeConnectionToolsButton.Visible = true;
        _homeAdvancedConnectionMenuItem.Visible = ad;
        // Never leave AD credential and server fields visible while
        // browsing a cloud-only recovery session.
        if (!ad && _advancedConnectionGroup.Visible)
        {
            _advancedConnectionGroup.Visible = false;
            _homeAdvancedConnectionMenuItem.Text = "Advanced AD settings...";
        }
        _homeConnectionStatus.Visible = ad;
        _homeAdAccountStatus.Visible = ad;

        _homeConnectCloudButton.Visible = cloud;
        _homeConfigureCloudButton.Visible = cloud;
        _homeCloudConnectionStatus.Visible = cloud ||
            SelectedStartSource == StartConnectionPolicy.Choose;
        _homeReconnectWizardButton.Enabled = !_directoryConnecting &&
            !_cloudConnecting && !_connectionWizardOpening;
        _homeConnectCloudButton.Enabled = cloud && !_cloudConnecting;
        _homeConfigureCloudButton.Enabled = cloud && !_cloudWizardOpening;

        if (cloud)
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                HasCloudSearchSession()
                    ? "Connected to Microsoft Entra / Intune using delegated Graph permissions."
                    : StartCloudAppConfigured()
                        ? "Cloud is configured. Use Connect / Change for delegated MFA sign-in."
                        : "Cloud app not configured. Connect / Change opens guided setup.",
                HasCloudSearchSession() ? UiStatusKind.Success :
                StartCloudAppConfigured() ? UiStatusKind.Neutral : UiStatusKind.Warning);
        }
        else if (SelectedStartSource == StartConnectionPolicy.Choose)
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                "Use Connect / Change to choose a source. No credentials are requested until Connect.",
                UiStatusKind.Neutral);
        }
    }

    private async Task<bool> ShowStartCloudWizardAsync(bool connectAfterSave)
    {
        if (_cloudWizardOpening)
            return false;

        _cloudWizardOpening = true;
        UpdateStartConnectionSourceControls();
        try
        {
            using var wizard = new CloudConnectionWizardDialog(_cloudConfig);
            if (wizard.ShowDialog(this) != DialogResult.OK)
                return false;

            if (wizard.RequestAdministrativeSetup)
            {
                if (!SecurityContext.IsAdministrator())
                {
                    UiStyle.SetStatus(_homeCloudConnectionStatus,
                        "Creating an application and granting tenant consent requires " +
                        "an administrator. Ask IT to create and approve a BitKeyBridge " +
                        "application, then enter its Tenant ID and Client ID here. " +
                        "Daily use does not need local administrator rights.",
                        UiStatusKind.Warning);
                    return false;
                }

                await OpenIntuneSetupWizardAsync();
                LoadCloudFields();
                UpdateStartConnectionSourceControls();
                return StartCloudAppConfigured();
            }

            // A helpdesk operator can store their own non-secret client/tenant
            // identifiers in LocalAppData without UAC or Entra admin credentials.
            _cloudTenant.Text = wizard.TenantId;
            _cloudClient.Text = wizard.ClientId;
            _cloudAuthMode.SelectedIndex = 0; // Device code + MFA, not ROPC.
            SaveCloudFields();
            UpdateStartConnectionSourceControls();

            if (connectAfterSave)
                await ConnectStartCloudAsync();

            return true;
        }
        finally
        {
            _cloudWizardOpening = false;
            UpdateStartConnectionSourceControls();
        }
    }

    private async Task ConnectStartCloudAsync()
    {
        if (_cloudConnecting)
            return;

        if (!StartCloudAppConfigured())
        {
            await ShowStartCloudWizardAsync(connectAfterSave: true);
            return;
        }

        // Reuse existing Graph authentication, tenant policy checks and audit.
        // Never request/create privileged app-only tokens implicitly.
        // Start is the least-privilege interactive helpdesk path. Even if
        // administration configured an app-only certificate, never silently
        // elevate a Start session to that application identity.
        _cloudAuthMode.SelectedIndex = 0; // delegated Device Code + MFA

        await ConnectCloudFromUiAsync();
        UpdateStartConnectionSourceControls();
        RefreshHomeConnectionUi();
        if (HasCloudSearchSession())
        {
            UiStyle.SetStatus(_homeSearchStatus,
                "Cloud connected. Search Entra/Intune devices. Passwords remain masked.",
                UiStatusKind.Success);
        }
        else
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                "Cloud sign-in did not complete. Review Microsoft Graph diagnostics " +
                "in Administration if permitted, or ask IT to verify app consent and user RBAC.",
                UiStatusKind.Warning);
        }
    }
}
