namespace BitKeyBridge;

/// <summary>
/// Manual, copyable BitLocker troubleshooting instructions. No scripts run.
/// </summary>
public sealed class BitLockerDiagnosticCommandsDialog : DpiAwareForm
{
    public BitLockerDiagnosticCommandsDialog(string instructions)
    {
        Name = "BitLockerDiagnosticCommandsDialog";
        Text = "BitLocker diagnostic steps and commands";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Size = new Size(980, 700);
        MinimumSize = new Size(570, 440);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            Text = "Run the commands only on the indicated workstation/DC. Default probes read metadata only. " +
                   "The OPTIONAL permission probe retrieves a secret attribute; never share its raw output.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        UiStyle.ConfigureWrappingLabel(intro);
        layout.Controls.Add(intro, 0, 0);

        var viewer = new RichTextBox
        {
            Name = "BitLockerDiagnosticCommandsText",
            AccessibleName = "Manual BitLocker recovery troubleshooting commands",
            ReadOnly = true,
            Dock = DockStyle.Fill,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            DetectUrls = false,
            Font = UiStyle.CreateMonospaceFont(),
            Text = instructions
        };
        layout.Controls.Add(viewer, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
        };
        var close = UiStyle.CreateActionButton("Close");
        close.DialogResult = DialogResult.Cancel;
        close.Click += (_, _) => Close();
        var copy = UiStyle.CreateActionButton("Copy commands");
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(instructions);
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Clipboard unavailable. Select the required text manually.",
                    "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        CancelButton = close;
        AcceptButton = close;
    }
}
