namespace BitKeyBridge;

public sealed class RbacSettingsDialog : DpiAwareForm
{
    private readonly CheckBox _enabled = new();
    private readonly CheckBox _adminBypass = new();
    private readonly TextBox _readers = new();
    private readonly TextBox _rotators = new();
    private readonly Label _status = new();

    public bool RbacEnabled => _enabled.Checked;
    public bool AllowLocalAdministrators => _adminBypass.Checked;
    public List<string> RecoveryReaders => ParsePrincipals(_readers.Text);
    public List<string> RotationOperators => ParsePrincipals(_rotators.Text);

    public RbacSettingsDialog(AppConfig config)
    {
        Text = "BitKeyBridge RBAC";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 620);
        MinimumSize = new Size(700, 560);
        Font = new Font("Segoe UI", 9F);

        var title = new Label
        {
            Text = "Windows User / Group Authorization",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Left = 18,
            Top = 16
        };
        Controls.Add(title);

        var note = new Label
        {
            Text =
                "RBAC controls recovery-password access and Intune rotation inside BitKeyBridge. " +
                "Enter DOMAIN\\group, DOMAIN\\user, local account/group, or SID — one per line.",
            Left = 20,
            Top = 55,
            Width = 700,
            Height = 46
        };
        Controls.Add(note);

        _enabled.Text = "Enable RBAC";
        _enabled.SetBounds(20, 105, 150, 26);
        _enabled.Checked = config.RbacEnabled;
        Controls.Add(_enabled);

        _adminBypass.Text =
            "Allow members of local Administrators to bypass RBAC";
        _adminBypass.SetBounds(200, 105, 420, 26);
        _adminBypass.Checked = config.RbacAllowLocalAdministrators;
        Controls.Add(_adminBypass);

        Controls.Add(new Label
        {
            Text = "Recovery Readers",
            Left = 20,
            Top = 148,
            Width = 200,
            Height = 24,
            Font = new Font("Segoe UI Semibold", 10F)
        });

        Controls.Add(new Label
        {
            Text =
                "May search the local recovery CSV, retrieve/reveal/copy AD or Entra recovery passwords.",
            Left = 20,
            Top = 173,
            Width = 700,
            Height = 24
        });

        _readers.Multiline = true;
        _readers.ScrollBars = ScrollBars.Vertical;
        _readers.SetBounds(20, 202, 700, 110);
        _readers.Text = string.Join(
            Environment.NewLine,
            config.RbacRecoveryReaders);
        Controls.Add(_readers);

        Controls.Add(new Label
        {
            Text = "Rotation Operators",
            Left = 20,
            Top = 327,
            Width = 200,
            Height = 24,
            Font = new Font("Segoe UI Semibold", 10F)
        });

        Controls.Add(new Label
        {
            Text =
                "May submit Intune BitLocker recovery-key rotation requests. RecoveryRead is independent.",
            Left = 20,
            Top = 352,
            Width = 700,
            Height = 24
        });

        _rotators.Multiline = true;
        _rotators.ScrollBars = ScrollBars.Vertical;
        _rotators.SetBounds(20, 381, 700, 100);
        _rotators.Text = string.Join(
            Environment.NewLine,
            config.RbacRotationOperators);
        Controls.Add(_rotators);

        _status.SetBounds(20, 492, 700, 45);
        _status.Text =
            "Current identity: " + AuthorizationService.CurrentIdentityName();
        Controls.Add(_status);

        var validate = new Button
        {
            Text = "Validate",
            Left = 20,
            Top = 542,
            Width = 110,
            Height = 34
        };
        var save = new Button
        {
            Text = "Save",
            Left = 495,
            Top = 542,
            Width = 105,
            Height = 34
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Left = 615,
            Top = 542,
            Width = 105,
            Height = 34
        };
        Controls.AddRange([validate, save, cancel]);

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

    private bool ValidateSettings(bool showSuccess)
    {
        var config = new AppConfig
        {
            RbacEnabled = _enabled.Checked,
            RbacAllowLocalAdministrators = _adminBypass.Checked,
            RbacRecoveryReaders = RecoveryReaders,
            RbacRotationOperators = RotationOperators
        };

        if (config.RbacEnabled &&
            !config.RbacAllowLocalAdministrators &&
            config.RbacRecoveryReaders.Count == 0 &&
            config.RbacRotationOperators.Count == 0)
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

        _status.Text =
            $"Current identity: {AuthorizationService.CurrentIdentityName()} | " +
            $"RecoveryRead={(read.Allowed ? "Allowed" : "Denied")} | " +
            $"Rotate={(rotate.Allowed ? "Allowed" : "Denied")}";

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
