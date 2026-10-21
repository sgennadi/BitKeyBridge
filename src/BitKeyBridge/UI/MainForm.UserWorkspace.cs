using System.Drawing;

namespace BitKeyBridge;

public sealed partial class MainForm
{
    private UiWorkspacePreferences _workspacePreferences = new();

    private void RestoreWorkspace()
    {
        _workspacePreferences = UiWorkspacePreferenceService.Load();
        var saved = new Rectangle(
            _workspacePreferences.X, _workspacePreferences.Y,
            _workspacePreferences.Width, _workspacePreferences.Height);
        var screens = Screen.AllScreens.Select(s => s.WorkingArea);
        var restored = UiWorkspacePreferenceService.RestoreToVisibleArea(
            saved, screens, MinimumSize);

        if (restored is not null)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = restored.Value;
        }

        // A removed display or a smaller RDP session must never reopen as
        // an enormous, unreachable restored window.
        WindowState = _workspacePreferences.Maximized || restored is null
            ? FormWindowState.Maximized
            : FormWindowState.Normal;

        ApplyUpdateCenterDensity(_workspacePreferences.CompactUpdates);
    }

    private void ResetVisualLayout()
    {
        if (!_layoutSelfTest)
        {
            var answer = MessageBox.Show(this,
                "Restore default panel order, compact Update Center and window layout? " +
                "Connections, passwords, recovery permissions and audit policies will not change.",
                "Reset visual layout", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
                return;
        }

        RestoreDefaultHomePaneOrder();
        ApplyUpdateCenterDensity(compact: true);
        _workspacePreferences = new UiWorkspacePreferences();
        if (!_layoutSelfTest)
        {
            try
            {
                ConfigService.SaveStartConnectionPreferences(_startConnectionPreferences);
                UiWorkspacePreferenceService.Save(_workspacePreferences);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException ||
                                       ex is System.Security.SecurityException)
            {
                System.Diagnostics.Debug.WriteLine("Visual reset persistence failed: " +
                    DiagnosticRedaction.Sanitize(ex.GetType().Name));
                ShowAppMessage("Visual layout reset for this session, but preferences could not be saved.",
                    "Reset visual layout");
            }
            WindowState = FormWindowState.Maximized;
        }
        QueueMainViewportReflow();
    }

    internal void PreviewResetVisualLayoutForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI tests only.");
        ResetVisualLayout();
    }

    private void SaveWorkspace()
    {
        if (_layoutSelfTest || WindowState == FormWindowState.Minimized)
            return;

        try
        {
            var current = WindowState == FormWindowState.Normal
                ? Bounds
                : RestoreBounds;
            var screens = Screen.AllScreens.Select(s => s.WorkingArea);
            if (UiWorkspacePreferenceService.FitsAnyScreen(current, screens))
            {
                _workspacePreferences.X = current.X;
                _workspacePreferences.Y = current.Y;
                _workspacePreferences.Width = current.Width;
                _workspacePreferences.Height = current.Height;
            }

            _workspacePreferences.Maximized =
                WindowState == FormWindowState.Maximized;
            _workspacePreferences.CompactUpdates = _compactUpdateCenter.Checked;
            UiWorkspacePreferenceService.Save(_workspacePreferences);
        }
        catch (Exception ex)
        {
            // Display preferences never block the recovery UI or shutdown.
            System.Diagnostics.Debug.WriteLine("Workspace preference save failed: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
        }
    }
}
