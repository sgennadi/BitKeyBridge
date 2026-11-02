namespace BitKeyBridge;

/// <summary>
/// Source Health is metadata-only and session-local. It never creates
/// sign-in prompts, disk caches, recovery reads or authorization bypasses.
/// </summary>
public sealed partial class MainForm
{
    private readonly Button _unifiedRetryButton = new();
    private UnifiedDeviceSearchReport? _unifiedSourceReport;
    private DateTime _unifiedLastSourceRetryUtc = DateTime.MinValue;

    private static string RetrySourceLabel(UnifiedMetadataSource source) =>
        source switch
        {
            UnifiedMetadataSource.Ad => "Active Directory",
            UnifiedMetadataSource.Entra => "Entra",
            UnifiedMetadataSource.Intune => "Intune",
            _ => "Unknown"
        };

    private bool CanRetryUnifiedSource(UnifiedMetadataSource source) =>
        UnifiedSourceRetry.CanRetry(_unifiedSourceReport, source) &&
        (source == UnifiedMetadataSource.Ad
            ? _directoryConnected
            : HasCloudSearchSession());

    private void RefreshUnifiedSourceRetryButton()
    {
        var caption = UiLanguageService.Translate(
            _uiLanguageCode, "DevicesRetryFailedSource");
        if (_unifiedRetryButton.Text != caption)
            _unifiedRetryButton.Text = caption;
        if (_unifiedRetryButton.AccessibleName != caption)
            _unifiedRetryButton.AccessibleName = caption;

        var report = _unifiedSourceReport;
        var failed = report is not null &&
            ((report.AdAttempted && !report.AdSucceeded) ||
             (report.EntraAttempted && !report.EntraSucceeded) ||
             (report.IntuneAttempted && !report.IntuneSucceeded));
        var eligible = report?.SourceState is { } state &&
            string.Equals(state.Query, _unifiedQuery.Text.Trim(),
                StringComparison.Ordinal) &&
            Enum.GetValues<UnifiedMetadataSource>()
                .Any(CanRetryUnifiedSource);
        _unifiedRetryButton.Visible = failed;
        _unifiedRetryButton.Enabled = failed && eligible &&
            _unifiedCancellation is null;
        _unifiedRetryButton.AccessibleDescription = eligible
            ? "Retry one failed metadata source only. No sign-in or secret read."
            : "Inspect source diagnostics; disconnected or denied sources cannot be retried.";
    }

    private void ShowUnifiedSourceRetryMenu()
    {
        RefreshUnifiedSourceRetryButton();
        if (!_unifiedRetryButton.Enabled)
            return;

        var menu = new ContextMenuStrip();
        foreach (var source in Enum.GetValues<UnifiedMetadataSource>())
        {
            if (!CanRetryUnifiedSource(source))
                continue;
            var key = source switch
            {
                UnifiedMetadataSource.Ad => "DevicesRetryAd",
                UnifiedMetadataSource.Entra => "DevicesRetryEntra",
                _ => "DevicesRetryIntune"
            };
            var item = new ToolStripMenuItem(
                UiLanguageService.Translate(_uiLanguageCode, key));
            item.Click += async (_, _) => await RetryUnifiedFailedSourceAsync(source);
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0)
        {
            menu.Dispose();
            return;
        }
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(_unifiedRetryButton,
            new Point(0, _unifiedRetryButton.Height));
    }

    private static bool SameDeviceAfterSourceRetry(
        UnifiedDeviceInfo before, UnifiedDeviceInfo after)
    {
        if (before.IdentityStatus is "Name collision" or "Ambiguous device ID" ||
            after.IdentityStatus is "Name collision" or "Ambiguous device ID" ||
            !Device360Snapshot.FromUnified(before).IdentityVerified ||
            !Device360Snapshot.FromUnified(after).IdentityVerified)
            return false;
        static bool Same(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        return Same(before.IdentityKey, after.IdentityKey) &&
            Same(before.AdObjectGuid, after.AdObjectGuid) &&
            Same(before.AdDistinguishedName, after.AdDistinguishedName) &&
            Same(before.EntraDeviceId, after.EntraDeviceId) &&
            Same(before.ManagedDeviceId, after.ManagedDeviceId);
    }

    private async Task RetryUnifiedFailedSourceAsync(UnifiedMetadataSource source)
    {
        if (_unifiedCancellation is not null ||
            _unifiedSourceReport is not { SourceState: { } } previous ||
            !CanRetryUnifiedSource(source))
        {
            UiStyle.SetStatus(_unifiedStatus,
                UiLanguageService.Translate(
                    _uiLanguageCode, "DevicesRetryUnavailable"),
                UiStatusKind.Warning);
            return;
        }
        var state = previous.SourceState!;
        if (!string.Equals(_unifiedQuery.Text.Trim(), state.Query,
                StringComparison.Ordinal))
        {
            UiStyle.SetStatus(_unifiedStatus,
                UiLanguageService.Translate(
                    _uiLanguageCode, "DevicesRetryChanged"),
                UiStatusKind.Warning);
            return;
        }
        // Avoid accidental request storms from rapid repeated clicks.
        if (DateTime.UtcNow - _unifiedLastSourceRetryUtc <
            TimeSpan.FromSeconds(10))
        {
            UiStyle.SetStatus(_unifiedStatus,
                UiLanguageService.Translate(
                    _uiLanguageCode, "DevicesRetryCooldown"),
                UiStatusKind.Warning);
            return;
        }
        _unifiedLastSourceRetryUtc = DateTime.UtcNow;

        // Only the exact selected source and the same current session are
        // queried; an expired Graph token requires an explicit user login.
        var selected = _unifiedResults.SelectedItems.Count == 1
            ? _unifiedResults.SelectedItems[0].Tag as UnifiedDeviceInfo
            : null;
        using var cancellation = new CancellationTokenSource();
        var generation = ++_unifiedSearchGeneration;
        _unifiedCancellation = cancellation;
        _unifiedSearch.Enabled = false;
        _unifiedCancel.Enabled = true;
        _unifiedQuery.Enabled = false;
        _unifiedProgress.Visible = true;
        _unifiedRetryButton.Enabled = false;
        SetDevicesCloudWarningAction(false);
        UiStyle.SetStatus(_unifiedStatus,
            UiLanguageService.Translate(_uiLanguageCode, "DevicesRetryBusy") +
            " " + RetrySourceLabel(source), UiStatusKind.Busy);
        try
        {
            var token = source == UnifiedMetadataSource.Ad
                ? string.Empty : _cloudToken!.AccessToken;
            var refreshed = await new UnifiedDeviceService(_config)
                .RetryFailedSourceAsync(previous, source, token,
                    cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _unifiedSearchGeneration || IsDisposed)
                return;
            if (!string.Equals(_unifiedQuery.Text.Trim(), state.Query,
                    StringComparison.Ordinal) ||
                (source != UnifiedMetadataSource.Ad && !HasCloudSearchSession()) ||
                (source == UnifiedMetadataSource.Ad && !_directoryConnected))
            {
                UiStyle.SetStatus(_unifiedStatus,
                    UiLanguageService.Translate(
                        _uiLanguageCode, "DevicesRetryChanged"),
                    UiStatusKind.Warning);
                return;
            }

            // Replace only after completed validation. Never leave a
            // recovered password or an Entra recovery ID selected across
            // a changed source identity.
            _deviceCurrentKey = null;
            _deviceRecoveryKey.Clear();
            _deviceRecoveryIds.Items.Clear();
            _unifiedDetails.Clear();
            _unifiedResults.BeginUpdate();
            try
            {
                _unifiedResults.Items.Clear();
                ListViewItem? restoredSelection = null;
                foreach (var row in refreshed.Rows)
                {
                    var item = AddUnifiedDeviceSearchResultRow(row);
                    if (selected is not null &&
                        SameDeviceAfterSourceRetry(selected, row))
                        restoredSelection = item;
                }
                if (restoredSelection is not null)
                {
                    restoredSelection.Selected = true;
                    restoredSelection.Focused = true;
                }
            }
            finally
            {
                _unifiedResults.EndUpdate();
            }

            _unifiedSourceReport = refreshed;
            _device360LastUnifiedPartial = refreshed.IsPartial;
            _device360UnifiedLookupFailed = (
                Ad: refreshed.AdAttempted && !refreshed.AdSucceeded,
                Entra: refreshed.EntraAttempted && !refreshed.EntraSucceeded,
                Intune: refreshed.IntuneAttempted && !refreshed.IntuneSucceeded);
            _device360UnifiedFailureKinds = (
                refreshed.AdFailureKind,
                refreshed.EntraFailureKind,
                refreshed.IntuneFailureKind);
            _unifiedDiagnostics.Clear();
            if (refreshed.IsPartial)
                _unifiedDiagnostics.ShowMessage(
                    "Source Health: partial metadata remains.",
                    refreshed.SourceSummary + Environment.NewLine +
                    refreshed.DiagnosticSummary +
                    Environment.NewLine +
                    "Failed or missing metadata never proves that no backup exists.");

            UiStyle.SetStatus(_unifiedStatus,
                RetrySourceLabel(source) + ": " + refreshed.SourceSummary +
                (selected is not null &&
                 _unifiedResults.SelectedItems.Count == 0
                    ? " " + UiLanguageService.Translate(
                        _uiLanguageCode, "DevicesRetryChanged")
                    : " Source-qualified records preserved."),
                refreshed.IsPartial ? UiStatusKind.Warning : UiStatusKind.Success);
            _audit.Write("UnifiedDeviceSourceRetry",
                source: RetrySourceLabel(source),
                details: "Rows=" + refreshed.Rows.Count +
                    "; Partial=" + refreshed.IsPartial);
        }
        catch (OperationCanceledException)
        {
            if (generation == _unifiedSearchGeneration)
                UiStyle.SetStatus(_unifiedStatus, "Metadata retry canceled.",
                    UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            if (generation == _unifiedSearchGeneration)
            {
                UiStyle.SetStatus(_unifiedStatus,
                    "Source retry failed. Original results were preserved.",
                    UiStatusKind.Error);
                _unifiedDiagnostics.ShowError(
                    "Source retry failed.", "UnifiedDeviceSourceRetry", ex);
            }
        }
        finally
        {
            if (generation == _unifiedSearchGeneration)
            {
                if (ReferenceEquals(_unifiedCancellation, cancellation))
                    _unifiedCancellation = null;
                _unifiedSearch.Enabled = true;
                _unifiedCancel.Enabled = false;
                _unifiedQuery.Enabled = true;
                _unifiedProgress.Visible = false;
                SetDevicesCloudWarningAction(!HasCloudSearchSession());
                RefreshUnifiedSourceRetryButton();
            }
        }
    }
}
