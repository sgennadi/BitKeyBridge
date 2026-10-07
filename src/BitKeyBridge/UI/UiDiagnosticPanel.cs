namespace BitKeyBridge;

public sealed class UiDiagnosticPanel : Panel
{
    private readonly Label _summary = new();
    private readonly Label _copyStatus = new();
    private readonly Button _copy = UiStyle.CreateActionButton("Copy diagnostics");
    private readonly Button _dismiss = UiStyle.CreateActionButton("Dismiss");
    private string _diagnostics = string.Empty;

    public UiDiagnosticPanel()
    {
        Name = "DiagnosticPanel";
        AccessibleName = "Diagnostics";
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Visible = false;
        Padding = new Padding(UiStyle.ControlGap);
        Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap);
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _summary.AutoSize = true;
        _summary.Dock = DockStyle.Top;
        _summary.MaximumSize = new Size(980, 0);
        _summary.Font = UiStyle.CreateEmphasisFont();
        _summary.AccessibleName = "Diagnostic reason";
        root.Controls.Add(_summary, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
        };
        actions.Controls.Add(_copy);
        actions.Controls.Add(_dismiss);
        root.Controls.Add(actions, 0, 1);

        _copyStatus.AutoSize = true;
        _copyStatus.Dock = DockStyle.Top;
        _copyStatus.AccessibleName = "Diagnostic copy status";
        _copyStatus.Margin = new Padding(0, UiStyle.ControlGap, 0, 0);
        root.Controls.Add(_copyStatus, 0, 2);

        Controls.Add(root);

        _copy.Click += (_, _) => CopyDiagnostics();
        _dismiss.Click += (_, _) => Clear();
    }

    public void ShowError(
        string summary,
        string operation,
        Exception exception,
        params (string Name, string? Value)[] context)
    {
        var lines = new List<string>
        {
            $"Time: {DateTimeOffset.Now:O}",
            $"Operation: {operation}",
            $"Exception: {exception.GetType().FullName}",
            $"HResult: 0x{unchecked((uint)exception.HResult):X8}",
            $"Reason: {exception.Message}"
        };

        foreach (var item in context)
        {
            if (string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Value))
            {
                continue;
            }

            lines.Add($"{item.Name}: {item.Value}");
        }

        _summary.Text = DiagnosticRedaction.Sanitize(summary);
        _diagnostics = DiagnosticRedaction.Sanitize(string.Join(Environment.NewLine, lines));
        _copyStatus.Text = string.Empty;
        Visible = true;
        BringToFront();
    }

    public void ShowMessage(
        string summary,
        string diagnostics)
    {
        _summary.Text = DiagnosticRedaction.Sanitize(summary);
        _diagnostics = DiagnosticRedaction.Sanitize(diagnostics);
        _copyStatus.Text = string.Empty;
        Visible = true;
        BringToFront();
    }

    public void Clear()
    {
        _summary.Text = string.Empty;
        _diagnostics = string.Empty;
        _copyStatus.Text = string.Empty;
        Visible = false;
    }

    private void CopyDiagnostics()
    {
        if (string.IsNullOrWhiteSpace(_diagnostics))
            return;

        try
        {
            Clipboard.SetText(_diagnostics);
            _copyStatus.Text = "Diagnostics copied. Secrets are redacted.";
        }
        catch (Exception ex)
        {
            _copyStatus.Text =
                "Could not copy diagnostics: " +
                DiagnosticRedaction.Sanitize(ex.Message);
        }
    }
}
