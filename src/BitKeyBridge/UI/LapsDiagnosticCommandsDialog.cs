namespace BitKeyBridge;

/// <summary>Copyable, read-only operator guidance; does not run scripts.</summary>
public sealed class LapsDiagnosticCommandsDialog : DpiAwareForm
{
    public LapsDiagnosticCommandsDialog(string commands)
    {
        Name = "LapsDiagnosticCommandsDialog";
        Text = "LAPS diagnostic commands - where to run them";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(540, 400);
        Size = new Size(940, 690);
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;

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

        var heading = new Label
        {
            Text = "Copy only the checks relevant to your source. Nothing runs automatically. " +
                   "The optional AD live-read test requests a password; never share its output unredacted.",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoEllipsis = false,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        UiStyle.ConfigureWrappingLabel(heading);
        layout.Controls.Add(heading, 0, 0);

        var viewer = new RichTextBox
        {
            Name = "LapsDiagnosticCommandsText",
            AccessibleName = "Copyable LAPS diagnostic commands",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            Font = UiStyle.CreateMonospaceFont(),
            DetectUrls = false,
            Text = commands
        };
        layout.Controls.Add(viewer, 0, 1);

        var actions = new FlowLayoutPanel
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
                Clipboard.SetText(commands);
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Could not copy the commands. You can still select and copy text manually.",
                    "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        actions.Controls.Add(close);
        actions.Controls.Add(copy);
        layout.Controls.Add(actions, 0, 2);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }
}
