using System.Diagnostics;

namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Operator-authorized public support contact. Never build support
    // messages from recovery metadata, identities, diagnostics or secrets.
    internal const string SupportEmailAddress = "sgennadi@hotmail.com";

    internal static string BuildSupportEmailUri() =>
        "mailto:" + SupportEmailAddress + "?subject=" +
        Uri.EscapeDataString("BitKeyBridge Support");

    private void OpenSupportEmail()
    {
        if (_layoutSelfTest)
            return;

        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                BuildSupportEmailUri())
            {
                UseShellExecute = true
            });
            if (process is null)
                throw new InvalidOperationException(
                    "Windows did not start a default email application.");
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Cannot open the default email app.",
                "Send your message to " + SupportEmailAddress +
                ". Configure an email app in Windows Default apps, or " +
                "use Support > Copy support email. " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private void OpenGitHubReportsWorkspace()
    {
        // Reuse Helpdesk; no second collection or implicit network request.
        var tab = _mainTabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "HelpdeskDashboardTab");
        if (tab is not null)
            _mainTabs.SelectedTab = tab;
        if (!_layoutSelfTest)
            _appDiagnostics.ShowMessage(
                "Anonymous GitHub diagnostics preview",
                GitHubDiagnosticReporter.Preview());
    }

    private void CopySupportEmail()
    {
        if (_layoutSelfTest)
            return;

        try
        {
            Clipboard.SetText(SupportEmailAddress);
            _appDiagnostics.ShowMessage(
                "Support email copied.",
                SupportEmailAddress + " is ready to paste into your mail app.");
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Support email could not be copied.",
                "Use this address: " + SupportEmailAddress + ". " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }
}
