namespace BitKeyBridge;

/// <summary>
/// Ordinary-user cloud onboarding. Unlike the privileged tenant bootstrap
/// wizard, this NEVER creates an app, modifies tenant permissions or requests
/// a privileged credential. It stores only tenant and public client IDs.
/// </summary>
public sealed class CloudConnectionWizardDialog : DpiAwareForm
{
    private readonly RadioButton _approvedApp = new();
    private readonly RadioButton _adminSetup = new();
    private readonly TextBox _tenant = new();
    private readonly TextBox _client = new();
    private readonly Label _requiredPermissions = new();
    private readonly Button _continue = UiStyle.CreateActionButton("Continue");
    private readonly Button _cancel = UiStyle.CreateActionButton("Cancel", DialogResult.Cancel);

    public string TenantId => _tenant.Text.Trim();
    public string ClientId => _client.Text.Trim();
    public bool RequestAdministrativeSetup => _adminSetup.Checked;

    public CloudConnectionWizardDialog(CloudAuthConfig existing)
    {
        Name = "CloudConnectionWizardDialog";
        Text = "Connect to Microsoft Entra / Intune";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(750, 520);
        MinimumSize = new Size(650, 480);
        AutoScroll = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1,
            RowCount = 8,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 7; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var heading = new Label
        {
            Text = "Microsoft Entra / Intune",
            Font = UiStyle.CreatePageTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        root.Controls.Add(heading, 0, 0);

        var explanation = new Label
        {
            Text = "Connect with your work account using Device Code and MFA. " +
                "An IT-approved application and delegated permissions are required. " +
                "This does not grant access to recovery secrets.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        UiStyle.ConfigureWrappingLabel(explanation);
        root.Controls.Add(explanation, 0, 1);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty
        };
        _approvedApp.Name = "CloudWizardExistingApp";
        _approvedApp.Text = "Use an IT-approved app (recommended)";
        _approvedApp.AutoSize = true;
        _approvedApp.Checked = true;
        _adminSetup.Name = "CloudWizardAdministratorSetup";
        _adminSetup.Text = "Administrator: configure an app";
        _adminSetup.AutoSize = true;
        options.Controls.Add(_approvedApp);
        options.Controls.Add(_adminSetup);
        root.Controls.Add(options, 0, 2);

        var ids = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, UiStyle.SectionGap, 0, UiStyle.SectionGap)
        };
        ids.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        ids.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        ids.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _tenant.Name = "CloudWizardTenant";
        _tenant.Dock = DockStyle.Fill;
        _tenant.Text = existing.TenantId;
        _tenant.PlaceholderText = "Tenant ID or verified domain";
        _client.Name = "CloudWizardClient";
        _client.Dock = DockStyle.Fill;
        _client.Text = existing.ClientId;
        _client.PlaceholderText = "Application (client) ID provided by IT";
        ids.Controls.Add(new Label
        {
            Text = "Tenant:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, UiStyle.ControlGap, UiStyle.ControlGap)
        }, 0, 0);
        ids.Controls.Add(_tenant, 1, 0);
        ids.Controls.Add(new Label
        {
            Text = "Client ID:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, UiStyle.ControlGap, 0)
        }, 0, 1);
        ids.Controls.Add(_client, 1, 1);
        root.Controls.Add(ids, 0, 3);

        var showPermissions = UiStyle.CreateActionButton("Show required permissions...");
        showPermissions.Name = "CloudWizardPermissionsToggle";
        showPermissions.AccessibleName = "Show delegated Graph scopes and recovery authorization requirements";
        root.Controls.Add(showPermissions, 0, 4);

        _requiredPermissions.Name = "CloudWizardPermissions";
        _requiredPermissions.AutoSize = true;
        _requiredPermissions.Visible = false;
        _requiredPermissions.MaximumSize = new Size(720, 0);
        _requiredPermissions.Text =
            "The app must have admin-approved delegated Graph permissions. " +
            "For BitLocker metadata: BitlockerKey.ReadBasic.All; " +
            "for recovery passwords: BitlockerKey.Read.All; " +
            "for Entra LAPS passwords: DeviceLocalCredential.Read.All. " +
            "Reading secrets also requires appropriate per-user Entra RBAC; " +
            "an approved app alone does not grant password access.";
        UiStyle.ConfigureWrappingLabel(_requiredPermissions);
        root.Controls.Add(_requiredPermissions, 0, 5);
        showPermissions.Click += (_, _) =>
        {
            _requiredPermissions.Visible = !_requiredPermissions.Visible;
            showPermissions.Text = _requiredPermissions.Visible
                ? "Hide required permissions" : "Show required permissions...";
            root.PerformLayout();
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Margin = new Padding(0, UiStyle.SectionGap, 0, 0)
        };
        _continue.Name = "CloudWizardContinue";
        actions.Controls.Add(_continue);
        actions.Controls.Add(_cancel);
        root.Controls.Add(actions, 0, 6);
        Controls.Add(root);

        _approvedApp.CheckedChanged += (_, _) => UpdateMode();
        _adminSetup.CheckedChanged += (_, _) => UpdateMode();
        _continue.Click += (_, _) => Confirm();
        CancelButton = _cancel;
        AcceptButton = _continue;
        UpdateMode();
    }

    private void UpdateMode()
    {
        _client.Enabled = _approvedApp.Checked;
        _tenant.Enabled = _approvedApp.Checked;
        _continue.Text = _adminSetup.Checked
            ? "Open administrator wizard"
            : "Save and sign in";
    }

    private void Confirm()
    {
        // The administrator setup wizard collects its own tenant/app data.
        // Do not demand a pre-existing registration just to open that flow.
        if (_adminSetup.Checked)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (string.IsNullOrWhiteSpace(TenantId))
        {
            MessageBox.Show(this, "Enter a tenant ID or verified domain.",
                "Microsoft Entra connection", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _tenant.Focus();
            return;
        }

        if (_approvedApp.Checked &&
            !Guid.TryParse(ClientId, out _))
        {
            MessageBox.Show(this, "Enter the application (client) GUID approved by IT.",
                "Microsoft Entra connection", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _client.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
