namespace BitKeyBridge;

public sealed partial class MainForm
{
    private void ShowEnvironmentDiagnosticDialog()
    {
        var computer = _homeQuery.Text.Trim();
        if (string.IsNullOrWhiteSpace(computer))
            computer = _startQuery.Text.Trim();

        var cloudConfigured =
            !string.IsNullOrWhiteSpace(_cloudConfig.TenantId) &&
            !string.IsNullOrWhiteSpace(_cloudConfig.ClientId);

        using var dialog = new EnvironmentDiagnosticDialog(
            _config,
            cloudConfigured,
            computer);
        dialog.ShowDialog(this);
    }
}
