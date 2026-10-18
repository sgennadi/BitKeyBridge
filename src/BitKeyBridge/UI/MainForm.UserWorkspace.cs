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

        if (UiWorkspacePreferenceService.FitsAnyScreen(saved, screens))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = saved;
        }

        WindowState = _workspacePreferences.Maximized
            ? FormWindowState.Maximized
            : FormWindowState.Normal;

        ApplyUpdateCenterDensity(_workspacePreferences.CompactUpdates);
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
