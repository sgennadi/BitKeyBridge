namespace BitKeyBridge;

public sealed class SecureOutputDialog : Form
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
        ClientSize = new Size(720, 430);
        MinimumSize = new Size(680, 400);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        var title = new Label
        {
            Text = "Create a protected BitLocker recovery directory",
            Font = new Font("Segoe UI Semibold", 14F),
            AutoSize = true,
            Left = 18,
            Top = 16
        };
        Controls.Add(title);

        var description = new Label
        {
            Text = "NTFS inheritance will be disabled. SYSTEM, local Administrators and the current administrator keep Full Control. Optional reader principals receive Read & Execute only.",
            Left = 20,
            Top = 55,
            Width = 675,
            Height = 45
        };
        Controls.Add(description);

        AddLabel("Directory:", 20, 112);
        _directory.SetBounds(125, 108, 485, 27);
        _directory.Text = string.IsNullOrWhiteSpace(currentOutputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BitKeyBridge", "Recovery")
            : currentOutputDirectory;
        var browse = new Button { Text = "Browse...", Left = 620, Top = 106, Width = 80, Height = 31 };
        Controls.AddRange([_directory, browse]);

        AddLabel("SMB share:", 20, 153);
        _share.SetBounds(125, 149, 260, 27);
        _share.Text = "BitKeyBridgeRecovery$";
        var shareHint = new Label
        {
            Text = "Optional. Clear this field to create only the protected local directory.",
            Left = 395,
            Top = 154,
            Width = 300,
            Height = 25
        };
        Controls.AddRange([_share, shareHint]);

        AddLabel("Readers:", 20, 195);
        _readers.SetBounds(125, 191, 575, 115);
        _readers.Multiline = true;
        _readers.ScrollBars = ScrollBars.Vertical;
        var readerHint = new Label
        {
            Text = "One account/group per line, for example DOMAIN\\BitLocker-Recovery-Readers. Leave empty for administrators only.",
            Left = 125,
            Top = 310,
            Width = 575,
            Height = 38
        };
        Controls.AddRange([_readers, readerHint]);

        _updateConfig.Text = "Use this directory as BitKeyBridge export output";
        _updateConfig.Checked = true;
        _updateConfig.SetBounds(125, 350, 380, 25);
        Controls.Add(_updateConfig);

        var ok = new Button { Text = "Create Secure Output", DialogResult = DialogResult.OK, Left = 450, Top = 388, Width = 145, Height = 32 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 605, Top = 388, Width = 95, Height = 32 };
        Controls.AddRange([ok, cancel]);
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

    private void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label { Text = text, Left = x, Top = y, Width = 95, Height = 24 });
    }
}
