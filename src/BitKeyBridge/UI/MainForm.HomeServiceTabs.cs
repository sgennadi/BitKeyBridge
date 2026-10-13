namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Start is the shared computer workspace. Switching tabs never reads secrets.
    private readonly TabControl _homeServiceTabs = new();
    private readonly TabPage _homeBitLockerTab = new("BitLocker");
    private readonly TabPage _homeLapsTab = new("LAPS");
    private readonly Label _homeBitLockerTabSummary = new();
    private readonly Label _homeLapsTabSummary = new();
    private readonly Button _homeRevealLapsButton = new();
    private readonly Button _homeCopyLapsButton = new();
    private bool _homeLapsBusy;
    private string _homeServiceTabIdentity = string.Empty;

    private Control BuildHomeServiceTabs()
    {
        _homeServiceTabs.Name = "HomeServiceTabs";
        _homeServiceTabs.AccessibleName = "Selected computer BitLocker and LAPS";
        _homeServiceTabs.Dock = DockStyle.Top;
        _homeServiceTabs.Height = UiStyle.HomeServiceTabsHeight;
        _homeServiceTabs.MinimumSize = new Size(0, UiStyle.HomeServiceTabsHeight);
        _homeServiceTabs.Visible = false;

        _homeBitLockerTab.Name = "HomeBitLockerTab";
        _homeLapsTab.Name = "HomeLapsTab";
        _homeBitLockerTab.AutoScroll = true;
        _homeLapsTab.AutoScroll = true;

        _homeBitLockerTab.Controls.Add(BuildHomeServicePage(
            _homeBitLockerTabSummary,
            [
                _homeRevealBitLockerButton,
                _homeCopyBitLockerButton,
                _homeBitLockerButton,
                _homeBitLockerDiagnosticButton,
                _homeRotateBitLockerButton
            ]));

        _homeRevealLapsButton.Name = "HomeRevealLapsButton";
        _homeRevealLapsButton.Text = "Reveal LAPS Password";
        _homeRevealLapsButton.AccessibleName = "Read and reveal the selected computer LAPS password";
        UiStyle.ConfigureActionButton(_homeRevealLapsButton);
        _homeRevealLapsButton.Enabled = false;

        _homeCopyLapsButton.Name = "HomeCopyLapsButton";
        _homeCopyLapsButton.Text = "Copy LAPS Password";
        _homeCopyLapsButton.AccessibleName = "Read and copy the selected computer LAPS password";
        UiStyle.ConfigureActionButton(_homeCopyLapsButton);
        _homeCopyLapsButton.Enabled = false;

        _homeLapsTab.Controls.Add(BuildHomeServicePage(
            _homeLapsTabSummary,
            [
                _homeRevealLapsButton,
                _homeCopyLapsButton,
                _homeLapsButton
            ]));

        _homeServiceTabs.TabPages.Add(_homeBitLockerTab);
        _homeServiceTabs.TabPages.Add(_homeLapsTab);
        _homeRevealLapsButton.Click += async (_, _) => await ShowHomeLapsPasswordAsync(copy: false);
        _homeCopyLapsButton.Click += async (_, _) => await ShowHomeLapsPasswordAsync(copy: true);
        return _homeServiceTabs;
    }

    private static Control BuildHomeServicePage(Label summary, Control[] buttons)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(UiStyle.ControlGap),
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        summary.AutoSize = true;
        summary.Dock = DockStyle.Top;
        summary.Font = UiStyle.CreateBodyFont(10F);
        summary.MaximumSize = new Size(1100, 0);
        summary.Margin = new Padding(0, 0, 0, UiStyle.ControlGap);
        layout.Controls.Add(summary, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty
        };
        actions.Controls.AddRange(buttons);
        layout.Controls.Add(actions, 0, 1);
        return layout;
    }

    private void UpdateHomeServiceTabs(HomeSearchResult? selected)
    {
        bool showBitLocker = selected is not null &&
            (selected.BitLockerKeyCount > 0 || selected.BitLockerLookupFailed);
        bool showLaps = selected is not null &&
            (selected.LapsCandidateFound || selected.LapsLookupFailed);

        // Inconclusive metadata must not be mistaken for a missing backup.
        if (selected is not null && !showBitLocker && !showLaps)
            showBitLocker = showLaps = true;

        _homeBitLockerTabSummary.Text = selected is null
            ? "BitLocker recovery key is not loaded."
            : "BitLocker: " + selected.BitLockerStatus +
              " | Recovery records: " + selected.BitLockerKeyCount +
              " | Key not loaded until Reveal or Copy.";
        _homeLapsTabSummary.Text = selected is null
            ? "LAPS password is not loaded."
            : "LAPS: " + selected.LapsStatus +
              " | Password not loaded until Reveal or Copy.";

        var identity = selected is null
            ? string.Empty
            : selected.ComputerName + "|" + selected.ComputerId + "|" +
              showBitLocker + "|" + showLaps + "|" + selected.IsCloudRecord;

        if (!string.Equals(identity, _homeServiceTabIdentity, StringComparison.Ordinal))
        {
            _homeServiceTabIdentity = identity;
            _homeServiceTabs.SuspendLayout();
            try
            {
                _homeServiceTabs.TabPages.Clear();
                if (showBitLocker && selected?.IsCloudRecord != true)
                    _homeServiceTabs.TabPages.Add(_homeBitLockerTab);
                if (showLaps && selected?.IsCloudRecord != true)
                    _homeServiceTabs.TabPages.Add(_homeLapsTab);

                // Alphabetical order, BitLocker selected when both exist.
                if (_homeServiceTabs.TabPages.Contains(_homeBitLockerTab))
                    _homeServiceTabs.SelectedTab = _homeBitLockerTab;
                else if (_homeServiceTabs.TabPages.Contains(_homeLapsTab))
                    _homeServiceTabs.SelectedTab = _homeLapsTab;
            }
            finally
            {
                _homeServiceTabs.ResumeLayout();
            }
        }

        _homeServiceTabs.Visible = _homeServiceTabs.TabPages.Count > 0;
        bool canReadLaps = selected is not null &&
            _directoryConnected && !_directoryConnecting &&
            !selected.IdentityAmbiguous && !selected.IsCloudRecord &&
            !_homeLapsBusy && !_lapsReading;
        _homeRevealLapsButton.Enabled = canReadLaps;
        _homeCopyLapsButton.Enabled = canReadLaps;
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
