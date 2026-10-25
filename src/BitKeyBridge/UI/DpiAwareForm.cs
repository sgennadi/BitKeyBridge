namespace BitKeyBridge;

/// <summary>
/// Shared WinForms base for BitKeyBridge. UI coordinates are authored at the
/// standard 96-DPI logical baseline; this keeps forms and dialogs consistent
/// when Windows display scaling changes between monitors.
/// </summary>
public class DpiAwareForm : Form
{
    private readonly Dictionary<Label, Size> _labelMaximumSizes =
        new();
    // A tab with many captions can fire dozens of VisibleChanged events.
    // Batch them once on the UI thread; never capture or log label contents.
    private readonly HashSet<Label> _pendingVisibleLabelWidths = new();
    private bool _visibleLabelWidthRefreshPosted;
    private int _visibleLabelWidthBatchCount;

    internal int VisibleLabelWidthBatchCountForUiSelfTest =>
        _visibleLabelWidthBatchCount;
    internal int PendingVisibleLabelWidthsForUiSelfTest =>
        _pendingVisibleLabelWidths.Count;

    // WinForms can send hundreds of WM_SIZE notifications while dragging or
    // reconnecting an RDP session. Batch expensive text measurement.
    private readonly System.Windows.Forms.Timer _responsiveLabelTimer =
        new() { Interval = 65 };
    private readonly System.Windows.Forms.Timer _responsiveLayoutTimer =
        new() { Interval = 65 };
    private bool _responsiveLayoutRunning;
    private bool _responsiveLayoutInitialized;
    private int _lastFullLayoutDpi;
    private bool _lastHighContrast;

    public DpiAwareForm()
    {
        AutoScaleMode =
            AutoScaleMode.Dpi;
        AutoScaleDimensions =
            new SizeF(
                UiStyle.BaselineDpi,
                UiStyle.BaselineDpi);
        Font =
            UiStyle.BodyFont;

        _responsiveLabelTimer.Tick += (_, _) =>
        {
            _responsiveLabelTimer.Stop();
            if (!IsDisposed && !Disposing && IsHandleCreated)
                RefreshResponsiveLabelWidths();
        };
        _responsiveLayoutTimer.Tick += (_, _) =>
        {
            _responsiveLayoutTimer.Stop();
            if (!IsDisposed && !Disposing && IsHandleCreated)
                RefreshResponsiveLayout();
        };
        // A height-only resize cannot change label wrapping; ignore it.
        var lastMeasuredWidth = 0;
        SizeChanged += (_, _) =>
        {
            if (!IsHandleCreated || IsDisposed || Disposing ||
                ClientSize.Width == lastMeasuredWidth)
                return;

            lastMeasuredWidth = ClientSize.Width;
            _responsiveLabelTimer.Stop();
            _responsiveLabelTimer.Start();
        };
        Disposed += (_, _) =>
        {
            _pendingVisibleLabelWidths.Clear();
            _responsiveLabelTimer.Dispose();
            _responsiveLayoutTimer.Dispose();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Reuse the per-user EN/RU/HE preference for every modal dialog.
        // Perform this once before DPI/layout measurement. The main shell
        // and explicit-language recovery workflow have their own localizer.
        if (this is not MainForm && this is not RecoveryWorkflowDialog)
            UiDialogLocalization.Apply(this);

        // A scrollbar is preferable to clipped controls on high DPI, large-text
        // configurations, RDP sessions, or small displays.
        // MainForm owns a Dock=Fill workspace and its pages provide scrolling.
        // Form-level AutoScroll can freeze a maximized docked root at its
        // original preferred size on high-DPI and remote-desktop desktops.
        AutoScroll = this is not MainForm;
        RefreshResponsiveLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // OnLoad already measured the full control tree. A second full
        // traversal here on every form show used to visibly freeze the UI.
        // Newly added controls are themed through ControlAdded and ordinary
        // WinForms docking handles their first layout.
        if (!_responsiveLayoutInitialized)
            RefreshResponsiveLayout();
    }

    protected override void OnDpiChanged(
        DpiChangedEventArgs e)
    {
        base.OnDpiChanged(
            e);

        QueueResponsiveLayoutRefresh();
    }

    protected override void OnFontChanged(
        EventArgs e)
    {
        base.OnFontChanged(
            e);

        if (IsHandleCreated)
        {
            QueueResponsiveLayoutRefresh();
        }
    }

    internal void PrepareResponsiveLayoutForTesting()
    {
        AutoScroll = this is not MainForm;
        UiModernTheme.Apply(this);
        ApplyResponsiveDefaults(
            this);
        PerformLayout();
        RefreshResponsiveLabelWidths();
        PerformLayout();
        UiModernTheme.Refresh(this);
    }

    private void QueueResponsiveLayoutRefresh()
    {
        if (!IsHandleCreated || IsDisposed || Disposing)
            return;

        _responsiveLayoutTimer.Stop();
        _responsiveLayoutTimer.Start();
    }

    private void RefreshResponsiveLayout()
    {
        if (_responsiveLayoutRunning || IsDisposed || Disposing)
            return;

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var initialRefresh = !_responsiveLayoutInitialized;
        _responsiveLayoutRunning = true;
        _responsiveLabelTimer.Stop();
        try
        {
            // Attribute each expensive initial or subsequent DPI operation
            // separately. The phase counters are allocation-light aggregates;
            // we never record a window name, control text, or recovery data.
            var stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            // Apply() is idempotent and subscribes to new child controls once.
            UiModernTheme.Apply(this);
            UiLayoutTelemetry.RecordDpiPhase(
                stageStarted, UiLayoutTelemetry.DpiPhase.ThemeInstall);

            stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            ApplyResponsiveDefaults(this);
            UiLayoutTelemetry.RecordDpiPhase(
                stageStarted, UiLayoutTelemetry.DpiPhase.ControlDefaults);

            stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            ConstrainToWorkingArea();
            UiLayoutTelemetry.RecordDpiPhase(
                stageStarted, UiLayoutTelemetry.DpiPhase.WorkArea);

            stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            PerformLayout();
            UiLayoutTelemetry.RecordDpiPhase(
                stageStarted, UiLayoutTelemetry.DpiPhase.Layout);

            // Label.MaximumSize setters automatically request WinForms layout
            // only for labels whose available width truly changed.
            // Keep the layout behavior unchanged while measuring its phases.
            stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            RefreshResponsiveLabelWidths();
            UiLayoutTelemetry.RecordDpiPhase(
                stageStarted, UiLayoutTelemetry.DpiPhase.LabelWidths);

            var dpi = DeviceDpi;
            var highContrast = SystemInformation.HighContrast;
            if (_responsiveLayoutInitialized &&
                (dpi != _lastFullLayoutDpi ||
                 highContrast != _lastHighContrast))
            {
                stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                UiModernTheme.Refresh(this);
                UiLayoutTelemetry.RecordDpiPhase(
                    stageStarted, UiLayoutTelemetry.DpiPhase.ThemeRefresh);
            }
            _lastFullLayoutDpi = dpi;
            _lastHighContrast = highContrast;
            _responsiveLayoutInitialized = true;
        }
        finally
        {
            _responsiveLayoutRunning = false;
            UiLayoutTelemetry.RecordResponsiveForm(started);
            UiLayoutTelemetry.RecordResponsivePhase(
                started, initialRefresh, this is MainForm);
        }
    }

    private void ApplyResponsiveDefaults(
        Control root)
    {
        if (root is FlowLayoutPanel flow &&
            flow.FlowDirection is
                FlowDirection.LeftToRight or
                FlowDirection.RightToLeft)
        {
            // Horizontal action/status rows must be allowed to wrap when
            // DPI, text size, RDP width, or localization increases.
            flow.WrapContents =
                true;
        }

        if (root is Button button &&
            button.AutoSize)
        {
            button.MinimumSize =
                new Size(
                    Math.Max(
                        button.MinimumSize.Width,
                        UiStyle.MinimumButtonWidth),
                    Math.Max(
                        button.MinimumSize.Height,
                        UiStyle.MinimumButtonHeight));
        }

        if (root is UiStatusLabel statusLabel)
        {
            UiStyle.RefreshStatusLabel(
                statusLabel);
        }

        if (root is ListView listView)
        {
            UiStyle.RefreshListViewColumns(
                listView);
        }

        if (root is Label label &&
            label.AutoSize &&
            label.MaximumSize.Width > 0)
        {
            if (!_labelMaximumSizes.ContainsKey(
                    label))
            {
                _labelMaximumSizes[label] =
                    label.MaximumSize;
                label.VisibleChanged +=
                    ResponsiveLabelVisibleChanged;
                label.Disposed +=
                    ResponsiveLabelDisposed;
            }

            // Widths are computed in RefreshResponsiveLabelWidths()
            // after the final form layout, once per refresh. Measuring here
            // as well doubled TableLayoutPanel.GetColumnWidths() calls and
            // could use transient hidden-page geometry at startup.
        }

        foreach (Control child in
                 root.Controls)
        {
            ApplyResponsiveDefaults(
                child);
        }
    }

    private void ResponsiveLabelVisibleChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is not Label label || !label.Visible || label.IsDisposed ||
            IsDisposed || Disposing || !IsHandleCreated)
            return;

        _pendingVisibleLabelWidths.Add(label);
        if (_visibleLabelWidthRefreshPosted)
            return;

        _visibleLabelWidthRefreshPosted = true;
        try
        {
            // Exactly one posted callback for a burst of newly visible
            // labels. It runs after WinForms has activated the tab/page.
            BeginInvoke(new MethodInvoker(DrainPendingVisibleLabelWidths));
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ObjectDisposedException)
        {
            _visibleLabelWidthRefreshPosted = false;
            _pendingVisibleLabelWidths.Clear();
            System.Diagnostics.Debug.WriteLine(
                "Visible-label reflow was not scheduled: " + ex.GetType().Name);
        }
    }

    private void DrainPendingVisibleLabelWidths()
    {
        _visibleLabelWidthRefreshPosted = false;
        var pending = _pendingVisibleLabelWidths.ToArray();
        _pendingVisibleLabelWidths.Clear();

        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        // MaximumSize already requests layout on an actual width change.
        // Preserve one explicit parent reflow per parent at most; never
        // force N identical layouts for N captions on one selected tab.
        var parents = new HashSet<Control>();
        var changed = 0;
        foreach (var label in pending)
        {
            if (label.IsDisposed || !label.Visible || label.Parent is null)
                continue;

            if (RefreshResponsiveLabelWidth(label))
            {
                changed++;
                if (!label.Parent.IsDisposed)
                    parents.Add(label.Parent);
            }
        }

        var reflowed = 0;
        foreach (var parent in parents)
        {
            if (parent.IsDisposed || !parent.Visible)
                continue;
            parent.PerformLayout();
            reflowed++;
        }

        _visibleLabelWidthBatchCount++;
        UiLayoutTelemetry.RecordVisibleLabelBatch(changed, reflowed);
    }

    private void ResponsiveLabelDisposed(
        object? sender,
        EventArgs e)
    {
        if (sender is Label label)
        {
            _labelMaximumSizes.Remove(label);
            _pendingVisibleLabelWidths.Remove(label);
        }
    }

    private void RefreshResponsiveLabelWidths()
    {
        foreach (var label in
                 _labelMaximumSizes.Keys
                     .Where(
                         x =>
                             !x.IsDisposed &&
                             x.Visible)
                     .ToArray())
        {
            RefreshResponsiveLabelWidth(
                label);
        }
    }

    private bool RefreshResponsiveLabelWidth(
        Label label)
    {
        if (!_labelMaximumSizes.TryGetValue(
                label,
                out var designMaximum) ||
            designMaximum.Width <= 0 ||
            label.Parent is null)
        {
            return false;
        }

        var availableWidth =
            GetAvailableWidth(
                label);

        if (availableWidth <= 0)
            return false;

        var maximumWidth =
            Math.Max(
                1,
                Math.Min(
                    designMaximum.Width,
                    availableWidth));

        var target =
            new Size(
                maximumWidth,
                designMaximum.Height);

        if (label.MaximumSize ==
            target)
            return false;

        label.MaximumSize =
            target;
        return true;
    }

    private static int GetAvailableWidth(
        Control control)
    {
        var parent =
            control.Parent;

        if (parent is null)
            return 0;

        var available =
            parent.ClientSize.Width -
            control.Margin.Horizontal;

        if (parent is TableLayoutPanel table)
        {
            try
            {
                var position =
                    table.GetPositionFromControl(
                        control);
                var widths =
                    table.GetColumnWidths();

                if (position.Column >= 0 &&
                    position.Column <
                    widths.Length)
                {
                    var span =
                        Math.Max(
                            1,
                            table.GetColumnSpan(
                                control));
                    var end =
                        Math.Min(
                            widths.Length,
                            position.Column +
                            span);

                    available =
                        0;

                    for (var column =
                             position.Column;
                         column <
                         end;
                         column++)
                    {
                        available +=
                            widths[column];
                    }

                    available -=
                        control.Margin.Horizontal;
                }
            }
            catch
            {
                // Fall back to the parent client width.
            }
        }
        else
        {
            available -=
                parent.Padding.Horizontal;
        }

        var parentAvailable =
            Math.Max(
                0,
                parent.ClientSize.Width -
                parent.Padding.Horizontal -
                control.Margin.Horizontal);

        if (parentAvailable > 0)
        {
            available =
                Math.Min(
                    available,
                    parentAvailable);
        }

        return Math.Max(
            0,
            available);
    }

    private int ScaleLogical(
        int value) =>
        Math.Max(
            1,
            (int)Math.Round(
                value *
                DeviceDpi /
                (double)UiStyle.BaselineDpi));

    private void ConstrainToWorkingArea()
    {
        // Never resize/reposition a maximized window. Changing Size/Location
        // here breaks WinForms Dock=Fill after WM_SIZE (especially RDP / 4K).
        if (WindowState != FormWindowState.Normal)
            return;

        var workingArea = Screen.FromControl(this).WorkingArea;
        var margin =
            ScaleLogical(
                UiStyle.WindowMargin);

        var maxWidth =
            Math.Max(
                ScaleLogical(
                    320),
                workingArea.Width -
                margin *
                2);
        var maxHeight =
            Math.Max(
                ScaleLogical(
                    240),
                workingArea.Height -
                margin *
                2);
        var targetWidth = Math.Min(Width, maxWidth);
        var targetHeight = Math.Min(Height, maxHeight);

        if (MinimumSize.Width > targetWidth ||
            MinimumSize.Height > targetHeight)
        {
            MinimumSize = new Size(
                Math.Min(MinimumSize.Width, targetWidth),
                Math.Min(MinimumSize.Height, targetHeight));
        }

        if (targetWidth != Width || targetHeight != Height)
            Size = new Size(targetWidth, targetHeight);

        var maxLeft = Math.Max(
            workingArea.Left + margin,
            workingArea.Right - Width - margin);
        var maxTop = Math.Max(
            workingArea.Top + margin,
            workingArea.Bottom - Height - margin);

        Location = new Point(
            Math.Clamp(Left, workingArea.Left + margin, maxLeft),
            Math.Clamp(Top, workingArea.Top + margin, maxTop));
    }
}
