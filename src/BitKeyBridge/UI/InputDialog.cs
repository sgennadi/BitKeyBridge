namespace BitKeyBridge;

public sealed class InputDialog : DpiAwareForm
{
    private readonly TextBox _text = new();
    public string Value => _text.Text.Trim();

    public InputDialog(string title, string prompt, string value = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(570, 170);
        MinimumSize = new Size(430, 170);
        Font = UiStyle.BodyFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        var label = new Label
        {
            Text = prompt,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(label, 0, 0);

        _text.Dock = DockStyle.Top;
        _text.Text = value;
        _text.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(_text, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        var cancel =
            UiStyle.CreateActionButton(
                "Cancel",
                DialogResult.Cancel);
        var ok =
            UiStyle.CreateActionButton(
                "OK",
                DialogResult.OK);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
