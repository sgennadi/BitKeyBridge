namespace BitKeyBridge;

public sealed class AdCredentialPromptDialog : DpiAwareForm
{
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();

    public string Username =>
        _username.Text.Trim();

    public AdCredentialPromptDialog(
        string configuredUsername)
    {
        Text =
            "Connect to Active Directory";
        StartPosition =
            FormStartPosition.CenterParent;
        ClientSize =
            new Size(
                600,
                285);
        MinimumSize =
            new Size(
                480,
                250);
        MaximizeBox =
            false;
        MinimizeBox =
            false;
        Font =
            UiStyle.BodyFont;

        var root =
            new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding =
                    new Padding(
                        UiStyle.PagePadding),
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
                100F));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        Controls.Add(
            root);

        var title =
            new Label
            {
                Text =
                    "Active Directory session credentials",
                AutoSize = true,
                Font =
                    UiStyle.CreateDialogTitleFont(),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };

        root.Controls.Add(
            title,
            0,
            0);

        var description =
            new Label
            {
                Text =
                    "Live AD is configured to use explicit session credentials. " +
                    "Enter the account and password for this BitKeyBridge session. " +
                    "The password is not written to appsettings.json.",
                AutoSize = true,
                MaximumSize =
                    new Size(
                        540,
                        0),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            };

        root.Controls.Add(
            description,
            0,
            1);

        var fields =
            new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            };

        fields.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        fields.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        AddField(
            fields,
            0,
            "User:",
            _username);

        AddField(
            fields,
            1,
            "Password:",
            _password);

        _username.Text =
            configuredUsername ?? string.Empty;
        _username.PlaceholderText =
            @"DOMAIN\user or user@domain";

        _password.UseSystemPasswordChar =
            true;

        root.Controls.Add(
            fields,
            0,
            2);

        var buttons =
            new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection =
                    FlowDirection.RightToLeft,
                WrapContents = true,
                Margin =
                    new Padding(
                        0)
            };

        var cancel =
            new Button
            {
                Text = "Cancel",
                DialogResult =
                    DialogResult.Cancel
            };

        UiStyle.ConfigureActionButton(
            cancel);

        var connect =
            new Button
            {
                Text = "Connect",
                DialogResult =
                    DialogResult.OK
            };

        UiStyle.ConfigureActionButton(
            connect);

        buttons.Controls.Add(
            cancel);
        buttons.Controls.Add(
            connect);

        root.Controls.Add(
            buttons,
            0,
            3);

        AcceptButton =
            connect;
        CancelButton =
            cancel;

        connect.Click +=
            (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(
                        _username.Text) &&
                    !string.IsNullOrEmpty(
                        _password.Text))
                {
                    return;
                }

                DialogResult =
                    DialogResult.None;

                MessageBox.Show(
                    this,
                    "Enter both the Active Directory user and password.",
                    "Active Directory Credentials",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

        Shown +=
            (_, _) =>
            {
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
    }

    public string TakePassword()
    {
        var value =
            _password.Text;

        _password.Clear();
        return value;
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
            _password.Clear();

        base.Dispose(
            disposing);
    }

    private static void AddField(
        TableLayoutPanel layout,
        int row,
        string caption,
        Control control)
    {
        var label =
            new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor =
                    AnchorStyles.Left,
                Margin =
                    new Padding(
                        0,
                        7,
                        UiStyle.ControlGap,
                        6)
            };

        layout.Controls.Add(
            label,
            0,
            row);

        control.Dock =
            DockStyle.Top;
        control.Margin =
            new Padding(
                0,
                3,
                0,
                6);

        layout.Controls.Add(
            control,
            1,
            row);
    }
}
