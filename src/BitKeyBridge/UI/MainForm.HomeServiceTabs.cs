namespace BitKeyBridge;

public sealed partial class MainForm
{
    // These are two independent side-by-side cards, not navigation tabs.
    // Selecting a computer only loads metadata; secrets require an explicit click.
    private readonly TableLayoutPanel _homeServiceCards = new();
    private readonly GroupBox _homeBitLockerCard = new();
    private readonly GroupBox _homeLapsCard = new();
    private readonly Label _homeBitLockerCardStatus = new();
    private readonly Label _homeBitLockerCardKeyId = new();
    private readonly Label _homeLapsCardStatus = new();
    private readonly Label _homeLapsCardAccount = new();
    private readonly Button _homeRevealLapsButton = new();
    private readonly Button _homeCopyLapsButton = new();
    private bool _homeLapsBusy;
    private bool _homeCardsHasBitLocker;
    private bool _homeCardsHasLaps;
    private bool _homeCardsLayingOut;
    private string _homeCardsLayoutKey = string.Empty;

    private Control BuildHomeServiceCards()
    {
        _homeServiceCards.Name = "HomeServiceCards";
        _homeServiceCards.AccessibleName = "BitLocker and LAPS computer recovery cards";
        _homeServiceCards.Dock = DockStyle.Top;
        _homeServiceCards.AutoSize = true;
        _homeServiceCards.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _homeServiceCards.Margin = Padding.Empty;
        _homeServiceCards.Padding = Padding.Empty;
        _homeServiceCards.Visible = false;

        _homeBitLockerCard.Name = "HomeBitLockerCard";
        _homeBitLockerCard.Text = "BitLocker";
        _homeBitLockerCard.Controls.Add(BuildHomeRecoveryCardBody(
            _homeBitLockerCardStatus,
            _homeBitLockerCardKeyId,
            "Recovery password: ••••••••••••",
            [_homeRevealBitLockerButton, _homeCopyBitLockerButton]));

        _homeRevealLapsButton.Name = "HomeRevealLapsButton";
        _homeRevealLapsButton.Text = "Reveal Password";
        _homeRevealLapsButton.AccessibleName = "Read and reveal the selected computer LAPS password";
        UiStyle.ConfigureActionButton(_homeRevealLapsButton);
        _homeRevealLapsButton.Enabled = false;

        _homeCopyLapsButton.Name = "HomeCopyLapsButton";
        _homeCopyLapsButton.Text = "Copy Password";
        _homeCopyLapsButton.AccessibleName = "Read and copy the selected computer LAPS password";
        UiStyle.ConfigureActionButton(_homeCopyLapsButton);
        _homeCopyLapsButton.Enabled = false;

        _homeLapsCard.Name = "HomeLapsCard";
        _homeLapsCard.Text = "LAPS";
        _homeLapsCard.Controls.Add(BuildHomeRecoveryCardBody(
            _homeLapsCardStatus,
            _homeLapsCardAccount,
            "Password: ••••••••••••",
            [_homeRevealLapsButton, _homeCopyLapsButton]));

        foreach (var card in new[] { _homeBitLockerCard, _homeLapsCard })
        {
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Padding = new Padding(UiStyle.ControlGap);
            card.Margin = new Padding(
                UiStyle.ControlGap / 2,
                UiStyle.ControlGap,
                UiStyle.ControlGap / 2,
                UiStyle.ControlGap);
        }

        // Keep both cards in the initial control tree for accessibility and
        // HiDPI self-tests, even though Start has no selected computer yet.
        _homeServiceCards.ColumnCount = 2;
        _homeServiceCards.RowCount = 1;
        _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _homeServiceCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _homeServiceCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _homeServiceCards.Controls.Add(_homeBitLockerCard, 0, 0);
        _homeServiceCards.Controls.Add(_homeLapsCard, 1, 0);

        _homeServiceCards.SizeChanged += (_, _) => ArrangeHomeServiceCards();
        _homeRevealLapsButton.Click +=
            async (_, _) => await ShowHomeLapsPasswordAsync(copy: false);
        _homeCopyLapsButton.Click +=
            async (_, _) => await ShowHomeLapsPasswordAsync(copy: true);

        return _homeServiceCards;
    }

    private static Control BuildHomeRecoveryCardBody(
        Label status,
        Label details,
        string maskedSecretLabel,
        Control[] actions)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(UiStyle.ControlGap)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 4; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        foreach (var label in new[] { status, details })
        {
            label.AutoSize = true;
            label.AutoEllipsis = false;
            label.Dock = DockStyle.Top;
            label.MaximumSize = new Size(UiStyle.HomeRecoveryCardMaxTextWidth, 0);
            label.Margin = new Padding(0, 0, 0, UiStyle.ControlGap);
            label.UseMnemonic = false;
        }
        status.Font = UiStyle.CreateEmphasisFont();
        layout.Controls.Add(status, 0, 0);
        layout.Controls.Add(details, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = maskedSecretLabel,
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Top,
            MaximumSize = new Size(UiStyle.HomeRecoveryCardMaxTextWidth, 0),
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap),
            UseMnemonic = false
        }, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        buttons.Controls.AddRange(actions);
        layout.Controls.Add(buttons, 0, 3);
        return layout;
    }

    private void UpdateHomeServiceCards(HomeSearchResult? selected)
    {
        bool showBitLocker = selected is not null && !selected.IsCloudRecord &&
            (selected.BitLockerKeyCount > 0 || selected.BitLockerLookupFailed);
        bool showLaps = selected is not null && !selected.IsCloudRecord &&
            (selected.LapsCandidateFound || selected.LapsLookupFailed);

        // Incomplete/missing metadata cannot prove that a recovery backup
        // is absent. Give the operator both manual read/diagnostic options.
        if (selected is { IsCloudRecord: false } && !showBitLocker && !showLaps)
            showBitLocker = showLaps = true;

        _homeBitLockerCardStatus.Text = selected is null
            ? "Status: Not selected"
            : "Status: " + selected.BitLockerStatus +
              " | Recovery entries: " + selected.BitLockerKeyCount;
        _homeBitLockerCardKeyId.Text = selected is null ||
            string.IsNullOrWhiteSpace(selected.LatestRecoveryId)
            ? "Recovery Key ID: Not verified"
            : "Recovery Key ID: " + selected.LatestRecoveryId;

        _homeLapsCardStatus.Text = selected is null
            ? "Status: Not selected"
            : "Status: " + selected.LapsStatus;
        _homeLapsCardAccount.Text = "Account: Not loaded (read on demand)";

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

            // When no computer is selected, keep both hidden cards parented
            // to the control tree for UI self-testing.
            bool initial = !_homeCardsHasBitLocker && !_homeCardsHasLaps;
            if (both && !stacked || initial)
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

    private async Task ShowHomeLapsPasswordAsync(bool copy)
    {
        var selected = GetSelectedHomeSearchResult();
        if (selected is null || selected.IdentityAmbiguous ||
            selected.IsCloudRecord || !_directoryConnected ||
            _directoryConnecting || _homeLapsBusy || _lapsReading)
            return;

        var computer = selected.ComputerName;
        _homeLapsBusy = true;
        UpdateHomeServiceTabs(selected);
        UiStyle.SetStatus(_homeSearchStatus,
            "Reading LAPS for " + computer + " (password requested by operator)...",
            UiStatusKind.Busy);
        try
        {
            // Reuse the established LAPS read, privileged authorization,
            // audit and DPAPI-NG decryption code without navigating away.
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

            await ReadLapsAsync();

            // A different selected computer must never receive the old
            // computer password after an asynchronous LDAP operation.
            if (!ReferenceEquals(selected, GetSelectedHomeSearchResult()))
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Computer selection changed; the LAPS result was discarded.",
                    UiStatusKind.Warning);
                return;
            }

            var result = _lapsResult;
            if (result is null ||
                !string.Equals(result.ComputerName, computer,
                    StringComparison.OrdinalIgnoreCase))
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "LAPS read did not complete. Open LAPS details for diagnostics.",
                    UiStatusKind.Warning);
                return;
            }

            var current = result.OrderedEntries.Where(row =>
                !row.IsHistory &&
                row.HasPassword &&
                row.Status == LapsPasswordStatus.Available).ToList();

            if (current.Count == 0)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    result.Entries.Count == 0
                        ? LapsReadDiagnostics.EmptyStatus(result, cloud: false)
                        : LapsReadDiagnostics.UnreadableStatus(result, cloud: false),
                    UiStatusKind.Warning);
                return;
            }

            if (current.Count > 1)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Multiple current LAPS passwords are readable. Open LAPS details " +
                    "to choose the exact account and password attribute.",
                    UiStatusKind.Warning);
                return;
            }

            var row = current[0];
            var action = copy ? "CopyLapsPassword" : "RevealLapsPassword";
            var context = AuthorizeLapsAccess(
                action, result.ComputerName, _lapsResourceId, _lapsAuditSource);
            if (context is null)
                return;

            WriteRecoveryAudit(action, context,
                computerName: result.ComputerName,
                recoveryId: _lapsResourceId,
                source: _lapsAuditSource,
                details: "Attribute=" + row.Attribute +
                    "; Account=" + row.AccountName +
                    "; History=False; Updated=" +
                    row.UpdatedAtUtc?.ToString("O"));

            RecordRecentComputer(result.ComputerName, result.ComputerId,
                copy ? "LAPS copy" : "LAPS reveal");

            var password = row.CopyPassword();
            if (copy)
            {
                // Clear the LAPS result BEFORE copying. ClearLapsResult
                // deliberately clears a clipboard matching any stored secret.
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
                    "LAPS Password",
                    result.ComputerName + " | " + row.AccountName +
                        " | " + row.Source,
                    password,
                    "This password was read on demand. The display is closed " +
                    "and its in-memory content is cleared when you close this window.",
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
            if (_lapsResult is not null)
                ClearLapsResult();
            _homeLapsBusy = false;
            UpdateHomeServiceTabs(GetSelectedHomeSearchResult());
        }
    }
}
