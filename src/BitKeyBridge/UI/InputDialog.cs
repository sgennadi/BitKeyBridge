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
        ClientSize = new Size(570, 145);

        var label = new Label { Text = prompt, Left = 12, Top = 14, Width = 540, Height = 34 };
        _text.SetBounds(12, 52, 540, 25);
        _text.Text = value;
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 372, Top = 96, Width = 85 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 467, Top = 96, Width = 85 };
        Controls.AddRange([label, _text, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
