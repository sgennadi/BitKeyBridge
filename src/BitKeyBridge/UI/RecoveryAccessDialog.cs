namespace BitKeyBridge;

public sealed class RecoveryAccessDialog : DpiAwareForm
{
    private readonly TextBox _reference = new();
    private readonly TextBox _reason = new();
    private readonly CheckBox _remember = new();
    private readonly UiStatusLabel _status = new();
    private readonly bool _requireReference;
    private readonly string _referencePattern;

    public RecoveryAccessContext Context => new()
    {
        Reference = _reference.Text.Trim(),
        Reason = _reason.Text.Trim(),
        RemindRotation = false,
        RememberForComputer = _remember.Checked,
        CreatedAtUtc = DateTime.UtcNow
    };

    public RecoveryAccessDialog(
        string computerName,
        string recoveryId,
        bool requireReference,
        string? referencePattern = null,
        string? referenceExample = null)
    {
        _requireReference = requireReference;
        _referencePattern = referencePattern?.Trim() ?? string.Empty;

        Text = "Recovery Access Context";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 440);
        MinimumSize = new Size(560, 400);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = UiStyle.BodyFont;

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
            Text = requireReference
                ? "Record the helpdesk ticket/reference for this recovery access."
                : "Optional recovery access note.",
            Font = UiStyle.CreateSectionTitleFont(),
            AutoSize = true,
            MaximumSize = new Size(620, 0),
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
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 10)
        };
        referenceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        referenceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        referenceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        referenceRow.Controls.Add(new Label
        {
            Text = requireReference ? "Ticket / Reference *:" : "Ticket / Reference:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        }, 0, 0);

        _reference.Dock = DockStyle.Fill;
        _reference.PlaceholderText =
            string.IsNullOrWhiteSpace(referenceExample)
                ? "INC-12345"
                : referenceExample.Trim();
        referenceRow.Controls.Add(_reference, 1, 0);

        var copyReference =
            UiStyle.CreateActionButton(
                "Copy reference");
        copyReference.Enabled = false;
        referenceRow.Controls.Add(copyReference, 2, 0);
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
        _reason.PlaceholderText =
            "Optional note, for example: user is at BitLocker recovery screen";
        details.Controls.Add(_reason, 1, 0);

        _remember.Text =
            "Reuse this ticket/reference and reason for this computer during this BitKeyBridge session";
        _remember.Checked = true;
        _remember.AutoSize = true;
        _remember.Margin = new Padding(0, 8, 0, 4);
        details.Controls.Add(_remember, 1, 1);

        var auditNote = new Label
        {
            Text =
                "Ticket/reference and reason are written to the local security audit. " +
                "The BitLocker recovery password is never written to the audit.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 4, 0, 0)
        };
        details.Controls.Add(auditNote, 1, 2);
        root.Controls.Add(details, 0, 3);

        UiStyle.ConfigureStatusLabel(
            _status);
        _status.AccessibleName =
            "Recovery access validation status";
        UiStyle.SetStatus(
            _status,
            requireReference
                ? "A ticket/reference is required by the configured helpdesk policy."
                : "Ticket/reference is optional.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _status,
            0,
            4);

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
                "Continue");
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 5);

        AcceptButton = ok;
        CancelButton = cancel;

        _reference.TextChanged +=
            (_, _) =>
                copyReference.Enabled =
                    !string.IsNullOrWhiteSpace(
                        _reference.Text);

        copyReference.Click +=
            (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(
                        _reference.Text))
                {
                    Clipboard.SetText(
                        _reference.Text.Trim());
                }
            };

        ok.Click += (_, _) =>
        {
            var reference =
                _reference.Text.Trim();

            if (_requireReference &&
                string.IsNullOrWhiteSpace(reference))
            {
                UiStyle.SetStatus(
                    _status,
                    "A ticket/reference is required before a recovery key can be accessed.",
                    UiStatusKind.Warning);
                _reference.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    reference) &&
                !string.IsNullOrWhiteSpace(
                    _referencePattern))
            {
                bool matches;
                try
                {
                    matches =
                        System.Text.RegularExpressions.Regex.IsMatch(
                            reference,
                            _referencePattern,
                            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                            TimeSpan.FromMilliseconds(250));
                }
                catch
                {
                    matches = false;
                }

                if (!matches)
                {
                    UiStyle.SetStatus(
                        _status,
                        "Ticket/reference does not match the configured format.",
                        UiStatusKind.Warning);
                    _reference.Focus();
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        };
    }
}
