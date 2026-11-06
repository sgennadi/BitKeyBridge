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

    private void OpenUniversalDiagnosticsWorkspace()
    {
        // Reuse Helpdesk; no second collection or implicit network request.
        var tab = _mainTabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "HelpdeskDashboardTab");
        if (tab is not null)
            _mainTabs.SelectedTab = tab;
        if (!_layoutSelfTest)
            _appDiagnostics.ShowMessage(
                "Universal Diagnostics local preview",
                UniversalDiagnosticService.Preview());
    }

    private void SaveSafeUniversalDiagnosticsSummary()
    {
        if (_layoutSelfTest)
            return;

        // Preview() returns only an allowlisted category/count summary, not
        // the original JSON, event descriptions or device identity. Export
        // is operator-initiated, never sent to GitHub or attached to email.
        using var dialog = new SaveFileDialog
        {
            Title = "Save safe diagnostic summary",
            Filter = "Text files (*.txt)|*.txt",
            FileName = "BitKeyBridge-UniversalDiagnostics-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt",
            AddExtension = true,
            DefaultExt = "txt",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var summary = UniversalDiagnosticService.Preview();
            File.WriteAllText(dialog.FileName, summary,
                new System.Text.UTF8Encoding(false));
            _appDiagnostics.ShowMessage(
                "Local diagnostic summary saved.",
                "Only classified error categories and counts were exported. " +
                "No report was uploaded.");
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Cannot save safe diagnostic summary.",
                "No upload was attempted (" + ex.GetType().Name + ").");
        }
    }

    private void OpenUniversalDiagnosticsFolder()
    {
        if (_layoutSelfTest)
            return;
        try
        {
            // Only the current user's local UDX queue. No upload or recovery
            // secret collection; Explorer is launched on explicit click.
            var directory = UniversalDiagnosticService.LocalQueueDirectory;
            Directory.CreateDirectory(directory);
            using var opened = Process.Start(new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                ArgumentList = { directory }
            });
            if (opened is null)
                throw new InvalidOperationException("Windows Explorer did not start.");
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowMessage(
                "Cannot open local diagnostic folder.",
                "Local reports remain unchanged (" + ex.GetType().Name + ").");
        }
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
