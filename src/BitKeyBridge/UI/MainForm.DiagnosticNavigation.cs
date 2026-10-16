namespace BitKeyBridge;

/// <summary>
/// One discoverable diagnostic entry point. Existing specialized dialogs remain
/// available from their context links without duplicating probe engines.
/// </summary>
public sealed partial class MainForm
{
    private TabPage BuildDiagnosticToolsPage()
    {
        var page = new TabPage("Tools")
        {
            Name = "DiagnosticToolsTab"
        };

        var workspace = CreateVerticalWorkspace();
        page.Controls.Add(workspace);
        workspace.Controls.Add(CreateSectionTitle(
            "Connection and recovery diagnostics",
            "Run environment checks or open the existing LAPS, BitLocker and " +
            "Active Directory / Microsoft Graph diagnostic runbooks. " +
            "No secret is retrieved by opening a diagnostic tool."));

        var buttons = CreateActionFlow();

        var environment = NewActionButton("Environment Check...");
        environment.Name = "RunEnvironmentCheckButton";
        environment.Click += (_, _) => ShowEnvironmentDiagnosticDialog();

        var advanced = NewActionButton("Device / Graph Diagnostics...");
        advanced.Name = "RunAdvancedDeviceDiagnosticsButton";
        advanced.Click += (_, _) => ShowAdvancedDiagnosticCenter();

        var bitLocker = NewActionButton("BitLocker Commands...");
        bitLocker.Name = "RunBitLockerCommandsButton";
        bitLocker.Click += (_, _) =>
        {
            if (GetSelectedHomeSearchResult() is not null)
                ShowHomeBitLockerDiagnosticCommands();
            else
                ShowBitLockerDiagnosticCommands();
        };

        var laps = NewActionButton("LAPS Commands...");
        laps.Name = "RunLapsCommandsButton";
        laps.Click += (_, _) => ShowLapsDiagnosticCommands();

        buttons.Controls.AddRange([
            environment,
            advanced,
            bitLocker,
            laps
        ]);
        workspace.Controls.Add(buttons);
        return page;
    }
}
