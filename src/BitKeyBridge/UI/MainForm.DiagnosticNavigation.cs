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
        workspace.Name = "DiagnosticToolsWorkspace";
        page.Controls.Add(workspace);
        workspace.Controls.Add(CreateSectionTitle(
            "Connection and recovery diagnostics",
            "Run environment checks or open the existing LAPS, BitLocker and " +
            "Active Directory / Microsoft Graph diagnostic runbooks. " +
            "No secret is retrieved by opening a diagnostic tool."));

        var buttons = CreateActionFlow();
        buttons.Name = "DiagnosticToolsActions";

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

        var protectedAudit = NewActionButton("Check Protected Audit...");
        protectedAudit.Name = "RunProtectedAuditCheckButton";
        protectedAudit.AccessibleName =
            "Verify secure named-pipe access and durable audit acknowledgement";
        protectedAudit.Click += async (_, _) =>
        {
            protectedAudit.Enabled = false;
            try
            {
                // Never block the WinForms UI for a potentially missing
                // service. The probe is metadata-only and honors current RBAC.
                var committed = await Task.Run(
                    ProtectedAuditBrokerClient.TryProbe);
                ShowAppMessage(
                    committed ? "Protected audit verified" : "Protected audit unavailable",
                    committed
                        ? "The Windows Service authenticated your account through " +
                          "the local named pipe and committed a synthetic event " +
                          "to the protected journal. No recovery key was read."
                        : "No protected audit acknowledgement was received. " +
                          "Check that the Windows Service is running, that your " +
                          "account belongs to RecoveryReaders and that the " +
                          "protected audit journal has valid NTFS permissions. " +
                          "Strict mode blocks secret reads until this is resolved.");
            }
            catch (Exception ex)
            {
                ShowAppMessage(
                    "Protected audit diagnostic failed",
                    DiagnosticRedaction.Sanitize(ex.Message));
            }
            finally
            {
                if (!protectedAudit.IsDisposed)
                    protectedAudit.Enabled = true;
            }
        };

        var uiPerformance = NewActionButton("UI Performance...");
        uiPerformance.Name = "RunUiPerformanceDiagnosticsButton";
        uiPerformance.AccessibleName =
            "Show in-memory layout timings without computer identities or secrets";
        uiPerformance.Click += (_, _) =>
            ShowAppMessage("UI performance (current session)",
                UiLayoutTelemetry.Report());

        buttons.Controls.AddRange([
            environment,
            advanced,
            bitLocker,
            laps,
            protectedAudit,
            uiPerformance
        ]);
        workspace.Controls.Add(buttons);
        return page;
    }
}
