namespace BitKeyBridge;

public sealed partial class MainForm
{
    private void QueueHomeSearch()
    {
        if (_layoutSelfTest)
            return;

        _homeSearchDebounceTimer?.Stop();
        _homeSearchGeneration++;

        var query =
            _homeQuery.Text.Trim();

        if (query.Length <
            SearchText.MinimumLiveSearchCharacters ||
            !_directoryConnected)
        {
            return;
        }

        _homeSearchDebounceTimer?.Start();
    }

    private void CancelHomeSearch()
    {
        _homeSearchDebounceTimer?.Stop();
        _homeSearchCancellation?.Cancel();
    }

    private async Task SearchHomeAsync(
        bool immediate)
    {
        _homeSearchDebounceTimer?.Stop();
        _homeSearchCancellation?.Cancel();

        var query =
            _homeQuery.Text.Trim();

        if (query.Length <
            SearchText.MinimumLiveSearchCharacters)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Enter at least two characters.",
                UiStatusKind.Warning);
            return;
        }

        if (!_directoryConnected)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Connect to Active Directory before using the unified search.",
                UiStatusKind.Warning);
            return;
        }

        if (_startScope is null &&
            !TryUseDefaultRecoveryScope(
                "Using the entire domain for Start search."))
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "No Active Directory search scope is available.",
                UiStatusKind.Error);
            return;
        }

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                "StartUnifiedSearch",
                source: "AD"))
        {
            return;
        }

        using var cancellation =
            new CancellationTokenSource();

        var generation =
            ++_homeSearchGeneration;

        _homeSearchCancellation =
            cancellation;
        _homeSearchButton.Enabled =
            false;
        _homeSearchCancelButton.Enabled =
            true;
        _homeSearchProgress.Visible =
            true;
        _homeSearchResults.Rows.Clear();

        UiStyle.SetStatus(
            _homeSearchStatus,
            immediate
                ? "Searching BitLocker and LAPS metadata..."
                : "Live search: checking BitLocker and LAPS metadata...",
            UiStatusKind.Busy);

        try
        {
            var scope =
                _startScope!;

            var recoveryTask =
                SearchLiveAdRecoveryMetadataAsync(
                    query,
                    scope,
                    cancellation.Token);

            var lapsTask =
                SearchHomeLapsMetadataAsync(
                    query,
                    cancellation.Token);

            await Task.WhenAll(
                recoveryTask,
                lapsTask);

            var recoveryRows =
                await recoveryTask;
            var lapsRows =
                await lapsTask;

            cancellation.Token
                .ThrowIfCancellationRequested();

            if (recoveryRows.Count > 0 &&
                lapsRows.Count == 0)
            {
                foreach (var computer in
                         recoveryRows
                             .Select(
                                 row =>
                                     row.ComputerName)
                             .Where(
                                 name =>
                                     !string.IsNullOrWhiteSpace(
                                         name))
                             .Distinct(
                                 StringComparer.OrdinalIgnoreCase)
                             .Take(10))
                {
                    var extra =
                        await SearchHomeLapsMetadataAsync(
                            computer,
                            cancellation.Token);

                    lapsRows.AddRange(
                        extra.Where(
                            row =>
                                row.ComputerName.Equals(
                                    computer,
                                    StringComparison.OrdinalIgnoreCase)));
                }
            }
            else if (lapsRows.Count > 0 &&
                     recoveryRows.Count == 0)
            {
                foreach (var computer in
                         lapsRows
                             .Select(
                                 row =>
                                     row.ComputerName)
                             .Where(
                                 name =>
                                     !string.IsNullOrWhiteSpace(
                                         name))
                             .Distinct(
                                 StringComparer.OrdinalIgnoreCase)
                             .Take(10))
                {
                    var extra =
                        await SearchLiveAdRecoveryMetadataAsync(
                            computer,
                            scope,
                            cancellation.Token);

                    recoveryRows.AddRange(
                        extra.Where(
                            row =>
                                row.ComputerName.Equals(
                                    computer,
                                    StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (generation !=
                    _homeSearchGeneration ||
                IsDisposed)
            {
                return;
            }

            var names =
                recoveryRows
                    .Select(
                        row =>
                            row.ComputerName)
                    .Concat(
                        lapsRows.Select(
                            row =>
                                row.ComputerName))
                    .Where(
                        name =>
                            !string.IsNullOrWhiteSpace(
                                name))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        name =>
                            name,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(100)
                    .ToList();

            foreach (var computer in names)
            {
                var bitLocker =
                    recoveryRows
                        .Where(
                            row =>
                                row.ComputerName.Equals(
                                    computer,
                                    StringComparison.OrdinalIgnoreCase))
                        .GroupBy(
                            row =>
                                row.RecoveryId,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(
                            group =>
                                group.First())
                        .OrderByDescending(
                            row =>
                                row.KeyDate)
                        .ToList();

                var latestBitLocker =
                    bitLocker
                        .FirstOrDefault(
                            row =>
                                row.IsLatest == true) ??
                    bitLocker.FirstOrDefault();

                var laps =
                    lapsRows
                        .Where(
                            row =>
                                row.ComputerName.Equals(
                                    computer,
                                    StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(
                            row =>
                                row.KeyDateUtc)
                        .FirstOrDefault();

                var result =
                    new HomeSearchResult
                    {
                        ComputerName =
                            computer,
                        ComputerId =
                            laps?.ComputerId ??
                            string.Empty,
                        BitLockerKeyCount =
                            bitLocker.Count,
                        LatestBitLockerKeyUtc =
                            latestBitLocker?.KeyDate,
                        LatestRecoveryId =
                            latestBitLocker?.RecoveryId ??
                            string.Empty,
                        LapsDetected =
                            HelpdeskStatus.Laps(
                                laps) !=
                            "No backup",
                        LapsKeyDateUtc =
                            laps?.KeyDateUtc,
                        LapsExpiresAtUtc =
                            laps?.ExpiresAtUtc,
                        BitLockerStatus =
                            HelpdeskStatus.BitLocker(
                                bitLocker,
                                _config.CoverageOldCloudKeyDays),
                        LapsStatus =
                            HelpdeskStatus.Laps(
                                laps)
                    };

                var rowIndex =
                    _homeSearchResults.Rows.Add(
                        result.ComputerName,
                        result.BitLockerStatus,
                        result.LatestBitLockerKeyUtc,
                        result.LapsStatus,
                        result.LapsKeyDateUtc);

                _homeSearchResults.Rows[rowIndex].Tag =
                    result;
            }

            _homeSearchResults.ClearSelection();
            _homeSearchResults.CurrentCell =
                null;

            if (_homeSearchResults.Rows.Count ==
                1)
            {
                _homeSearchResults.Rows[0].Selected =
                    true;
                _homeSearchResults.CurrentCell =
                    _homeSearchResults.Rows[0].Cells[0];
            }

            UpdateHomeSearchSelection();

            UiStyle.SetStatus(
                _homeSearchStatus,
                names.Count == 0
                    ? "No matching computer, Recovery ID or device ID was found."
                    : names.Count == 1
                        ? "One computer found. Choose BitLocker or LAPS."
                        : $"Found {names.Count} computers. Select one, then choose BitLocker or LAPS.",
                names.Count == 0
                    ? UiStatusKind.Warning
                    : UiStatusKind.Success);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed &&
                generation ==
                    _homeSearchGeneration)
            {
                UiStyle.SetStatus(
                    _homeSearchStatus,
                    "Search canceled.",
                    UiStatusKind.Warning);
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed ||
                generation !=
                    _homeSearchGeneration)
            {
                return;
            }

            UiStyle.SetStatus(
                _homeSearchStatus,
                "Unified search failed. Diagnostics are available.",
                UiStatusKind.Error);

            _appDiagnostics.ShowError(
                "Start unified search failed.",
                "StartUnifiedSearch",
                ex,
                ("Query", query));
        }
        finally
        {
            if (ReferenceEquals(
                    _homeSearchCancellation,
                    cancellation))
            {
                _homeSearchCancellation =
                    null;
            }

            if (!IsDisposed &&
                generation ==
                    _homeSearchGeneration)
            {
                _homeSearchButton.Enabled =
                    _directoryConnected;
                _homeSearchCancelButton.Enabled =
                    false;
                _homeSearchProgress.Visible =
                    false;
            }
        }
    }

    private Task<List<LapsSearchResult>>
        SearchHomeLapsMetadataAsync(
            string query,
            CancellationToken ct)
    {
        var snapshot =
            new AppConfig
            {
                AdConnectionMode =
                    _config.AdConnectionMode,
                AdDomain =
                    _config.AdDomain,
                AdServer =
                    _config.AdServer,
                AdPort =
                    _config.AdPort,
                AdUseLdaps =
                    _config.AdUseLdaps
            };

        var credential =
            AdSessionCredentials
                .CreateNetworkCredential(
                    _config);

        return Task.Run(
            () =>
            {
                var service =
                    new LapsDirectoryService(
                        snapshot,
                        credential);

                return service.SearchMetadata(
                    query,
                    100,
                    ct);
            },
            ct);
    }

    private HomeSearchResult?
        GetSelectedHomeSearchResult()
    {
        if (_homeSearchResults.SelectedRows.Count ==
            0)
        {
            return null;
        }

        return _homeSearchResults
            .SelectedRows[0]
            .Tag as HomeSearchResult;
    }

    private void UpdateHomeSearchSelection()
    {
        var selected =
            GetSelectedHomeSearchResult();

        _homeBitLockerButton.Text =
            selected is null
                ? "BitLocker Recovery"
                : $"BitLocker Recovery — {selected.ComputerName}";

        _homeLapsButton.Text =
            selected is null
                ? "LAPS Passwords"
                : $"LAPS Passwords — {selected.ComputerName}";

        var connected =
            _directoryConnected &&
            !_directoryConnecting;

        _homeBitLockerButton.Enabled =
            connected;
        _homeLapsButton.Enabled =
            connected;
    }

    private async void
        OpenSelectedHomeDefaultAction()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
            return;

        var query =
            _homeQuery.Text.Trim();

        if (SearchText.LooksLikeIdentifierFragment(
                query) &&
            selected.BitLockerKeyCount > 0)
        {
            await OpenSelectedHomeBitLockerAsync();
            return;
        }

        if (selected.BitLockerKeyCount > 0 &&
            !selected.LapsDetected)
        {
            await OpenSelectedHomeBitLockerAsync();
            return;
        }

        if (selected.LapsDetected &&
            selected.BitLockerKeyCount == 0)
        {
            await OpenSelectedHomeLapsAsync();
            return;
        }

        UiStyle.SetStatus(
            _homeSearchStatus,
            $"Both BitLocker and LAPS are available for {selected.ComputerName}. Choose the required action.",
            UiStatusKind.Success);
    }

    private async Task
        OpenSelectedHomeBitLockerAsync()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
        {
            NavigateToRecoveryWorkspace();
            return;
        }

        RecordRecentComputer(
            selected,
            "BitLocker");

        NavigateToRecoveryWorkspace();
        _startQuery.Text =
            selected.ComputerName;

        await SearchStartRecoveryAsync();
    }

    private void OpenSelectedHomeBitLocker()
    {
        _ =
            OpenSelectedHomeBitLockerAsync();
    }

    private async Task
        OpenSelectedHomeLapsAsync()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
        {
            NavigateToLapsWorkspace();
            return;
        }

        RecordRecentComputer(
            selected,
            "LAPS");

        NavigateToLapsWorkspace();

        _suppressLapsSearchQueue =
            true;
        try
        {
            _lapsQuery.Text =
                selected.ComputerName;
        }
        finally
        {
            _suppressLapsSearchQueue =
                false;
        }

        await ReadLapsAsync();
    }

    private async Task
        SearchRecentComputerAsync()
    {
        if (_homeRecentResults.SelectedRows.Count ==
            0)
        {
            return;
        }

        if (_homeRecentResults
                .SelectedRows[0]
                .Tag is not
            RecentComputerEntry recent)
        {
            return;
        }

        _homeQuery.Text =
            recent.ComputerName;

        await SearchHomeAsync(
            immediate: true);
    }

    private void LoadRecentComputers()
    {
        _homeRecentResults.Rows.Clear();

        try
        {
            var rows =
                JsonStore.Read<List<RecentComputerEntry>>(
                    AppPaths.RecentComputersFile) ??
                [];

            foreach (var recent in
                     rows
                         .Where(
                             row =>
                                 !string.IsNullOrWhiteSpace(
                                     row.ComputerName))
                         .OrderByDescending(
                             row =>
                                 row.LastUsedUtc)
                         .Take(15))
            {
                var index =
                    _homeRecentResults.Rows.Add(
                        recent.ComputerName,
                        recent.LastAction,
                        recent.LastUsedUtc);

                _homeRecentResults.Rows[index].Tag =
                    recent;
            }

            _homeRecentResults.ClearSelection();
            _homeRecentResults.CurrentCell =
                null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Recent computer list could not be loaded: " +
                DiagnosticRedaction.Sanitize(
                    ex.Message));
        }
    }

    private void RecordRecentComputer(
        HomeSearchResult selected,
        string action)
    {
        try
        {
            var rows =
                JsonStore.Read<List<RecentComputerEntry>>(
                    AppPaths.RecentComputersFile) ??
                [];

            rows.RemoveAll(
                row =>
                    row.ComputerName.Equals(
                        selected.ComputerName,
                        StringComparison.OrdinalIgnoreCase));

            rows.Insert(
                0,
                new RecentComputerEntry
                {
                    ComputerName =
                        selected.ComputerName,
                    ComputerId =
                        selected.ComputerId,
                    LastAction =
                        action,
                    LastUsedUtc =
                        DateTime.UtcNow
                });

            JsonStore.WriteAtomic(
                AppPaths.RecentComputersFile,
                rows.Take(15).ToList());

            LoadRecentComputers();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Recent computer list could not be saved: " +
                DiagnosticRedaction.Sanitize(
                    ex.Message));
        }
    }
}
