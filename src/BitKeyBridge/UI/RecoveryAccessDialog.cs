namespace BitKeyBridge;

public sealed class RecoveryAccessDialog : DpiAwareForm
{
    private readonly TextBox _reference = new();
    private readonly TextBox _reason = new();
    private readonly CheckBox _rotationReminder = new();
    private readonly bool _requireReference;

    public RecoveryAccessContext Context => new()
    {
        Reference = _reference.Text.Trim(),
        Reason = _reason.Text.Trim(),
        RemindRotation = _rotationReminder.Checked,
        CreatedAtUtc = DateTime.UtcNow
    };

    public RecoveryAccessDialog(
        string computerName,
        string recoveryId,
        bool requireReference,
        bool allowRotationReminder,
        bool defaultRotationReminder)
    {
        _requireReference = requireReference;

        Text = "Recovery Access Context";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 430);
        MinimumSize = new Size(560, 390);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Record why this BitLocker recovery secret is being accessed.",
            Font = new Font("Segoe UI Semibold", 13F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var device = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        device.Controls.Add(new Label
        {
            Text = $"Computer: {computerName}",
            AutoSize = true,
            AutoEllipsis = true
        });
        device.Controls.Add(new Label
        {
            Text = $"Recovery ID: {recoveryId}",
            AutoSize = true,
            AutoEllipsis = true
        });
        root.Controls.Add(device, 0, 1);

        var referenceRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        referenceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        referenceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        referenceRow.Controls.Add(new Label
        {
            Text = requireReference ? "Ticket / Reference *:" : "Ticket / Reference:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        }, 0, 0);
        _reference.Dock = DockStyle.Fill;
        referenceRow.Controls.Add(_reference, 1, 0);
        root.Controls.Add(referenceRow, 0, 2);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 8)
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        details.Controls.Add(new Label
        {
            Text = "Reason:",
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        }, 0, 0);

        _reason.Dock = DockStyle.Fill;
        _reason.Multiline = true;
        _reason.ScrollBars = ScrollBars.Vertical;
        _reason.MinimumSize = new Size(0, 90);
        details.Controls.Add(_reason, 1, 0);

        _rotationReminder.Text =
            "Remind me to rotate the Intune recovery key after recovery is complete";
        _rotationReminder.Checked = allowRotationReminder && defaultRotationReminder;
        _rotationReminder.Enabled = allowRotationReminder;
        _rotationReminder.AutoSize = true;
        _rotationReminder.Margin = new Padding(0, 8, 0, 4);
        details.Controls.Add(_rotationReminder, 1, 1);

        var auditNote = new Label
        {
            Text = "The ticket/reference and reason are written to the local security audit. The recovery password is never written to the audit.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 4, 0, 0)
        };
        details.Controls.Add(auditNote, 1, 2);
        root.Controls.Add(details, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(95, 34)
        };
        var ok = new Button
        {
            Text = "Continue",
            AutoSize = true,
            MinimumSize = new Size(95, 34)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 5);

        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            if (_requireReference && string.IsNullOrWhiteSpace(_reference.Text))
            {
                MessageBox.Show(
                    this,
                    "A ticket/reference is required before a recovery key can be accessed.",
                    "Recovery Access Context",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _reference.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
    }
}
