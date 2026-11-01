namespace BitKeyBridge;

public sealed class UiDiagnosticPanel : Panel
{
    private readonly Label _summary = new();
    private readonly Label _copyStatus = new();
    private readonly Button _copy = UiStyle.CreateActionButton("Copy diagnostics");
    private readonly Button _dismiss = UiStyle.CreateActionButton("Dismiss");
    private string _diagnostics = string.Empty;
    private string _language = "en";
    private bool _compactHeight;

    // The diagnostics strip must consume its *content* height, never fill a
    // phantom AutoSize row above Start. Short windows keep both buttons and
    // the complete summary; the full sanitized report stays copyable.
    internal void SetCompactHeight(bool compact)
    {
        if (_compactHeight == compact)
            return;
        _compactHeight = compact;
        SuspendLayout();
        try
        {
            var gap = compact ? Math.Max(3, UiStyle.ControlGap / 2) :
                UiStyle.ControlGap;
            Padding = new Padding(gap);
            Margin = new Padding(0, compact ? 0 : gap, 0, gap);
            _summary.Margin = new Padding(0, 0, gap, 0);
            _copyStatus.Margin = new Padding(
                0, compact ? 0 : Math.Max(2, gap / 2), gap, 0);
            UpdateSummaryWrapWidth();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

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
        UpdateSummaryWrapWidth();
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
        // Dock.Top fills the AutoSize cell's width WITHOUT feeding an
        // artificially expanded cell height back into its preferred height.
        // Dock.Fill previously left a tall empty diagnostic band on short
        // desktop/RDP windows, hiding the recovery cards.
        Dock = DockStyle.Top;
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
            // A simple notice should not reserve a tall empty band above
            // Start. Keep the summary and Copy/Dismiss on the same line.
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
            Margin = new Padding(UiStyle.ControlGap, 0, 0, 0)
        };
        _copy.Name = "CopyDiagnosticsButton";
        _copy.AccessibleName = "Copy redacted diagnostics to clipboard";
        _copy.RightToLeft = RightToLeft.No;
        _dismiss.Name = "DismissDiagnosticsButton";
        _dismiss.RightToLeft = RightToLeft.No;
        actions.Controls.Add(_copy);
        actions.Controls.Add(_dismiss);
        root.Controls.Add(actions, 1, 0);
        root.SetRowSpan(actions, 2);

        _summary.AutoSize = true;
        _summary.Dock = DockStyle.Top;
        _summary.MaximumSize = new Size(980, 0);
        _summary.Font = UiStyle.CreateEmphasisFont();
        _summary.AccessibleName = "Diagnostic reason";
        _summary.Margin = new Padding(0, 0, UiStyle.ControlGap, 0);
        root.Controls.Add(_summary, 0, 0);

        _copyStatus.AutoSize = true;
        _copyStatus.Dock = DockStyle.Top;
        _copyStatus.AccessibleName = "Diagnostic copy status";
        _copyStatus.Margin = new Padding(
            0, Math.Max(2, UiStyle.ControlGap / 2), UiStyle.ControlGap, 0);
        root.Controls.Add(_copyStatus, 0, 1);

        Controls.Add(root);
        SizeChanged += (_, _) => UpdateSummaryWrapWidth();

        _copy.Click += (_, _) => CopyDiagnostics();
        _dismiss.Click += (_, _) => Clear();
    }

    private void UpdateSummaryWrapWidth()
    {
        if (IsDisposed || ClientSize.Width < 160)
            return;

        // AutoSize summaries must wrap BEFORE they push Copy/Dismiss
        // out of the visible two-column viewport at 200% DPI.
        var actionWidth = _copy.Parent?.GetPreferredSize(Size.Empty).Width ??
            (2 * UiStyle.MinimumButtonWidth);
        var remaining = ClientSize.Width - Padding.Horizontal -
            actionWidth - UiStyle.ControlGap - 8;
        var maximum = Math.Max(110, Math.Min(980, remaining));
        if (_summary.MaximumSize.Width != maximum)
            _summary.MaximumSize = new Size(maximum, 0);
        if (_copyStatus.MaximumSize.Width != maximum)
            _copyStatus.MaximumSize = new Size(maximum, 0);
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
        UpdateSummaryWrapWidth();
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
        UpdateSummaryWrapWidth();
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
