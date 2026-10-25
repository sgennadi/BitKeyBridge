using System.Runtime.CompilerServices;

namespace BitKeyBridge;

/// <summary>
/// Localize fixed, non-secret captions without recreating the MainForm tree.
/// Runtime status messages and secret/identity fields are deliberately excluded.
/// </summary>
public sealed partial class MainForm
{
    private static readonly IReadOnlyDictionary<string, string> LocalizedCaptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HomeUseWindowsIdentity"] = "MainWindowsAccount",
            ["HomeUseOtherAccount"] = "MainOtherAdAccount",
            ["HomeConnectAdButton"] = "MainConnectAd",
            ["HomeDisconnectAdButton"] = "MainDisconnectAd",
            ["HomeConnectionToolsButton"] = "MainConnectionTools",
            ["HomeRecoverySteps"] = "MainRecoverySteps",
            ["HomeBitLockerDiagnosticLink"] = "MainViewCommands",
            ["HomeLoadLapsHistoryButton"] = "MainLoadHistory",
            ["RecoveryBackToStart"] = "MainBackToStart",
            ["LapsBackToStart"] = "MainBackToStart",
            ["BitLockerDiagnosticCommandsButton"] = "MainBitLockerCommands",
            ["LapsDiagnosticCommandsButton"] = "MainLapsCommands",
            ["HelpdeskOpenDeviceWorkspaceButton"] = "MainOpenDeviceSearch",
            ["HelpdeskRecoveryStepsButton"] = "MainRecoverySteps",
            ["DiagnosticToolsTab"] = "Tools",
            ["EnterpriseSettingsTab"] = "MainEnterprise",
            ["RunEnvironmentCheckButton"] = "MainEnvironmentCheck",
            ["RunAdvancedDeviceDiagnosticsButton"] = "MainDeviceDiagnostics",
            ["RunBitLockerCommandsButton"] = "MainBitLockerCommands",
            ["RunLapsCommandsButton"] = "MainLapsCommands",
            ["RunProtectedAuditCheckButton"] = "MainCheckProtectedAudit",
            ["RunUiPerformanceDiagnosticsButton"] = "MainUiPerformance",
            ["ResetVisualLayoutButton"] = "MainResetLayout",
            ["CloudEnableRotationPermission"] = "MainCloudRotation",
            ["DevicesOpenCloudSettingsButton"] = "DevicesConfigureCloud"
        };

    // Strong references are kept only to controls with fixed captions.
    // Removed/disposed controls are deregistered; the observer uses weak keys.
    // Multiple controls with the same Name must all receive translations.
    private const string OriginalCaptionPrefix = "\u0001";
    private readonly Dictionary<Control, string> _localizedCaptionControls =
        new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<Control, object> _localizedCaptionObserved = new();
    // List-view header source literals are captured only once, before
    // translation, and are not mistaken for live AD/Graph result values.
    private readonly Dictionary<ListView, (string English, int InitialWidth)[]> _localizedListViewHeaders =
        new(ReferenceEqualityComparer.Instance);
    // WinForms DataGridViewColumn is not a Control. Cache source headers by
    // object identity so language switching never translates a previously
    // translated caption again or changes any cell values.
    private readonly Dictionary<DataGridView,
        Dictionary<DataGridViewColumn, (string English, int MinimumWidth)>>
        _localizedGridHeaders = new(ReferenceEqualityComparer.Instance);
    private static readonly object LocalizedCaptionObserverMarker = new();
    // ToolStripItem is not a Control. Preserve caption source separately from
    // Tag, which is reserved for application command data.
    private readonly Dictionary<ToolStripItem, string> _localizedMenuSources =
        new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<ToolStripDropDownItem, object>
        _observedLocalizedMenus = new();
    private bool _localizedCaptionCacheBuilt;

    private void ObserveLocalizedCaptionTree(
        Control root, bool applyImmediately = true)
    {
        var pending = new Stack<Control>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var control = pending.Pop();
            if (control.IsDisposed || control.Disposing ||
                _localizedCaptionObserved.TryGetValue(control, out _))
                continue;

            _localizedCaptionObserved.Add(control, LocalizedCaptionObserverMarker);
            // A late-created tab, wizard section, or diagnostics panel should
            // be translated without scanning the entire WinForms tree again.
            control.ControlAdded += OnLocalizedCaptionControlAdded;
            control.ControlRemoved += OnLocalizedCaptionControlRemoved;

            string? key = null;
            if (!string.IsNullOrEmpty(control.Name) &&
                LocalizedCaptions.TryGetValue(control.Name, out var namedKey))
                key = namedKey;
            else if (control is Button or CheckBox or RadioButton or
                         LinkLabel or GroupBox or TabPage ||
                     control is Label and not UiStatusLabel &&
                     !ReferenceEquals(control, _exportSummary) &&
                     !ReferenceEquals(control, _lastSuccess) &&
                     !ReferenceEquals(control, _coverageSummary))
            {
                // Only exact known static captions. Never discover translations
                // by scanning arbitrary TextBoxes, credentials or status data.
                if (UiStaticCaptionCatalog.Contains(control.Text))
                    key = OriginalCaptionPrefix + control.Text;
            }

            if (key is not null)
            {
                _localizedCaptionControls[control] = key;
                control.Disposed += OnLocalizedCaptionControlDisposed;
                control.TextChanged += OnLocalizedCaptionTextChanged;
                if (UsesLocalizedCaptionDirection(control))
                    control.VisibleChanged += OnLocalizedCaptionVisibleChanged;
                // Cold startup: discover and cache captions first; the
                // subsequent batched language pass suspends their parents.
                // Dynamic controls still localize immediately.
                if (applyImmediately)
                    ApplyLocalizedCaption(control, key,
                        UiLanguageService.IsRightToLeft(_uiLanguageCode)
                            ? RightToLeft.Yes : RightToLeft.No);
            }

            if (control is ListView list && list.Columns.Count > 0 &&
                list.Columns.Cast<ColumnHeader>().Any(x =>
                    UiStaticCaptionCatalog.Contains(x.Text)))
            {
                _localizedListViewHeaders[list] = list.Columns.Cast<ColumnHeader>()
                    .Select(x => (x.Text, x.Width)).ToArray();
                list.Disposed += OnLocalizedListViewDisposed;
                if (applyImmediately)
                    ApplyLocalizedListViewHeaders(list);
            }

            if (control is DataGridView grid)
            {
                _localizedGridHeaders[grid] = new Dictionary<DataGridViewColumn,
                    (string English, int MinimumWidth)>(ReferenceEqualityComparer.Instance);
                grid.ColumnAdded += OnLocalizedGridColumnAdded;
                grid.ColumnRemoved += OnLocalizedGridColumnRemoved;
                grid.Disposed += OnLocalizedGridDisposed;
                CaptureLocalizedGridHeaders(grid);
                if (applyImmediately)
                    ApplyLocalizedGridHeaders(grid);
            }

            foreach (Control child in control.Controls)
                pending.Push(child);
        }
    }

    private void OnLocalizedCaptionControlAdded(object? sender, ControlEventArgs args)
    {
        if (_localizedCaptionCacheBuilt && !IsDisposed && !Disposing &&
            args.Control is { } child)
            ObserveLocalizedCaptionTree(child);
    }

    private void OnLocalizedCaptionControlRemoved(object? sender, ControlEventArgs args)
    {
        if (args.Control is not { } removed)
            return;
        var pending = new Stack<Control>();
        pending.Push(removed);
        while (pending.Count > 0)
        {
            var control = pending.Pop();
            if (_localizedCaptionObserved.Remove(control))
            {
                control.ControlAdded -= OnLocalizedCaptionControlAdded;
                control.ControlRemoved -= OnLocalizedCaptionControlRemoved;
            }

            if (_localizedCaptionControls.Remove(control))
            {
                control.Disposed -= OnLocalizedCaptionControlDisposed;
                control.TextChanged -= OnLocalizedCaptionTextChanged;
                control.VisibleChanged -= OnLocalizedCaptionVisibleChanged;
            }
            if (control is ListView list && _localizedListViewHeaders.Remove(list))
                list.Disposed -= OnLocalizedListViewDisposed;
            if (control is DataGridView grid)
                StopObservingLocalizedGrid(grid);

            foreach (Control child in control.Controls)
                pending.Push(child);
        }
    }

    private void OnLocalizedCaptionControlDisposed(object? sender, EventArgs args)
    {
        if (sender is Control control)
        {
            _localizedCaptionControls.Remove(control);
            control.TextChanged -= OnLocalizedCaptionTextChanged;
            control.VisibleChanged -= OnLocalizedCaptionVisibleChanged;
        }
    }

    private void OnLocalizedListViewDisposed(object? sender, EventArgs args)
    {
        if (sender is ListView list)
        {
            _localizedListViewHeaders.Remove(list);
            list.Disposed -= OnLocalizedListViewDisposed;
        }
    }

    private void StopObservingLocalizedGrid(DataGridView grid)
    {
        if (!_localizedGridHeaders.Remove(grid))
            return;
        grid.ColumnAdded -= OnLocalizedGridColumnAdded;
        grid.ColumnRemoved -= OnLocalizedGridColumnRemoved;
        grid.Disposed -= OnLocalizedGridDisposed;
    }

    private void OnLocalizedGridDisposed(object? sender, EventArgs args)
    {
        if (sender is DataGridView grid)
            StopObservingLocalizedGrid(grid);
    }

    private void OnLocalizedGridColumnAdded(
        object? sender, DataGridViewColumnEventArgs args)
    {
        if (sender is DataGridView grid && !grid.IsDisposed &&
            _localizedGridHeaders.ContainsKey(grid))
        {
            CaptureLocalizedGridHeaders(grid);
            ApplyLocalizedGridHeaders(grid);
        }
    }

    private void OnLocalizedGridColumnRemoved(
        object? sender, DataGridViewColumnEventArgs args)
    {
        if (sender is DataGridView grid &&
            _localizedGridHeaders.TryGetValue(grid, out var headers))
            headers.Remove(args.Column);
    }

    private void CaptureLocalizedGridHeaders(DataGridView grid)
    {
        if (!_localizedGridHeaders.TryGetValue(grid, out var headers))
            return;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (!headers.ContainsKey(column) &&
                column.HeaderText is { } caption &&
                UiStaticCaptionCatalog.Contains(caption))
                headers.Add(column, (caption, column.MinimumWidth));
        }
    }

    private void ApplyLocalizedGridHeaders(DataGridView grid)
    {
        if (grid.IsDisposed || !_localizedGridHeaders.TryGetValue(grid, out var headers) ||
            headers.Count == 0)
            return;

        // Headers only. Never rewrite a row, secret, account, SID or metadata.
        // MinimumWidth permits horizontal scrolling instead of cutting longer
        // Hebrew and Russian captions at 175%-200% DPI.
        var headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
        var flags = TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        // A repeated switch to the same language, or a visible-page
        // refresh with unchanged headers, must not force a grid layout.
        // Defer SuspendLayout until an actual header or width change.
        var suspended = false;
        try
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!headers.TryGetValue(column, out var source))
                    continue;
                var caption = UiStaticCaptionCatalog.TranslateOrOriginal(
                    _uiLanguageCode, source.English);
                var captionChanged = !string.Equals(
                    column.HeaderText, caption, StringComparison.Ordinal);
                var required = TextRenderer.MeasureText(
                    caption, headerFont, new Size(4096, 1024), flags).Width +
                    UiStyle.ScaleLogicalPixels(26, grid.DeviceDpi);
                var width = Math.Max(source.MinimumWidth, required);
                var widthChanged = column.MinimumWidth != width;
                if (!captionChanged && !widthChanged)
                    continue;
                if (!suspended)
                {
                    grid.SuspendLayout();
                    suspended = true;
                }
                if (captionChanged)
                    column.HeaderText = caption;
                if (widthChanged)
                    column.MinimumWidth = width;
            }
        }
        finally
        {
            if (suspended)
                grid.ResumeLayout(performLayout: grid.Visible);
        }
    }

    private void ApplyLocalizedListViewHeaders(ListView list)
    {
        if (list.IsDisposed || !_localizedListViewHeaders.TryGetValue(list, out var sources))
            return;
        for (var i = 0; i < Math.Min(list.Columns.Count, sources.Length); i++)
        {
            var (english, originalWidth) = sources[i];
            if (!UiStaticCaptionCatalog.TryTranslate(_uiLanguageCode, english,
                    out var translation))
                continue;
            var column = list.Columns[i];
            if (!string.Equals(column.Text, translation, StringComparison.Ordinal))
                column.Text = translation;
            // Longer Hebrew/Russian titles must never disappear under fixed
            // narrow 70-80px English header widths. Scrolling is preferable
            // to truncation; the original width is restored in English.
            var required = TextRenderer.MeasureText(translation, list.Font).Width +
                UiStyle.ScaleLogicalPixels(18, list.DeviceDpi);
            var width = Math.Max(originalWidth, required);
            if (column.Width != width)
                column.Width = width;
        }
    }

    private void OnLocalizedCaptionTextChanged(object? sender, EventArgs args)
    {
        // Fixed captions must not disappear because another refresh cleared
        // their Text. Runtime status and identity fields are not registered.
        if (sender is Control control &&
            string.IsNullOrWhiteSpace(control.Text) &&
            _localizedCaptionControls.TryGetValue(control, out var key))
        {
            ApplyLocalizedCaption(control, key,
                UiLanguageService.IsRightToLeft(_uiLanguageCode)
                    ? RightToLeft.Yes : RightToLeft.No);
        }
    }

    private static bool UsesLocalizedCaptionDirection(Control control) =>
        control is Button or CheckBox or RadioButton or LinkLabel or GroupBox;

    // Text is localized on every page, but a warm EN/RU/HE switch does not
    // need to recreate native child handles in invisible workspaces.
    // Before form handles exist (including headless UI tests), apply eagerly.
    private bool CanUpdateCaptionDirection(Control control) =>
        _layoutSelfTest || !IsHandleCreated || control.Visible;

    private void OnLocalizedCaptionVisibleChanged(object? sender, EventArgs args)
    {
        if (sender is not Control control || !control.Visible ||
            control.IsDisposed || control.Disposing ||
            !_localizedCaptionControls.ContainsKey(control))
            return;

        var direction = UiLanguageService.IsRightToLeft(_uiLanguageCode)
            ? RightToLeft.Yes : RightToLeft.No;
        if (UsesLocalizedCaptionDirection(control) &&
            control.RightToLeft != direction)
            control.RightToLeft = direction;
    }

    private void OnLocalizedMenuDisposed(object? sender, EventArgs args)
    {
        if (sender is ToolStripItem item)
        {
            _localizedMenuSources.Remove(item);
            item.Disposed -= OnLocalizedMenuDisposed;
            if (item is ToolStripDropDownItem submenu &&
                _observedLocalizedMenus.Remove(submenu))
                submenu.DropDownOpening -= OnLocalizedMenuOpening;
        }
    }

    private void OnLocalizedMenuOpening(object? sender, EventArgs args)
    {
        if (sender is ToolStripDropDownItem submenu && !IsDisposed)
            ApplyKnownMenuCaptions(submenu.DropDownItems);
    }

    // This is intentionally limited to known fixed captions. It does not
    // translate device IDs, dynamic results or arbitrary ToolStrip Tag values.
    private void ApplyKnownMenuCaptions(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripSeparator)
                continue;

            if (!_localizedMenuSources.TryGetValue(item, out var original))
            {
                // Never infer a caption from Tag; it may be a command
                // identifier, a security context, or a custom binding.
                var source = item.Text;
                if (!string.IsNullOrWhiteSpace(source) &&
                    UiStaticCaptionCatalog.Contains(source))
                {
                    original = source;
                    _localizedMenuSources.Add(item, source);
                    item.Disposed += OnLocalizedMenuDisposed;
                }
            }

            if (original is not null &&
                UiStaticCaptionCatalog.TryTranslate(_uiLanguageCode, original,
                    out var caption) &&
                !string.Equals(item.Text, caption, StringComparison.Ordinal))
                item.Text = caption;

            if (item is ToolStripDropDownItem submenu)
            {
                if (!_observedLocalizedMenus.TryGetValue(submenu, out _))
                {
                    _observedLocalizedMenus.Add(submenu,
                        LocalizedCaptionObserverMarker);
                    submenu.DropDownOpening += OnLocalizedMenuOpening;
                    if (!_localizedMenuSources.ContainsKey(submenu))
                        submenu.Disposed += OnLocalizedMenuDisposed;
                }
                ApplyKnownMenuCaptions(submenu.DropDownItems);
            }
        }
    }

    private string TranslateFixedCaption(string key) =>
        key.StartsWith(OriginalCaptionPrefix, StringComparison.Ordinal)
            ? UiStaticCaptionCatalog.TranslateOrOriginal(
                _uiLanguageCode, key[OriginalCaptionPrefix.Length..])
            : UiLanguageService.Translate(_uiLanguageCode, key);

    private void ApplyLocalizedCaption(
        Control control, string key, RightToLeft direction)
    {
        if (control.IsDisposed || control.Disposing)
            return;

        var translated = TranslateFixedCaption(key);
        if (!string.IsNullOrWhiteSpace(translated) &&
            !string.Equals(control.Text, translated, StringComparison.Ordinal))
            control.Text = translated;

        // Only captions follow RTL. Device identity, recovery keys and typed
        // input remain LTR; changing Form.RightToLeft recreates HWNDs.
        if (UsesLocalizedCaptionDirection(control) &&
            CanUpdateCaptionDirection(control) &&
            control.RightToLeft != direction)
            control.RightToLeft = direction;
    }

    private void ApplyLocalizedShellCaptions(bool rtl)
    {
        if (!_localizedCaptionCacheBuilt)
        {
            _localizedCaptionCacheBuilt = true;
            ObserveLocalizedCaptionTree(_mainTabs, applyImmediately: false);
        }

        var direction = rtl ? RightToLeft.Yes : RightToLeft.No;
        // Never suspend and force-layout the entire registered form on every
        // language switch. Most Controls are on unselected nested tabs and
        // do not need visible layout until that page is shown.
        var changed = new List<(Control Control, string Caption, bool ChangeText,
            bool ChangeDirection)>();
        foreach (var (control, key) in _localizedCaptionControls)
        {
            if (control.IsDisposed || control.Disposing)
                continue;
            var caption = TranslateFixedCaption(key);
            var changeText = !string.IsNullOrWhiteSpace(caption) &&
                !string.Equals(control.Text, caption, StringComparison.Ordinal);
            var changeDirection =
                UsesLocalizedCaptionDirection(control) &&
                CanUpdateCaptionDirection(control) &&
                control.RightToLeft != direction;
            if (changeText || changeDirection)
                changed.Add((control, caption, changeText, changeDirection));
        }

        var parents = changed.Select(x => x.Control.Parent)
            .Concat(_localizedListViewHeaders.Keys
                .Where(x => !x.IsDisposed).Select(x => x.Parent))
            .OfType<Control>().Distinct().ToArray();
        foreach (var parent in parents)
            parent.SuspendLayout();
        try
        {
            foreach (var change in changed)
            {
                if (change.ChangeText)
                    change.Control.Text = change.Caption;
                if (change.ChangeDirection)
                    change.Control.RightToLeft = direction;
            }
            foreach (var list in _localizedListViewHeaders.Keys.ToArray())
                ApplyLocalizedListViewHeaders(list);
            foreach (var grid in _localizedGridHeaders.Keys.ToArray())
                ApplyLocalizedGridHeaders(grid);
        }
        finally
        {
            // Hidden pages retain translated Text but must not execute full
            // FlowLayoutPanel/TableLayoutPanel passes now. Normal tab
            // selection explicitly repairs/reflows the selected hierarchy.
            for (var i = parents.Length - 1; i >= 0; i--)
                if (!parents[i].IsDisposed)
                    parents[i].ResumeLayout(performLayout: parents[i].Visible);
        }

        // The summary is a mutable run result: translate only its original
        // empty-session placeholder; never replace an actual export result.
        if (UiStaticCaptionCatalog.Contains("No run in this GUI session yet.") &&
            new[] { "en", "ru", "he" }.Any(code =>
                string.Equals(_exportSummary.Text,
                    UiStaticCaptionCatalog.TranslateOrOriginal(code,
                        "No run in this GUI session yet."),
                    StringComparison.Ordinal)))
            _exportSummary.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
                _uiLanguageCode, "No run in this GUI session yet.");

        // Last-success detail can be device-specific and must never be treated
        // as a static caption. The absent/unavailable states are safe to relabel.
        foreach (var original in new[]
        {
            "Last successful export: not recorded yet.",
            "Last successful export: status unavailable."
        })
        {
            if (new[] { "en", "ru", "he" }.Any(code =>
                string.Equals(_lastSuccess.Text,
                    UiStaticCaptionCatalog.TranslateOrOriginal(code, original),
                    StringComparison.Ordinal)))
            {
                _lastSuccess.Text = UiStaticCaptionCatalog.TranslateOrOriginal(
                    _uiLanguageCode, original);
                break;
            }
        }

        // Menus are outside the WinForms Control tree. Cache original labels
        // independently of Tag and update known nested entries as well.
        if (_toolsTopMenu is not null)
            ApplyKnownMenuCaptions(_toolsTopMenu.DropDownItems);
        ApplyKnownMenuCaptions(_homeConnectionToolsMenu.Items);

        SetCloudCaption(_cloudSaveButton, "MainSaveConfig", direction);
        SetCloudCaption(_cloudWizardButton, "MainEntraWizard", direction);
        SetCloudCaption(_cloudConnectButton, "MainConnectTest", direction);
        SetCloudCaption(_cloudConnectCancelButton, "Cancel", direction);
        SetCloudCaption(_cloudSetupButton, "MainFirstRunRepair", direction);
        SetCloudCaption(_cloudBootstrapButton, "MainBootstrap", direction);
        SetCloudCaption(_cloudRolloverButton, "MainRolloverCertificate", direction);
        SetCloudCaption(_cloudLapsPermissions, "MainCloudLapsRead", direction);

        foreach (var (name, key) in new[]
        {
            ("HomeEnvironmentCheckMenuItem", "MainEnvironmentCheck"),
            ("HomeDeviceDiagnosticsMenuItem", "MainDeviceDiagnostics")
        })
        {
            foreach (var item in _homeConnectionToolsMenu.Items.Find(name, false))
            {
                var caption = UiLanguageService.Translate(_uiLanguageCode, key);
                if (!string.IsNullOrWhiteSpace(caption) &&
                    !string.Equals(item.Text, caption, StringComparison.Ordinal))
                    item.Text = caption;
            }
        }
        RefreshStartConnectionLocalization();
        RefreshAdvancedConnectionMenuCaption();
    }

    private void SetCloudCaption(Control control, string key, RightToLeft direction)
    {
        if (control.Parent is null || control.IsDisposed)
            return;
        var caption = UiLanguageService.Translate(_uiLanguageCode, key);
        if (!string.IsNullOrWhiteSpace(caption) &&
            !string.Equals(control.Text, caption, StringComparison.Ordinal))
            control.Text = caption;
        if ((control is Button or CheckBox) && control.RightToLeft != direction)
            control.RightToLeft = direction;
    }

    private void RefreshAdvancedConnectionMenuCaption()
    {
        var key = _advancedConnectionGroup.Visible
            ? "MainHideAdvancedAd"
            : "MainAdvancedAd";
        var caption = UiLanguageService.Translate(_uiLanguageCode, key);
        if (!string.IsNullOrWhiteSpace(caption) &&
            !string.Equals(_homeAdvancedConnectionMenuItem.Text, caption,
                StringComparison.Ordinal))
            _homeAdvancedConnectionMenuItem.Text = caption;
    }
}
