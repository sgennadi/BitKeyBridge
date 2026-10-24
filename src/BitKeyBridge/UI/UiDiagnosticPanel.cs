namespace BitKeyBridge;

public sealed class UiDiagnosticPanel : Panel
{
    private readonly Label _summary = new();
    private readonly Label _copyStatus = new();
    private readonly Button _copy = UiStyle.CreateActionButton("Copy diagnostics");
    private readonly Button _dismiss = UiStyle.CreateActionButton("Dismiss");
    private string _diagnostics = string.Empty;
    private string _language = "en";

    // The source text can be lost visually when native tab handles are
    // rebuilt. Keep the sanitized report independent of visibility/painting.
    internal bool HasActiveDiagnostics => !string.IsNullOrWhiteSpace(_diagnostics);

    internal void ApplyLanguage(string code)
    {
        _language = UiLanguageService.Normalize(code);
        _copy.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
            _language, "Copy diagnostics");
        _dismiss.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
            _language, "Dismiss");
        if (_copyStatus.Text.Length > 0 &&
            new[] { "en", "ru", "he" }.Any(language =>
                _copyStatus.Text == UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Diagnostics copied. Secrets are redacted.")))
            _copyStatus.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
                _language, "Diagnostics copied. Secrets are redacted.");
    }

    // Also available to the always-visible Tools/status-bar shortcuts.
    internal void CopyCurrentDiagnostics() => CopyDiagnostics();

    // Geometry-only self-test: report content never leaves this panel.
    // Each nested Control must fit its own clipping parent, not merely exist
    // with a translated Text property.
    internal bool AreActionsWithinVisibleBounds()
    {
        if (!HasActiveDiagnostics || !Visible)
            return false;

        return FitsVisibleParents(_copy) && FitsVisibleParents(_dismiss);

        bool FitsVisibleParents(Control action)
        {
            if (!action.Visible || action.IsDisposed ||
                action.Width < UiStyle.MinimumButtonWidth ||
                action.Height < UiStyle.MinimumButtonHeight)
                return false;

            var rectangle = action.Bounds;
            for (Control? parent = action.Parent; parent is not null;
                 parent = parent.Parent)
            {
                if (parent.IsDisposed || !parent.Visible ||
                    !parent.ClientRectangle.Contains(rectangle))
                    return false;
                if (ReferenceEquals(parent, this))
                    return true;
                rectangle.Offset(parent.Left, parent.Top);
            }
            return false;
        }
    }

    public UiDiagnosticPanel()
    {
        Name = "DiagnosticPanel";
        AccessibleName = "Diagnostics";
        // This lives in an AutoSize row of MainLayoutRoot. Dock.Fill makes
        // its bounds fill that row on every DPI/RTL change instead of keeping
        // a narrow preferred-size rectangle whose copy controls are clipped.
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(0, UiStyle.MinimumButtonHeight +
            2 * UiStyle.ControlGap);
        // Only the navigation tab strip should mirror. This report's action
        // strip must never inherit Hebrew RTL or reverse the copy controls.
        RightToLeft = RightToLeft.No;
        Visible = false;
        Padding = new Padding(UiStyle.ControlGap);
        Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap);
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;

        var root = new TableLayoutPanel
        {
            Name = "DiagnosticPanelLayout",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RightToLeft = RightToLeft.No,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Buttons FIRST. Even if summary text expands or an inherited
        // WinForms AutoSize pass clips a later row, copy stays at the top.
        var actions = new FlowLayoutPanel
        {
            Name = "DiagnosticPanelActions",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, UiStyle.MinimumButtonHeight),
            RightToLeft = RightToLeft.No,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        _copy.Name = "CopyDiagnosticsButton";
        _copy.AccessibleName = "Copy redacted diagnostics to clipboard";
        _copy.RightToLeft = RightToLeft.No;
        _dismiss.Name = "DismissDiagnosticsButton";
        _dismiss.RightToLeft = RightToLeft.No;
        actions.Controls.Add(_copy);
        actions.Controls.Add(_dismiss);
        root.Controls.Add(actions, 0, 0);

        _summary.AutoSize = true;
        _summary.Dock = DockStyle.Top;
        _summary.MaximumSize = new Size(980, 0);
        _summary.Font = UiStyle.CreateEmphasisFont();
        _summary.AccessibleName = "Diagnostic reason";
        _summary.Margin = new Padding(0, UiStyle.ControlGap, 0, 0);
        root.Controls.Add(_summary, 0, 1);

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
            _copyStatus.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
                _language, "Diagnostics copied. Secrets are redacted.");
        }
        catch (Exception ex)
        {
            _copyStatus.Text =
                "Could not copy diagnostics: " +
                DiagnosticRedaction.Sanitize(ex.Message);
        }
    }
}
