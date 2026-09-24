namespace BitKeyBridge;

public sealed class RbacSettingsDialog : DpiAwareForm
{
    private readonly CheckBox _enabled = new();
    private readonly CheckBox _adminBypass = new();
    private readonly TextBox _readers = new();
    private readonly TextBox _rotators = new();
    private readonly TextBox _administrators = new();
    private readonly Label _status = new();

    public bool RbacEnabled => _enabled.Checked;
    public bool AllowLocalAdministrators => _adminBypass.Checked;
    public List<string> RecoveryReaders => ParsePrincipals(_readers.Text);
    public List<string> RotationOperators => ParsePrincipals(_rotators.Text);
    public List<string> Administrators => ParsePrincipals(_administrators.Text);

    public RbacSettingsDialog(AppConfig config)
    {
        Text = "BitKeyBridge RBAC";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 800);
        MinimumSize = new Size(650, 620);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 8; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Windows User / Group Authorization",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text =
                "RBAC controls recovery-password access and Intune rotation inside BitKeyBridge. " +
                "Enter DOMAIN\\group, DOMAIN\\user, local account/group, or SID — one per line.",
            AutoSize = true,
            MaximumSize = new Size(750, 0),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 1);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        _enabled.Text = "Enable RBAC";
        _enabled.AutoSize = true;
        _enabled.Checked = config.RbacEnabled;
        _adminBypass.Text =
            "Allow members of local Administrators to bypass RBAC";
        _adminBypass.AutoSize = true;
        _adminBypass.Checked = config.RbacAllowLocalAdministrators;
        options.Controls.Add(_enabled);
        options.Controls.Add(_adminBypass);
        root.Controls.Add(options, 0, 2);

        _readers.Text = string.Join(
            Environment.NewLine,
            config.RbacRecoveryReaders);
        root.Controls.Add(
            BuildPrincipalGroup(
                "Recovery Readers",
                "May search the local recovery CSV, retrieve/reveal/copy AD or Entra recovery passwords.",
                _readers),
            0,
            3);

        _rotators.Text = string.Join(
            Environment.NewLine,
            config.RbacRotationOperators);
        root.Controls.Add(
            BuildPrincipalGroup(
                "Rotation Operators",
                "May submit Intune BitLocker recovery-key rotation requests. RecoveryRead is independent.",
                _rotators),
            0,
            4);

        _administrators.Text = string.Join(
            Environment.NewLine,
            config.RbacAdministrators);
        root.Controls.Add(
            BuildPrincipalGroup(
                "BitKeyBridge Administrators",
                "May see Administration and Health & Audit sections without being a local Windows Administrator.",
                _administrators),
            0,
            5);

        _status.Text =
            "Current identity: " + AuthorizationService.CurrentIdentityName();
        _status.AutoSize = true;
        _status.MaximumSize = new Size(750, 0);
        _status.Margin = new Padding(0, 8, 0, 8);
        root.Controls.Add(_status, 0, 6);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var cancel = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            MinimumSize = new Size(105, 34)
        };
        var save = new Button
        {
            Text = "Save",
            AutoSize = true,
            MinimumSize = new Size(105, 34)
        };
        var validate = new Button
        {
            Text = "Validate",
            AutoSize = true,
            MinimumSize = new Size(110, 34)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(validate);
        root.Controls.Add(buttons, 0, 7);

        validate.Click += (_, _) => ValidateSettings(showSuccess: true);
        save.Click += (_, _) =>
        {
            if (!ValidateSettings(showSuccess: false))
                return;

            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
    }

    private static GroupBox BuildPrincipalGroup(
        string title,
        string description,
        TextBox editor)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 0);

        editor.Multiline = true;
        editor.ScrollBars = ScrollBars.Vertical;
        editor.Dock = DockStyle.Top;
        editor.Height = 96;
        editor.MinimumSize = new Size(0, 80);
        layout.Controls.Add(editor, 0, 1);

        group.Controls.Add(layout);
        return group;
    }

    private bool ValidateSettings(bool showSuccess)
    {
        var config = new AppConfig
        {
            RbacEnabled = _enabled.Checked,
            RbacAllowLocalAdministrators = _adminBypass.Checked,
            RbacRecoveryReaders = RecoveryReaders,
            RbacRotationOperators = RotationOperators,
            RbacAdministrators = Administrators
        };

        if (config.RbacEnabled &&
            !config.RbacAllowLocalAdministrators &&
            config.RbacRecoveryReaders.Count == 0 &&
            config.RbacRotationOperators.Count == 0 &&
            config.RbacAdministrators.Count == 0)
        {
            _status.Text =
                "Invalid: RBAC would deny all privileged actions.";
            MessageBox.Show(
                this,
                _status.Text,
                "RBAC Validation",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        var errors = new AuthorizationService(config)
            .ValidateConfiguredPrincipals();

        if (errors.Count > 0)
        {
            _status.Text =
                $"Validation failed: {errors.Count} unresolved principal(s).";
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, errors),
                "RBAC Validation",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        var auth = new AuthorizationService(config);
        var read = auth.Check(BitKeyBridgePermission.RecoveryRead);
        var rotate = auth.Check(BitKeyBridgePermission.Rotate);
        var admin = auth.Check(BitKeyBridgePermission.Administrator);

        _status.Text =
            $"Current identity: {AuthorizationService.CurrentIdentityName()} | " +
            $"RecoveryRead={(read.Allowed ? "Allowed" : "Denied")} | " +
            $"Rotate={(rotate.Allowed ? "Allowed" : "Denied")} | " +
            $"AdminUI={(admin.Allowed ? "Allowed" : "Denied")}";

        if (showSuccess)
        {
            MessageBox.Show(
                this,
                "RBAC principals resolved successfully." +
                Environment.NewLine + Environment.NewLine +
                _status.Text,
                "RBAC Validation",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        return true;
    }

    private static List<string> ParsePrincipals(string value) =>
        value
            .Split(
                ['\r', '\n', ';', ','],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
