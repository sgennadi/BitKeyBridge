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
        ClientSize = new Size(700, 390);
        MinimumSize = new Size(660, 360);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        Controls.Add(new Label
        {
            Text = "Record why this BitLocker recovery secret is being accessed.",
            Font = new Font("Segoe UI Semibold", 13F),
            Left = 18,
            Top = 16,
            AutoSize = true
        });

        Controls.Add(new Label
        {
            Text = $"Computer: {computerName}",
            Left = 20,
            Top = 58,
            Width = 650,
            Height = 24
        });
        Controls.Add(new Label
        {
            Text = $"Recovery ID: {recoveryId}",
            Left = 20,
            Top = 82,
            Width = 650,
            Height = 24
        });

        Controls.Add(new Label
        {
            Text = requireReference ? "Ticket / Reference *:" : "Ticket / Reference:",
            Left = 20,
            Top = 124,
            Width = 135,
            Height = 24
        });
        _reference.SetBounds(160, 120, 510, 27);
        Controls.Add(_reference);

        Controls.Add(new Label
        {
            Text = "Reason:",
            Left = 20,
            Top = 166,
            Width = 135,
            Height = 24
        });
        _reason.SetBounds(160, 162, 510, 86);
        _reason.Multiline = true;
        _reason.ScrollBars = ScrollBars.Vertical;
        Controls.Add(_reason);

        _rotationReminder.Text =
            "Remind me to rotate the Intune recovery key after recovery is complete";
        _rotationReminder.Checked = allowRotationReminder && defaultRotationReminder;
        _rotationReminder.Enabled = allowRotationReminder;
        _rotationReminder.SetBounds(160, 260, 510, 28);
        Controls.Add(_rotationReminder);

        Controls.Add(new Label
        {
            Text = "The ticket/reference and reason are written to the local security audit. The recovery password is never written to the audit.",
            Left = 160,
            Top = 294,
            Width = 510,
            Height = 42
        });

        var ok = new Button
        {
            Text = "Continue",
            Left = 470,
            Top = 346,
            Width = 95,
            Height = 32
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 575,
            Top = 346,
            Width = 95,
            Height = 32
        };
        Controls.AddRange([ok, cancel]);
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
