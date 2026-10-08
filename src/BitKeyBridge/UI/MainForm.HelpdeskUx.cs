namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly HashSet<string> _rotationSuggestionSessions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly UiStatusLabel _homeSelectionSummary = new();
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
        _secretLifetimeTimer?.Stop();
        _secretLifetimeTimer?.Dispose();

        _secretVisibleUntilUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    15,
                    seconds));

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

            if (secretSeconds <= 0 &&
                clipboardSeconds <= 0)
            {
                UiStyle.SetStatus(
                    _secretLifetimeStatus,
                    "No recovery secret is currently retained.",
                    UiStatusKind.Neutral);
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

    private void MarkClipboardLifetime()
    {
        _clipboardClearAtUtc =
            DateTime.UtcNow.AddSeconds(
                Math.Max(
                    5,
                    _config.SensitiveClipboardSeconds));

        StartSecretLifetimeCountdown(
            "Recovery secret",
            _secretVisibleUntilUtc is null
                ? _config.SecretDisplaySeconds
                : Math.Max(
                    15,
                    (int)Math.Ceiling(
                        (_secretVisibleUntilUtc.Value -
                         DateTime.UtcNow)
                        .TotalSeconds)));
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
