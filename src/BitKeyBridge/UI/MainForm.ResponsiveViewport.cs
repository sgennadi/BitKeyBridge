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
    private int _startHeightDensity = -1;
    private int _startMinimumCardHeight = UiStyle.HomeThreePaneMinimumHeight;
    private bool _homeConnectionsRecentReflowRunning;
    private int _lastRecentComputerLayoutCount = -1;
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

    internal void PreviewHoldMainViewportReflowForUiSelfTest(bool busy)
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Viewport busy fixture is test-only.");
        _mainViewportReflowRunning = busy;
    }

    // Called after the synchronous captions pass. Measure the user-requested
    // change through the eventual selected-tab reflow, not just ApplyLanguage.
    private void QueueLanguageViewportReflow(long languageStarted)
    {
        if (languageStarted <= 0 || !IsHandleCreated || IsDisposed || Disposing)
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
        // When a routine search result changes into a warning/error, restore
        // its row even if a compact selected-device view had hidden it.
        _homeSearchStatus.TextChanged += (_, _) => QueueMainViewportReflow();
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
                // ReflowMainViewport() may skip an overlapping native layout.
                // Only the successful geometry pass itself may mark language
                // timing ready; a skipped callback leaves it for a later pass.
                ReflowMainViewport();
                _navigationReflowLastDispatchedIndex = selectedIndex;
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
            // The message was never posted: an unrecorded stopwatch must
            // not be attached to a later unrelated tab change.
            _languageViewportStarted = 0;
            _languageViewportQueued = 0;
            // A destroyed HWND cannot process the callback; a later Shown
            // or resize does the ordinary native viewport repair instead.
            System.Diagnostics.Debug.WriteLine(
                "Navigation reflow was not scheduled: " + ex.GetType().Name);
        }
    }

    private double EffectiveHomeDpiScale() =>
        UiDisplayPreferencePolicy.EffectiveScale(
            DeviceDpi, Font.Size, UiStyle.BodyFont.Size, _uiScalePercent);

    // Short RDP sessions and 1366x768/1600x900 laptop screens have much
    // less usable vertical space than an empty 1080p CI screenshot suggests.
    // Compact only decorative/redundant content; never hide connection,
    // search, selected-device, audit, or recovery buttons and never shrink
    // their font. Breakpoints are LOGICAL pixels, not monitor pixels.
    private void ReflowStartVerticalDensity()
    {
        if (_homeWorkspaceTab is null || _homeBrandBanner is null ||
            _homeServiceCards.IsDisposed)
            return;

        var root = _homeWorkspaceTab.Controls
            .OfType<TableLayoutPanel>()
            .FirstOrDefault(x => x.Name == "HomeLayoutRoot");
        if (root is null || root.IsDisposed || root.ClientSize.Height < 150)
            return;

        var scale = EffectiveHomeDpiScale();
        var logicalHeight = root.ClientSize.Height / scale;
        // Typical 1080p windows retain full-size branding. Only the
        // *available Start client height* determines compactness.
        var tier = UiDisplayPreferencePolicy.ResolveStartTier(
            _uiDensity, logicalHeight);

        // The selected-device summary already confirms a successful search
        // and the card header identifies the machine. On a short display,
        // don't spend a full 35-50px row repeating routine instructions or
        // a successful-search notification. Never suppress active progress,
        // partial-result warnings or failures. Status changes request reflow.
        var hideRoutineStatus = tier != 0 &&
            GetSelectedHomeSearchResult() is not null &&
            _homeSearchStatus.StatusKind is
                UiStatusKind.Neutral or UiStatusKind.Success;
        if (_homeSearchStatus.Visible == hideRoutineStatus)
            _homeSearchStatus.Visible = !hideRoutineStatus;

        if (tier == _startHeightDensity)
            return;

        _startHeightDensity = tier;
        var compact = tier != 0;
        var tight = tier == 2;
        _startMinimumCardHeight = tier switch
        {
            2 => 210,
            1 => 250,
            _ => UiStyle.HomeThreePaneMinimumHeight
        };
        root.SuspendLayout();
        try
        {
            var pagePadding = tight ? 8 : compact ? 11 : UiStyle.PagePadding;
            var padding = new Padding(pagePadding);
            if (root.Padding != padding)
                root.Padding = padding;
            _homeBrandBanner.SetCompactHeight(compact);
            _homeBrandBanner.Margin = new Padding(0, 0, 0,
                tight ? 0 : compact ? UiStyle.ControlGap / 2 : UiStyle.SectionGap);
            _appDiagnostics.SetCompactHeight(compact);

            var searchRow = root.Controls.OfType<TableLayoutPanel>()
                .FirstOrDefault(x => x.Name == "HomeSearchControls");
            if (searchRow is not null)
                searchRow.Margin = new Padding(
                    0, 0, 0, tight ? 0 : UiStyle.ControlGap);

            var group = root.Controls.OfType<GroupBox>()
                .FirstOrDefault(x => x.Name == "HomeConnectionsGroup");
            if (group is not null)
            {
                group.Padding = new Padding(tight ? 7 : compact ? 9 : 14);
                group.Margin = new Padding(0, 0, 0,
                    tight ? 0 : compact ? UiStyle.ControlGap / 2 :
                    UiStyle.SectionGap);
            }
            _homeSelectionPanel.Margin = new Padding(
                0, tight ? 0 : compact ? 2 : UiStyle.ControlGap, 0, 0);
            var recentHint = _homeRecentQuickPanel?.Controls
                .OfType<Label>()
                .FirstOrDefault(x => x.Name == "HomeRecentExplanation");
            if (recentHint is not null)
                recentHint.Visible = !compact;
            // The short-height recent list remains readable and scrollable.
            // It must never consume the height needed for recovery actions.
            _lastRecentComputerLayoutCount = -1;
        }
        finally
        {
            root.ResumeLayout(true);
        }

        // The density change must also invalidate the AutoSize rows that
        // hold duplicate AD status labels. Simply setting Visible=false
        // leaves a retained preferred height in the connection GroupBox.
        SyncStartOptionalConnectionRows(
            StartConnectionPolicy.NeedsActiveDirectory(SelectedStartSource));
    }

    // Recent metadata is optional. With no entries it must not reserve the
    // right 38% of Connections or a permanent 154px-high empty grid. A short
    // list gets its intrinsic rows; a longer one scrolls inside the 154px cap.
    // This is UI-only: no AD/Graph lookup or recovery secret is requested.
    private void MeasureHomeRecentQuickPanel()
    {
        var recent = _homeRecentQuickPanel;
        if (recent is null || recent.IsDisposed ||
            recent.RowStyles.Count < 2 || _homeRecentResults.IsDisposed)
            return;

        var count = _homeRecentResults.Rows.Count;
        if (_lastRecentComputerLayoutCount != count)
        {
            recent.Visible = count > 0;
            _lastRecentComputerLayoutCount = count;
        }

        var scale = (float)EffectiveHomeDpiScale();
        var logicalMaximum = _startHeightDensity switch
        {
            2 => 72,
            1 => 94,
            _ => UiStyle.HomeRecentQuickListHeight
        };
        var maximum = (int)Math.Ceiling(logicalMaximum * scale);
        var minimum = (int)Math.Ceiling(
            (_startHeightDensity == 0 ? 64F : 52F) * scale);
        var preferred = count == 0 ? 0 :
            (_homeRecentResults.ColumnHeadersVisible
                ? _homeRecentResults.ColumnHeadersHeight : 0) +
            _homeRecentResults.Rows.Cast<DataGridViewRow>()
                .Take(15).Sum(row => row.Height) +
            (int)Math.Ceiling(6F * scale);
        var gridHeight = count == 0 ? 0 :
            Math.Min(maximum, Math.Max(minimum, preferred));

        var listRow = recent.RowStyles[1];
        if (listRow.SizeType != SizeType.Absolute)
            listRow.SizeType = SizeType.Absolute;
        if (Math.Abs(listRow.Height - gridHeight) > 0.5F)
            listRow.Height = gridHeight;
    }

    private void ReflowConnectionsRecent()
    {
        var layout = _homeConnectionsRecentLayout;
        var connection = _homeAdConnectionLayout;
        var recent = _homeRecentQuickPanel;
        if (_homeConnectionsRecentReflowRunning || layout is null ||
            connection is null || recent is null || layout.IsDisposed ||
            layout.Parent is null)
            return;

        MeasureHomeRecentQuickPanel();
        var hasRecent = _homeRecentResults.Rows.Count > 0;
        var width = Math.Max(0,
            layout.Parent.ClientSize.Width - layout.Parent.Padding.Horizontal);
        var horizontal = hasRecent &&
            width >= (int)Math.Ceiling(
                UiStyle.HomeConnectionRecentPairMinimumWidth * EffectiveHomeDpiScale());
        var key = !hasRecent ? "ConnectionsOnly" :
            horizontal ? "ConnectionsRecentWide" : "ConnectionsRecentStacked";
        if (_homeConnectionsRecentLayoutKey == key)
            return;

        _homeConnectionsRecentReflowRunning = true;
        layout.SuspendLayout();
        try
        {
            layout.Controls.Clear();
            layout.ColumnStyles.Clear();
            layout.RowStyles.Clear();
            connection.Margin = new Padding(
                0, 0, horizontal ? UiStyle.SectionGap : 0, 0);
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
                // Keep the hidden Recent panel attached for restoration and
                // localization, but never allocate a phantom AutoSize row.
                layout.RowStyles.Add(new RowStyle(
                    hasRecent ? SizeType.AutoSize : SizeType.Absolute, 0F));
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
    /// Three draggable panes, adapting from three columns to two columns
    /// (with the last pane spanning the bottom row) and finally one column.
    /// This avoids the previous jump straight to a three-row stack on RDP
    /// and HiDPI viewports, without shrinking readable fonts or hiding actions.
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
            _startMinimumCardHeight * effectiveScale);
        var threeColumns = width >= scaledWidth;
        var twoColumns = !threeColumns && width >= (int)Math.Ceiling(
            UiStyle.HomeRecoveryCardsPairMinimumWidth * effectiveScale);
        UpdateHomePaneDragUi(threeColumns);

        var desiredKey = (threeColumns ? "3cols:" :
            twoColumns ? "2cols:" : "3rows:") +
            string.Join(",", _homePaneOrder) + ":" + DeviceDpi +
            ":min" + scaledHeight;
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
                // A previous two-column layout may have a two-cell span.
                // Clear that span before reattaching the preserved pane hosts.
                foreach (Control pane in _homeServiceCards.Controls)
                    _homeServiceCards.SetColumnSpan(pane, 1);
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
                else if (twoColumns)
                {
                    // Keep two recovery panes side by side at intermediate
                    // widths; let the third pane span the next row rather
                    // than spending three full screen-heights on the stack.
                    _homeServiceCards.ColumnCount = 2;
                    _homeServiceCards.RowCount = 2;
                    for (var i = 0; i < 2; i++)
                        _homeServiceCards.ColumnStyles.Add(
                            new ColumnStyle(SizeType.Percent, 50F));
                    for (var i = 0; i < 2; i++)
                        _homeServiceCards.RowStyles.Add(
                            new RowStyle(SizeType.Absolute, scaledHeight));
                    _homeServiceCards.MinimumSize =
                        new Size(0, scaledHeight * 2);
                    for (var i = 0; i < 3; i++)
                        _homeServiceCards.Controls.Add(
                            HomePaneHost(_homePaneOrder[i]),
                            i == 2 ? 0 : i, i == 2 ? 1 : 0);
                    _homeServiceCards.SetColumnSpan(
                        HomePaneHost(_homePaneOrder[2]), 2);
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
        if (_mainViewportReflowRunning)
        {
            // A language switch needs a completed geometry pass. If an
            // overlapping native layout skipped this attempt, retry once
            // the 55-ms idle debounce is available rather than silently
            // leaving its measurement pending forever.
            if (_languageViewportStarted > 0 && IsHandleCreated &&
                !IsDisposed && !Disposing)
                QueueMainViewportReflow();
            return;
        }
        if (IsDisposed || Disposing || ClientSize.Width <= 0)
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
                ReflowStartVerticalDensity();
                ReflowConnectionsRecent();
                ReflowHomeResultsArea();
                // If all rows cannot fit (e.g. 200% scaling on a 768px RDP
                // desktop), expose actual vertical scrolling rather than
                // cutting off the lower panes with a clipped percent row.
                var startRoot = _homeWorkspaceTab?.Controls
                    .OfType<TableLayoutPanel>()
                    .FirstOrDefault(x => x.Name == "HomeLayoutRoot");
                if (startRoot is not null && startRoot.RowStyles.Count >= 6)
                {
                    var heights = startRoot.GetRowHeights();
                    var topHeight = heights.Take(5).Sum();
                    var minimumCardHeight = (int)Math.Ceiling(
                        _startMinimumCardHeight * EffectiveHomeDpiScale());
                    var stackedRows = Math.Clamp(
                        _homeServiceCards.RowCount, 1, 3);
                    var required = topHeight +
                        minimumCardHeight * stackedRows +
                        startRoot.Padding.Vertical;
                    var minScroll = required > startRoot.ClientSize.Height
                        ? new Size(0, required + UiStyle.ControlGap)
                        : Size.Empty;
                    if (startRoot.AutoScrollMinSize != minScroll)
                        startRoot.AutoScrollMinSize = minScroll;
                }
                AlignHomeStatusGlyphs();
            }

            // Theme controls subscribe to SizeChanged and DpiChanged already.
            // UiModernTheme.Refresh(shell) rebuilt every rounded HWND region and
            // invalidated all tab controls on each window resize. It belongs
            // only in the shared theme/DPI refresh path, NOT in WM_SIZE.

            // A callback can be dispatched while native WinForms layout is
            // busy; it must not claim a completed repaint if the method above
            // returned early. Consuming the stopwatch only here also covers
            // a real WM_SIZE/timer pass that beats the queued callback.
            if (_languageViewportStarted > 0 && _languageViewportQueued > 0)
            {
                var languageStarted = _languageViewportStarted;
                UiLayoutTelemetry.RecordLanguageViewportReflow(viewportStarted);
                UiLayoutTelemetry.RecordLanguageViewportReady(languageStarted);
                _languageViewportStarted = 0;
                _languageViewportQueued = 0;
            }
        }
        finally
        {
            _mainViewportReflowRunning = false;
            UiLayoutTelemetry.RecordViewport(viewportStarted);
        }
    }
}
