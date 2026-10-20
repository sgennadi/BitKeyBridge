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

        // Honor the selected search source. An old AD session must not
        // override a new cloud-only choice, and vice versa.
        if (!StartConnectionPolicy.NeedsActiveDirectory(SelectedStartSource) ||
            !_directoryConnected)
        {
            if (StartConnectionPolicy.NeedsMicrosoftCloud(SelectedStartSource) &&
                HasCloudSearchSession())
            {
                await SearchHomeCloudOnlyAsync(query);
                return;
            }

            UiStyle.SetStatus(
                _homeSearchStatus,
                SelectedStartSource == StartConnectionPolicy.Choose
                    ? "Choose Active Directory, Microsoft Entra / Intune, or both above."
                    : "Connect to the selected source above. For Microsoft 365, use " +
                      "Connect to Microsoft 365 and follow Device Code / MFA.",
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
                BitKeyBridgePermission.SearchDevices,
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

            // Keep successful results when one metadata source fails.
            var recoveryTask =
                MetadataSourceIsolation.ExecuteAsync<RecoverySearchResult>(
                    () => SearchLiveAdRecoveryMetadataAsync(
                        query, scope, cancellation.Token),
                    cancellation.Token);
            var lapsTask =
                MetadataSourceIsolation.ExecuteAsync<LapsSearchResult>(
                    () => SearchHomeLapsMetadataAsync(
                        query, cancellation.Token),
                    cancellation.Token);

            await Task.WhenAll(recoveryTask, lapsTask);

            var recoveryOutcome = await recoveryTask;
            var lapsOutcome = await lapsTask;
            var recoveryRows = recoveryOutcome.Rows;
            var lapsRows = lapsOutcome.Rows;
            string? recoveryFailure = recoveryOutcome.Error;
            string? lapsFailure = lapsOutcome.Error;

            cancellation.Token
                .ThrowIfCancellationRequested();

            // Reconcile each computer separately. A remembered OU may
            // exclude BitLocker objects while domain-wide LAPS still sees it.
            var recoveryNames = recoveryRows
                .Select(row => row.ComputerName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            if (lapsFailure is null)
            {
                foreach (var computer in recoveryNames)
                {
                    if (lapsRows.Any(row => row.ComputerName.Equals(
                            computer, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var extra = await MetadataSourceIsolation.ExecuteAsync<LapsSearchResult>(
                        () => SearchHomeLapsMetadataAsync(
                            computer, cancellation.Token),
                        cancellation.Token);
                    if (!extra.Succeeded)
                    {
                        lapsFailure = extra.Error;
                        break;
                    }

                    lapsRows.AddRange(extra.Rows.Where(row =>
                        row.ComputerName.Equals(
                            computer, StringComparison.OrdinalIgnoreCase)));
                }
            }

            var domainRecoveryNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lapsNames = lapsRows
                .Select(row => row.ComputerName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            if (recoveryFailure is null)
            {
                foreach (var computer in lapsNames)
                {
                    if (recoveryRows.Any(row => row.ComputerName.Equals(
                            computer, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var extra = await MetadataSourceIsolation.ExecuteAsync<RecoverySearchResult>(
                        () => SearchLiveAdRecoveryMetadataAsync(
                            computer, scope, cancellation.Token),
                        cancellation.Token);
                    if (!extra.Succeeded)
                    {
                        recoveryFailure = extra.Error;
                        break;
                    }

                    var extraRows = extra.Rows;
                    if (HelpdeskStatus.ShouldRetryBitLockerInDomain(
                        extraRows.Count, scope.SearchBase, _startDomainDn))
                    {
                        // Retry without changing the operator's current scope.
                        var domainScope = new BitLockerScope(
                            "Entire domain", _startDomainDn);
                        var retried = await MetadataSourceIsolation.ExecuteAsync<RecoverySearchResult>(
                            () => SearchLiveAdRecoveryMetadataAsync(
                                computer, domainScope, cancellation.Token),
                            cancellation.Token);
                        if (!retried.Succeeded)
                        {
                            recoveryFailure = retried.Error;
                            break;
                        }

                        extraRows = retried.Rows;
                        if (extraRows.Any(row => row.ComputerName.Equals(
                                computer, StringComparison.OrdinalIgnoreCase)))
                        {
                            domainRecoveryNames.Add(computer);
                        }
                    }

                    recoveryRows.AddRange(extraRows.Where(row =>
                        row.ComputerName.Equals(
                            computer, StringComparison.OrdinalIgnoreCase)));
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

            var ambiguousNames = names
                .Where(name => HelpdeskStatus.HasIdentityCollision(
                    recoveryRows, lapsRows, name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _updatingHomeSearchResults = true;

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

                var exactComputerDn = bitLocker
                    .Select(item => item.ComputerDistinguishedName)
                    .Append(laps?.ComputerDistinguishedName ?? string.Empty)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? string.Empty;

                var result =
                    new HomeSearchResult
                    {
                        ComputerName =
                            computer,
                        ComputerId =
                            laps?.ComputerId ??
                            string.Empty,
                        AdComputerDistinguishedName = exactComputerDn,
                        SourceIdentity = string.IsNullOrWhiteSpace(exactComputerDn)
                            ? string.Empty : "AD:DN:" + exactComputerDn,
                        BitLockerKeyCount =
                            bitLocker.Count,
                        BitLockerLookupFailed =
                            recoveryFailure is not null,
                        LapsLookupFailed =
                            lapsFailure is not null,
                        IdentityAmbiguous =
                            ambiguousNames.Contains(computer),
                        BitLockerOutsideSelectedScope =
                            domainRecoveryNames.Contains(computer),
                        LatestBitLockerKeyUtc =
                            latestBitLocker?.KeyDate,
                        LatestRecoveryId =
                            latestBitLocker?.RecoveryId ??
                            string.Empty,
                        LapsCandidateFound =
                            laps is not null,
                        LapsKeyDateUtc =
                            laps?.KeyDateUtc,
                        LapsExpiresAtUtc =
                            laps?.ExpiresAtUtc,
                        BitLockerStatus =
                            ambiguousNames.Contains(computer)
                                ? "Ambiguous"
                                : recoveryFailure is not null
                                    ? "Lookup failed"
                                    : bitLocker.Count == 0
                                        ? "Not verified"
                                        : HelpdeskStatus.BitLocker(
                                            bitLocker,
                                            _config.CoverageOldCloudKeyDays),
                        LapsStatus =
                            ambiguousNames.Contains(computer)
                                ? "Ambiguous"
                                : lapsFailure is not null
                                    ? "Lookup failed"
                                    : HelpdeskStatus.Laps(laps),
                        SourceSummary =
                            ambiguousNames.Contains(computer)
                                ? "AD (ambiguous)"
                                : domainRecoveryNames.Contains(computer)
                                    ? "AD (domain retry)"
                                    : "AD"
                    };

                var rowIndex =
                    _homeSearchResults.Rows.Add(
                        result.ComputerName,
                        result.SourceSummary,
                        result.BitLockerStatus,
                        result.LatestBitLockerKeyUtc,
                        result.LapsStatus,
                        result.LapsKeyDateUtc ??
                        result.LapsExpiresAtUtc);

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

            _updatingHomeSearchResults = false;

            HomeCloudLookupStatus? cloudOutcome = null;
            // Live keystrokes stay AD-only to avoid unbounded Graph requests.
            // Search/Enter queries all authenticated sources.
            if (immediate && HasCloudSearchSession())
            {
                cloudOutcome = await AppendHomeCloudRowsAsync(
                    query, generation, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (generation != _homeSearchGeneration || IsDisposed)
                    return;
            }

            UpdateHomeSearchSelection();

            var failedSources = new List<string>();
            var diagnosticDetails = new List<string>();
            if (recoveryFailure is not null)
            {
                failedSources.Add("BitLocker");
                diagnosticDetails.Add("BitLocker metadata: " + recoveryFailure);
            }
            if (lapsFailure is not null)
            {
                failedSources.Add("LAPS");
                diagnosticDetails.Add("LAPS metadata: " + lapsFailure);
            }

            if (cloudOutcome is { Failure: not null })
            {
                failedSources.Add("Cloud");
                diagnosticDetails.Add("Graph metadata: " + cloudOutcome.Failure);
            }
            else if (cloudOutcome is { IsPartial: true })
            {
                failedSources.Add("Cloud (partial)");
                diagnosticDetails.Add(cloudOutcome.SourceDiagnostics);
            }

            if (failedSources.Count > 0)
            {
                _appDiagnostics.ShowMessage(
                    "Start metadata search was incomplete.",
                    string.Join(Environment.NewLine, diagnosticDetails) +
                    Environment.NewLine +
                    "An LDAP error is not proof that a backup is absent.");
            }

            var statusText = failedSources.Count > 0
                ? $"Partial search: {string.Join(" and ", failedSources)} lookup failed. " +
                  $"{names.Count} result(s) found; see diagnostics."
                : ambiguousNames.Count > 0
                    ? $"{names.Count} result(s); {ambiguousNames.Count} ambiguous computer name(s). " +
                      "Use an exact AD object GUID or OU in LAPS/Recovery."
                    : names.Count == 0
                        ? "No matching computer, Recovery ID or device ID was found."
                        : names.Count == 1
                            ? "One computer found. BitLocker and LAPS actions are below."
                            : $"Found {names.Count} computers. Select one to view its recovery cards.";

            if (cloudOutcome is not null)
            {
                statusText += $" | Entra/Intune: {cloudOutcome.RecordsAdded} record(s). " +
                    "Select a cloud record to open Device 360.";
            }

            UiStyle.SetStatus(
                _homeSearchStatus,
                statusText,
                failedSources.Count > 0 || ambiguousNames.Count > 0 ||
                    _homeSearchResults.Rows.Count == 0
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
            _updatingHomeSearchResults = false;

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
                    _directoryConnected || HasCloudSearchSession();
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

        if (!_updatingHomeSearchResults &&
            selected is not null)
        {
            RememberComputerQuery(selected.ComputerName);
        }

        var connected = _directoryConnected && !_directoryConnecting;

        _homeRecoveryStepsButton.Enabled = selected is not null;

        _homeRevealBitLockerButton.Enabled =
            connected &&
            selected is not null &&
            selected.BitLockerKeyCount > 0 &&
            !selected.IdentityAmbiguous &&
            !selected.IsCloudRecord;

        _homeCopyBitLockerButton.Enabled =
            connected &&
            selected is not null &&
            selected.BitLockerKeyCount > 0 &&
            !selected.IdentityAmbiguous &&
            !selected.IsCloudRecord;

        // Opens the real BitLocker diagnostic-command dialog in-place.
        _homeBitLockerDiagnosticLink.Enabled = selected is not null;

        _homeSelectionPanel.Visible =
            selected is not null ||
            _secretVisibleUntilUtc is not null ||
            _clipboardClearAtUtc is not null;

        UpdateHomeServiceCards(selected);
        RefreshHomeSelectionSummary();
        // Helpdesk reflects this exact selected record, without a duplicate
        // query, background lookup or new credential request.
        UpdateHelpdeskSelectedDeviceSummary();
        // Selection changes the Start split layout immediately. A selected
        // computer must never be hidden below an oversized empty result grid.
        QueueMainViewportReflow();
    }

    private async void
        OpenSelectedHomeDefaultAction()
    {
        var selected = GetSelectedHomeSearchResult();
        if (selected?.IdentityAmbiguous == true)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Multiple AD objects share this name. Use an exact object GUID in LAPS or an exact computer/OU in Recovery.",
                UiStatusKind.Warning);
            return;
        }

        // Double-clicking a computer stays on Start, with both available
        // service cards shown below. It does not request any password.
        if (selected is not null && !selected.IsCloudRecord)
        {
            UpdateHomeServiceCards(selected);
            if (_homeServiceCards.Visible)
            {
                _homeServiceCards.Focus();
                UiStyle.SetStatus(_homeSearchStatus,
                    "Computer selected. BitLocker and LAPS actions are shown below. " +
                    "Click Reveal or Copy to request a password.",
                    UiStatusKind.Neutral);
                return;
            }
        }

        switch (HelpdeskStatus.ResolveHomeDefaultAction(selected))
        {
            case HomeDefaultAction.None:
                return;
            case HomeDefaultAction.BitLocker:
                // BitLocker takes priority when both backups are detected.
                await OpenSelectedHomeBitLockerAsync();
                return;
            case HomeDefaultAction.Laps:
                // A LAPS-only computer opens LAPS when metadata confirms it.
                await OpenSelectedHomeLapsAsync();
                return;
            case HomeDefaultAction.Devices:
                // Requires exact source-qualified selection before cloud operations.
                OpenHomeDeviceDetails();
                await SearchUnifiedDevicesAsync();
                return;
            case HomeDefaultAction.ChooseRecoveryType:
                // Missing metadata is not proof that BitLocker is absent.
                UiStyle.SetStatus(
                    _homeSearchStatus,
                    $"BitLocker for {selected!.ComputerName} is not verified. " +
                    "Use the BitLocker and LAPS recovery cards to retry or view diagnostics.",
                    UiStatusKind.Warning);
                return;
        }
    }

    private async Task
        OpenSelectedHomeBitLockerAsync()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
        {
            NavigateToRecoveryWorkspace(fromStart: true);
            return;
        }

        if (selected.IdentityAmbiguous)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Ambiguous computer name. Select an exact AD computer in the Recovery workspace.",
                UiStatusKind.Warning);
            return;
        }

        RememberComputerQuery(selected.ComputerName);

        RecordRecentComputer(
            selected,
            "BitLocker");

        if (HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, _startScope?.SearchBase, _startDomainDn))
        {
            // The Recovery view must use the same scope that found
            // the BitLocker metadata on Start, otherwise it vanishes.
            SetRecoveryScope(
                new BitLockerScope("Entire domain", _startDomainDn),
                $"Searching BitLocker across the domain for {selected.ComputerName} " +
                "(outside the previously selected OU or missing metadata).",
                UiStatusKind.Warning);
        }

        NavigateToRecoveryWorkspace(fromStart: true);
        _startSearchDebounceTimer?.Stop();

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
            NavigateToLapsWorkspace(fromStart: true);
            return;
        }

        if (selected.IdentityAmbiguous)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Ambiguous computer name. Use the exact AD object GUID in the LAPS workspace.",
                UiStatusKind.Warning);
            return;
        }

        RememberComputerQuery(selected.ComputerName);

        RecordRecentComputer(
            selected,
            "LAPS");

        NavigateToLapsWorkspace(fromStart: true);
        _lapsSearchDebounceTimer?.Stop();

        // The detailed LAPS page also requires an explicit Read action.
        // Navigation alone never fetches a password.
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
        RecordRecentComputer(
            selected.ComputerName,
            selected.ComputerId,
            action);
    }

    private void RecordRecentComputer(
        string computerName,
        string computerId,
        string action)
    {
        if (string.IsNullOrWhiteSpace(
                computerName))
        {
            return;
        }

        try
        {
            var rows =
                JsonStore.Read<List<RecentComputerEntry>>(
                    AppPaths.RecentComputersFile) ??
                [];

            rows.RemoveAll(
                row =>
                    row.ComputerName.Equals(
                        computerName,
                        StringComparison.OrdinalIgnoreCase));

            rows.Insert(
                0,
                new RecentComputerEntry
                {
                    ComputerName =
                        computerName,
                    ComputerId =
                        computerId,
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
