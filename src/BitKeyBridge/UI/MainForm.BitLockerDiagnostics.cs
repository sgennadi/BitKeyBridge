namespace BitKeyBridge;

public sealed partial class MainForm
{
    private void ShowBitLockerWarning(string message)
    {
        UiStyle.SetStatus(_bitLockerZeroWarning, message, UiStatusKind.Warning);
        _bitLockerZeroWarningPanel.Visible = true;
    }

    private void ClearBitLockerWarning()
    {
        _bitLockerZeroWarningPanel.Visible = false;
    }

    private void ShowBitLockerDiagnosticCommands()
    {
        var selected = GetSelectedStartRecoveryRow();
        var query = selected?.ComputerName ?? _startQuery.Text.Trim();
        var sourceIsCache = _recoverySource.SelectedIndex == 1;
        var dc = _config.AdConnectionMode.Equals("Explicit", StringComparison.OrdinalIgnoreCase)
            ? _config.AdServer : string.Empty;

        var context = new BitLockerDiagnosticContext
        {
            SearchQuery = query,
            ComputerDistinguishedName =
                selected?.ComputerDistinguishedName ?? string.Empty,
            RecoveryDistinguishedName =
                selected?.RecoveryDistinguishedName ?? string.Empty,
            RecoveryId = selected?.RecoveryId ??
                (Guid.TryParse(query, out var recoveryId) ? recoveryId.ToString("D") : string.Empty),
            DirectoryServer = dc,
            Domain = _config.AdDomain,
            ScopeDistinguishedName = _startScope?.SearchBase ?? string.Empty,
            LocalCachePath = _config.OutputCsv,
            LocalCache = sourceIsCache,
            RecordsReturned = _startResults.Rows.Count
        };

        using var dialog = new BitLockerDiagnosticCommandsDialog(
            BitLockerDiagnosticRunbook.Build(context));
        dialog.ShowDialog(this);
    }

    private void ShowHomeBitLockerDiagnosticCommands()
    {
        var selected = GetSelectedHomeSearchResult();
        if (selected is null)
            return;

        var source = selected.SourceSummary;
        var hasAd = source.Contains("AD", StringComparison.OrdinalIgnoreCase);
        var hasCloud = source.Contains("Entra", StringComparison.OrdinalIgnoreCase) ||
                       source.Contains("Intune", StringComparison.OrdinalIgnoreCase);
        var dc = _config.AdConnectionMode.Equals("Explicit", StringComparison.OrdinalIgnoreCase)
            ? _config.AdServer : string.Empty;

        var context = new BitLockerDiagnosticContext
        {
            SearchQuery = selected.ComputerName,
            RecoveryId = selected.LatestRecoveryId,
            DirectoryServer = dc,
            Domain = _config.AdDomain,
            ScopeDistinguishedName = _startScope?.SearchBase ?? string.Empty,
            Entra = hasCloud && !hasAd,
            RecordsReturned = selected.BitLockerKeyCount
        };

        var instructions = BitLockerDiagnosticRunbook.Build(context);
        if (hasAd && hasCloud)
        {
            instructions += Environment.NewLine + Environment.NewLine +
                            "ALSO CHECK ENTRA / INTUNE SOURCE:" +
                            Environment.NewLine +
                            BitLockerDiagnosticRunbook.Build(
                                new BitLockerDiagnosticContext
                                {
                                    SearchQuery = selected.ComputerName,
                                    RecoveryId = selected.LatestRecoveryId,
                                    Entra = true,
                                    RecordsReturned = selected.BitLockerKeyCount
                                });
        }

        using var dialog = new BitLockerDiagnosticCommandsDialog(instructions);
        dialog.ShowDialog(this);
    }
}
