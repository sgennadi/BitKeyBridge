namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Frequent WM_SIZE events during RDP/4K window drags must not trigger
    // recursive full-tree layout and GDI region regeneration on every frame.
    private readonly System.Windows.Forms.Timer _mainViewportIdleTimer = new()
    {
        Interval = 55
    };
    private bool _mainViewportReflowRunning;
    private bool _homeResultsReflowRunning;
    private string _homeResultsLayoutKey = string.Empty;
    private string _homeConnectionsRecentLayoutKey = string.Empty;
    private bool _homeConnectionsRecentReflowRunning;
    private string _lastRepairedTabPath = string.Empty;
    // Multiple tab and nested-tab events in one UI-message burst share one
    // BeginInvoke. Keep only the most recently requested page and start time.
    private bool _navigationReflowPosted;
    private int _navigationReflowHandleGeneration;
    private bool _mainViewportHandleRecreated;
    private int _navigationReflowLatestIndex;
    private long _navigationReflowLatestStarted;
    // Latest user-requested language change (timings only, no language or
    // identity strings). Superseded changes are not mistaken for completed.
    private long _languageViewportStarted;
    private long _languageViewportQueued;
    private int _navigationReflowPostCount;
    private int _navigationReflowDispatchCount;
    private int _navigationReflowCoalescedCount;
    private int _navigationReflowLastDispatchedIndex = -1;

    internal (int Posted, int Dispatched, int Coalesced, bool Pending,
              int RequestedIndex, int LastDispatchedIndex, bool HasPendingTiming)
        PreviewNavigationReflowStateForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Navigation counters are test-only.");
        return (_navigationReflowPostCount, _navigationReflowDispatchCount,
            _navigationReflowCoalescedCount, _navigationReflowPosted,
            _navigationReflowLatestIndex, _navigationReflowLastDispatchedIndex,
            _navigationReflowLatestStarted > 0);
    }

    internal void PreviewQueueNavigationReflowForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Navigation burst is test-only.");
        QueueNavigationViewportReflow();
    }

    internal void PreviewRecreateNativeHandleForUiSelfTest()
    {
        if (!_layoutSelfTest || !IsHandleCreated || IsDisposed || Disposing)
            throw new InvalidOperationException("HWND recreation is test-only.");
        RecreateHandle();
    }

    internal bool PreviewLanguageViewportPendingForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Language timing state is test-only.");
        return _languageViewportStarted > 0;
    }

    // Called after the synchronous captions pass. Measure the user-requested
    // change through the eventual selected-tab reflow, not just ApplyLanguage.
    private void QueueLanguageViewportReflow(long languageStarted)
    {
        if (languageStarted <= 0 || IsDisposed || Disposing)
            return;

        if (_languageViewportStarted > 0)
            UiLayoutTelemetry.RecordLanguageViewportSuperseded();
        _languageViewportStarted = languageStarted;
        _languageViewportQueued = System.Diagnostics.Stopwatch.GetTimestamp();
        QueueNavigationViewportReflow();
    }

    private void InitializeMainViewport()
    {
        AutoScroll = false;
        InitializeHomeStatusGlyphAlignment();
        _mainViewportIdleTimer.Tick += (_, _) =>
        {
            _mainViewportIdleTimer.Stop();
            if (!IsDisposed && IsHandleCreated)
                ReflowMainViewport();
        };
        ClientSizeChanged += (_, _) => QueueMainViewportReflow();
        DpiChanged += (_, _) => QueueMainViewportReflow();
        FontChanged += (_, _) => QueueMainViewportReflow();
        Shown += (_, _) => QueueMainViewportReflow();
        FormClosed += (_, _) => _mainViewportIdleTimer.Dispose();
        HandleDestroyed += (_, _) =>
        {
            // A queued callback belongs to the old native HWND, and may
            // never execute after RTL/DPI/RDP handle recreation. Its late
            // arrival must not clear or reflow a newly queued selected page.
            unchecked { _navigationReflowHandleGeneration++; }
            _navigationReflowPosted = false;
            _navigationReflowLatestStarted = 0;
            // Retain the last language stopwatch across RecreateHandle; the
            // replacement HWND will get its own callback below. A stale
            // callback's generation check must never record it as complete.
            _mainViewportHandleRecreated = !IsDisposed && !Disposing;
            if (!_mainViewportHandleRecreated)
            {
                _languageViewportStarted = 0;
                _languageViewportQueued = 0;
            }
            if (_mainViewportHandleRecreated)
                _mainViewportIdleTimer.Stop();
        };
        HandleCreated += (_, _) =>
        {
            if (!_mainViewportHandleRecreated || IsDisposed || Disposing)
                return;
            _mainViewportHandleRecreated = false;
            // The new native window must repair its current selected page,
            // even if no fresh SelectedIndexChanged event arrives.
            QueueNavigationViewportReflow();
        };
    }

    private void QueueMainViewportReflow()
    {
        if (!IsHandleCreated || IsDisposed || Disposing)
            return;

        // A 55-ms quiet-period batch is imperceptible when dragging, but
        // eliminates dozens of layout passes for one native WM_SIZE burst.
        // Dock=Fill still updates immediately through normal WinForms layout.
        _mainViewportIdleTimer.Stop();
        _mainViewportIdleTimer.Start();
    }


    // Tab navigation is discrete, unlike high-frequency WM_SIZE bursts.
    // A rapid burst previously posted N BeginInvoke callbacks, N-1 of which
    // checked their obsolete sequence and exited. Retain only one actual UI
    // callback; it always uses the MOST RECENT selected tab and start time.
    // No window, LDAP/Graph request or device state is reconstructed.
    private void QueueNavigationViewportReflow(long navigationStarted = 0)
    {
        if (!IsHandleCreated || IsDisposed || Disposing)
            return;

        _mainViewportIdleTimer.Stop();
        var selectedIndex = _mainTabs.SelectedIndex;
        // A nested-tab callback has no top-level navigation stopwatch.
        // Preserve the latest top-level switch timing when nested events
        // belong to that same still-pending selected main page. Do NOT
        // preserve it across separate queued operations or main-page changes.
        if (!_navigationReflowPosted ||
            _navigationReflowLatestIndex != selectedIndex ||
            navigationStarted > 0)
            _navigationReflowLatestStarted = navigationStarted;
        _navigationReflowLatestIndex = selectedIndex;
        if (_navigationReflowPosted)
        {
            _navigationReflowCoalescedCount++;
            UiTabNavigationTelemetry.RecordCoalescedReflow();
            return;
        }

        _navigationReflowPosted = true;
        try
        {
            var generation = _navigationReflowHandleGeneration;
            BeginInvoke((Action)(() =>
            {
                if (generation != _navigationReflowHandleGeneration)
                    return;
                _navigationReflowPosted = false;
                if (IsDisposed || Disposing || !_mainTabs.IsHandleCreated ||
                    _mainTabs.SelectedIndex != _navigationReflowLatestIndex)
                    return;

                _navigationReflowDispatchCount++;
                var selectedIndex = _navigationReflowLatestIndex;
                var started = _navigationReflowLatestStarted;
                var languageStarted = _languageViewportStarted;
                var languageQueued = _languageViewportQueued;
                if (started > 0)
                    UiTabNavigationTelemetry.RecordDispatch(
                        selectedIndex, started);
                if (languageStarted > 0 && languageQueued > 0)
                    UiLayoutTelemetry.RecordLanguageViewportDispatch(languageQueued);
                var viewportStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                ReflowMainViewport();
                _navigationReflowLastDispatchedIndex = selectedIndex;
                if (languageStarted > 0 &&
                    languageStarted == _languageViewportStarted)
                {
                    UiLayoutTelemetry.RecordLanguageViewportReflow(viewportStarted);
                    UiLayoutTelemetry.RecordLanguageViewportReady(languageStarted);
                    _languageViewportStarted = 0;
                    _languageViewportQueued = 0;
                }
                if (started > 0)
                {
                    UiTabNavigationTelemetry.RecordLayout(
                        selectedIndex, viewportStarted);
                    UiTabNavigationTelemetry.Record(selectedIndex, started);
                }
            }));
            _navigationReflowPostCount++;
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ObjectDisposedException)
        {
            _navigationReflowPosted = false;
            // A destroyed HWND cannot process the callback; a later Shown
            // or resize does the ordinary native viewport repair instead.
            System.Diagnostics.Debug.WriteLine(
                "Navigation reflow was not scheduled: " + ex.GetType().Name);
        }
    }

    private double EffectiveHomeDpiScale() =>
        Math.Max(DeviceDpi / (double)UiStyle.BaselineDpi,
            Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));

    private void ReflowConnectionsRecent()
    {
        var layout = _homeConnectionsRecentLayout;
        var connection = _homeAdConnectionLayout;
        var recent = _homeRecentQuickPanel;
        if (_homeConnectionsRecentReflowRunning || layout is null ||
            connection is null || recent is null || layout.IsDisposed ||
            layout.Parent is null)
            return;

        var width = Math.Max(0,
            layout.Parent.ClientSize.Width - layout.Parent.Padding.Horizontal);
        bool horizontal = width >= (int)Math.Ceiling(
            UiStyle.HomeConnectionRecentPairMinimumWidth * EffectiveHomeDpiScale());
        var key = horizontal ? "ConnectionsRecentWide" : "ConnectionsRecentStacked";
        if (_homeConnectionsRecentLayoutKey == key)
            return;

        _homeConnectionsRecentReflowRunning = true;
        layout.SuspendLayout();
        try
        {
            layout.Controls.Clear();
            layout.ColumnStyles.Clear();
            layout.RowStyles.Clear();
            if (horizontal)
            {
                layout.ColumnCount = 2;
                layout.RowCount = 1;
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(connection, 0, 0);
                layout.Controls.Add(recent, 1, 0);
            }
            else
            {
                layout.ColumnCount = 1;
                layout.RowCount = 2;
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(connection, 0, 0);
                layout.Controls.Add(recent, 0, 1);
            }
            _homeConnectionsRecentLayoutKey = key;
        }
        finally
        {
            layout.ResumeLayout(true);
            _homeConnectionsRecentReflowRunning = false;
        }
    }

    /// <summary>
    /// Three equal-size draggable panes. The main form owns the height,
    /// not a data grid with a percentage row that hides recovery actions.
    /// At high DPI or small RDP widths the panes stack at equal heights.
    /// </summary>
    private void ReflowHomeResultsArea()
    {
        if (_homeResultsReflowRunning || _homeServiceCards.IsDisposed ||
            _homeServiceCards.Parent is null)
            return;

        var width = _homeResultsArea.Parent?.ClientSize.Width ?? 0;
        if (width <= 0)
            width = _homeResultsArea.ClientSize.Width;
        width -= _homeResultsArea.Parent?.Padding.Horizontal ?? 0;
        var effectiveScale = EffectiveHomeDpiScale();
        var scaledWidth = (int)Math.Ceiling(
            UiStyle.HomeThreePaneMinimumWidth * effectiveScale);
        var scaledHeight = (int)Math.Ceiling(
            UiStyle.HomeThreePaneMinimumHeight * effectiveScale);
        bool threeColumns = width >= scaledWidth;
        UpdateHomePaneDragUi(threeColumns);

        var desiredKey = (threeColumns ? "3cols:" : "3rows:") +
            string.Join(",", _homePaneOrder) + ":" + DeviceDpi;
        if (_homeResultsLayoutKey == desiredKey)
            return;

        var cardsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        _homeResultsReflowRunning = true;
        _homeServiceCards.SuspendLayout();
        try
        {
            // On a simple drag/reorder, move only cell positions. Existing
            // card and DataGridView handles (including secret/history state)
            // stay intact. No AD or Graph call occurs.
            if (threeColumns && _homeServiceCards.ColumnCount == 3 &&
                _homeServiceCards.RowCount == 1 &&
                _homePaneOrder.All(pane =>
                    _homeServiceCards.Controls.Contains(HomePaneHost(pane))))
            {
                for (var i = 0; i < _homePaneOrder.Length; i++)
                    _homeServiceCards.SetCellPosition(
                        HomePaneHost(_homePaneOrder[i]),
                        new TableLayoutPanelCellPosition(i, 0));
                _homeServiceCards.MinimumSize =
                    new Size(0, scaledHeight);
            }
            else
            {
                var selectedComputer = GetSelectedHomeSearchResult();
                _homeServiceCards.Controls.Clear();
                _homeServiceCards.ColumnStyles.Clear();
                _homeServiceCards.RowStyles.Clear();

                if (threeColumns)
                {
                    _homeServiceCards.ColumnCount = 3;
                    _homeServiceCards.RowCount = 1;
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.ColumnStyles.Add(
                            new ColumnStyle(SizeType.Percent, 100F / 3F));
                    _homeServiceCards.RowStyles.Add(
                        new RowStyle(SizeType.Percent, 100F));
                    _homeServiceCards.MinimumSize =
                        new Size(0, scaledHeight);
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.Controls.Add(
                            HomePaneHost(_homePaneOrder[i]), i, 0);
                }
                else
                {
                    _homeServiceCards.ColumnCount = 1;
                    _homeServiceCards.RowCount = 3;
                    _homeServiceCards.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100F));
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.RowStyles.Add(
                            new RowStyle(SizeType.Absolute, scaledHeight));
                    _homeServiceCards.MinimumSize =
                        new Size(0, scaledHeight * 3);
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.Controls.Add(
                            HomePaneHost(_homePaneOrder[i]), 0, i);
                }

                if (selectedComputer is not null &&
                    GetSelectedHomeSearchResult() is null)
                {
                    var row = _homeSearchResults.Rows
                        .Cast<DataGridViewRow>()
                        .FirstOrDefault(item =>
                            ReferenceEquals(item.Tag, selectedComputer));
                    if (row is not null)
                    {
                        _homeSearchResults.ClearSelection();
                        row.Selected = true;
                        if (row.Cells.Count > 0)
                            _homeSearchResults.CurrentCell = row.Cells[0];
                    }
                }
            }
            _homeResultsLayoutKey = desiredKey;
        }
        finally
        {
            // ResumeLayout(true) already lays out the modified card grid once.
            // A second forced PerformLayout repaints all recovery children.
            _homeServiceCards.ResumeLayout(true);
            _homeResultsReflowRunning = false;
            UiLayoutTelemetry.RecordHomeCards(cardsStarted);
        }
    }

    // Selection (including a nested tab) may expose panels initially built
    // at WinForms' provisional 200x100 size. Only a new visible tab path,
    // DPI change or genuinely repaired geometry needs an explicit reflow.
    // A routine WM_SIZE with correctly docked controls does not.
    private static string SelectedTabPath(TabControl root)
    {
        var path = new System.Text.StringBuilder();
        var tabs = root;
        while (tabs.SelectedTab is TabPage page)
        {
            path.Append('/').Append(page.Name);
            var next = page.Controls.OfType<TabControl>()
                .FirstOrDefault(control => control.Dock == DockStyle.Fill);
            if (next is null)
                break;
            tabs = next;
        }
        return path.ToString();
    }

    private static void RepairSelectedTabHierarchy(
        TabControl tabs, bool forceFlowLayout)
    {
        if (tabs.IsDisposed || tabs.Disposing ||
            tabs.SelectedTab is not TabPage page)
            return;

        var display = tabs.DisplayRectangle;
        if (display.Width <= 0 || display.Height <= 0)
            return;

        // Do not continuously fight native tab borders (a few pixels).
        // Only correct an obviously stale page after a hidden-tab resize.
        var widthTolerance = Math.Max(16, display.Width / 8);
        var heightTolerance = Math.Max(16, display.Height / 8);
        var pageRepaired =
            Math.Abs(page.Width - display.Width) > widthTolerance ||
            Math.Abs(page.Height - display.Height) > heightTolerance;
        if (pageRepaired)
            page.Bounds = display;

        foreach (Control content in page.Controls)
        {
            if (content.IsDisposed || content.Dock != DockStyle.Fill)
                continue;

            var viewport = page.DisplayRectangle;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                continue;

            var boundsRepaired = content.Bounds != viewport;
            if (boundsRepaired)
                content.Bounds = viewport;

            if (content is TabControl nested)
                RepairSelectedTabHierarchy(
                    nested, forceFlowLayout || pageRepaired || boundsRepaired);
            else if (content is FlowLayoutPanel
                     { FlowDirection: FlowDirection.TopDown } flow &&
                     (forceFlowLayout || pageRepaired || boundsRepaired))
            {
                // Inactive pages still receive an initial explicit layout
                // on selection. Stable pages are laid out by native docking
                // and must not be forcibly measured on every WM_SIZE event.
                flow.PerformLayout();
            }
        }
    }

    internal void ReflowMainViewport()
    {
        if (_mainViewportReflowRunning || IsDisposed || ClientSize.Width <= 0)
            return;

        var viewportStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        _mainViewportReflowRunning = true;
        try
        {
            if (AutoScroll)
                AutoScroll = false;

            var shell = Controls.OfType<TableLayoutPanel>()
                .FirstOrDefault(x => x.Name == "MainLayoutRoot");
            if (shell is null)
                return;

            // Docking is automatic during WM_SIZE. Only repair actual geometry
            // mismatches, instead of forcing 6+ nested PerformLayout passes.
            var geometryChanged = false;
            if (shell.Dock != DockStyle.Fill)
            {
                shell.Dock = DockStyle.Fill;
                geometryChanged = true;
            }
            if (shell.Bounds != ClientRectangle)
            {
                shell.Bounds = ClientRectangle;
                geometryChanged = true;
            }
            if (_mainTabs.Dock != DockStyle.Fill)
            {
                _mainTabs.Dock = DockStyle.Fill;
                geometryChanged = true;
            }

            if (geometryChanged)
                shell.PerformLayout();

            // TabPage sizes are lazily calculated by WinForms. After switching
            // Diagnostics > Tools, Settings > Enterprise, or reconnecting RDP,
            // nested Dock=Fill content can retain a 200x100 provisional size
            // even though the outer workspace fills the window. Repair only
            // the visible tab chain; never rebuild its controls or re-theme.
            var selectedPath = SelectedTabPath(_mainTabs) + ":" + DeviceDpi;
            var changedPath = !string.Equals(
                selectedPath, _lastRepairedTabPath, StringComparison.Ordinal);
            RepairSelectedTabHierarchy(_mainTabs, changedPath);
            _lastRepairedTabPath = selectedPath;

            // Home cards and connection panes are not part of any other
            // workspace. Do not update their geometry or glyph regions while
            // a different page is selected. Returning to Start schedules an
            // immediate reflow using the current RDP/window dimensions.
            if (ReferenceEquals(_mainTabs.SelectedTab, _homeWorkspaceTab))
            {
                ReflowConnectionsRecent();
                ReflowHomeResultsArea();
                AlignHomeStatusGlyphs();
            }

            // Theme controls subscribe to SizeChanged and DpiChanged already.
            // UiModernTheme.Refresh(shell) rebuilt every rounded HWND region and
            // invalidated all tab controls on each window resize. It belongs
            // only in the shared theme/DPI refresh path, NOT in WM_SIZE.
        }
        finally
        {
            _mainViewportReflowRunning = false;
            UiLayoutTelemetry.RecordViewport(viewportStarted);
        }
    }
}
