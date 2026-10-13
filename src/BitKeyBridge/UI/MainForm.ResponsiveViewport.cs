namespace BitKeyBridge;

public sealed partial class MainForm
{
    private bool _mainViewportReflowQueued;
    private bool _mainViewportReflowRunning;

    private void InitializeMainViewport()
    {
        AutoScroll = false;
        ClientSizeChanged += (_, _) => QueueMainViewportReflow();
        Shown += (_, _) => QueueMainViewportReflow();
    }

    private void QueueMainViewportReflow()
    {
        if (_mainViewportReflowQueued || !IsHandleCreated || IsDisposed)
            return;

        _mainViewportReflowQueued = true;
        BeginInvoke(new MethodInvoker(() =>
        {
            _mainViewportReflowQueued = false;
            if (!IsDisposed)
                ReflowMainViewport();
        }));
    }

    internal void ReflowMainViewport()
    {
        if (_mainViewportReflowRunning || IsDisposed || ClientSize.Width == 0)
            return;

        _mainViewportReflowRunning = true;
        try
        {
            AutoScroll = false;
            var shell = Controls.OfType<TableLayoutPanel>()
                .FirstOrDefault(x => x.Name == "MainLayoutRoot");
            if (shell is null)
                return;

            // Do not let a pre-maximize preferred width constrain the entire
            // application to the top-left of a 4K / Remote Desktop screen.
            shell.Dock = DockStyle.Fill;
            if (shell.Bounds != ClientRectangle)
                shell.Bounds = ClientRectangle;

            shell.PerformLayout();
            _mainTabs.Dock = DockStyle.Fill;
            _mainTabs.PerformLayout();

            if (_mainTabs.SelectedTab is TabPage page)
            {
                page.PerformLayout();
                if (page.Controls.Count > 0 &&
                    page.Controls[0].Dock == DockStyle.Fill)
                {
                    var content = page.Controls[0];
                    if (content.Bounds != page.DisplayRectangle)
                        content.Bounds = page.DisplayRectangle;
                    content.PerformLayout();
                }
            }

            UiModernTheme.Refresh(shell);
        }
        finally
        {
            _mainViewportReflowRunning = false;
        }
    }
}
