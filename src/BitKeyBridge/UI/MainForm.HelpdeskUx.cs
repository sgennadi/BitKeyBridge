namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly HashSet<string> _rotationSuggestionSessions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly UiStatusLabel _homeSelectionSummary = new();
    private readonly Button _homeRevealBitLockerButton = new();
    private readonly Button _homeCopyBitLockerButton = new();
    private readonly Button _homeDeviceDetailsButton = new();
    private readonly UiStatusLabel _secretLifetimeStatus = new();
    private readonly UiStatusLabel _setupStatus = new();
    private readonly Button _clearSecretNow = new();
    private System.Windows.Forms.Timer? _secretLifetimeTimer;
    private DateTime? _secretVisibleUntilUtc;
    private DateTime? _clipboardClearAtUtc;

    private readonly TextBox _auditFilter = new();
    private readonly ComboBox _auditFilterField = new();
    private List<AuditEntry> _auditRows = [];

    private readonly ListView _accessHealthResults = new();
    private readonly UiStatusLabel _accessHealthStatus = new();
    private readonly Button _accessHealthRun = new();
    private readonly ProgressBar _accessHealthProgress = new();

    private readonly ComboBox _helpdeskProfile = new();
    private readonly UiStatusLabel _helpdeskProfileStatus = new();

    private async Task MaybeSuggestCloudRotationAfterRecoveryAsync(
        RecoveryAccessContext context,
        UnifiedDeviceInfo row,
        string recoveryId)
    {
        if (!_config.SuggestRotationAfterCloudKeyRetrieval ||
            string.IsNullOrWhiteSpace(
                row.ManagedDeviceId) ||
            !_rotationSuggestionSessions.Add(
                context.SessionId))
        {
            return;
        }

        var answer =
            MessageBox.Show(
                this,
                $"Recovery access for {row.ComputerName} is complete.{Environment.NewLine}{Environment.NewLine}" +
                "Request Intune to rotate its BitLocker recovery key now?",
                "BitLocker Key Rotation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        await RotateManagedDeviceAsync(
            row.ManagedDeviceId,
            row.ComputerName,
            recoveryId,
            context);
    }

    private void StartSecretLifetimeCountdown(
        string label,
        int seconds)
    {
        _secretVisibleUntilUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    15,
                    seconds));

        EnsureSecretLifetimeTimer(
            label);
    }

    private void MarkClipboardLifetime()
    {
        _clipboardClearAtUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    5,
                    _config.SensitiveClipboardSeconds));

        EnsureSecretLifetimeTimer(
            "Recovery secret");
    }

    private void EnsureSecretLifetimeTimer(
        string label)
    {
        _secretLifetimeTimer?.Stop();
        _secretLifetimeTimer?.Dispose();

        _secretLifetimeTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

        void Refresh()
        {
            var secretSeconds =
                _secretVisibleUntilUtc is null
                    ? 0
                    : Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (_secretVisibleUntilUtc.Value -
                             DateTime.UtcNow)
                            .TotalSeconds));

            var clipboardSeconds =
                _clipboardClearAtUtc is null
                    ? 0
                    : Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (_clipboardClearAtUtc.Value -
                             DateTime.UtcNow)
                            .TotalSeconds));

            if (_secretVisibleUntilUtc is not null &&
                secretSeconds <= 0)
            {
                ClearVisibleSecretsNow();
                return;
            }

            if (_clipboardClearAtUtc is not null &&
                clipboardSeconds <= 0)
            {
                ClearTrackedRecoveryClipboard();
                _clipboardClearAtUtc =
                    null;
                clipboardSeconds =
                    0;
            }

            if (secretSeconds <= 0 &&
                clipboardSeconds <= 0)
            {
                UiStyle.SetStatus(
                    _secretLifetimeStatus,
                    "No recovery secret is currently retained.",
                    UiStatusKind.Neutral);
                _secretLifetimeTimer?.Stop();
                return;
            }

            var parts =
                new List<string>();

            if (secretSeconds > 0)
            {
                parts.Add(
                    $"{label} clears in {TimeSpan.FromSeconds(secretSeconds):mm\\:ss}");
            }

            if (clipboardSeconds > 0)
            {
                parts.Add(
                    $"clipboard clears in {TimeSpan.FromSeconds(clipboardSeconds):mm\\:ss}");
            }

            UiStyle.SetStatus(
                _secretLifetimeStatus,
                string.Join(" • ", parts),
                UiStatusKind.Warning);
        }

        _secretLifetimeTimer.Tick +=
            (_, _) =>
                Refresh();

        Refresh();
        _secretLifetimeTimer.Start();
    }

    private void ClearVisibleSecretsNow()
    {
        ClearTrackedRecoveryClipboard();
        _clipboardClearAtUtc = null;

        _startCurrentKey = null;
        _startKey.Clear();
        _startKey.UseSystemPasswordChar = true;
        _startShow.Text = "Reveal Recovery Key";

        _deviceCurrentKey = null;
        _deviceRecoveryKey.Clear();
        _deviceRecoveryKey.UseSystemPasswordChar = true;
        _deviceShowKey.Text = "Reveal Key";

        ClearLapsResult();

        _secretVisibleUntilUtc = null;
        _secretLifetimeTimer?.Stop();

        UiStyle.SetStatus(
            _secretLifetimeStatus,
            "Recovery secrets cleared.",
            UiStatusKind.Success);
    }

    private async Task<RecoverySearchResult?>
        GetLatestHomeRecoveryRowAsync(
            HomeSearchResult selected)
    {
        if (!_directoryConnected ||
            _startScope is null)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Connect to Active Directory before reading a BitLocker recovery key.",
                UiStatusKind.Warning);
            return null;
        }

        var rows =
            await SearchLiveAdRecoveryMetadataAsync(
                selected.ComputerName,
                _startScope,
                CancellationToken.None);

        return rows
            .Where(
                row =>
                    row.ComputerName.Equals(
                        selected.ComputerName,
                        StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(
                row =>
                    row.IsLatest == true)
            .ThenByDescending(
                row =>
                    row.KeyDate)
            .FirstOrDefault();
    }

    private async Task<(string Key, RecoverySearchResult Row, RecoveryAccessContext Context)?>
        GetHomeBitLockerSecretAsync(
            string action)
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null ||
            selected.BitLockerKeyCount <= 0)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "Select a computer with BitLocker recovery metadata.",
                UiStatusKind.Warning);
            return null;
        }

        var row =
            await GetLatestHomeRecoveryRowAsync(
                selected);

        if (row is null)
        {
            UiStyle.SetStatus(
                _homeSearchStatus,
                "The selected BitLocker recovery record could not be resolved.",
                UiStatusKind.Warning);
            return null;
        }

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                action,
                row.ComputerName,
                row.RecoveryId,
                "AD"))
        {
            return null;
        }

        var context =
            GetOrRequestRecoveryAccessContext(
                "AD",
                row.RecoveryId,
                row.ComputerName,
                allowRotationReminder:
                    false);

        if (context is null ||
            !AuthorizePrivilegedRecoveryAccess(
                context,
                action,
                row.ComputerName,
                row.RecoveryId,
                "AD"))
        {
            return null;
        }

        _startCurrentKey =
            null;

        var key =
            await EnsureStartRecoveryKeyAsync(
                row);

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return null;
        }

        return (
            key,
            row,
            context);
    }

    private async Task RevealHomeBitLockerAsync()
    {
        var access =
            await GetHomeBitLockerSecretAsync(
                "RevealHomeRecoveryKey");

        if (access is null)
            return;

        WriteRecoveryAudit(
            "RevealHomeRecoveryKey",
            access.Value.Context,
            computerName:
                access.Value.Row.ComputerName,
            recoveryId:
                access.Value.Row.RecoveryId,
            source:
                "AD");

        RecordRecentComputer(
            access.Value.Row.ComputerName,
            string.Empty,
            "BitLocker reveal");

        using var dialog =
            new SecretDisplayDialog(
                "BitLocker Recovery Key",
                $"{access.Value.Row.ComputerName} • Recovery ID {access.Value.Row.RecoveryId}",
                access.Value.Key,
                $"The secret is kept only in memory and is cleared when this window closes. Clipboard copies clear after {_config.SensitiveClipboardSeconds} seconds.",
                _config.SensitiveClipboardSeconds);

        dialog.ShowDialog(this);

        _startCurrentKey =
            null;

        UiStyle.SetStatus(
            _homeSearchStatus,
            "BitLocker recovery key view closed and the in-memory copy was cleared.",
            UiStatusKind.Success);
    }

    private async Task CopyHomeBitLockerAsync()
    {
        var access =
            await GetHomeBitLockerSecretAsync(
                "CopyHomeRecoveryKey");

        if (access is null)
            return;

        WriteRecoveryAudit(
            "CopyHomeRecoveryKey",
            access.Value.Context,
            computerName:
                access.Value.Row.ComputerName,
            recoveryId:
                access.Value.Row.RecoveryId,
            source:
                "AD");

        RecordRecentComputer(
            access.Value.Row.ComputerName,
            string.Empty,
            "BitLocker copy");

        CopyKeyWithAutoClear(
            access.Value.Key);

        _startCurrentKey =
            null;

        UiStyle.SetStatus(
            _homeSearchStatus,
            $"Recovery key copied. Clipboard clears in {_config.SensitiveClipboardSeconds} seconds.",
            UiStatusKind.Success);
    }

    private void OpenHomeDeviceDetails()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
            return;

        _unifiedQuery.Text =
            selected.ComputerName;

        foreach (TabPage page in
                 _mainTabs.TabPages)
        {
            if (page.Text.Equals(
                    "Devices",
                    StringComparison.OrdinalIgnoreCase))
            {
                _mainTabs.SelectedTab =
                    page;
                break;
            }
        }
    }

    private void RefreshHomeSelectionSummary()
    {
        var selected =
            GetSelectedHomeSearchResult();

        if (selected is null)
        {
            UiStyle.SetStatus(
                _homeSelectionSummary,
                "Select a result to see the helpdesk actions and source summary.",
                UiStatusKind.Neutral);
            return;
        }

        var source =
            string.IsNullOrWhiteSpace(
                selected.SourceSummary)
                ? "AD"
                : selected.SourceSummary;

        UiStyle.SetStatus(
            _homeSelectionSummary,
            $"{selected.ComputerName} • Source: {source} • BitLocker: {selected.BitLockerStatus} • LAPS: {selected.LapsStatus}",
            selected.BitLockerKeyCount > 0 ||
            selected.LapsDetected
                ? UiStatusKind.Success
                : UiStatusKind.Warning);
    }
}
