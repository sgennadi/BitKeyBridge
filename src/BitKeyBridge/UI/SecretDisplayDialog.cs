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
        ClientSize = new Size(760, 340);
        MinimumSize = new Size(600, 300);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var secretRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14)
        };
        secretRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        secretRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _secret.Dock = DockStyle.Fill;
        _secret.ReadOnly = true;
        _secret.Text = secret;
        _secret.Font = new Font("Consolas", 10F);
        secretRow.Controls.Add(_secret, 0, 0);

        var copy = new Button
        {
            Text = "Copy",
            AutoSize = true,
            MinimumSize = new Size(95, 32),
            Margin = new Padding(10, 0, 0, 0)
        };
        secretRow.Controls.Add(copy, 1, 0);
        root.Controls.Add(secretRow, 0, 1);

        root.Controls.Add(new Label
        {
            Text = footer,
            AutoSize = true,
            MaximumSize = new Size(700, 0)
        }, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(95, 32)
        };
        buttons.Controls.Add(close);
        root.Controls.Add(buttons, 0, 3);

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
                        string.Equals(
                            Clipboard.GetText(),
                            _secret.Text,
                            StringComparison.Ordinal))
                    {
                        Clipboard.Clear();
                    }
                }
                catch
                {
                }

                if (!IsDisposed)
                    copy.Text = "Copy";
            }
            catch
            {
            }
        };

        FormClosed += (_, _) =>
        {
            try
            {
                if (Clipboard.ContainsText() &&
                    string.Equals(
                        Clipboard.GetText(),
                        _secret.Text,
                        StringComparison.Ordinal))
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
            }

            _secret.Clear();
        };
    }
}
