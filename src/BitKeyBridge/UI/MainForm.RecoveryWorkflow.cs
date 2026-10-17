namespace BitKeyBridge;

public sealed partial class MainForm
{
    /// <summary>
    /// Informational preflight only. The existing privileged recovery handlers
    /// remain authoritative for secrets, ticket validation and audit commits.
    /// </summary>
    private void ShowRecoveryWorkflowSteps()
    {
        RecoveryWorkflowReadinessService.Snapshot report;
        try
        {
            AuthorizationDecision? decision = _layoutSelfTest ? null :
                new AuthorizationService(_config).Check(BitKeyBridgePermission.RecoveryRead);
            report = RecoveryWorkflowReadinessService.Build(
                _config, GetSelectedHomeSearchResult(),
                _directoryConnected && !_directoryConnecting,
                HasCloudSearchSession(), decision);
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Recovery preparation could not inspect the Windows token.",
                DiagnosticRedaction.Sanitize(ex.Message) +
                " No authorization has been granted.");
            return;
        }

        using var dialog = new RecoveryWorkflowDialog(report);
        var choice = dialog.ShowDialog(this);
        if (IsDisposed)
            return;
        if (choice == DialogResult.Yes && _homeWorkspaceTab is not null)
        {
            _mainTabs.SelectedTab = _homeWorkspaceTab;
            _homeQuery.Focus();
        }
        else if (choice == DialogResult.No)
        {
            OpenHelpdeskAccessHealth();
        }
    }
}
