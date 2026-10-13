namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly ComboBox _homeConnectionSource = new();
    private readonly Button _homeConnectCloudButton = new();
    private readonly Button _homeConfigureCloudButton = new();
    private readonly UiStatusLabel _homeCloudConnectionStatus = new();

    private readonly StartConnectionPreferences _startConnectionPreferences =
        ConfigService.LoadStartConnectionPreferences();

    private Control? _startAdIdentityPanel;
    private bool _initializingStartSource;
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
            RowCount = 2,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            WrapContents = true,
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
        _homeConnectionSource.Items.AddRange([
            "Choose a source",
            "Active Directory",
            "Microsoft Entra / Intune (Microsoft 365)",
            "Both: Active Directory + Microsoft Entra"
        ]);
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
        layout.Controls.Add(row, 0, 0);

        _homeCloudConnectionStatus.Name = "HomeCloudConnectionStatus";
        _homeCloudConnectionStatus.AccessibleName =
            "Microsoft Entra / Intune connection status";
        UiStyle.ConfigureStatusLabel(_homeCloudConnectionStatus);
        _homeCloudConnectionStatus.MaximumSize = new Size(1100, 0);
        UiStyle.SetStatus(_homeCloudConnectionStatus,
            "Select AD, Microsoft Entra / Intune, or both to begin.",
            UiStatusKind.Neutral);
        layout.Controls.Add(_homeCloudConnectionStatus, 0, 1);

        _homeConnectionSource.SelectedIndexChanged += async (_, _) =>
        {
            if (_initializingStartSource)
                return;
            await OnStartConnectionSourceSelectedAsync();
        };
        _homeConfigureCloudButton.Click += async (_, _) =>
            await ShowStartCloudWizardAsync(connectAfterSave: true);
        _homeConnectCloudButton.Click += async (_, _) =>
            await ConnectStartCloudAsync();

        UpdateStartConnectionSourceControls();
        return layout;
    }

    private string SelectedStartSource => _startConnectionPreferences.Source;

    private async Task OnStartConnectionSourceSelectedAsync()
    {
        _startConnectionPreferences.Source = _homeConnectionSource.SelectedIndex switch
        {
            1 => StartConnectionPolicy.ActiveDirectory,
            2 => StartConnectionPolicy.MicrosoftCloud,
            3 => StartConnectionPolicy.Both,
            _ => StartConnectionPolicy.Choose
        };

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

        if (StartConnectionPolicy.NeedsMicrosoftCloud(SelectedStartSource) &&
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
            _startAdIdentityPanel.Visible = ad;
        _homeAdvancedButton.Visible = ad;
        _homeEnvironmentCheckButton.Visible = ad;
        _homeConnectionStatus.Visible = ad;
        _homeAdAccountStatus.Visible = ad;

        _homeConnectCloudButton.Visible = cloud;
        _homeConfigureCloudButton.Visible = cloud;
        _homeCloudConnectionStatus.Visible = cloud ||
            SelectedStartSource == StartConnectionPolicy.Choose;
        _homeConnectCloudButton.Enabled = cloud && !_cloudConnecting;
        _homeConfigureCloudButton.Enabled = cloud && !_cloudWizardOpening;

        if (cloud)
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                HasCloudSearchSession()
                    ? "Connected to Microsoft Entra / Intune using delegated Graph permissions."
                    : StartCloudAppConfigured()
                        ? "Cloud is configured. Click Connect to Microsoft 365 to sign in with MFA."
                        : "No cloud application configured. The connection wizard will open.",
                HasCloudSearchSession() ? UiStatusKind.Success :
                StartCloudAppConfigured() ? UiStatusKind.Neutral : UiStatusKind.Warning);
        }
        else if (SelectedStartSource == StartConnectionPolicy.Choose)
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                "Choose where recovery data is stored. No AD or cloud credential prompt is required yet.",
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
        if (_cloudConnecting || _cloudWizardOpening)
            return;

        if (!StartCloudAppConfigured())
        {
            await ShowStartCloudWizardAsync(connectAfterSave: true);
            return;
        }

        // Reuse existing Graph authentication, tenant policy checks and audit.
        // Never request/create privileged app-only tokens implicitly.
        _cloudAuthMode.SelectedIndex =
            _cloudConfig.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase)
                ? 2 : 0;

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
