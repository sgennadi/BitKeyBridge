namespace BitKeyBridge;

public sealed class SecureOutputDialog : DpiAwareForm
{
    private readonly TextBox _directory = new();
    private readonly TextBox _share = new();
    private readonly TextBox _readers = new();
    private readonly CheckBox _updateConfig = new();

    public string DirectoryPath => _directory.Text.Trim();
    public string ShareName => _share.Text.Trim();
    public IReadOnlyList<string> ReaderPrincipals =>
        _readers.Lines
            .SelectMany(x => x.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    public bool UpdateApplicationConfig => _updateConfig.Checked;

    public SecureOutputDialog(string currentOutputDirectory)
    {
        Text = "Secure BitLocker Output";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 470);
        MinimumSize = new Size(620, 430);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Create a protected BitLocker recovery directory",
            Font = new Font("Segoe UI Semibold", 14F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "NTFS inheritance will be disabled. SYSTEM, local Administrators and the current administrator keep Full Control. Optional reader principals receive Read & Execute only.",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(fields, 0, 2);

        fields.Controls.Add(MakeFieldLabel("Directory:"), 0, 0);
        _directory.Dock = DockStyle.Fill;
        _directory.Text = string.IsNullOrWhiteSpace(currentOutputDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "BitKeyBridge",
                "Recovery")
            : currentOutputDirectory;
        fields.Controls.Add(_directory, 1, 0);

        var browse = new Button
        {
            Text = "Browse...",
            AutoSize = true,
            MinimumSize = new Size(85, 31),
            Margin = new Padding(8, 0, 0, 4)
        };
        fields.Controls.Add(browse, 2, 0);

        fields.Controls.Add(MakeFieldLabel("SMB share:"), 0, 1);
        var sharePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1
        };
        _share.Dock = DockStyle.Top;
        _share.Text = "BitKeyBridgeRecovery$";
        sharePanel.Controls.Add(_share);
        sharePanel.Controls.Add(new Label
        {
            Text = "Optional. Clear this field to create only the protected local directory.",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 8)
        });
        fields.Controls.Add(sharePanel, 1, 1);
        fields.SetColumnSpan(sharePanel, 2);

        fields.Controls.Add(MakeFieldLabel("Readers:"), 0, 2);
        var readerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        readerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        readerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _readers.Dock = DockStyle.Fill;
        _readers.Multiline = true;
        _readers.ScrollBars = ScrollBars.Vertical;
        _readers.MinimumSize = new Size(0, 110);
        readerPanel.Controls.Add(_readers, 0, 0);
        readerPanel.Controls.Add(new Label
        {
            Text = "One account/group per line, for example DOMAIN\\BitLocker-Recovery-Readers. Leave empty for administrators only.",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        }, 0, 1);
        fields.Controls.Add(readerPanel, 1, 2);
        fields.SetColumnSpan(readerPanel, 2);

        _updateConfig.Text = "Use this directory as BitKeyBridge export output";
        _updateConfig.Checked = true;
        _updateConfig.AutoSize = true;
        _updateConfig.Margin = new Padding(0, 8, 0, 0);
        fields.Controls.Add(_updateConfig, 1, 3);
        fields.SetColumnSpan(_updateConfig, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(95, 32)
        };
        var ok = new Button
        {
            Text = "Create Secure Output",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(145, 32)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 4);

        AcceptButton = ok;
        CancelButton = cancel;

        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog
            {
                Description = "Select or create the protected BitLocker recovery output directory",
                UseDescriptionForTitle = true,
                SelectedPath = _directory.Text
            };

            if (picker.ShowDialog(this) == DialogResult.OK)
                _directory.Text = picker.SelectedPath;
        };
    }

    private static Label MakeFieldLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        };
}
