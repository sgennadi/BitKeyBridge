namespace BitKeyBridge;

public sealed class AdCredentialPromptDialog : DpiAwareForm
{
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly TextBox _domain = new();
    private readonly TextBox _server = new();
    private readonly CheckBox _windowsIdentity = new();
    private readonly UiStatusLabel _status = new();

    public string Username => _username.Text.Trim();
    public string DomainName => _domain.Text.Trim();
    public string Server => _server.Text.Trim();
    public bool UseWindowsIdentity => _windowsIdentity.Checked;

    public AdCredentialPromptDialog(string configuredUsername,
        string configuredDomain = "", string configuredServer = "")
    {
        Text = "Connect to Active Directory";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 440);
        MinimumSize = new Size(480, 340);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = UiStyle.BodyFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1, RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 6; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = "Active Directory credentials", AutoSize = true,
            Font = UiStyle.CreateDialogTitleFont(), Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Enter the account for this connection. The password stays in this process. " +
                "A domain or DC can be entered on a standalone computer. Empty DC uses automatic discovery.",
            AutoSize = true, Dock = DockStyle.Top, MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        }, 0, 1);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        AddField(fields, 0, "Domain:", _domain);
        AddField(fields, 1, "DC / server:", _server);
        AddField(fields, 2, "User:", _username);
        AddField(fields, 3, "Password:", _password);
        _username.Text = configuredUsername;
        _domain.Text = configuredDomain;
        _server.Text = configuredServer;
        _username.PlaceholderText = @"DOMAIN\user or user@domain";
        _domain.PlaceholderText = "Domain DNS name (optional on a domain-joined host)";
        _server.PlaceholderText = "DC DNS name (optional; empty = automatic discovery)";
        _password.UseSystemPasswordChar = true;
        root.Controls.Add(fields, 0, 2);

        _windowsIdentity.Text = "Use current Windows account";
        _windowsIdentity.AutoSize = true;
        _windowsIdentity.Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.SectionGap);
        _windowsIdentity.CheckedChanged += (_, _) =>
        {
            _username.Enabled = _password.Enabled = !_windowsIdentity.Checked;
            _password.Clear();
        };
        root.Controls.Add(_windowsIdentity, 0, 3);

        UiStyle.ConfigureStatusLabel(
            _status);
        _status.AccessibleName =
            "Active Directory credential validation status";
        UiStyle.SetStatus(
            _status,
            "Enter manual credentials or select the current Windows account.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _status,
            0,
            4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true, Margin = new Padding(0)
        };
        var cancel = UiStyle.CreateActionButton("Cancel", DialogResult.Cancel);
        var connect = UiStyle.CreateActionButton("Connect", DialogResult.OK);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(connect);
        root.Controls.Add(buttons, 0, 5);
        AcceptButton = connect;
        CancelButton = cancel;
        connect.Click += (_, _) =>
        {
            if (UseWindowsIdentity || (!string.IsNullOrWhiteSpace(_username.Text) && !string.IsNullOrEmpty(_password.Text)))
                return;
            DialogResult = DialogResult.None;
            UiStyle.SetStatus(
                _status,
                "Enter both the AD user and password, or select the current Windows account.",
                UiStatusKind.Warning);

            if (string.IsNullOrWhiteSpace(
                    _username.Text))
            {
                _username.Focus();
            }
            else
            {
                _password.Focus();
            }
        };
        Shown += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_username.Text)) _username.Focus();
            else _password.Focus();
        };
    }

    public string TakePassword()
    {
        var password = _password.Text;
        _password.Clear();
        return password;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _password.Clear();
        base.Dispose(disposing);
    }

    private static void AddField(TableLayoutPanel fields, int row, string caption, TextBox control)
    {
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.Controls.Add(new Label
        {
            Text = caption, AutoSize = true, Anchor = AnchorStyles.Left,
            Margin = new Padding(0, UiStyle.ControlGap, UiStyle.ControlGap, UiStyle.ControlGap)
        }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap);
        fields.Controls.Add(control, 1, row);
    }
}
