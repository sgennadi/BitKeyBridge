namespace BitKeyBridge;

public sealed class SecretDisplayDialog : DpiAwareForm
{
    private readonly TextBox _secret = new();

    public SecretDisplayDialog(
        string title,
        string description,
        string secret,
        string footer)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 330);
        MinimumSize = new Size(700, 300);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        var descriptionLabel = new Label
        {
            Text = description,
            Left = 18,
            Top = 18,
            Width = 720,
            Height = 62
        };
        Controls.Add(descriptionLabel);

        _secret.SetBounds(18, 92, 610, 28);
        _secret.ReadOnly = true;
        _secret.Text = secret;
        _secret.Font = new Font("Consolas", 10F);
        Controls.Add(_secret);

        var copy = new Button
        {
            Text = "Copy",
            Left = 640,
            Top = 90,
            Width = 95,
            Height = 32
        };
        Controls.Add(copy);

        var footerLabel = new Label
        {
            Text = footer,
            Left = 18,
            Top = 142,
            Width = 720,
            Height = 88
        };
        Controls.Add(footerLabel);

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Left = 640,
            Top = 270,
            Width = 95,
            Height = 32
        };
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;

        copy.Click += async (_, _) =>
        {
            try
            {
                Clipboard.SetText(_secret.Text);
                copy.Text = "Copied";
                await Task.Delay(120000);
                try
                {
                    if (Clipboard.ContainsText() &&
                        string.Equals(Clipboard.GetText(), _secret.Text, StringComparison.Ordinal))
                        Clipboard.Clear();
                }
                catch { }
                if (!IsDisposed) copy.Text = "Copy";
            }
            catch { }
        };

        FormClosed += (_, _) =>
        {
            try
            {
                if (Clipboard.ContainsText() &&
                    string.Equals(Clipboard.GetText(), _secret.Text, StringComparison.Ordinal))
                    Clipboard.Clear();
            }
            catch { }
            _secret.Clear();
        };
    }
}
