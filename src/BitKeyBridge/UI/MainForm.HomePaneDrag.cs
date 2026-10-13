namespace BitKeyBridge;

public sealed partial class MainForm
{
    // The contents are hosted separately from their layout cells, allowing
    // side swaps without rebuilding cards, losing selection or reading secrets.
    private readonly TableLayoutPanel _homeRecoveryPaneHost = new();
    private readonly TableLayoutPanel _homeComputersPaneHost = new();
    private readonly Label _homeRecoveryDragHandle = new();
    private readonly Label _homeComputersDragHandle = new();
    private readonly Button _homeSwapPanesButton = new();
    private Point? _homePaneDragOrigin;

    private sealed record HomePaneDragToken(MainForm Owner, bool FromRecovery);

    private void BuildHomePaneHosts()
    {
        BuildHomePaneHost(
            _homeRecoveryPaneHost, "HomeRecoveryPaneHost",
            _homeRecoveryDragHandle, "BitLocker / LAPS",
            _homeSelectionPanel, _homeSwapPanesButton);
        BuildHomePaneHost(
            _homeComputersPaneHost, "HomeComputersPaneHost",
            _homeComputersDragHandle, "Computers",
            _homeSearchResults, null);

        _homeSwapPanesButton.Name = "HomeSwapPanesButton";
        _homeSwapPanesButton.Text = "Swap sides";
        _homeSwapPanesButton.AccessibleName =
            "Swap the BitLocker/LAPS and computer list pane positions";
        UiStyle.ConfigureActionButton(_homeSwapPanesButton);
        _homeSwapPanesButton.AutoSize = true;
        _homeSwapPanesButton.Click += (_, _) => SwapHomePanes();

        EnableHomePaneDrag(_homeRecoveryDragHandle, fromRecovery: true);
        EnableHomePaneDrag(_homeComputersDragHandle, fromRecovery: false);
        EnableHomePaneDrop(_homeRecoveryPaneHost, targetRecovery: true);
        EnableHomePaneDrop(_homeComputersPaneHost, targetRecovery: false);
        EnableHomePaneDrop(_homeSelectionPanel, targetRecovery: true);
        EnableHomePaneDrop(_homeSearchResults, targetRecovery: false);
        EnableHomePaneDrop(_homeServiceCards, targetRecovery: true);
        EnableHomePaneDrop(_homeRecoveryDragHandle, targetRecovery: true);
        EnableHomePaneDrop(_homeComputersDragHandle, targetRecovery: false);
        if (_homeRecoveryDragHandle.Parent is Control recoveryHeader)
            EnableHomePaneDrop(recoveryHeader, targetRecovery: true);
        if (_homeComputersDragHandle.Parent is Control computersHeader)
            EnableHomePaneDrop(computersHeader, targetRecovery: false);
        EnableHomePaneDrop(_homeSwapPanesButton, targetRecovery: true);
        EnableHomePaneDrop(_homeBitLockerCard, targetRecovery: true);
        EnableHomePaneDrop(_homeLapsCard, targetRecovery: true);
    }

    private static void BuildHomePaneHost(
        TableLayoutPanel host, string name, Label grip, string title,
        Control content, Control? action)
    {
        host.Name = name;
        host.Dock = DockStyle.Fill;
        host.AutoSize = true;
        host.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        host.Margin = new Padding(UiStyle.ControlGap / 2);
        host.Padding = Padding.Empty;
        host.ColumnCount = 1;
        host.RowCount = 2;
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new FlowLayoutPanel
        {
            Name = name + "Header",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap),
            Padding = new Padding(UiStyle.ControlGap)
        };

        grip.Name = name + "DragHandle";
        grip.Text = "⠿  " + title + "  (drag to swap)";
        grip.AccessibleName = "Drag " + title + " to the opposite pane to swap sides";
        grip.AccessibleRole = AccessibleRole.StaticText;
        grip.AutoSize = true;
        grip.Font = UiStyle.CreateEmphasisFont(10F);
        grip.Cursor = Cursors.SizeAll;
        grip.ForeColor = UiStyle.ModernForeground;
        grip.Margin = new Padding(0, UiStyle.ControlGap / 2,
            UiStyle.SectionGap, UiStyle.ControlGap / 2);
        header.Controls.Add(grip);
        if (action is not null)
            header.Controls.Add(action);

        host.Controls.Add(header, 0, 0);
        host.Controls.Add(content, 0, 1);
        content.Dock = DockStyle.Top;
    }

    private void EnableHomePaneDrag(Control grip, bool fromRecovery)
    {
        grip.MouseDown += (_, e) =>
        {
            _homePaneDragOrigin = e.Button == MouseButtons.Left ? e.Location : null;
        };
        grip.MouseUp += (_, _) => _homePaneDragOrigin = null;
        grip.MouseMove += (_, e) =>
        {
            if (!_homePaneDragOrigin.HasValue || e.Button != MouseButtons.Left ||
                _homeResultsArea.ColumnCount != 2)
                return;

            var origin = _homePaneDragOrigin.Value;
            var threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - origin.X) < threshold.Width / 2 &&
                Math.Abs(e.Y - origin.Y) < threshold.Height / 2)
                return;

            _homePaneDragOrigin = null;
            grip.DoDragDrop(new HomePaneDragToken(this, fromRecovery),
                DragDropEffects.Move);
        };
    }

    private void EnableHomePaneDrop(Control target, bool targetRecovery)
    {
        target.AllowDrop = true;
        target.DragEnter += (_, e) =>
        {
            e.Effect = TryGetHomePaneDrag(e.Data, targetRecovery)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        };
        target.DragOver += (_, e) =>
        {
            e.Effect = TryGetHomePaneDrag(e.Data, targetRecovery)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        };
        target.DragDrop += (_, e) =>
        {
            if (TryGetHomePaneDrag(e.Data, targetRecovery))
                SwapHomePanes();
        };
    }

    private bool TryGetHomePaneDrag(IDataObject? data, bool targetRecovery)
    {
        return _homeResultsArea.ColumnCount == 2 &&
            data?.GetData(typeof(HomePaneDragToken)) is HomePaneDragToken drag &&
            ReferenceEquals(drag.Owner, this) &&
            drag.FromRecovery != targetRecovery;
    }

    private void SwapHomePanes()
    {
        _startConnectionPreferences.RecoveryCardsOnLeft =
            !_startConnectionPreferences.RecoveryCardsOnLeft;

        // Runtime layout self-tests must never overwrite an operator's preference.
        if (!_layoutSelfTest)
        {
            try
            {
                ConfigService.SaveStartConnectionPreferences(_startConnectionPreferences);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Pane positions changed for this session, but the per-user " +
                    "layout preference could not be saved.",
                    UiStatusKind.Warning);
            }
        }

        _homeResultsLayoutKey = string.Empty;
        ReflowHomeResultsArea();
    }

    private void UpdateHomePaneDragUi(bool sideBySide)
    {
        _homeRecoveryDragHandle.Cursor = sideBySide ? Cursors.SizeAll : Cursors.Default;
        _homeComputersDragHandle.Cursor = sideBySide ? Cursors.SizeAll : Cursors.Default;
        _homeSwapPanesButton.Visible = sideBySide;
        _homeSwapPanesButton.Enabled = sideBySide;
        _homeRecoveryPaneHost.Visible = GetSelectedHomeSearchResult() is not null;
    }
}
