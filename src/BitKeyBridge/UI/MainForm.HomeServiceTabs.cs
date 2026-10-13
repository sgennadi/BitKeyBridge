namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly TableLayoutPanel _homeServiceCards = new();
    private readonly UiRecoveryCard _homeBitLockerCard = new();
    private readonly UiRecoveryCard _homeLapsCard = new();
    private readonly UiRecoveryBadge _homeBitLockerCardStatus = new();
    private readonly LinkLabel _homeBitLockerDiagnosticLink = new();
    private readonly Label _homeBitLockerCardKeyId = new();
    private readonly UiRecoveryBadge _homeLapsCardStatus = new();
    private readonly Label _homeLapsCardAccount = new();
    private readonly Button _homeRevealLapsButton = new();
    private readonly Button _homeCopyLapsButton = new();
    private readonly Button _homeLoadLapsHistoryButton = new();
    private readonly Label _homeLapsHistoryStatus = new();
    private readonly Label _homeLapsSelectedRecord = new();
    private readonly DataGridView _homeLapsHistoryGrid = new();

    private bool _homeLapsBusy;
    private bool _homeLapsHistoryLoaded;
    private string _homeLapsHistoryComputer = string.Empty;
    private string _homeCardsSelectedIdentity = string.Empty;
    private bool _homeCardsHasBitLocker;
    private bool _homeCardsHasLaps;
    private bool _homeCardsLayingOut;
    private string _homeCardsLayoutKey = string.Empty;

    private Control BuildHomeServiceCards()
    {
        _homeServiceCards.Name = "HomeServiceCards";
        _homeServiceCards.AccessibleName = "Selected computer: BitLocker and LAPS recovery";
        _homeServiceCards.Dock = DockStyle.Top;
        _homeServiceCards.AutoSize = true;
        _homeServiceCards.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _homeServiceCards.Margin = Padding.Empty;
        _homeServiceCards.Padding = Padding.Empty;
        _homeServiceCards.BackColor = SystemColors.Control;
        _homeServiceCards.Visible = false;

        _homeBitLockerCard.Name = "HomeBitLockerCard";
        _homeBitLockerCard.AccessibleName = "BitLocker recovery";
        _homeLapsCard.Name = "HomeLapsCard";
        _homeLapsCard.AccessibleName = "Windows and legacy LAPS recovery";

        _homeBitLockerDiagnosticLink.Name = "HomeBitLockerDiagnosticLink";
        _homeBitLockerDiagnosticLink.AccessibleName =
            "Open commands to diagnose BitLocker recovery for the selected computer";
        _homeBitLockerDiagnosticLink.Text = "View diagnostic commands";
        _homeBitLockerDiagnosticLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _homeBitLockerDiagnosticLink.LinkColor = UiStyle.ModernAccent;
        _homeBitLockerDiagnosticLink.ActiveLinkColor = UiStyle.ModernAccent;
        _homeBitLockerDiagnosticLink.Font = UiStyle.CreateEmphasisFont(9F);
        _homeBitLockerDiagnosticLink.AutoSize = true;
        _homeBitLockerDiagnosticLink.Enabled = false;
        _homeBitLockerDiagnosticLink.Margin =
            new Padding(0, UiStyle.SectionGap, 0, 0);
        _homeBitLockerDiagnosticLink.LinkClicked += (_, _) =>
            ShowHomeBitLockerDiagnosticCommands();

        var bitLockerHelp = new FlowLayoutPanel
        {
            Name = "HomeBitLockerHelp",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Dock = DockStyle.Top,
            Margin = Padding.Empty
        };
        bitLockerHelp.Controls.Add(_homeBitLockerDiagnosticLink);

        _homeBitLockerCard.Controls.Add(BuildHomeRecoveryCardBody(
            RecoveryGlyphKind.Shield,
            "BitLocker",
            _homeBitLockerCardStatus,
            "Recovery Key ID",
            _homeBitLockerCardKeyId,
            "Recovery password",
            [
                (_homeRevealBitLockerButton, RecoveryGlyphKind.Eye, "Reveal Key"),
                (_homeCopyBitLockerButton, RecoveryGlyphKind.Copy, "Copy Key")
            ],
            bitLockerHelp));

        _homeRevealLapsButton.Name = "HomeRevealLapsButton";
        _homeCopyLapsButton.Name = "HomeCopyLapsButton";
        _homeLoadLapsHistoryButton.Name = "HomeLoadLapsHistoryButton";
        _homeRevealLapsButton.AccessibleName =
            "Reveal the selected LAPS current or historical password";
        _homeCopyLapsButton.AccessibleName =
            "Copy the selected LAPS current or historical password";
        _homeLoadLapsHistoryButton.AccessibleName =
            "Load current and historical LAPS records with permission checks";
        // No selected computer at startup: every privileged LAPS action is disabled.
        _homeRevealLapsButton.Enabled = false;
        _homeCopyLapsButton.Enabled = false;
        _homeLoadLapsHistoryButton.Enabled = false;

        var historyArea = BuildHomeLapsHistoryArea();
        _homeLapsCard.Controls.Add(BuildHomeRecoveryCardBody(
            RecoveryGlyphKind.Key,
            "LAPS",
            _homeLapsCardStatus,
            "Account",
            _homeLapsCardAccount,
            "Password",
            [
                (_homeRevealLapsButton, RecoveryGlyphKind.Eye, "Reveal Password"),
                (_homeCopyLapsButton, RecoveryGlyphKind.Copy, "Copy Password")
            ],
            historyArea));

        _homeServiceCards.ColumnCount = 2;
        _homeServiceCards.RowCount = 1;
        _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _homeServiceCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _homeServiceCards.Controls.Add(_homeBitLockerCard, 0, 0);
        _homeServiceCards.Controls.Add(_homeLapsCard, 1, 0);

        _homeBitLockerCard.Margin = new Padding(0, 6, UiStyle.ControlGap, 6);
        _homeLapsCard.Margin = new Padding(UiStyle.ControlGap, 6, 0, 6);
        _homeBitLockerCard.Dock = DockStyle.Fill;
        _homeLapsCard.Dock = DockStyle.Fill;
        _homeServiceCards.SizeChanged += (_, _) => ArrangeHomeServiceCards();

        _homeRevealLapsButton.Click +=
            async (_, _) => await ShowHomeLapsPasswordAsync(copy: false);
        _homeCopyLapsButton.Click +=
            async (_, _) => await ShowHomeLapsPasswordAsync(copy: true);
        _homeLoadLapsHistoryButton.Click +=
            async (_, _) => await LoadHomeLapsHistoryAsync();
        _homeLapsHistoryGrid.SelectionChanged +=
            (_, _) => UpdateHomeLapsHistorySelection();

        return _homeServiceCards;
    }

    private static Control BuildHomeRecoveryCardBody(
        RecoveryGlyphKind glyph,
        string title,
        UiRecoveryBadge status,
        string detailCaption,
        Label detailValue,
        string secretCaption,
        (Button Button, RecoveryGlyphKind Glyph, string Caption)[] actions,
        Control? bottom)
    {
        var layout = new TableLayoutPanel
        {
            Name = title + "CardContent",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = bottom is null ? 7 : 8,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < layout.RowCount; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var glyphControl = new UiRecoveryGlyph(glyph)
        {
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 0)
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = UiStyle.CreatePageTitleFont(),
            ForeColor = UiStyle.RecoveryCardForeground,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };
        header.Controls.Add(glyphControl, 0, 0);
        header.Controls.Add(titleLabel, 1, 0);
        layout.Controls.Add(header, 0, 0);

        var statusArea = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        status.Text = "Not verified";
        statusArea.Controls.Add(status);
        layout.Controls.Add(statusArea, 0, 1);

        var detailLabel = new Label { Text = detailCaption };
        UiStyle.ConfigureRecoveryFieldCaption(detailLabel);
        layout.Controls.Add(detailLabel, 0, 2);

        UiStyle.ConfigureRecoveryFieldValue(detailValue);
        detailValue.Text = "Not loaded";
        layout.Controls.Add(detailValue, 0, 3);

        var secretLabel = new Label { Text = secretCaption };
        UiStyle.ConfigureRecoveryFieldCaption(secretLabel);
        layout.Controls.Add(secretLabel, 0, 4);

        var masked = new Label { Text = "••••••••••••", AccessibleName = secretCaption + " hidden" };
        UiStyle.ConfigureRecoveryFieldValue(masked);
        masked.Font = UiStyle.CreateEmphasisFont(15F);
        masked.Margin = new Padding(0, 0, 0, UiStyle.SectionGap);
        layout.Controls.Add(masked, 0, 5);

        var buttonStack = new TableLayoutPanel
        {
            Name = title + "RecoveryActions",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = actions.Length,
            Margin = Padding.Empty
        };
        buttonStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < actions.Length; i++)
        {
            buttonStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var (button, buttonGlyph, caption) = actions[i];
            button.Text = caption;
            UiStyle.ConfigureRecoveryActionButton(button, buttonGlyph, i == actions.Length - 1);
            button.Dock = DockStyle.Fill;
            buttonStack.Controls.Add(button, 0, i);
        }
        layout.Controls.Add(buttonStack, 0, 6);

        if (bottom is not null)
            layout.Controls.Add(bottom, 0, 7);

        return layout;
    }

    private Control BuildHomeLapsHistoryArea()
    {
        var history = new TableLayoutPanel
        {
            Name = "HomeLapsHistoryArea",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, UiStyle.SectionGap, 0, 0)
        };
        history.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 5; row++)
            history.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = "Password history",
            Font = UiStyle.CreateEmphasisFont(11F),
            AutoSize = true,
            Margin = new Padding(0, UiStyle.ControlGap, 0, 2),
            Dock = DockStyle.Top
        };
        history.Controls.Add(heading, 0, 0);

        UiStyle.ConfigureRecoveryFieldCaption(_homeLapsHistoryStatus);
        _homeLapsHistoryStatus.Name = "HomeLapsHistoryStatus";
        _homeLapsHistoryStatus.Text = "History has not been loaded.";
        _homeLapsHistoryStatus.AccessibleName = "LAPS history status";
        history.Controls.Add(_homeLapsHistoryStatus, 0, 1);

        _homeLapsHistoryGrid.Name = "HomeLapsHistoryGrid";
        _homeLapsHistoryGrid.AccessibleName = "Current and historical LAPS password records, no plaintext";
        _homeLapsHistoryGrid.Dock = DockStyle.Top;
        _homeLapsHistoryGrid.Height = UiStyle.RecoveryHistoryGridHeight;
        _homeLapsHistoryGrid.MinimumSize = new Size(0, UiStyle.RecoveryHistoryGridMinimumHeight);
        _homeLapsHistoryGrid.ScrollBars = ScrollBars.Both;
        _homeLapsHistoryGrid.Visible = false;
        UiStyle.ConfigureDataGridView(_homeLapsHistoryGrid);
        _homeLapsHistoryGrid.Columns.AddRange([
            UiStyle.CreateSortableTextColumn("Type", "Type", 80),
            UiStyle.CreateSortableTextColumn("Account", "Account", 130),
            UiStyle.CreateSortableTextColumn("Updated", "Updated (UTC)", 135),
            UiStyle.CreateSortableTextColumn("Status", "Status", 130)
        ]);
        UiStyle.ConfigureReadableDataGridColumns(_homeLapsHistoryGrid,
            ("Type", "History", 80),
            ("Account", "Administrator", 125),
            ("Updated", "2026-10-13 14:22", 140),
            ("Status", "Decrypt unauthorized", 140));
        history.Controls.Add(_homeLapsHistoryGrid, 0, 2);

        UiStyle.ConfigureRecoveryFieldCaption(_homeLapsSelectedRecord);
        _homeLapsSelectedRecord.Name = "HomeLapsSelectedRecord";
        _homeLapsSelectedRecord.Text = "Current password will be used until you choose a history record.";
        history.Controls.Add(_homeLapsSelectedRecord, 0, 3);

        _homeLoadLapsHistoryButton.Text = "Load history";
        UiStyle.ConfigureRecoveryActionButton(
            _homeLoadLapsHistoryButton, RecoveryGlyphKind.History, secondary: true);
        _homeLoadLapsHistoryButton.Dock = DockStyle.Top;
        _homeLoadLapsHistoryButton.Margin = new Padding(0, UiStyle.ControlGap, 0, 0);
        _homeLoadLapsHistoryButton.Enabled = false;
        history.Controls.Add(_homeLoadLapsHistoryButton, 0, 4);
        return history;
    }

    private void UpdateHomeServiceCards(HomeSearchResult? selected)
    {
        var identity = selected is null ? string.Empty :
            selected.ComputerName + "|" + selected.ComputerId + "|" + selected.SourceIdentity;
        if (!string.Equals(_homeCardsSelectedIdentity, identity, StringComparison.OrdinalIgnoreCase))
        {
            _homeCardsSelectedIdentity = identity;
            if (_homeLapsHistoryLoaded || _homeLapsHistoryGrid.Rows.Count > 0)
                ClearLapsResult();
        }

        var (showBitLocker, showLaps) = HelpdeskStatus.VisibleHomeServices(selected);
        _homeBitLockerCardStatus.Text = selected is null ? "Not verified" :
            selected.BitLockerLookupFailed ? "Not verified" : selected.BitLockerStatus;
        _homeBitLockerCardStatus.Kind = selected is null ? UiStatusKind.Neutral :
            selected.BitLockerLookupFailed || selected.BitLockerKeyCount == 0 ? UiStatusKind.Warning :
            selected.BitLockerStatus.Contains("Old", StringComparison.OrdinalIgnoreCase) ?
            UiStatusKind.Warning : UiStatusKind.Success;
        _homeBitLockerCardKeyId.Text = selected is not null &&
            !string.IsNullOrWhiteSpace(selected.LatestRecoveryId) ?
            selected.LatestRecoveryId : "Not verified";

        _homeLapsCardStatus.Text = selected is null ? "Not verified" :
            selected.LapsLookupFailed ? "Not verified" : selected.LapsStatus;
        _homeLapsCardStatus.Kind = selected is null ? UiStatusKind.Neutral :
            selected.LapsLookupFailed || selected.LapsStatus == "Expired" ?
            UiStatusKind.Warning : selected.LapsStatus == "Detected" ?
            UiStatusKind.Success : UiStatusKind.Neutral;

        if (!_homeLapsHistoryLoaded ||
            !string.Equals(_homeLapsHistoryComputer, selected?.ComputerName,
                StringComparison.OrdinalIgnoreCase))
            _homeLapsCardAccount.Text = "Not loaded";

        _homeCardsHasBitLocker = showBitLocker;
        _homeCardsHasLaps = showLaps;
        ArrangeHomeServiceCards();
        _homeServiceCards.Visible = selected is not null &&
            !selected.IsCloudRecord && (showBitLocker || showLaps);

        bool canReadLaps = selected is not null &&
            _directoryConnected && !_directoryConnecting &&
            !selected.IdentityAmbiguous && !selected.IsCloudRecord &&
            !_homeLapsBusy && !_lapsReading;
        _homeRevealLapsButton.Enabled = canReadLaps;
        _homeCopyLapsButton.Enabled = canReadLaps;
        _homeLoadLapsHistoryButton.Enabled = canReadLaps;
    }

    private void ArrangeHomeServiceCards()
    {
        if (_homeCardsLayingOut || _homeServiceCards.IsDisposed)
            return;

        var available = _homeSelectionPanel.ClientSize.Width;
        if (available <= 0)
            available = _homeServiceCards.ClientSize.Width;
        var threshold = (int)Math.Ceiling(
            UiStyle.HomeRecoveryCardsPairMinimumWidth *
            DeviceDpi / (double)UiStyle.BaselineDpi);

        bool both = _homeCardsHasBitLocker && _homeCardsHasLaps;
        bool stacked = both && available > 0 && available < threshold;
        var desiredKey = (_homeCardsHasBitLocker ? "B" : "-") +
                         (_homeCardsHasLaps ? "L" : "-") +
                         (stacked ? "S" : "H");
        if (_homeCardsLayoutKey == desiredKey)
            return;

        _homeCardsLayoutKey = desiredKey;
        _homeCardsLayingOut = true;
        _homeServiceCards.SuspendLayout();
        try
        {
            _homeServiceCards.Controls.Clear();
            _homeServiceCards.ColumnStyles.Clear();
            _homeServiceCards.RowStyles.Clear();

            bool initial = !_homeCardsHasBitLocker && !_homeCardsHasLaps;
            if ((both && !stacked) || initial)
            {
                _homeServiceCards.ColumnCount = 2;
                _homeServiceCards.RowCount = 1;
                _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                _homeServiceCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _homeServiceCards.Controls.Add(_homeBitLockerCard, 0, 0);
                _homeServiceCards.Controls.Add(_homeLapsCard, 1, 0);
            }
            else
            {
                _homeServiceCards.ColumnCount = 1;
                _homeServiceCards.RowCount = both ? 2 : 1;
                _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _homeServiceCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                if (both)
                    _homeServiceCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                if (_homeCardsHasBitLocker)
                    _homeServiceCards.Controls.Add(_homeBitLockerCard, 0, 0);
                if (_homeCardsHasLaps)
                    _homeServiceCards.Controls.Add(_homeLapsCard, 0,
                        _homeCardsHasBitLocker ? 1 : 0);
            }
        }
        finally
        {
            _homeServiceCards.ResumeLayout(true);
            _homeCardsLayingOut = false;
        }
    }


    // History must be requested explicitly: AD history attributes are
    // privileged, and may contain encrypted older passwords.
    private async Task LoadHomeLapsHistoryAsync()
    {
        var selected = GetSelectedHomeSearchResult();
        if (selected is null || selected.IdentityAmbiguous ||
            selected.IsCloudRecord || !_directoryConnected ||
            _directoryConnecting || _homeLapsBusy || _lapsReading)
            return;

        var computer = selected.ComputerName;
        _homeLapsBusy = true;
        UpdateHomeServiceCards(selected);
        _homeLapsHistoryStatus.Text = "Loading current and historical LAPS records...";
        try
        {
            PrepareHomeLapsRead(computer);
            await ReadLapsAsync(includeHistoryOverride: true);

            if (!ReferenceEquals(selected, GetSelectedHomeSearchResult()))
            {
                ClearLapsResult();
                UiStyle.SetStatus(_homeSearchStatus,
                    "Computer selection changed. LAPS history was discarded.",
                    UiStatusKind.Warning);
                return;
            }

            var result = _lapsResult;
            if (result is null ||
                !string.Equals(result.ComputerName, computer,
                    StringComparison.OrdinalIgnoreCase))
            {
                _homeLapsHistoryStatus.Text =
                    "History could not be loaded. Check the LAPS diagnostic messages.";
                return;
            }

            _homeLapsHistoryComputer = computer;
            _homeLapsHistoryLoaded = true;
            _homeLapsHistoryGrid.Rows.Clear();

            foreach (var entry in result.OrderedEntries)
            {
                var index = _homeLapsHistoryGrid.Rows.Add(
                    entry.IsHistory ? "History" : "Current",
                    string.IsNullOrWhiteSpace(entry.AccountName) ?
                        "Not returned" : entry.AccountName,
                    entry.UpdatedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "Unknown",
                    HelpdeskStatus.LapsEntry(entry));
                // Never put password strings in DataGridView cells or audit metadata.
                _homeLapsHistoryGrid.Rows[index].Tag = entry;
            }

            var historyCount = result.Entries.Count(x => x.IsHistory);
            var readable = result.Entries.Count(x => x.HasPassword &&
                x.Status == LapsPasswordStatus.Available);
            _homeLapsHistoryGrid.Visible = _homeLapsHistoryGrid.Rows.Count > 0;

            _homeLapsHistoryStatus.Text =
                result.Entries.Count == 0
                    ? LapsReadDiagnostics.EmptyStatus(result, cloud: false)
                    : historyCount == 0
                        ? "No previous passwords were returned. History may be disabled, empty or unreadable."
                        : historyCount + " historical record(s). Select a row to reveal or copy its password.";

            if (_homeLapsHistoryGrid.Rows.Count > 0)
            {
                _homeLapsHistoryGrid.ClearSelection();
                var current = _homeLapsHistoryGrid.Rows
                    .Cast<DataGridViewRow>()
                    .FirstOrDefault(x => x.Tag is LapsPasswordEntry { IsHistory: false }) ??
                    _homeLapsHistoryGrid.Rows[0];
                current.Selected = true;
                if (current.Cells.Count > 0)
                    _homeLapsHistoryGrid.CurrentCell = current.Cells[0];
                UpdateHomeLapsHistorySelection();
            }

            UiStyle.SetStatus(_homeSearchStatus,
                result.Entries.Count == 0
                    ? LapsReadDiagnostics.EmptyStatus(result, cloud: false)
                    : $"LAPS history loaded: {result.Entries.Count} records; " +
                      $"{readable} readable. Passwords remain hidden.",
                result.Entries.Count == 0 || readable == 0 ?
                    UiStatusKind.Warning : UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_homeSearchStatus,
                "LAPS history could not be loaded. Review diagnostics.",
                UiStatusKind.Error);
            _appDiagnostics.ShowError("LAPS history failed.", "HomeLapsHistory", ex);
        }
        finally
        {
            _homeLapsBusy = false;
            UpdateHomeServiceCards(GetSelectedHomeSearchResult());
        }
    }

    private void PrepareHomeLapsRead(string computer)
    {
        if (_lapsSource.SelectedIndex != 0)
            _lapsSource.SelectedIndex = 0;
        _suppressLapsSearchQueue = true;
        try
        {
            _lapsQuery.Text = computer;
        }
        finally
        {
            _suppressLapsSearchQueue = false;
        }
        _lapsSearchDebounceTimer?.Stop();
    }

    private void UpdateHomeLapsHistorySelection()
    {
        if (!_homeLapsHistoryLoaded ||
            _homeLapsHistoryGrid.SelectedRows.Count != 1 ||
            _homeLapsHistoryGrid.SelectedRows[0].Tag is not LapsPasswordEntry entry)
        {
            _homeLapsSelectedRecord.Text =
                "Current password is used unless you select a history record.";
            return;
        }

        _homeLapsCardAccount.Text =
            string.IsNullOrWhiteSpace(entry.AccountName) ? "Not returned" : entry.AccountName;
        _homeLapsSelectedRecord.Text =
            (entry.IsHistory ? "Selected history" : "Selected current") +
            " • " + entry.Source +
            " • " + HelpdeskStatus.LapsEntry(entry);
    }

    // Called on explicit secret clear, computer change and password timeout.
    private void ClearHomeLapsHistory()
    {
        _homeLapsHistoryLoaded = false;
        _homeLapsHistoryComputer = string.Empty;
        if (!_homeLapsHistoryGrid.IsDisposed)
        {
            _homeLapsHistoryGrid.Rows.Clear();
            _homeLapsHistoryGrid.Visible = false;
        }
        _homeLapsHistoryStatus.Text = "History has not been loaded.";
        _homeLapsSelectedRecord.Text =
            "Current password is used unless you select a history record.";
        _homeLapsCardAccount.Text = "Not loaded";
    }

    private async Task ShowHomeLapsPasswordAsync(bool copy)
    {
        var selected = GetSelectedHomeSearchResult();
        if (selected is null || selected.IdentityAmbiguous ||
            selected.IsCloudRecord || !_directoryConnected ||
            _directoryConnecting || _homeLapsBusy || _lapsReading)
            return;

        var computer = selected.ComputerName;
        _homeLapsBusy = true;
        UpdateHomeServiceCards(selected);
        bool retainHistory = false;
        try
        {
            retainHistory = _homeLapsHistoryLoaded &&
                string.Equals(_homeLapsHistoryComputer, computer,
                    StringComparison.OrdinalIgnoreCase) &&
                _lapsResult is not null;

            if (!retainHistory)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Reading the current LAPS password for " + computer + "...",
                    UiStatusKind.Busy);
                PrepareHomeLapsRead(computer);
                await ReadLapsAsync(includeHistoryOverride: false);
            }

            if (!ReferenceEquals(selected, GetSelectedHomeSearchResult()))
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Computer selection changed; the LAPS result was discarded.",
                    UiStatusKind.Warning);
                retainHistory = false;
                return;
            }

            var result = _lapsResult;
            if (result is null ||
                !string.Equals(result.ComputerName, computer,
                    StringComparison.OrdinalIgnoreCase))
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "LAPS read did not complete. Check LAPS diagnostics.",
                    UiStatusKind.Warning);
                retainHistory = false;
                return;
            }

            LapsPasswordEntry? row;
            if (retainHistory)
            {
                row = _homeLapsHistoryGrid.SelectedRows.Count == 1 ?
                    _homeLapsHistoryGrid.SelectedRows[0].Tag as LapsPasswordEntry : null;
                if (row is null || !result.Entries.Contains(row))
                {
                    UiStyle.SetStatus(_homeSearchStatus,
                        "Select a current or historical LAPS record first.",
                        UiStatusKind.Warning);
                    return;
                }
            }
            else
            {
                var current = result.OrderedEntries.Where(entry =>
                    !entry.IsHistory &&
                    entry.HasPassword &&
                    entry.Status == LapsPasswordStatus.Available).ToList();
                if (current.Count != 1)
                {
                    UiStyle.SetStatus(_homeSearchStatus,
                        current.Count == 0
                            ? result.Entries.Count == 0
                                ? LapsReadDiagnostics.EmptyStatus(result, cloud: false)
                                : LapsReadDiagnostics.UnreadableStatus(result, cloud: false)
                            : "Multiple current LAPS credentials exist. " +
                              "Load history and choose the exact account.",
                        UiStatusKind.Warning);
                    return;
                }
                row = current[0];
            }

            if (!row.HasPassword || row.Status != LapsPasswordStatus.Available)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "The selected LAPS record is not readable: " +
                    HelpdeskStatus.LapsEntry(row) + ". " +
                    row.StatusDetail,
                    UiStatusKind.Warning);
                return;
            }

            var action = copy ? "CopyLapsPassword" : "RevealLapsPassword";
            var context = AuthorizeLapsAccess(
                action, result.ComputerName, _lapsResourceId, _lapsAuditSource);
            if (context is null)
                return;

            // Access dialogs can outlive the secure-memory timeout. Never use
            // a disposed historical entry or a newly selected computer.
            if (!ReferenceEquals(result, _lapsResult) ||
                !ReferenceEquals(selected, GetSelectedHomeSearchResult()) ||
                !row.HasPassword)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "LAPS credentials expired or the computer changed. Reload the record.",
                    UiStatusKind.Warning);
                return;
            }

            WriteRecoveryAudit(action, context,
                computerName: result.ComputerName,
                recoveryId: _lapsResourceId,
                source: _lapsAuditSource,
                details: "Attribute=" + row.Attribute +
                    "; Account=" + row.AccountName +
                    "; History=" + row.IsHistory +
                    "; Updated=" + row.UpdatedAtUtc?.ToString("O"));

            RecordRecentComputer(result.ComputerName, result.ComputerId,
                copy ? "LAPS copy" : "LAPS reveal");
            var password = row.CopyPassword();

            if (copy)
            {
                // The transient current-password read can be erased immediately.
                // For loaded history the existing LAPS lifetime timer will erase
                // all retained credentials and conditionally clear the clipboard.
                if (!retainHistory)
                    ClearLapsResult();
                CopyKeyWithAutoClear(password);
                UiStyle.SetStatus(_homeSearchStatus,
                    "LAPS password copied. Clipboard clears in " +
                    _config.SensitiveClipboardSeconds + " seconds.",
                    UiStatusKind.Success);
            }
            else
            {
                using var dialog = new SecretDisplayDialog(
                    row.IsHistory ? "Historical LAPS Password" : "LAPS Password",
                    result.ComputerName + " | " + row.AccountName +
                        " | " + row.Source,
                    password,
                    "Password shown on demand; the window and any clipboard copy " +
                    "are cleared on close or after the configured interval.",
                    _config.SensitiveClipboardSeconds);
                dialog.ShowDialog(this);
                UiStyle.SetStatus(_homeSearchStatus,
                    "LAPS password display closed.",
                    UiStatusKind.Success);
            }
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_homeSearchStatus,
                "LAPS password operation failed. See diagnostics.",
                UiStatusKind.Error);
            _appDiagnostics.ShowError(
                "LAPS password operation failed.", "HomeLapsPassword", ex);
        }
        finally
        {
            if (_lapsResult is not null && !retainHistory)
                ClearLapsResult();
            _homeLapsBusy = false;
            UpdateHomeServiceCards(GetSelectedHomeSearchResult());
        }
    }
}
