namespace BitKeyBridge;

public sealed partial class MainForm
{
    private enum HomePaneKind { BitLocker, Laps, Computers }

    private readonly TableLayoutPanel _homeBitLockerPaneHost = new();
    private readonly TableLayoutPanel _homeLapsPaneHost = new();
    private readonly TableLayoutPanel _homeComputersPaneHost = new();
    private readonly UiRecoveryCard _homeComputersCard = new();
    private readonly Label _homeBitLockerDragHandle = new();
    private readonly Label _homeLapsDragHandle = new();
    private readonly Label _homeComputersDragHandle = new();
    private Point? _homePaneDragOrigin;
    private HomePaneKind[] _homePaneOrder =
        [HomePaneKind.BitLocker, HomePaneKind.Laps, HomePaneKind.Computers];

    private sealed record HomePaneDragToken(MainForm Owner, HomePaneKind Pane);

    private void BuildHomePaneHosts()
    {
        _homePaneOrder = ParseHomePaneOrder(
            _startConnectionPreferences.HomePaneOrder,
            _startConnectionPreferences.RecoveryCardsOnLeft);

        _homeComputersCard.Name = "HomeComputersCard";
        _homeComputersCard.AccessibleName = "Computer search results";
        _homeComputersCard.AutoSize = false;
        _homeComputersCard.AutoScroll = false;
        _homeComputersCard.Dock = DockStyle.Fill;
        _homeComputersCard.Padding = new Padding(UiStyle.ControlGap);
        _homeSearchResults.Dock = DockStyle.Fill;
        _homeSearchResults.Margin = Padding.Empty;
        _homeComputersCard.Controls.Add(_homeSearchResults);

        BuildHomePaneHost(
            _homeBitLockerPaneHost, "HomeBitLockerPaneHost",
            _homeBitLockerDragHandle, "BitLocker",
            _homeBitLockerCard);
        BuildHomePaneHost(
            _homeLapsPaneHost, "HomeLapsPaneHost",
            _homeLapsDragHandle, "LAPS",
            _homeLapsCard);
        BuildHomePaneHost(
            _homeComputersPaneHost, "HomeComputersPaneHost",
            _homeComputersDragHandle, "Computers",
            _homeComputersCard);

        EnableHomePaneDrag(_homeBitLockerDragHandle, HomePaneKind.BitLocker);
        EnableHomePaneDrag(_homeLapsDragHandle, HomePaneKind.Laps);
        EnableHomePaneDrag(_homeComputersDragHandle, HomePaneKind.Computers);

        foreach (var (host, card, handle, kind) in new[]
        {
            (_homeBitLockerPaneHost, (Control)_homeBitLockerCard,
                _homeBitLockerDragHandle, HomePaneKind.BitLocker),
            (_homeLapsPaneHost, (Control)_homeLapsCard,
                _homeLapsDragHandle, HomePaneKind.Laps),
            (_homeComputersPaneHost, (Control)_homeComputersCard,
                _homeComputersDragHandle, HomePaneKind.Computers)
        })
        {
            EnableHomePaneDrop(host, kind);
            EnableHomePaneDrop(card, kind);
            EnableHomePaneDrop(handle, kind);
            if (handle.Parent is Control header)
                EnableHomePaneDrop(header, kind);
        }
        EnableHomePaneDrop(_homeSearchResults, HomePaneKind.Computers);

        // Never take a credential or recreate a secret-bearing card when
        // reordering. Only these three outer WinForms hosts change position.
        foreach (var (kind, index) in _homePaneOrder.Select((value, index) => (value, index)))
            _homeServiceCards.Controls.Add(HomePaneHost(kind), index, 0);
    }

    private static HomePaneKind[] ParseHomePaneOrder(string? saved, bool oldRecoveryLeft)
    {
        var order = saved?.Split(',', StringSplitOptions.TrimEntries |
            StringSplitOptions.RemoveEmptyEntries);
        if (order is { Length: 3 } &&
            order.All(value => Enum.TryParse<HomePaneKind>(value, true, out _)))
        {
            var parsed = order.Select(value =>
                Enum.Parse<HomePaneKind>(value, true)).ToArray();
            if (parsed.Distinct().Count() == 3)
                return parsed;
        }

        return oldRecoveryLeft
            ? [HomePaneKind.BitLocker, HomePaneKind.Laps, HomePaneKind.Computers]
            : [HomePaneKind.Computers, HomePaneKind.BitLocker, HomePaneKind.Laps];
    }

    private TableLayoutPanel HomePaneHost(HomePaneKind pane) => pane switch
    {
        HomePaneKind.BitLocker => _homeBitLockerPaneHost,
        HomePaneKind.Laps => _homeLapsPaneHost,
        _ => _homeComputersPaneHost
    };

    private static void BuildHomePaneHost(
        TableLayoutPanel host, string name, Label handle, string title,
        Control card)
    {
        host.Name = name;
        host.AccessibleName = title + " draggable panel";
        host.Dock = DockStyle.Fill;
        host.AutoSize = false;
        host.Margin = new Padding(UiStyle.ControlGap / 2);
        host.Padding = Padding.Empty;
        host.ColumnCount = 1;
        host.RowCount = 2;
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new FlowLayoutPanel
        {
            Name = name + "Header",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap),
            Padding = new Padding(UiStyle.ControlGap / 2)
        };
        handle.Name = name + "DragHandle";
        handle.Text = "⠿  " + title + "  — drag to rearrange";
        handle.AccessibleName = "Drag " + title + " onto another panel to exchange positions";
        handle.AutoSize = true;
        handle.Font = UiStyle.CreateEmphasisFont(9F);
        handle.Cursor = Cursors.SizeAll;
        handle.Margin = Padding.Empty;
        header.Controls.Add(handle);

        host.Controls.Add(header, 0, 0);
        card.Dock = DockStyle.Fill;
        card.AutoSize = false;
        card.Margin = Padding.Empty;
        host.Controls.Add(card, 0, 1);
    }

    private void EnableHomePaneDrag(Control handle, HomePaneKind source)
    {
        handle.MouseDown += (_, e) =>
            _homePaneDragOrigin = e.Button == MouseButtons.Left ? e.Location : null;
        handle.MouseUp += (_, _) => _homePaneDragOrigin = null;
        handle.MouseMove += (_, e) =>
        {
            if (!_homePaneDragOrigin.HasValue || e.Button != MouseButtons.Left ||
                _homeServiceCards.ColumnCount != 3)
                return;

            var origin = _homePaneDragOrigin.Value;
            var threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - origin.X) < threshold.Width / 2 &&
                Math.Abs(e.Y - origin.Y) < threshold.Height / 2)
                return;

            _homePaneDragOrigin = null;
            handle.DoDragDrop(new HomePaneDragToken(this, source),
                DragDropEffects.Move);
        };

        // Right-click or the keyboard context-menu key gives a no-drag path.
        var menu = new ContextMenuStrip();
        var moveLeft = menu.Items.Add("Move panel left");
        var moveRight = menu.Items.Add("Move panel right");
        moveLeft.Click += (_, _) => MoveHomePaneRelative(source, -1);
        moveRight.Click += (_, _) => MoveHomePaneRelative(source, +1);
        menu.Opening += (_, _) =>
        {
            var position = Array.IndexOf(_homePaneOrder, source);
            moveLeft.Enabled = _homeServiceCards.ColumnCount == 3 && position > 0;
            moveRight.Enabled = _homeServiceCards.ColumnCount == 3 && position < 2;
        };
        handle.ContextMenuStrip = menu;
    }

    private void EnableHomePaneDrop(Control target, HomePaneKind destination)
    {
        target.AllowDrop = true;
        target.DragEnter += (_, e) =>
            e.Effect = CanDropHomePane(e.Data, destination)
                ? DragDropEffects.Move : DragDropEffects.None;
        target.DragOver += (_, e) =>
            e.Effect = CanDropHomePane(e.Data, destination)
                ? DragDropEffects.Move : DragDropEffects.None;
        target.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(typeof(HomePaneDragToken)) is HomePaneDragToken token &&
                CanDropHomePane(e.Data, destination))
                ExchangeHomePanePositions(token.Pane, destination);
        };
    }

    private bool CanDropHomePane(IDataObject? data, HomePaneKind destination) =>
        _homeServiceCards.ColumnCount == 3 &&
        data?.GetData(typeof(HomePaneDragToken)) is HomePaneDragToken token &&
        ReferenceEquals(token.Owner, this) &&
        token.Pane != destination;

    private void MoveHomePaneRelative(HomePaneKind source, int direction)
    {
        if (_homeServiceCards.ColumnCount != 3)
            return;
        var index = Array.IndexOf(_homePaneOrder, source);
        var other = index + direction;
        if (other is >= 0 and < 3)
            ExchangeHomePanePositions(source, _homePaneOrder[other]);
    }

    private void ExchangeHomePanePositions(HomePaneKind source, HomePaneKind destination)
    {
        if (source == destination || _homeServiceCards.ColumnCount != 3)
            return;

        var left = Array.IndexOf(_homePaneOrder, source);
        var right = Array.IndexOf(_homePaneOrder, destination);
        if (left < 0 || right < 0)
            return;

        (_homePaneOrder[left], _homePaneOrder[right]) =
            (_homePaneOrder[right], _homePaneOrder[left]);
        _startConnectionPreferences.HomePaneOrder = string.Join(",", _homePaneOrder);
        _startConnectionPreferences.RecoveryCardsOnLeft =
            _homePaneOrder[0] != HomePaneKind.Computers;

        if (!_layoutSelfTest)
        {
            try
            {
                ConfigService.SaveStartConnectionPreferences(_startConnectionPreferences);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Panels rearranged. Could not save order for the next session.",
                    UiStatusKind.Warning);
            }
        }

        // SetCellPosition swaps existing parents and keeps every form control,
        // selected computer, loaded history and masked secret untouched.
        _homePaneLayoutKey = string.Empty;
        ReflowHomeResultsArea();
    }

    internal void ExchangeHomePanesForUiSelfTest(string source, string destination)
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI tests only.");
        ExchangeHomePanePositions(
            Enum.Parse<HomePaneKind>(source, true),
            Enum.Parse<HomePaneKind>(destination, true));
    }

    private void UpdateHomePaneDragUi(bool threeColumns)
    {
        var cursor = threeColumns ? Cursors.SizeAll : Cursors.Default;
        _homeBitLockerDragHandle.Cursor = cursor;
        _homeLapsDragHandle.Cursor = cursor;
        _homeComputersDragHandle.Cursor = cursor;
    }
}
