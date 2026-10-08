namespace BitKeyBridge;

public sealed class SecretDisplayDialog : DpiAwareForm
{
    private readonly TextBox _secret = new();
    private readonly UiStatusLabel _lifetimeStatus = new();
    private readonly System.Windows.Forms.Timer _countdown = new();
    private DateTime? _clipboardExpiresUtc;

    public SecretDisplayDialog(
        string title,
        string description,
        string secret,
        string footer,
        int clipboardSeconds = 120)
    {
        clipboardSeconds =
            Math.Clamp(
                clipboardSeconds,
                5,
                600);

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 370);
        MinimumSize = new Size(600, 320);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = UiStyle.BodyFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
        _secret.Font = UiStyle.CreateMonospaceFont(10F);
        secretRow.Controls.Add(_secret, 0, 0);

        var copy =
            UiStyle.CreateActionButton(
                "Copy");
        copy.Margin =
            new Padding(
                10,
                0,
                0,
                0);
        secretRow.Controls.Add(copy, 1, 0);
        root.Controls.Add(secretRow, 0, 1);

        UiStyle.ConfigureStatusLabel(
            _lifetimeStatus);
        _lifetimeStatus.AccessibleName =
            "Sensitive clipboard lifetime";
        UiStyle.SetStatus(
            _lifetimeStatus,
            "The recovery secret is visible only in this window. Clipboard is currently clear.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _lifetimeStatus,
            0,
            2);

        root.Controls.Add(new Label
        {
            Text = footer,
            AutoSize = true,
            MaximumSize = new Size(700, 0)
        }, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        var close =
            UiStyle.CreateActionButton(
                "Close",
                DialogResult.OK);
        var clearClipboard =
            UiStyle.CreateActionButton(
                "Clear clipboard now");
        clearClipboard.Enabled = false;
        buttons.Controls.Add(close);
        buttons.Controls.Add(clearClipboard);
        root.Controls.Add(buttons, 0, 4);

        AcceptButton = close;
        CancelButton = close;

        _countdown.Interval =
            1000;
        _countdown.Tick +=
            (_, _) =>
            {
                if (_clipboardExpiresUtc is null)
                    return;

                var seconds =
                    Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (_clipboardExpiresUtc.Value -
                             DateTime.UtcNow)
                            .TotalSeconds));

                if (seconds <= 0)
                {
                    ClearTrackedClipboard();
                    copy.Text = "Copy";
                    clearClipboard.Enabled = false;
                    UiStyle.SetStatus(
                        _lifetimeStatus,
                        "Clipboard cleared automatically.",
                        UiStatusKind.Success);
                    return;
                }

                UiStyle.SetStatus(
                    _lifetimeStatus,
                    $"Clipboard clears in {TimeSpan.FromSeconds(seconds):mm\\:ss}.",
                    UiStatusKind.Warning);
            };

        copy.Click +=
            (_, _) =>
            {
                try
                {
                    SecureClipboard.SetSensitiveText(
                        _secret.Text);
                    _clipboardExpiresUtc =
                        DateTime.UtcNow.AddSeconds(
                            clipboardSeconds);
                    copy.Text = "Copied";
                    clearClipboard.Enabled = true;
                    _countdown.Start();
                }
                catch (Exception ex)
                {
                    WindowsEventLogService.TryWrite(
                        "Sensitive clipboard copy failed: " + ex.Message,
                        EventLogSeverity.Warning,
                        4575,
                        "Clipboard");
                }
            };

        clearClipboard.Click +=
            (_, _) =>
            {
                ClearTrackedClipboard();
                copy.Text = "Copy";
                clearClipboard.Enabled = false;
                UiStyle.SetStatus(
                    _lifetimeStatus,
                    "Clipboard cleared.",
                    UiStatusKind.Success);
            };

        FormClosed +=
            (_, _) =>
            {
                _countdown.Stop();
                _countdown.Dispose();
                ClearTrackedClipboard();
                _secret.Clear();
            };
    }

    private void ClearTrackedClipboard()
    {
        _countdown.Stop();

        try
        {
            SecureClipboard.ClearIfMatches(
                _secret.Text);
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Secret clipboard cleanup failed: " + ex.Message,
                EventLogSeverity.Warning,
                4576,
                "Clipboard");
        }

        _clipboardExpiresUtc =
            null;
    }
}
