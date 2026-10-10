namespace BitKeyBridge;

public sealed partial class MainForm
{
    private static bool IsProtectedRecoveryRow(RecoverySearchResult row) =>
        row.Source.Equals("Protected cache", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalRecoveryRow(RecoverySearchResult row) =>
        IsProtectedRecoveryRow(row) ||
        row.Source.Equals("Local cache", StringComparison.OrdinalIgnoreCase);

    private void ShowAdvancedDiagnosticCenter()
    {
        var query = _homeQuery.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
            query = _startQuery.Text.Trim();

        using var dialog = new AdvancedDiagnosticCenterDialog(
            _config,
            async () =>
            {
                if (string.IsNullOrWhiteSpace(_cloudConfig.TenantId) ||
                    string.IsNullOrWhiteSpace(_cloudConfig.ClientId))
                    return null;
                return await EnsureCloudTokenAsync()
                    ? _cloudToken?.AccessToken : null;
            },
            dc =>
            {
                _adMode.SelectedIndex = 1;
                _adServer.Text = dc;
                _config.AdConnectionMode = "Explicit";
                _config.AdServer = dc;
                _advancedConnectionGroup.Visible = true;
                _homeAdvancedButton.Text = "Hide Advanced";
                try
                {
                    ConfigService.SaveAppConfig(_config);
                }
                catch (Exception ex)
                {
                    UiStyle.SetStatus(_homeConnectionStatus,
                        "Explicit DC is selected in the current session but settings could not be saved: " +
                        DiagnosticRedaction.Sanitize(ex.Message),
                        UiStatusKind.Warning);
                }
                // Do not silently discard active credentials or perform an LDAP rebind.
                // Operator applies the new selection by reconnecting from Start.
            },
            () =>
            {
                _recoverySource.SelectedIndex = 2;
                _config.RecoverySearchSource = "ProtectedCache";
                UpdateRecoverySourceUi();
                TrySaveRecoveryUiState();
            },
            query);
        dialog.ShowDialog(this);
    }
}
