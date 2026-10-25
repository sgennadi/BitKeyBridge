namespace BitKeyBridge;

public static class UiLayoutSelfTest
{
    private sealed record Scenario(
        string Name,
        Size ClientSize,
        float FontScale);

    private static readonly Scenario[] Scenarios =
    [
        new(
            "Compact",
            new Size(
                900,
                620),
            1.0F),
        new(
            "LargeText",
            new Size(
                1050,
                720),
            1.50F),
        new(
            "Scale175",
            new Size(
                1200,
                780),
            1.75F),
        new(
            "RdpCompact200",
            new Size(
                1024,
                768),
            2.00F),
        new(
            "Scale200Stress",
            new Size(
                1280,
                800),
            2.00F)
    ];

    public static int Run()
    {
        var reportPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "ui-self-test.log");

        try
        {
            if (File.Exists(
                    reportPath))
            {
                File.Delete(
                    reportPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "UI self-test report cleanup failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        var failures =
            new List<string>();

        VerifyModernAccentAndDynamicTheme(failures);

        VerifyStatusGlyphScaling(
            failures);

        VerifyUnchangedStatusDoesNotRepaint(failures);

        VerifyWrappingStatusMeasurement(
            failures);

        VerifyStatusGlyphAlignmentInset(failures);

        VerifyHighContrastStatusPalette(
            failures);

        VerifyListViewColumnScaling(
            failures);

        VerifyLapsHelpdeskStates(
            failures);

        VerifyHebrewIdentityDirection(
            failures);

        VerifyHelpdeskChecklistLocalization(
            failures);

        VerifyIntuneSetupWizardNavigation(failures);

        VerifyDiagnosticCopyVisibility(failures);

        // A DPI report must distinguish the initially measured MainForm from
        // any auxiliary form, and include only aggregate phase timings.
        var performanceReport = UiLayoutTelemetry.Report();
        foreach (var expected in new[]
        {
            "Initial DPI - main window",
            "Initial DPI - other windows",
            "DPI phase - theme installation",
            "DPI phase - control defaults",
            "DPI phase - work-area adjustment",
            "DPI phase - WinForms PerformLayout",
            "DPI phase - label widths",
            "DPI phase - theme refresh"
        })
            if (!performanceReport.Contains(expected, StringComparison.Ordinal))
                failures.Add("Missing DPI timing phase: " + expected);

        VerifyModalDialogLocalization(failures);

        VerifyStartSourcePolicies(failures);

        var factories =
            CreateFormFactories();

        foreach (var scenario in
                 Scenarios)
        {
            foreach (var factory in
                     factories)
            {
                DpiAwareForm? form =
                    null;
                Font? scaledFont =
                    null;

                try
                {
                    form =
                        factory.Factory();

                    form.MinimumSize =
                        Size.Empty;
                    form.MaximumSize =
                        Size.Empty;
                    form.ClientSize =
                        scenario.ClientSize;

                    if (Math.Abs(
                            scenario.FontScale -
                            1.0F) >
                        0.01F)
                    {
                        scaledFont =
                            new Font(
                                UiStyle.BodyFont.FontFamily,
                                UiStyle.BodyFont.Size *
                                scenario.FontScale,
                                FontStyle.Regular,
                                GraphicsUnit.Point);

                        form.Font =
                            scaledFont;
                    }

                    form.CreateControl();
                    NormalizeDockFillSizes(
                        form);
                    form.PrepareResponsiveLayoutForTesting();
                    form.PerformLayout();
                    NormalizeDockFillSizes(
                        form);
                    form.PrepareResponsiveLayoutForTesting();

                    VerifyForm(
                        form,
                        factory.Name,
                        scenario,
                        failures);
                }
                catch (Exception ex)
                {
                    failures.Add(
                        $"{factory.Name}/{scenario.Name}: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    form?.Dispose();
                    scaledFont?.Dispose();
                }
            }
        }

        var reportLines =
            failures.Count == 0
                ? new[]
                {
                    "UI-SELF-TEST OK"
                }
                : failures
                    .Select(
                        failure =>
                            "UI-SELF-TEST FAILED: " +
                            failure)
                    .ToArray();

        try
        {
            File.WriteAllLines(
                reportPath,
                reportLines);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "UI self-test report write failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        foreach (var line in
                 reportLines)
        {
            if (failures.Count == 0)
                Console.WriteLine(line);
            else
                Console.Error.WriteLine(line);
        }

        return failures.Count == 0
            ? 0
            : 1;
    }

    private static void VerifyModalDialogLocalization(List<string> failures)
    {
        static bool HasText(Control root, string text)
        {
            var queue = new Stack<Control>();
            queue.Push(root);
            while (queue.Count != 0)
            {
                var current = queue.Pop();
                if (current.Text == text)
                    return true;
                foreach (Control child in current.Controls)
                    queue.Push(child);
            }
            return false;
        }

        try
        {
            using var prompt = new AdCredentialPromptDialog(@"DOMAIN\helpdesk");
            using var startup = new StartupConnectionWizardDialog(
                StartConnectionPolicy.ActiveDirectory);
            foreach (var (language, domainTitle, startupTitle, connectText) in
                new[]
                {
                    ("ru", "Подключение к Active Directory", "Выберите подключение", "Подключиться"),
                    ("he", "התחברות ל-Active Directory", "בחירת חיבור", "התחברות"),
                    ("en", "Connect to Active Directory", "Choose a connection", "Connect"),
                    ("ru", "Подключение к Active Directory", "Выберите подключение", "Подключиться")
                })
            {
                UiDialogLocalization.Apply(prompt, language);
                UiDialogLocalization.Apply(startup, language);
                if (prompt.Text != domainTitle || !HasText(startup, startupTitle) ||
                    !HasText(prompt, connectText))
                    failures.Add("Modal dialog translation missing or stale: " + language);
                // Explanatory labels created from concatenated source
                // literals must be translated, not just dialog buttons.
                if (!HasText(prompt, UiStaticCaptionCatalog.TranslateOrOriginal(
                        language,
                        "Enter the account for this connection. The password stays in this process. " +
                        "A domain or DC can be entered on a standalone computer. Empty DC uses automatic discovery.")))
                    failures.Add("AD credential guidance was not localized: " + language);
                if (!HasText(startup, UiStaticCaptionCatalog.TranslateOrOriginal(
                        language,
                        "Select a source for this session. Recovery passwords remain hidden " +
                        "until an authorized technician explicitly requests them.")))
                    failures.Add("Startup source guidance was not localized: " + language);
                if (prompt.Username != @"DOMAIN\helpdesk")
                    failures.Add("Dialog localization modified supplied AD account data.");
            }
        }
        catch (Exception ex)
        {
            failures.Add("Modal EN/RU/HE translation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyUnchangedStatusDoesNotRepaint(
        List<string> failures)
    {
        using var status = new UiStatusLabel { Text = "Ready" };
        UiStyle.ConfigureStatusLabel(status);
        UiStyle.SetStatus(status, "Ready", UiStatusKind.Success);
        status.CreateControl();

        var original = status.Image;
        var invalidations = 0;
        status.Invalidated += (_, _) => invalidations++;

        // UI polling can repeat identical status updates many times.
        // There must be no Image replacement or forced repaint.
        UiStyle.SetStatus(status, "Ready", UiStatusKind.Success);
        UiStyle.RefreshStatusLabel(status);
        if (!ReferenceEquals(original, status.Image) ||
            invalidations != 0)
        {
            failures.Add(
                "Unchanged status text/kind forced a repaint or image replacement.");
        }

        UiStyle.SetStatus(status, "Disconnected", UiStatusKind.Warning);
        if (status.Text != "Disconnected" ||
            status.StatusKind != UiStatusKind.Warning)
        {
            failures.Add(
                "Status optimization suppressed an actual state change.");
        }
    }

    private static void VerifyModernAccentAndDynamicTheme(List<string> failures)
    {
        if (SystemInformation.HighContrast)
            return; // Native high contrast colors must always win.

        try
        {
            using var host = new Panel();
            var search = UiStyle.CreateActionButton("Search");
            search.Name = "HomeUnifiedSearchButton";
            var reveal = UiStyle.CreateActionButton("Reveal Key");
            reveal.Name = "HomeRevealBitLockerButton";
            host.Controls.Add(search);
            host.Controls.Add(reveal);

            UiModernTheme.Apply(host);
            if (!UiModernTheme.IsPrimaryAction(search) ||
                UiModernTheme.IsPrimaryAction(reveal) ||
                search.BackColor != UiStyle.ModernAccent ||
                search.ForeColor != Color.White ||
                reveal.BackColor != UiStyle.ModernSurface)
            {
                failures.Add("Modern accent incorrectly styled a recovery-secret action " +
                    "or failed to distinguish the main Search action.");
            }

            // An idempotent second application must preserve already-themed
            // controls, while ControlAdded styles dynamic buttons.
            UiModernTheme.Apply(host);
            var retry = UiStyle.CreateActionButton("Retry check");
            retry.Name = "UpdateCenterRetryCheckButton";
            host.Controls.Add(retry);
            if (retry.BackColor != UiStyle.ModernAccent ||
                search.BackColor != UiStyle.ModernAccent)
                failures.Add("Theme failed to style dynamically added primary controls.");
        }
        catch (Exception ex)
        {
            failures.Add("Modern theme actions: " + ex.GetType().Name + ": " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static IReadOnlyList<(
        string Name,
        Func<DpiAwareForm> Factory)>
        CreateFormFactories()
    {
        AppConfig NewConfig() =>
            new()
            {
                AutoConnectOnStart = false,
                RecoverySearchSource =
                    "LiveAD",
                HealthEndpointEnabled =
                    false,
                OutputRoot =
                    Path.GetTempPath(),
                OutputSubdirectory =
                    "BitKeyBridge-UiSelfTest"
            };

        return
        [
            (
                "MainForm",
                () =>
                    new MainForm(
                        NewConfig(),
                        layoutSelfTest: true)),
            (
                "Helpdesk deployment checklist",
                () =>
                {
                    var main = new MainForm(NewConfig(), layoutSelfTest: true);
                    try
                    {
                        var wizard = main.CreateHelpdeskDeploymentWizard();
                        wizard.Disposed += (_, _) => main.Dispose();
                        return wizard;
                    }
                    catch
                    {
                        main.Dispose();
                        throw;
                    }
                }),
            (
                "Environment prerequisites",
                () => new EnvironmentDiagnosticDialog(
                    NewConfig(), cloudConfigured: false, computer: "PC-TEST", runOnOpen: false)),
            (
                "Device diagnostics center",
                () => new AdvancedDiagnosticCenterDialog(
                    NewConfig(),
                    () => Task.FromResult<string?>(null),
                    _ => { },
                    () => { },
                    "PC-TEST")),
            (
                "BitLocker diagnostic commands",
                () => new BitLockerDiagnosticCommandsDialog(
                    BitLockerDiagnosticRunbook.Build(
                        new BitLockerDiagnosticContext { SearchQuery = "PC-TEST" }))),
            (
                "AD credential prompt",
                () =>
                    new AdCredentialPromptDialog(
                        @"DOMAIN\helpdesk")),
            (
                "AD access troubleshooting",
                () =>
                    new AdAccessTroubleshootingDialog(
                        "OU=Workstations,DC=example,DC=com",
                        @"DOMAIN\BitKeyBridge-Recovery-Readers",
                        "BitLocker / LAPS self-test")),
            (
                "Input",
                () =>
                    new InputDialog(
                        "Input",
                        "A deliberately long prompt used to verify that explanatory text wraps instead of being clipped at compact or large-text settings.")),
            (
                "OU browser",
                () =>
                    new OuBrowserForm()),
            (
                "RBAC",
                () =>
                    new RbacSettingsDialog(
                        NewConfig())),
            (
                "RBAC setup wizard",
                () =>
                    new RbacSetupWizardDialog(
                        NewConfig())),
            (
                "Intune setup wizard",
                () =>
                    new IntuneSetupWizardDialog(
                        new CloudAuthConfig
                        {
                            TenantId =
                                "contoso.onmicrosoft.com",
                            ClientId =
                                Guid.Empty.ToString(),
                            EnableLapsPermissions =
                                true
                        },
                        _ =>
                            Task.CompletedTask)),
            (
                "Startup connection wizard",
                () => new StartupConnectionWizardDialog(
                    StartConnectionPolicy.ActiveDirectory)),
            (
                "Non-admin Microsoft 365 connection wizard",
                () => new CloudConnectionWizardDialog(
                    new CloudAuthConfig
                    {
                        TenantId = "contoso.onmicrosoft.com",
                        ClientId = Guid.Empty.ToString()
                    })),
            (
                "Recovery workflow preflight",
                () => new RecoveryWorkflowDialog(
                    RecoveryWorkflowReadinessService.Build(
                        new AppConfig
                        {
                            RbacEnabled = true,
                            RequireRecoveryAccessReference = true,
                            RequireProtectedAuditForRecovery = true,
                            AuditBrokerEnabled = true,
                            RbacRecoveryReaders = ["S-1-5-32-544"]
                        },
                        new HomeSearchResult
                        {
                            ComputerName = "PC-12345",
                            AdComputerDistinguishedName = "CN=PC-12345,OU=Clients,DC=example,DC=test",
                            BitLockerKeyCount = 2,
                            LatestRecoveryId = Guid.Empty.ToString(),
                            LapsCandidateFound = true,
                            LapsStatus = "Detected"
                        },
                        adConnected: true,
                        cloudConnected: false,
                        new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" }))),
            (
                "Hebrew recovery workflow preflight",
                () => new RecoveryWorkflowDialog(
                    RecoveryWorkflowReadinessService.Build(
                        new AppConfig(), new HomeSearchResult
                        {
                            ComputerName = "PC-HEBREW", IsCloudRecord = true,
                            SourceIdentity = "ENTRA:DEVICE:some-device-guid",
                            BitLockerKeyCount = 1
                        },
                        adConnected: false, cloudConnected: true,
                        new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" }),
                    "he")),
            (
                "Russian recovery workflow preflight",
                () => new RecoveryWorkflowDialog(
                    RecoveryWorkflowReadinessService.Build(
                        new AppConfig(), new HomeSearchResult
                        {
                            ComputerName = "PC-RUS", BitLockerKeyCount = 1,
                            AdComputerDistinguishedName = "CN=PC-RUS,OU=Clients,DC=example,DC=test"
                        },
                        adConnected: true, cloudConnected: false,
                        new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" }),
                    "ru")),
            (
                "Recovery access",
                () =>
                    new RecoveryAccessDialog(
                        "PC-12345",
                        Guid.Empty.ToString(),
                        requireReference: true,
                        referencePattern: "^INC-[0-9]+$",
                        referenceExample: "INC-12345")),
            (
                "Incident verification",
                () =>
                    new RecoveryIncidentVerificationDialog()),
            (
                "Remote API tokens",
                () =>
                    new RemoteApiScopedTokenDialog(
                        NewConfig())),
            (
                "Secret display",
                () =>
                    new SecretDisplayDialog(
                        "Secret",
                        "A long description used only for layout validation.",
                        "SELF-TEST-SECRET",
                        "A long footer used only for layout validation.")),
            (
                "Secure output",
                () =>
                    new SecureOutputDialog(
                        Path.GetTempPath())),
            (
                "Storage maintenance",
                () =>
                    new StorageMaintenanceDialog(
                        NewConfig())),
            (
                "Privileged access",
                () =>
                    new PrivilegedAccessSettingsDialog(
                        NewConfig()))
        ];
    }

    private static void NormalizeDockFillSizes(
        Control root)
    {
        if (root is TabControl tabs)
        {
            var pageTarget =
                tabs.DisplayRectangle.Size;

            if (pageTarget.Width > 0 &&
                pageTarget.Height > 0)
            {
                foreach (TabPage page in
                         tabs.TabPages)
                {
                    if (page.Size !=
                        pageTarget)
                    {
                        page.Size =
                            pageTarget;
                    }
                }
            }
        }

        foreach (Control child in
                 root.Controls)
        {
            // TableLayoutPanel calculates Dock=Fill bounds per cell.
            // Overwriting them with its entire DisplayRectangle makes
            // pinned footers incorrectly fill the whole tab in headless
            // layout tests, producing false clipping errors.
            if (child.Dock ==
                    DockStyle.Fill &&
                child.Parent is not null &&
                child.Parent is not TableLayoutPanel)
            {
                var target =
                    child.Parent.DisplayRectangle.Size;

                if (target.Width > 0 &&
                    target.Height > 0 &&
                    child.Size != target)
                {
                    child.Size =
                        target;
                }
            }

            NormalizeDockFillSizes(
                child);
        }
    }

    /// <summary>
    /// Regression for a visually missing copy button: the old check only
    /// looked up button names, while Hebrew WinForms AutoSize/RTL could
    /// clip both action controls outside their visible parent rectangle.
    /// </summary>
    /// <summary>
    /// Recreate the screenshot regression: DpiAwareForm forced horizontal
    /// FlowLayoutPanel navigation to wrap, leaving only Cancel visible.
    /// These tests display REAL WinForms handles; they never call Graph or
    /// invoke the Configure & Verify operation.
    /// </summary>
    private static void VerifyIntuneSetupWizardNavigation(List<string> failures)
    {
        var config = new CloudAuthConfig
        {
            TenantId = "organizations",
            ClientId = "11111111-1111-4111-8111-111111111111"
        };
        foreach (var language in new[] { "en", "ru", "he" })
        {
            foreach (var fontScale in new[] { 1F, 2F })
            {
                try
                {
                    using var wizard = new IntuneSetupWizardDialog(
                        config, _ => Task.CompletedTask)
                    {
                        StartPosition = FormStartPosition.Manual,
                        ClientSize = new Size(920, 720)
                    };
                    using var font = new Font(UiStyle.BodyFont.FontFamily,
                        UiStyle.BodyFont.Size * fontScale,
                        FontStyle.Regular, GraphicsUnit.Point);
                    wizard.Font = font;
                    wizard.Show();
                    UiDialogLocalization.Apply(wizard, language);

                    if (FindByName(wizard, "IntuneSetupWizardNavigation")
                            is not TableLayoutPanel navigation ||
                        FindByName(wizard, "IntuneSetupWizardFooter")
                            is not TableLayoutPanel footer ||
                        FindByName(wizard, "IntuneSetupWizardBack") is not Button back ||
                        FindByName(wizard, "IntuneSetupWizardNext") is not Button next ||
                        FindByName(wizard, "IntuneSetupWizardCancel") is not Button cancel)
                    {
                        failures.Add("Intune wizard navigation controls are missing: " +
                            language + " / " + fontScale);
                        continue;
                    }

                    var backCell = navigation.GetPositionFromControl(back);
                    var nextCell = navigation.GetPositionFromControl(next);
                    var cancelCell = navigation.GetPositionFromControl(cancel);
                    if (navigation.ColumnCount != 3 ||
                        navigation.RowCount != 1 ||
                        navigation.RightToLeft != RightToLeft.No ||
                        backCell.Column != 0 || backCell.Row != 0 ||
                        nextCell.Column != 1 || nextCell.Row != 0 ||
                        cancelCell.Column != 2 || cancelCell.Row != 0 ||
                        !ReferenceEquals(wizard.AcceptButton, next) ||
                        !ReferenceEquals(wizard.CancelButton, cancel))
                        failures.Add("Intune wizard navigation must remain a single row with keyboard actions: " +
                            language + " / " + fontScale);

                    foreach (var step in new[] { 0, 1, 2 })
                    {
                        wizard.PreviewStepForUiSelfTest(step);
                        UiDialogLocalization.Apply(wizard, language);
                        wizard.PerformLayout();
                        Application.DoEvents();

                        var viewport = wizard.RectangleToScreen(wizard.ClientRectangle);
                        var footerBounds = footer.RectangleToScreen(footer.ClientRectangle);
                        var navBounds = navigation.RectangleToScreen(navigation.ClientRectangle);
                        if (!viewport.Contains(footerBounds) ||
                            !footerBounds.Contains(navBounds))
                        {
                            failures.Add("Intune wizard footer is clipped: " +
                                language + " / " + fontScale + " / step " + step);
                            continue;
                        }

                        foreach (var button in new[] { back, next, cancel })
                        {
                            var buttonBounds = button.RectangleToScreen(
                                button.ClientRectangle);
                            if (!button.Visible || button.IsDisposed ||
                                buttonBounds.Width < UiStyle.MinimumButtonWidth ||
                                buttonBounds.Height < UiStyle.MinimumButtonHeight ||
                                !navBounds.Contains(buttonBounds) ||
                                !footerBounds.Contains(buttonBounds) ||
                                !viewport.Contains(buttonBounds))
                            {
                                failures.Add("Intune wizard button clipped or missing: " +
                                    button.Name + " / " + language + " / " +
                                    fontScale + " / step " + step);
                            }
                        }

                        var expectedAction = step == 2
                            ? UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Configure & Verify")
                            : UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Next");
                        if (next.Text != expectedAction)
                            failures.Add("Intune wizard Next caption is not localized: " +
                                language + " / step " + step);
                    }

                    if (wizard.SetupResult is not null)
                        failures.Add("Navigation-only wizard test unexpectedly ran setup.");
                }
                catch (Exception ex)
                {
                    failures.Add("Intune wizard navigation regression (" +
                        language + " / " + fontScale + "): " +
                        DiagnosticRedaction.Sanitize(ex.Message));
                }
            }
        }
    }

    private static void VerifyDiagnosticCopyVisibility(List<string> failures)
    {
        try
        {
            using var form = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true)
            {
                StartPosition = FormStartPosition.Manual,
                ClientSize = new Size(1024, 768)
            };
            // A hidden Form reports Control.Visible=false for everything
            // and its AutoSize rows may not lay out. Test actual WinForms
            // handles and visibility, not just Controls.Find/Text.
            form.Show();
            Application.DoEvents();

            if (FindByName(form, "GlobalStatusBar") is not StatusStrip initialStatus ||
                initialStatus.RightToLeft != RightToLeft.No ||
                initialStatus.Items.Find("GlobalCopyDiagnostics", false)
                    .FirstOrDefault() is not ToolStripStatusLabel initialCopy ||
                initialCopy.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                    "en", "Copy diagnostics"))
                failures.Add("Initial LTR status-bar copy link is absent.");

            for (var i = 0; i < 2; i++)
            {
                using var largeFont = i == 0
                    ? new Font(UiStyle.BodyFont.FontFamily,
                        UiStyle.BodyFont.Size, FontStyle.Regular,
                        GraphicsUnit.Point)
                    : new Font(UiStyle.BodyFont.FontFamily,
                        UiStyle.BodyFont.Size * 2F, FontStyle.Regular,
                        GraphicsUnit.Point);
                form.Font = largeFont;
                form.PreviewShowUiPerformanceReportForLayoutTest();

                foreach (var language in new[] { "he", "ru", "en", "he" })
                {
                    form.PreviewShellLanguageForLayoutTest(language);
                    form.PerformLayout();
                    Application.DoEvents();

                    if (FindByName(form, "ApplicationDiagnostics") is not
                        UiDiagnosticPanel banner ||
                        !banner.AreActionsWithinVisibleBounds())
                        failures.Add("Copy Diagnostics is clipped or not visible: " +
                            language + ", font scale " + (i == 0 ? "100" : "200") + "%.");

                    if (FindByName(form, "GlobalStatusBar") is not StatusStrip status ||
                        status.Items.Find("GlobalCopyDiagnostics", false)
                            .FirstOrDefault() is not ToolStripStatusLabel copyLink ||
                        !copyLink.Available ||
                        copyLink.Placement != ToolStripItemPlacement.Main ||
                        copyLink.Bounds.Width <= 0 ||
                        copyLink.Bounds.Right > status.ClientSize.Width + 2 ||
                        copyLink.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Copy diagnostics"))
                        failures.Add("Persistent status bar copy link is clipped: " +
                            language + ", font scale " + (i == 0 ? "100" : "200") + "%.");
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add("Diagnostic copy visibility regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyHelpdeskChecklistLocalization(List<string> failures)
    {
        var englishSteps = new[]
        {
            "Install the Windows service on a test workstation (administrator)",
            "Configure a scoped RecoveryReaders group; disable admin bypass",
            "Verify AD and Entra / Intune delegation independently",
            "Provision protected audit and restart the service (administrator)",
            "Sign in as an authorized non-admin and run Access Health",
            "Verify unauthorized access is denied and secrets are never logged",
            "Validate protected journal, RDP, 100-200% DPI and rollback"
        };

        try
        {
            using var main = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true);

            foreach (var language in new[] { "en", "ru", "he" })
            {
                main.PreviewShellLanguageForLayoutTest(language);
                using var wizard = main.CreateHelpdeskDeploymentWizard();
                UiDialogLocalization.Apply(wizard, language);

                var list = FindByName(wizard, "HelpdeskDeploymentSteps")
                    as CheckedListBox;
                var progress = FindByName(wizard, "HelpdeskDeploymentProgress")
                    as Label;
                if (list is null || progress is null ||
                    list.Items.Count != englishSteps.Length)
                {
                    failures.Add("Helpdesk localized checklist is missing items: " +
                        language);
                    continue;
                }

                for (var i = 0; i < englishSteps.Length; i++)
                {
                    var expected = UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, englishSteps[i]);
                    if (expected == englishSteps[i] && language != "en")
                        failures.Add("Helpdesk checklist caption missing from catalog: " +
                            i + " (" + language + ")");
                    if (!string.Equals(list.Items[i] as string, expected,
                            StringComparison.Ordinal))
                        failures.Add("Helpdesk checklist left untranslated: " +
                            i + " (" + language + ")");
                }

                var template = UiLanguageService.Translate(
                    language, "HelpdeskDeploymentProgressTemplate");
                var expectedZero = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    template, 0, englishSteps.Length);
                if (progress.Text != expectedZero)
                    failures.Add("Helpdesk initial progress translation incorrect: " +
                        language);

                // This checklist is informational, not an authorization or
                // service operation. Its checked states must remain tied to
                // positions, independent of translated display text.
                list.SetItemChecked(0, true);
                var expectedOne = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    template, 1, englishSteps.Length);
                if (!list.GetItemChecked(0) || progress.Text != expectedOne)
                    failures.Add("Helpdesk progress counter lost state: " +
                        language);

                list.SetItemChecked(0, false);
                if (list.GetItemChecked(0) || progress.Text != expectedZero)
                    failures.Add("Helpdesk checklist did not clear its count: " +
                        language);
            }
        }
        catch (Exception ex)
        {
            failures.Add("Helpdesk EN/RU/HE checklist regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyHebrewIdentityDirection(List<string> failures)
    {
        try
        {
            using var form = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true);

            // ToolStripItem.Tag can carry command state; language switching
            // must not overwrite it with the original caption.
            var copyItemBeforeTranslation = form.MainMenuStrip?.Items
                .Find("CopyUiPerformanceReportMenuItem", true)
                .OfType<ToolStripMenuItem>().FirstOrDefault();
            var protectedMenuTag = new object();
            if (copyItemBeforeTranslation is not null)
                copyItemBeforeTranslation.Tag = protectedMenuTag;

            // Exercise recursion: a static caption inside an unknown nested
            // menu must be translated in all three languages.
            var topTools = form.MainMenuStrip?.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.DropDownItems
                    .OfType<ToolStripMenuItem>().Any(child =>
                        child.Name == "CopyUiPerformanceReportMenuItem"));
            using var nestedMenu = new ToolStripMenuItem("Nested menu fixture");
            using var nestedItem = new ToolStripMenuItem("Windows Event Log");
            nestedMenu.DropDownItems.Add(nestedItem);
            topTools?.DropDownItems.Add(nestedMenu);

            form.PreviewShellLanguageForLayoutTest("he");
            form.PreviewShowUiPerformanceReportForLayoutTest();

            var banner = FindByName(form, "ApplicationDiagnostics") as UiDiagnosticPanel;
            if (banner is null || !banner.HasActiveDiagnostics ||
                FindByName(banner, "CopyDiagnosticsButton") is not Button ||
                FindByName(banner, "DismissDiagnosticsButton") is not Button)
                failures.Add("Diagnostics banner lost the copy/dismiss controls.");
            var copyUiMenu = form.MainMenuStrip?.Items
                .Find("CopyUiPerformanceReportMenuItem", true)
                .OfType<ToolStripMenuItem>().FirstOrDefault();
            if (copyUiMenu is null ||
                copyUiMenu.ShortcutKeys != (Keys.Control | Keys.Shift | Keys.P) ||
                !copyUiMenu.ShowShortcutKeys)
                failures.Add("Global UI performance copy shortcut is missing or not registered.");

            var tabs = FindByName(form, "MainWorkspaceTabs") as TabControl;
            if (tabs is null || !tabs.RightToLeftLayout ||
                tabs.RightToLeft != RightToLeft.Yes ||
                tabs.DrawMode != TabDrawMode.Normal ||
                tabs.TabPages.Count < 6 ||
                tabs.TabPages[0].Text != UiLanguageService.Translate("he", "Start") ||
                tabs.TabPages[3].Name != "DevicesWorkspace")
                failures.Add("Hebrew mirrored navigation does not retain source-qualified tab identities.");

            if (FindByName(form, "UpdateCenterCompact") is not CheckBox updateDensity ||
                updateDensity.Text != UiLanguageService.Translate("he", "UpdateCenterCompact"))
                failures.Add("Hebrew Update Center switch did not localize.");

            // All administrative controls remain in the form, but verbose
            // optional descriptions start collapsed. They can be expanded
            // from one consistent Tools menu action in every language.
            var helpItem = form.MainMenuStrip?.Items
                .Find("UiShowSectionExplanations", true)
                .OfType<ToolStripMenuItem>().FirstOrDefault();
            if (helpItem is null || helpItem.Checked ||
                helpItem.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                    "he", "Show explanations"))
                failures.Add("Compact administrative explanation menu was not available in Hebrew.");
            if (helpItem is not null)
            {
                helpItem.Checked = true;
                if (!helpItem.Checked)
                    failures.Add("Could not expand optional admin section descriptions.");
                helpItem.Checked = false;
            }
            var optionalCount = 0;
            var explanationQueue = new Stack<Control>();
            explanationQueue.Push(form);
            while (explanationQueue.Count > 0)
            {
                var current = explanationQueue.Pop();
                if (current is Label label &&
                    string.Equals(label.Tag as string,
                        "BKB.OptionalSectionExplanation", StringComparison.Ordinal))
                    optionalCount++;
                foreach (Control child in current.Controls)
                    explanationQueue.Push(child);
            }
            if (optionalCount < 8)
                failures.Add("Administrative explanations were removed instead of safely collapsed.");
            // WinForms originally mirrored the Form itself, recursively
            // resetting native child windows and sometimes blanking Hebrew
            // captions after a language switch. Only the tab strip may mirror.
            if (form.RightToLeftLayout || form.RightToLeft == RightToLeft.Yes)
                failures.Add("Hebrew language selection must not mirror the MainForm HWND.");
            if (form.MainMenuStrip?.Items.Find("UiLanguageMenu", false).FirstOrDefault() is
                not ToolStripMenuItem languageItem ||
                languageItem.Text != UiLanguageService.Translate("he", "Language") ||
                !languageItem.AutoSize || languageItem.Padding.Horizontal < 12)
                failures.Add("Hebrew Language menu was clipped or lost its text layout.");
            if (tabs is not null && tabs.TabPages.Cast<TabPage>().Any(
                    page => page.RightToLeft != RightToLeft.No))
                failures.Add("Main workspace pages must keep child/secret controls LTR.");

            // Runtime-created and duplicate named actions must be translated.
            // They are deliberately added after the first Hebrew pass to catch
            // the old one-time control tree snapshot regression.
            var dynamicCaptionsPanel = new FlowLayoutPanel
            {
                Name = "LateLocalizedActionsFixture",
                AutoSize = true
            };
            tabs?.TabPages[0].Controls.Add(dynamicCaptionsPanel);
            using var lateAction1 = UiStyle.CreateActionButton("Untranslated late button");
            lateAction1.Name = "HomeRecoverySteps";
            using var lateAction2 = UiStyle.CreateActionButton("Another untranslated button");
            lateAction2.Name = "HomeRecoverySteps";
            dynamicCaptionsPanel.Controls.Add(lateAction1);
            dynamicCaptionsPanel.Controls.Add(lateAction2);
            if (lateAction1.Text != UiLanguageService.Translate("he", "MainRecoverySteps") ||
                lateAction2.Text != UiLanguageService.Translate("he", "MainRecoverySteps") ||
                lateAction1.RightToLeft != RightToLeft.Yes ||
                lateAction2.RightToLeft != RightToLeft.Yes)
                failures.Add("Late/duplicate WinForms controls were not translated in active Hebrew UI.");
            // Legacy UI refreshes may transiently clear Text. Only fixed,
            // registered captions are repaired; dynamic status is unaffected.
            lateAction2.Text = string.Empty;
            if (lateAction2.Text != UiLanguageService.Translate("he", "MainRecoverySteps"))
                failures.Add("Blank fixed Hebrew caption was not restored.");


            var translatedControls = new (string Name, string Key)[]
            {
                ("HomeUseWindowsIdentity", "MainWindowsAccount"),
                ("HomeUseOtherAccount", "MainOtherAdAccount"),
                ("HomeConnectAdButton", "MainConnectAd"),
                ("HomeConnectionToolsButton", "MainConnectionTools"),
                ("HomeRecoverySteps", "MainRecoverySteps"),
                ("HomeBitLockerDiagnosticLink", "MainViewCommands"),
                ("HomeLoadLapsHistoryButton", "MainLoadHistory"),
                ("HelpdeskOpenDeviceWorkspaceButton", "MainOpenDeviceSearch"),
                ("RunEnvironmentCheckButton", "MainEnvironmentCheck"),
                ("RunUiPerformanceDiagnosticsButton", "MainUiPerformance")
            };
            var originalConnectionSelection =
                (FindByName(form, "HomeConnectionSource") as ComboBox)?.SelectedIndex;
            var connectionChoiceEnglish = new[]
            {
                "Choose a source",
                "Active Directory",
                "Microsoft Entra / Intune (Microsoft 365)",
                "Both: Active Directory + Microsoft Entra"
            };
            foreach (var language in new[] { "he", "en", "ru", "he" })
            {
                form.PreviewShellLanguageForLayoutTest(language);
                var expectedEventLog = UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Windows Event Log");
                var expectedLatestRelease = UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Latest GitHub release");
                var latestReleaseItem = form.MainMenuStrip?.Items
                    .Find("LatestGithubReleaseMenuItem", true)
                    .FirstOrDefault();
                if (nestedItem.Text != expectedEventLog ||
                    latestReleaseItem?.Text != expectedLatestRelease)
                    failures.Add("Nested or top Tools menu caption not translated: " +
                        language);
                if (copyItemBeforeTranslation is not null &&
                    !ReferenceEquals(copyItemBeforeTranslation.Tag, protectedMenuTag))
                    failures.Add("Language switch overwrote ToolStrip command Tag: " +
                        language);
                // The performance report must not disappear when a
                // high-DPI WinForms TabControl changes RTL orientation.
                if (banner is null || !banner.HasActiveDiagnostics ||
                    FindByName(banner, "CopyDiagnosticsButton") is not Button copyButton ||
                    copyButton.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Copy diagnostics"))
                    failures.Add("Diagnostics copy action disappeared or lost translation: " +
                        language);
                if (copyUiMenu is null ||
                    copyUiMenu.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Copy UI diagnostics"))
                    failures.Add("Always-visible Tools diagnostic copy action lost its caption: " +
                        language);
                // This picker is an enum-by-index, not a text-matched choice:
                // EN/RU/HE switches may never select a different identity
                // provider or trigger an asynchronous new connection.
                if (FindByName(form, "HomeConnectionSource") is not ComboBox source ||
                    source.Items.Count != connectionChoiceEnglish.Length ||
                    source.SelectedIndex != originalConnectionSelection ||
                    connectionChoiceEnglish.Where((english, index) =>
                        source.Items[index] as string !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(language, english)).Any())
                    failures.Add("Start connection source picker translation changed the selected source: " +
                        language);
                if (FindByName(form, "HomeConnectionOptionsToggle") is not Button optionsToggle)
                    failures.Add("Connection options toggle not found.");
                else
                {
                    if (optionsToggle.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "More connection options..."))
                        failures.Add("Collapsed connection options were not translated: " + language);
                    form.PreviewSetConnectionOptionsExpandedForUiTest(true);
                    if (optionsToggle.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Hide connection options"))
                        failures.Add("Expanded connection options were not translated: " + language);
                    form.PreviewSetConnectionOptionsExpandedForUiTest(false);
                    if (optionsToggle.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "More connection options..."))
                        failures.Add("Collapsing connection options lost translation: " + language);
                }
                if (helpItem is not null &&
                    helpItem.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Show explanations"))
                    failures.Add("Section explanations menu was not translated: " + language);
                if (tabs is not null &&
                    (tabs.DrawMode != TabDrawMode.Normal ||
                     tabs.RightToLeftLayout != (language == "he") ||
                     tabs.RightToLeft != (language == "he"
                         ? RightToLeft.Yes : RightToLeft.No)))
                    failures.Add("Native main tabs were recreated or lost correct RTL/LTR orientation.");
                foreach (var lateAction in new[] { lateAction1, lateAction2 })
                {
                    if (lateAction.Text != UiLanguageService.Translate(language, "MainRecoverySteps") ||
                        lateAction.RightToLeft != (language == "he" ? RightToLeft.Yes : RightToLeft.No))
                        failures.Add("Dynamic duplicate caption remained blank, stale or LTR-invalid: " +
                            language);
                }
                foreach (var (name, key) in translatedControls)
                {
                    var control = FindByName(form, name);
                    if (control is null ||
                        string.IsNullOrWhiteSpace(control.Text) ||
                        control.Text != UiLanguageService.Translate(language, key))
                        failures.Add("Language switch left an empty or stale caption: " +
                            name + " (" + language + ")");
                }
                // Verify the actual unnamed title/description/button controls
                // from the reported Export & Automation screenshot. This was
                // previously all English except the top-level Settings tab.
                foreach (var english in new[]
                {
                    "Export & Automation", "Recovery Export Storage",
                    "Export Scopes", "Output root:", "Subdirectory:",
                    "Save Export Settings", "Secure Output...",
                    "Remove Selected", "Check All", "Run Export", "Dry Run",
                    "Override publish guards (row drop / scope change)"
                })
                {
                    var expected = UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, english);
                    var found = false;
                    var pending = new Stack<Control>();
                    pending.Push(form);
                    while (pending.Count > 0)
                    {
                        var current = pending.Pop();
                        if (string.Equals(current.Text, expected,
                                StringComparison.Ordinal))
                            found = true;
                        foreach (Control child in current.Controls)
                            pending.Push(child);
                    }
                    if (!found)
                        failures.Add("Export/admin caption was not translated: " +
                            english + " (" + language + ")");
                }
                // Headers are separate native ListView items, not Controls.
                var expectedScope = UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Scope");
                var headersFound = false;
                var listPending = new Stack<Control>();
                listPending.Push(form);
                while (listPending.Count > 0)
                {
                    var current = listPending.Pop();
                    if (current is ListView list && list.Columns
                        .Cast<ColumnHeader>().Any(h => h.Text == expectedScope))
                        headersFound = true;
                    foreach (Control child in current.Controls)
                        listPending.Push(child);
                }
                if (!headersFound)
                    failures.Add("Export scope ListView headers were not localized: " +
                        language);

                // The other core grids must also localize. Assert
                // names/selection and data remain untouched by UI language.
                foreach (var (gridName, columnName, english) in new[]
                {
                    ("HomeRecentComputers", "LastAction", "Last action"),
                    ("HomeRecentComputers", "LastUsed", "Last used"),
                    ("HomeUnifiedSearchResults", "BitLockerDate", "BitLocker key date"),
                    ("HomeUnifiedSearchResults", "LapsDate", "LAPS metadata date"),
                    ("BitLockerSearchResults", "KeyDate", "Key date"),
                    ("BitLockerSearchResults", "Latest", "Latest"),
                    ("LapsSearchResults", "ComputerId", "Device / AD object ID"),
                    ("LapsKeyResults", "RecordType", "Current / history"),
                    ("LapsKeyResults", "Account", "Account"),
                    ("LapsKeyResults", "Age", "Age"),
                    ("LapsKeyResults", "Expires", "Expires")
                })
                {
                    if (FindByName(form, gridName) is not DataGridView grid ||
                        grid.Columns[columnName] is not DataGridViewColumn column ||
                        column.HeaderText !=
                            UiStaticCaptionCatalog.TranslateOrOriginal(language, english))
                        failures.Add("Core data grid header untranslated: " +
                            gridName + "/" + columnName + " (" + language + ")");
                }

                // DataGridViewColumn is not part of the Control tree.
                // LAPS Coverage must preserve column identity and data while
                // localizing headers without clipping at high DPI.
                if (FindByName(form, "LapsCoverageDevices") is DataGridView lapsGrid)
                {
                    foreach (var (columnName, english) in new[]
                    {
                        ("Guid", "AD object GUID"),
                        ("OU", "OU / distinguished name"),
                        ("Backup", "Visible LAPS metadata"),
                        ("Expiry", "Expiration (UTC)"),
                        ("State", "Expiry state"),
                        ("LastLogon", "Last AD logon (UTC)")
                    })
                    {
                        var expected = UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, english);
                        if (lapsGrid.Columns[columnName] is not DataGridViewColumn column ||
                            string.IsNullOrWhiteSpace(column.HeaderText) ||
                            column.HeaderText != expected)
                            failures.Add("LAPS grid header not localized: " +
                                english + " (" + language + ")");
                    }
                }
                if (FindByName(form, "LapsCoverageChanges") is DataGridView changeGrid)
                    foreach (var english in new[] { "Change type", "Previous", "Current" })
                        if (!changeGrid.Columns.Cast<DataGridViewColumn>().Any(
                            column => column.HeaderText ==
                                UiStaticCaptionCatalog.TranslateOrOriginal(language, english)))
                            failures.Add("LAPS comparison grid header not localized: " +
                                english + " (" + language + ")");

                if (tabs is not null &&
                    tabs.TabPages.Cast<TabPage>().Any(
                        page => string.IsNullOrWhiteSpace(page.Text)))
                    failures.Add("Language switch left an empty main workspace tab.");
            }

            if (FindByName(form, "LapsCoverageChanges") is DataGridView lateGrid)
            {
                // Use centralized column factory (HiDPI/WinForms policy).
                using var lateColumn = UiStyle.CreateSortableTextColumn(
                    "LateLocalizedHeaderFixture", "Expiration (UTC)");
                lateGrid.Columns.AddRange([lateColumn]);
                if (lateColumn.HeaderText != UiStaticCaptionCatalog.TranslateOrOriginal(
                        "he", "Expiration (UTC)"))
                    failures.Add("Late-added DataGridView column was not localized in Hebrew.");
                form.PreviewShellLanguageForLayoutTest("ru");
                if (lateColumn.HeaderText != UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Expiration (UTC)"))
                    failures.Add("Late-added DataGridView column did not switch to Russian.");
                lateGrid.Columns.Remove(lateColumn);
                form.PreviewShellLanguageForLayoutTest("he");
            }

            // Disposed buttons must never remain in the active caption cache.
            // Replacing a control on a live page must pick up Hebrew instantly.
            dynamicCaptionsPanel.Controls.Remove(lateAction1);
            lateAction1.Dispose();
            using var replacementAction = UiStyle.CreateActionButton("Replacement action");
            replacementAction.Name = "HomeRecoverySteps";
            dynamicCaptionsPanel.Controls.Add(replacementAction);
            if (replacementAction.Text != UiLanguageService.Translate("he", "MainRecoverySteps"))
                failures.Add("Recreated action lost active Hebrew localization.");
            form.PreviewShellLanguageForLayoutTest("ru");
            if (replacementAction.Text != UiLanguageService.Translate("ru", "MainRecoverySteps"))
                failures.Add("Recreated action did not follow subsequent language switches.");
            form.PreviewShellLanguageForLayoutTest("he");

            if (FindByName(form, "UpdateCenterCompact") is not CheckBox updateDensityFinal ||
                updateDensityFinal.Text != UiLanguageService.Translate("he", "UpdateCenterCompact"))
                failures.Add("Hebrew Update Center switch lost its caption after repeated switching.");

            form.PreviewShellLanguageForLayoutTest("ru");
            if (FindByName(form, "UpdateCenterCompact") is not CheckBox russianDensity ||
                russianDensity.Text != UiLanguageService.Translate("ru", "UpdateCenterCompact"))
                failures.Add("Russian Update Center switch did not localize.");
            form.PreviewShellLanguageForLayoutTest("he");

            foreach (var name in new[]
            {
                "HomeUnifiedQuery", "BitLockerSearchQuery",
                "LapsLiveSearchQuery", "UnifiedDeviceSearchQuery",
                "BitLockerRecoverySecretField", "LapsPasswordValue",
                "EntraBitLockerSecretField"
            })
            {
                if (FindByName(form, name) is not TextBox literal ||
                    literal.RightToLeft != RightToLeft.No)
                    failures.Add("Hebrew shell changed identity or secret field direction: " + name);
            }
        }
        catch (Exception ex)
        {
            failures.Add("Hebrew main shell orientation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyLapsHelpdeskStates(
        List<string> failures)
    {
        var candidate = new LapsSearchResult
        {
            ComputerName = "IT-EUGENEB11"
        };

        if (HelpdeskStatus.Laps(null) != "Not verified" ||
            HelpdeskStatus.Laps(candidate) != "Not verified" ||
            new HomeSearchResult().LapsStatus != "Not verified")
        {
            failures.Add("Start LAPS missing metadata must be Not verified.");
        }

        var selected = new HomeSearchResult
        {
            ComputerName = candidate.ComputerName,
            LapsCandidateFound = false,
            LapsStatus = "Not verified"
        };
        if (!HelpdeskStatus.CanAttemptLapsRead(true, selected) ||
            HelpdeskStatus.CanAttemptLapsRead(false, selected) ||
            HelpdeskStatus.CanAttemptLapsRead(true, null))
        {
            failures.Add("Start LAPS action must permit explicit reads of selected AD computers without metadata.");
        }

        if (HelpdeskStatus.ResolveHomeDefaultAction(null) !=
                HomeDefaultAction.None ||
            HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
                HomeDefaultAction.ChooseRecoveryType ||
            HelpdeskStatus.CanAttemptBitLockerSearch(false, selected) ||
            !HelpdeskStatus.CanAttemptBitLockerSearch(true, selected))
        {
            failures.Add("Unverified metadata must require an explicit operator choice.");
        }

        selected.LapsCandidateFound = true;
        selected.LapsStatus = "Detected";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.Laps)
        {
            failures.Add("LAPS-only detected metadata must open LAPS.");
        }

        selected.LapsStatus = "Expired";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.Laps)
        {
            failures.Add("Expired LAPS metadata must still open LAPS.");
        }

        selected.LapsStatus = "Not verified";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.ChooseRecoveryType)
        {
            failures.Add("Unverified LAPS metadata must not route automatically.");
        }

        selected.BitLockerKeyCount = 1;
        selected.LapsCandidateFound = true;
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.BitLocker)
        {
            failures.Add("When BitLocker and LAPS coexist, the default action must be BitLocker.");
        }

        var cloud = new HomeSearchResult
        {
            ComputerName = "PC-CLOUD",
            IsCloudRecord = true,
            SourceIdentity = "INTUNE:MANAGED:managed-01",
            BitLockerKeyCount = 1
        };
        if (HelpdeskStatus.ResolveHomeDefaultAction(cloud) !=
            HomeDefaultAction.Devices)
        {
            failures.Add("Cloud metadata must route to Devices and not AD Recovery.");
        }

        selected.BitLockerKeyCount = 0;
        selected.BitLockerLookupFailed = true;
        selected.LapsCandidateFound = true;
        selected.LapsStatus = "Detected";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.ChooseRecoveryType)
        {
            failures.Add("A failed BitLocker lookup must not auto-route LAPS as if there were no key.");
        }

        selected.BitLockerLookupFailed = false;
        selected.IdentityAmbiguous = true;
        selected.BitLockerKeyCount = 1;
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
                HomeDefaultAction.ChooseRecoveryType ||
            HelpdeskStatus.CanAttemptLapsRead(true, selected) ||
            HelpdeskStatus.CanAttemptBitLockerSearch(true, selected))
        {
            failures.Add("Ambiguous AD object identity must block both quick recovery actions.");
        }

        selected.IdentityAmbiguous = false;
        if (!HelpdeskStatus.HasIdentityCollision(
                [
                    new RecoverySearchResult
                    {
                        ComputerName = "PC-DUP",
                        ComputerDistinguishedName = "CN=PC-DUP,OU=Old,DC=example,DC=test"
                    }
                ],
                [
                    new LapsSearchResult
                    {
                        ComputerName = "PC-DUP",
                        ComputerId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        ComputerDistinguishedName = "CN=PC-DUP,OU=New,DC=example,DC=test"
                    }
                ],
                "PC-DUP") ||
            HelpdeskStatus.HasIdentityCollision(
                [],
                [
                    new LapsSearchResult
                    {
                        ComputerName = "PC-SINGLE",
                        ComputerId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
                    }
                ],
                "PC-SINGLE"))
        {
            failures.Add("Stable AD identifiers are not being used to detect duplicate computer names.");
        }

        if (!HelpdeskStatus.ShouldRetryBitLockerInDomain(
                0, "OU=Win11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il") ||
            HelpdeskStatus.ShouldRetryBitLockerInDomain(
                1, "OU=Win11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il") ||
            HelpdeskStatus.ShouldRetryBitLockerInDomain(
                0, "DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il"))
        {
            failures.Add("BitLocker exact-computer domain retry must occur only after a scoped metadata miss.");
        }

        selected.BitLockerOutsideSelectedScope = true;
        if (!HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, "OU=Windows11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il") ||
            HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, "DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il"))
        {
            failures.Add("Recovery navigation must reuse full domain scope when the key was found outside the selected OU.");
        }

        selected.BitLockerOutsideSelectedScope = false;
        selected.BitLockerKeyCount = 0;
        if (!HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, "OU=Windows11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il"))
        {
            failures.Add("Unverified BitLocker navigation must retry the domain scope.");
        }

        var onlyBitLocker = new HomeSearchResult
        {
            BitLockerKeyCount = 1,
            LapsCandidateFound = true,
            LapsStatus = "Not verified"
        };
        var onlyLaps = new HomeSearchResult
        {
            LapsCandidateFound = true,
            LapsStatus = "Detected"
        };
        var both = new HomeSearchResult
        {
            BitLockerKeyCount = 1,
            LapsCandidateFound = true,
            LapsStatus = "Expired"
        };
        if (HelpdeskStatus.VisibleHomeServices(null) != (false, false) ||
            HelpdeskStatus.VisibleHomeServices(cloud) != (false, false) ||
            HelpdeskStatus.VisibleHomeServices(onlyBitLocker) != (true, false) ||
            HelpdeskStatus.VisibleHomeServices(onlyLaps) != (false, true) ||
            HelpdeskStatus.VisibleHomeServices(both) != (true, true) ||
            HelpdeskStatus.VisibleHomeServices(new HomeSearchResult()) != (true, true))
        {
            failures.Add(
                "Start cards must show only verified services while leaving completely unknown results diagnosable.");
        }

        candidate.ExpiresAtUtc = DateTime.UtcNow.AddDays(10);
        if (HelpdeskStatus.Laps(candidate) != "Detected")
            failures.Add("Start LAPS future expiration must be Detected.");

        candidate.ExpiresAtUtc = DateTime.UtcNow.AddDays(-10);
        if (HelpdeskStatus.Laps(candidate) != "Expired")
            failures.Add("Start LAPS past expiration must be Expired.");

        using var row = new LapsPasswordEntry
        {
            Attribute = "msLAPS-EncryptedPassword",
            Status = LapsPasswordStatus.AccessDenied
        };
        if (HelpdeskStatus.LapsEntry(row) != "Decrypt unauthorized")
            failures.Add("Encrypted LAPS AccessDenied must be Decrypt unauthorized.");

        row.Status = LapsPasswordStatus.DecryptionFailed;
        if (HelpdeskStatus.LapsEntry(row) != "Decrypt failed")
            failures.Add("Other encrypted LAPS decryption failures must not be called Unauthorized.");

        row.Status = LapsPasswordStatus.NotReturned;
        if (HelpdeskStatus.LapsEntry(row) != "Not returned")
            failures.Add("Unreturned LAPS record must not imply no backup.");
    }

    private static void VerifyForm(
        DpiAwareForm form,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (form.AutoScaleMode !=
            AutoScaleMode.Dpi)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: AutoScaleMode is {form.AutoScaleMode}, expected Dpi.");
        }

        if (form is MainForm main)
        {
            if (form.AutoScroll)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: MainForm must use page scrolling, not form scrolling.");
            }

            VerifyMainViewportFill(main, formName, scenario, failures);
            VerifyNestedWorkspaceViewport(main, formName, scenario, failures);
            VerifyMainFormTabs(form, formName, scenario, failures);
            var healthResults = FindByName(main, "AccessHealthResults") as ListView;
            if (healthResults is not null && !healthResults.ShowItemToolTips)
                failures.Add($"{formName}/{scenario.Name}: Access Health long diagnostics must have full-text hover access.");
            var toolsMenu = main.MainMenuStrip?.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(x => x.Text == "Tools");
            if (toolsMenu is null ||
                toolsMenu.DropDownItems.Find("RecoveryStepsMenu", false).Length != 1 ||
                toolsMenu.DropDownItems.Find("AccessHealthMenu", false).Length != 1)
                failures.Add($"{formName}/{scenario.Name}: global Recovery Steps and Access Health shortcuts must be discoverable from Tools.");
            if (FindByName(main, "HomeRecoverySteps") is not Button contextSteps ||
                contextSteps.Enabled)
                failures.Add($"{formName}/{scenario.Name}: Start Recovery Steps button should exist but remain disabled without a selected device.");
            VerifySelectedHomeRecoveryLayout(main, formName, scenario, failures);
            VerifyHomeStatusGlyphColumn(main, formName, scenario, failures);
        }
        else
        {
            if (!form.AutoScroll)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: dialog scrolling is disabled.");
            }
        }
        if (form is RecoveryWorkflowDialog)
        {
            var expectedCloud = formName.StartsWith("Hebrew", StringComparison.Ordinal);
            var expectedHebrew = expectedCloud;
            if (form.RightToLeftLayout != expectedHebrew ||
                (expectedCloud
                    ? FindByName(form, "RecoveryRouteDevices") is not Button dev || !dev.Enabled
                    : FindByName(form, "RecoveryRouteBitLocker") is not Button bit || !bit.Enabled ||
                      FindByName(form, "RecoveryRouteLaps") is not Button laps || !laps.Enabled))
                failures.Add($"{formName}/{scenario.Name}: translated recovery guide lacks correct RTL or source-specific metadata navigation.");
        }
        if (form is not MainForm)
        {
            VerifyDialogKeyboardBehavior(
                form,
                formName,
                scenario,
                failures);

            VerifyControlTree(
                form,
                formName,
                scenario,
                failures);
        }
    }

    private static void VerifyMainViewportFill(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var shell = FindByName(main, "MainLayoutRoot");
        var tabs = FindByName(main, "MainWorkspaceTabs");
        var start = FindByName(main, "HomeLayoutRoot");
        if (shell is null || tabs is null || start is null)
        {
            failures.Add($"{formName}/{scenario.Name}: docked workspace layout is missing.");
            return;
        }

        // Tests the real resizing logic, not NormalizeDockFillSizes: this
        // catches the formerly tiny, top-left Start viewport after maximize.
        main.ClientSize = new Size(
            Math.Max(1500, scenario.ClientSize.Width + 240),
            Math.Max(900, scenario.ClientSize.Height + 140));
        main.PerformLayout();
        main.ReflowMainViewport();
        main.PerformLayout();

        if (shell.Dock != DockStyle.Fill ||
            tabs.Dock != DockStyle.Fill ||
            start.Dock != DockStyle.Fill ||
            shell.Width < main.ClientSize.Width * 0.90 ||
            tabs.Width < shell.ClientSize.Width * 0.90)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: maximized window leaves a small top-left workspace instead of filling the client area.");
        }
    }

    /// <summary>
    /// Recreate the real screenshot failure: leave a selected Dock=Fill child
    /// at a provisional narrow size even though its outer tab is full width.
    /// Verify that one production reflow (not test-only normalization) repairs
    /// Start, BitLocker, LAPS, Helpdesk, Devices, Diagnostics, Settings, and
    /// every selected nested page at compact and maximized RDP viewport sizes.
    /// </summary>
    private static void VerifyNestedWorkspaceViewport(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (FindByName(main, "MainWorkspaceTabs") is not TabControl tabs)
            return;

        var originalSize = main.ClientSize;
        var originalTab = tabs.SelectedTab;
        try
        {
            var testSizes = new[]
            {
                scenario.ClientSize,
                new Size(
                    Math.Max(1500, scenario.ClientSize.Width + 240),
                    Math.Max(900, scenario.ClientSize.Height + 140))
            };

            foreach (var size in testSizes)
            {
                main.ClientSize = size;
                main.PerformLayout();
                NormalizeDockFillSizes(main);

                foreach (TabPage page in tabs.TabPages)
                {
                    tabs.SelectedTab = page;
                    Application.DoEvents();
                    NormalizeDockFillSizes(main);
                    main.ReflowMainViewport();
                    VerifySelectedPage(page, page.Text, size.Width);
                }
            }
        }
        finally
        {
            if (originalTab is not null)
                tabs.SelectedTab = originalTab;
            main.ClientSize = originalSize;
            main.ReflowMainViewport();
        }

        void VerifySelectedPage(TabPage page, string route, int windowWidth)
        {
            page.CreateControl();
            var content = page.Controls.Cast<Control>()
                .FirstOrDefault(child => child.Dock == DockStyle.Fill);

            if (content is null)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}/{route}: selected page has no full-size workspace.");
                return;
            }

            var available = page.ClientSize;
            if (available.Width < 240 || available.Height < 150)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}/{route}: tab content viewport is too small " +
                    $"({available.Width}x{available.Height} in {windowWidth}px window).");
                return;
            }

            // This deliberately poisons a currently selected, docked child.
            // A test-side NormalizeDockFillSizes after this point would hide
            // the defect, so the real MainForm routine must repair it.
            content.Size = new Size(
                Math.Min(128, available.Width / 4),
                Math.Min(86, available.Height / 4));
            main.ReflowMainViewport();

            if (content.Width < available.Width * 0.80 ||
                content.Height < available.Height * 0.80)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}/{route}: selected nested content remains " +
                    $"at {content.Width}x{content.Height} inside " +
                    $"{available.Width}x{available.Height} tab after ReflowMainViewport.");
            }

            if (content is FlowLayoutPanel
                { FlowDirection: FlowDirection.TopDown } flow)
            {
                flow.PerformLayout();
                var rightLimit = flow.ClientSize.Width - flow.Padding.Right + 3;
                var minimumSectionWidth = Math.Max(
                    0, flow.ClientSize.Width - flow.Padding.Horizontal -
                    SystemInformation.VerticalScrollBarWidth - 4);

                foreach (Control child in flow.Controls)
                {
                    if (child is not TableLayoutPanel { AutoSize: true } &&
                        child is not FlowLayoutPanel { AutoSize: true })
                        continue;

                    if (minimumSectionWidth > 100 &&
                        child.Width < minimumSectionWidth * 0.78)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}/{route}: AutoSize section " +
                            $"'{ControlName(child)}' remained narrow " +
                            $"({child.Width} of {minimumSectionWidth}px).");
                    }
                    if (child.Right > rightLimit)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}/{route}: AutoSize section " +
                            $"'{ControlName(child)}' is horizontally clipped " +
                            $"(Right={child.Right}, Limit={rightLimit}).");
                    }
                }

                if (page.Name == "DiagnosticToolsTab")
                {
                    if (FindByName(page, "DiagnosticToolsActions")
                        is not FlowLayoutPanel actions ||
                        actions.Controls.OfType<Button>().Count() != 6 ||
                        FindByName(page, "RunUiPerformanceDiagnosticsButton")
                            is not Button)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}: Diagnostics > Tools actions are missing.");
                    }
                    else
                    {
                        foreach (var button in actions.Controls.OfType<Button>())
                        {
                            if (button.Right > actions.ClientSize.Width + 3 ||
                                button.Bottom > actions.ClientSize.Height + 3)
                            {
                                failures.Add(
                                    $"{formName}/{scenario.Name}: Diagnostics > Tools " +
                                    $"button '{button.Text}' is clipped at {windowWidth}px.");
                            }
                        }
                    }
                }
            }

            if (content is not TabControl nested)
                return;

            var originalNestedTab = nested.SelectedTab;
            try
            {
                foreach (TabPage child in nested.TabPages)
                {
                    nested.SelectedTab = child;
                    Application.DoEvents();
                    NormalizeDockFillSizes(main);
                    main.ReflowMainViewport();
                    VerifySelectedPage(child, route + " > " + child.Text, windowWidth);
                }
            }
            finally
            {
                if (originalNestedTab is not null)
                    nested.SelectedTab = originalNestedTab;
            }
        }
    }

    private static void VerifySelectedHomeRecoveryLayout(
        MainForm main, string formName, Scenario scenario,
        List<string> failures)
    {
        var root = FindByName(main, "HomeLayoutRoot") as TableLayoutPanel;
        var area = FindByName(main, "HomeSearchResultsArea") as TableLayoutPanel;
        var cards = FindByName(main, "HomeServiceCards") as TableLayoutPanel;
        var grid = FindByName(main, "HomeUnifiedSearchResults") as DataGridView;
        var bitLocker = FindByName(main, "HomeBitLockerPaneHost") as TableLayoutPanel;
        var laps = FindByName(main, "HomeLapsPaneHost") as TableLayoutPanel;
        var computers = FindByName(main, "HomeComputersPaneHost") as TableLayoutPanel;
        var recent = FindByName(main, "HomeRecentComputers") as DataGridView;
        var quickRecent = FindByName(main, "HomeRecentQuickPanel") as TableLayoutPanel;

        if (root is null || area is null || cards is null || grid is null ||
            bitLocker is null || laps is null || computers is null ||
            recent is null || quickRecent is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: one of the three draggable Start panes or top-right Recent computers is missing.");
            return;
        }

        var connectionLayout =
            FindByName(main, "HomeConnectionsAndRecent") as TableLayoutPanel;
        if (connectionLayout is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: upper Connections and Recent layout must be responsive.");
        }
        else
        {
            var scale = Math.Max(
                main.DeviceDpi / (double)UiStyle.BaselineDpi,
                main.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));
            var actualWidth = connectionLayout.Parent?.ClientSize.Width ?? 0;
            actualWidth -= connectionLayout.Parent?.Padding.Horizontal ?? 0;
            var pair = actualWidth >= (int)Math.Ceiling(
                UiStyle.HomeConnectionRecentPairMinimumWidth * scale);
            if (pair && connectionLayout.ColumnCount != 2 ||
                !pair && connectionLayout.ColumnCount != 1)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Recent computers must use unused Connections space on wide screens and stack on narrow/high-DPI screens.");
            }
        }

        if (root.RowCount != 6 ||
            root.RowStyles[5].SizeType != SizeType.Percent ||
            !ReferenceEquals(cards.Parent, area) ||
            cards.Controls.Count != 3 ||
            !ReferenceEquals(recent.Parent, quickRecent))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Start must reserve its workspace for three equal panes, with Recent computers in Connections' top-right area.");
        }

        // Simulate metadata-only computer selection. Do not connect to AD,
        // call Graph or retrieve any recovery password in the layout test.
        try
        {
            var rowIndex = grid.Rows.Add(
                "UI-TEST-PC", "AD", "Available", DateTime.UtcNow,
                "Detected", DateTime.UtcNow);
            var computer = new HomeSearchResult
            {
                ComputerName = "UI-TEST-PC",
                ComputerId = Guid.Empty.ToString(),
                BitLockerKeyCount = 1,
                BitLockerStatus = "Available",
                LapsCandidateFound = true,
                LapsStatus = "Detected"
            };
            grid.Rows[rowIndex].Tag = computer;
            grid.ClearSelection();
            grid.Rows[rowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[rowIndex].Cells[0];
            main.ReflowMainViewport();

            var width = area.Parent?.ClientSize.Width ?? 0;
            width -= area.Parent?.Padding.Horizontal ?? 0;
            var effectiveScale = Math.Max(
                main.DeviceDpi / (double)UiStyle.BaselineDpi,
                main.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));
            bool expectedThree = width >= (int)Math.Ceiling(
                UiStyle.HomeThreePaneMinimumWidth * effectiveScale);

            var hosts = new[] { bitLocker, laps, computers };
            var cells = hosts.Select(cards.GetPositionFromControl).ToArray();
            if (expectedThree)
            {
                bool equallySized =
                    cards.ColumnCount == 3 && cards.RowCount == 1 &&
                    cells.All(position => position.Row == 0 &&
                        position.Column is >= 0 and <= 2) &&
                    cells.Select(position => position.Column).Distinct().Count() == 3 &&
                    cards.ColumnStyles.Count == 3 &&
                    cards.ColumnStyles.Cast<ColumnStyle>().All(style =>
                        style.SizeType == SizeType.Percent &&
                        Math.Abs(style.Width - 100F / 3F) < 0.1F);
                if (!equallySized)
                    failures.Add(
                        $"{formName}/{scenario.Name}: BitLocker, LAPS and Computers must each occupy one equal third of the wide Start workspace.");

                // Uniform margins and Dock=Fill also guarantee equal rendered
                // width and height, not just equal percentage column styles.
                if (hosts.All(host => host.Width > 0 && host.Height > 0) &&
                    (hosts.Max(host => host.Width) - hosts.Min(host => host.Width) > 4 ||
                     hosts.Max(host => host.Height) - hosts.Min(host => host.Height) > 4))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: the three draggable cards must have equal actual dimensions.");
                }

                // Any panel can exchange places with either other panel.
                // This action must preserve the selected computer and cards.
                var originalBitLocker = cards.GetPositionFromControl(bitLocker).Column;
                var originalComputers = cards.GetPositionFromControl(computers).Column;
                main.ExchangeHomePanesForUiSelfTest("BitLocker", "Computers");
                main.ReflowMainViewport();
                if (cards.GetPositionFromControl(bitLocker).Column != originalComputers ||
                    cards.GetPositionFromControl(computers).Column != originalBitLocker ||
                    grid.SelectedRows.Count != 1 ||
                    !ReferenceEquals(grid.SelectedRows[0].Tag, computer))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: dragging BitLocker onto Computers must swap their positions without losing the computer selection.");
                }
                main.ExchangeHomePanesForUiSelfTest("BitLocker", "Computers");
                main.ReflowMainViewport();

                // Verify the third pane can be moved independently.
                var originalLaps = cards.GetPositionFromControl(laps).Column;
                originalComputers = cards.GetPositionFromControl(computers).Column;
                main.ExchangeHomePanesForUiSelfTest("Laps", "Computers");
                main.ReflowMainViewport();
                if (cards.GetPositionFromControl(laps).Column != originalComputers ||
                    cards.GetPositionFromControl(computers).Column != originalLaps)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: LAPS must be independently draggable onto Computers.");
                }
                main.ExchangeHomePanesForUiSelfTest("Laps", "Computers");
                main.ReflowMainViewport();
            }
            else
            {
                if (cards.ColumnCount != 1 || cards.RowCount != 3 ||
                    cells.Any(position => position.Column != 0) ||
                    cells.Select(position => position.Row).Distinct().Count() != 3 ||
                    cards.RowStyles.Count != 3 ||
                    cards.RowStyles.Cast<RowStyle>().Any(row => row.SizeType != SizeType.Absolute))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: compact HiDPI Start must stack all three equally high panes with scrolling.");
                }
            }

            // Check reset while a real grid row is selected. Reordering
            // its surrounding hosts must preserve the exact result object.
            main.PreviewResetVisualLayoutForUiSelfTest();
            main.ReflowMainViewport();
            if (grid.SelectedRows.Count != 1 ||
                !ReferenceEquals(grid.SelectedRows[0].Tag, computer))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: visual reset lost the exact selected device metadata.");
            }
            // Validate actual WinForms grid coordinates, not only the
            // persisted pane-order string. It must work both in 3 columns
            // and in the vertically stacked compact/RDP layout.
            var resetCells = new[]
            {
                cards.GetPositionFromControl(bitLocker),
                cards.GetPositionFromControl(laps),
                cards.GetPositionFromControl(computers)
            };
            var resetLayoutCorrect = expectedThree
                ? resetCells.Select(x => x.Column).SequenceEqual([0, 1, 2]) &&
                  resetCells.All(x => x.Row == 0)
                : resetCells.Select(x => x.Row).SequenceEqual([0, 1, 2]) &&
                  resetCells.All(x => x.Column == 0);
            if (!resetLayoutCorrect)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: visual reset left overlapping or incorrectly ordered WinForms panes.");
            }

            if (FindByName(cards, "HomeRevealBitLockerButton") is Button { Enabled: true } ||
                FindByName(cards, "HomeRevealLapsButton") is Button { Enabled: true } ||
                FindByName(cards, "HomeLoadLapsHistoryButton") is Button { Enabled: true })
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: metadata-only selection cannot reveal LAPS/BitLocker secrets or load history without authorization.");
            }
        }
        finally
        {
            grid.Rows.Clear();
            main.ReflowMainViewport();
        }
    }

    private static void VerifyStartSourcePolicies(List<string> failures)
    {
        // Incomplete Microsoft 365 configuration must be rejected locally.
        // No sign-in callback, network request, disk write or Graph exception
        // is needed to validate these non-secret prerequisites.
        const string clientGuid = "11111111-1111-4111-8111-111111111111";
        if (MainForm.CloudConnectionPrerequisiteError(null, clientGuid,
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError(" ", clientGuid,
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", "",
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", "not-a-guid",
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", Guid.Empty.ToString(),
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", clientGuid,
                null, certificateMode: true) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", clientGuid,
                null, certificateMode: false) is not null ||
            MainForm.CloudConnectionPrerequisiteError("contoso.onmicrosoft.com",
                clientGuid, "ABCD1234", certificateMode: true) is not null)
        {
            failures.Add("Cloud setup preflight must reject incomplete tenant/client/certificate before Graph sign-in.");
        }

        // A Helpdesk GUI must never auto-elevate, and disabling the CLI UAC
        // prompt must not allow privileged maintenance without admin rights.
        if (UpdateTrustPolicy.CanEnableUnattended(false, "Example Publisher") ||
            UpdateTrustPolicy.CanEnableUnattended(true, " ") ||
            !UpdateTrustPolicy.CanEnableUnattended(true, "Example Publisher"))
        {
            failures.Add("Unattended updater requires both trusted signature policy and publisher.");
        }

        if (StartupPrivilegePolicy.NeedsElevation(
                false, false, false, false, false) ||
            !StartupPrivilegePolicy.NeedsElevation(
                true, false, false, false, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, true, false, false, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, false, true, false, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, false, false, true, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, false, false, false, true))
        {
            failures.Add("Least-privilege startup policy regression: GUI must not request UAC.");
        }

        if (OperatingSystem.IsWindows())
        {
            var metadataOnlyPolicy = new AppConfig
            {
                RbacEnabled = true,
                RbacAllowLocalAdministrators = false
            };
            var metadataAuthorization = new AuthorizationService(metadataOnlyPolicy);
            if (!metadataAuthorization.Check(BitKeyBridgePermission.SearchDevices).Allowed ||
                metadataAuthorization.Check(BitKeyBridgePermission.RecoveryRead).Allowed)
            {
                failures.Add("Metadata-only search must not grant secret RecoveryRead access.");
            }
        }

        // Simulate an unwritable audit destination; the audit service must
        // report failure, never claim that an access event was recorded.
        var invalidDirectory = System.IO.Path.GetTempFileName();
        try
        {
            var audit = new AuditService(
                System.IO.Path.Combine(invalidDirectory, "audit.jsonl"));
            if (audit.Write("AuditStorageRegression") is not null)
                failures.Add("Mandatory audit preflight must detect unwritable destinations.");
        }
        finally
        {
            try { File.Delete(invalidDirectory); }
            catch (IOException) { /* Only temporary test cleanup. */ }
            catch (UnauthorizedAccessException) { /* Only temporary test cleanup. */ }
        }
        if (StartConnectionPolicy.Normalize("unknown") != StartConnectionPolicy.Choose ||
            StartConnectionPolicy.NeedsActiveDirectory(StartConnectionPolicy.MicrosoftCloud) ||
            StartConnectionPolicy.NeedsMicrosoftCloud(StartConnectionPolicy.ActiveDirectory) ||
            !StartConnectionPolicy.NeedsActiveDirectory(StartConnectionPolicy.Both) ||
            !StartConnectionPolicy.NeedsMicrosoftCloud(StartConnectionPolicy.Both))
        {
            failures.Add("Start connection source must isolate AD from cloud-only searches.");
        }

        // Source persistence is per-user and opt-in. Existing saved
        // preferences from earlier builds must still show the chooser.
        failures.AddRange(StartConnectionPolicy.RunOfflineTests());

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.ActiveDirectory))
        {
            if (wizard.Controls.Find("StartupRememberLastConnection", true)
                is not [CheckBox { Checked: false, Enabled: true }])
            {
                failures.Add("Remembered startup connection must be unchecked by default.");
            }
        }

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.Both, rememberLastConnection: true))
        {
            if (!wizard.RememberLastConnectionOnStart ||
                wizard.Controls.Find("StartupRememberLastConnection", true)
                    is not [CheckBox { Checked: true, Enabled: true }])
            {
                failures.Add("Remember-last startup selection was not restored in the wizard.");
            }
        }

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.Choose, rememberLastConnection: true))
        {
            if (wizard.RememberLastConnectionOnStart ||
                wizard.Controls.Find("StartupRememberLastConnection", true)
                    is not [CheckBox { Checked: false, Enabled: false }])
            {
                failures.Add("Offline Start must disable remembered automatic connection.");
            }
            if (wizard.Controls.Find("StartupConnectionConfirm", true)
                    is not [Button { Text: "Open Start" }] ||
                wizard.Controls.Find("StartupConnectionSkip", true)
                    is not [Button { Text: "Cancel" }])
            {
                failures.Add("Offline Start must be a separate explicit action from Cancel.");
            }
        }

        // Cloud setup is allowed without a pre-existing tenant/app registration:
        // it opens the existing administrator-only setup wizard. No token,
        // Graph consent or credential is ever created by changing radio modes.
        using (var cloud = new CloudConnectionWizardDialog(new CloudAuthConfig()))
        {
            var approved = cloud.Controls.Find("CloudWizardExistingApp", true)
                .OfType<RadioButton>().FirstOrDefault();
            var admin = cloud.Controls.Find("CloudWizardAdministratorSetup", true)
                .OfType<RadioButton>().FirstOrDefault();
            var tenant = cloud.Controls.Find("CloudWizardTenant", true)
                .OfType<TextBox>().FirstOrDefault();
            var client = cloud.Controls.Find("CloudWizardClient", true)
                .OfType<TextBox>().FirstOrDefault();
            if (approved is null || admin is null || tenant is null || client is null ||
                cloud.Controls.Find("CloudWizardPermissionsToggle", true)
                    is not [Button] ||
                cloud.Controls.Find("CloudWizardPermissions", true)
                    is not [Label])
            {
                failures.Add("Cloud wizard must retain all sources and expandable delegated-permissions guidance.");
            }
            else
            {
                admin.Checked = true;
                if (tenant.Enabled || client.Enabled || !cloud.RequestAdministrativeSetup)
                    failures.Add("Admin setup must not require existing tenant or client IDs.");
                approved.Checked = true;
                if (!tenant.Enabled || !client.Enabled || cloud.RequestAdministrativeSetup)
                    failures.Add("Existing-app sign-in must expose its tenant and client ID controls.");
            }
        }

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.ActiveDirectory,
            rememberLastConnection: true, automaticConnectionsAllowed: false))
        {
            if (wizard.RememberLastConnectionOnStart ||
                wizard.Controls.Find("StartupRememberLastConnection", true)
                    is not [CheckBox { Checked: false, Enabled: false }])
            {
                failures.Add("Machine configuration must disable automatic reconnection.");
            }
        }

        // Detect wrong passwords using error codes (not only English text).
        // Network outages and unrelated LDAP errors must not trigger
        // an automatic credential prompt.
        if (!MainForm.IsAdLogonFailure(
                new System.ComponentModel.Win32Exception(1326)) ||
            !MainForm.IsAdLogonFailure(
                new Exception("The user name or password is incorrect.")) ||
            MainForm.IsAdLogonFailure(
                new Exception("The domain controller is unreachable.")))
        {
            failures.Add(
                "AD sign-in retry must recognize logon failure without retrying on connection outages.");
        }

        if (!new StartConnectionPreferences().RecoveryCardsOnLeft)
        {
            failures.Add(
                "First run must default to BitLocker/LAPS recovery on the left.");
        }
    }

    private static void VerifyDialogKeyboardBehavior(
        DpiAwareForm form,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (form is StorageMaintenanceDialog)
            return;

        if (form.CancelButton is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: modal dialog has no standard Escape/Cancel action.");
        }

        if (form is not RemoteApiScopedTokenDialog &&
            form.AcceptButton is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: modal dialog has no standard Enter/Accept action.");
        }
    }

    private static void VerifyMainFormTabs(
        DpiAwareForm form,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var tabs =
            FindFirst<TabControl>(
                form);

        if (tabs is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: main TabControl was not found.");
            return;
        }

        if (!form.KeyPreview)
            failures.Add($"{formName}/{scenario.Name}: Ctrl+1..7 global keyboard tab navigation requires KeyPreview.");

        if (!form.KeyPreview)
            failures.Add(
                $"{formName}/{scenario.Name}: keyboard shortcuts require MainForm.KeyPreview.");

        var preferredOrder = new[]
        {
            "Start", "BitLocker", "LAPS", "Devices", "Helpdesk", "Diagnostics"
        };
        foreach (var requiredBackgroundUpdaterControl in new[]
                 {
                     "InstallBackgroundUpdateTaskButton",
                     "BackgroundUpdateTaskStatusButton",
                     "RemoveBackgroundUpdateTaskButton",
                     "BackgroundUpdateHour"
                 })
        {
            if (FindByName(form, requiredBackgroundUpdaterControl) is null)
                failures.Add(
                    $"{formName}/{scenario.Name}: scheduled updater is missing " +
                    requiredBackgroundUpdaterControl);
        }
        // Every responsive size/DPI scenario must expose the Update Center
        // without creating a task, clearing a rollback hold or installing.
        foreach (var name in new[]
                 {
                     "UpdateCenterV2", "UpdateCenterSummary",
                     "UpdateCenterTaskDetails", "UpdateCenterRefreshButton",
                     "UpdateCenterRetryCheckButton", "UpdateCenterPauseButton",
                     "UpdateCenterEnableButton", "UpdateCenterCompact"
                 })
        {
            if (FindByName(form, name) is null)
                failures.Add(
                    $"{formName}/{scenario.Name}: Update Center control missing: " + name);
        }
        if (tabs.TabPages.Count > 3 &&
            tabs.TabPages[3].Name != "DevicesWorkspace")
            failures.Add(
                $"{formName}/{scenario.Name}: invariant Devices workspace ID missing; localization must not break navigation.");
        if (tabs.TabPages.Count > 5 &&
            tabs.TabPages[5].Name != "DiagnosticsWorkspace")
            failures.Add(
                $"{formName}/{scenario.Name}: invariant Diagnostics workspace ID missing.");
        if (tabs.TabPages.Count > 6 &&
            tabs.TabPages[6].Name != "SettingsWorkspace")
            failures.Add(
                $"{formName}/{scenario.Name}: invariant admin Settings workspace ID missing.");

        var actualOrder = tabs.TabPages.Cast<TabPage>()
            .Select(page => page.Text).ToArray();
        if (actualOrder.Length < preferredOrder.Length ||
            !actualOrder.Take(preferredOrder.Length)
                .SequenceEqual(preferredOrder, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: unexpected main tab order: " +
                string.Join(" / ", actualOrder) +
                "; required Start / BitLocker / LAPS / Devices / Helpdesk / Diagnostics.");
        }
        if (actualOrder.Length >= 5)
        {
            foreach (var (tabIndex, queryName) in new[]
            {
                (0, "HomeUnifiedQuery"),
                (1, "BitLockerSearchQuery"),
                (2, "LapsLiveSearchQuery"),
                (3, "UnifiedDeviceSearchQuery")
            })
            {
                if (FindByName(tabs.TabPages[tabIndex], queryName) is not TextBox)
                    failures.Add(
                        $"{formName}/{scenario.Name}: Ctrl+F search input {queryName} is missing.");
            }
        }

        // The Helpdesk landing tab reuses Start rather than maintaining
        // a separate query, duplicate BitLocker/LAPS buttons or AD/Graph work.
        if (tabs.TabPages.Count > 4)
        {
            var helpdeskPage = tabs.TabPages[4];
            if (FindByName(helpdeskPage, "HelpdeskOpenDeviceWorkspaceButton") is not Button ||
                FindByName(helpdeskPage, "HelpdeskSelectedDeviceSummary") is not UiStatusLabel ||
                FindByName(helpdeskPage, "HelpdeskRecoveryStepsButton") is not Button ||
                FindByName(helpdeskPage, "HelpdeskDashboardStatus") is not UiStatusLabel ||
                FindByName(helpdeskPage, "HelpdeskQuickSearch") is not null ||
                helpdeskPage.Controls.OfType<FlowLayoutPanel>()
                    .SelectMany(panel => panel.Controls.OfType<TextBox>()).Any())
            {
                failures.Add($"{formName}/{scenario.Name}: Helpdesk must reuse Start search and display selected-device metadata.");
            }
        }

        if (tabs.TabPages.Count == 0 ||
            !string.Equals(
                tabs.TabPages[0].Text,
                "Start",
                StringComparison.OrdinalIgnoreCase) ||
            !ReferenceEquals(tabs.SelectedTab, tabs.TabPages[0]))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Start must be the first and selected workspace at launch.");
        }
        else
        {
            var startPage =
                tabs.TabPages[0];

            var requiredStartControls =
                new[]
                {
                    "HomeConnectionSourceChooser",
                    "HomeConnectionSource",
                    "HomeConnectionPrimaryActions",
                    "HomeConnectionOptionsToggle",
                    "HomeConnectionExtraActions",
                    "HomeConnectionDirectActions",
                    "HomeConnectionDirectHint",
                    "HomeReconnectWizardButton",
                    "HomeBitLockerPaneHost",
                    "HomeLapsPaneHost",
                    "HomeComputersPaneHost",
                    "HomeRecentQuickPanel",
                    "HomeConnectCloudButton",
                    "HomeConfigureCloudButton",
                    "HomeCloudConnectionStatus",
                    "HomeConnectionStatus",
                    "HomeAdAccountStatus",
                    "HomeConnectAdButton",
                    "HomeDisconnectAdButton",
                    "HomeConnectionToolsButton",
                    "HomeServiceCards",
                    "HomeBitLockerDiagnosticLink",
                    "HomeBitLockerCard",
                    "HomeLapsCard",
                    "HomeLapsHistoryArea",
                    "HomeLapsHistoryGrid",
                    "HomeLapsHistoryStatus",
                    "HomeLoadLapsHistoryButton",
                    "HomeLapsSelectedRecord",
                    "HomeRevealBitLockerButton",
                    "HomeCopyBitLockerButton",
                    "HomeRevealLapsButton",
                    "HomeCopyLapsButton",
                    "HomeSelectionSummary",
                    "SecretLifetimeStatus",
                    "StartSetupStatus",
                    "HomeUnifiedQuery",
                    "HomeUnifiedSearchResults",
                    "HomeRecentComputers",
                    "HomeUseWindowsIdentity",
                    "HomeUseOtherAccount",

                    "AdvancedConnectionGroup"
                };

            if (FindByName(startPage, "HomeServiceCards") is TableLayoutPanel cards)
            {
                if (FindByName(cards, "HomeBitLockerCard") is not UiRecoveryCard ||
                    FindByName(cards, "HomeLapsCard") is not UiRecoveryCard ||
                    FindByName(cards, "HomeComputersCard") is not UiRecoveryCard ||
                    cards.Controls.Count != 3)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start requires three equal draggable cards: BitLocker, LAPS and Computers.");
                }

                if (FindByName(startPage, "HomeRevealLapsButton") is Button { Enabled: true } ||
                    FindByName(startPage, "HomeCopyLapsButton") is Button { Enabled: true } ||
                    FindByName(startPage, "HomeLoadLapsHistoryButton") is Button { Enabled: true })
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start cannot reveal or copy LAPS without a selected computer and explicit action.");
                }
            }

            if (FindByName(startPage, "HomeConnectionToolsButton") is Button toolsButton)
            {
                var actions = toolsButton.ContextMenuStrip?.Items
                    .OfType<ToolStripMenuItem>()
                    .Select(item => item.Name)
                    .ToHashSet(StringComparer.Ordinal) ?? [];
                if (!actions.Contains("HomeEnvironmentCheckMenuItem") ||
                    !actions.Contains("HomeDeviceDiagnosticsMenuItem") ||
                    !actions.Contains("HomeAdvancedConnectionMenuItem"))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: the consolidated Connection tools menu must keep Environment Check, Device Diagnostics and Advanced settings available.");
                }
            }

            if (FindByName(startPage, "HomeLapsHistoryGrid") is DataGridView historyGrid)
            {
                if (!historyGrid.ReadOnly ||
                    historyGrid.MultiSelect ||
                    historyGrid.ScrollBars != ScrollBars.Both ||
                    historyGrid.Columns.Count != 4 ||
                    historyGrid.Rows.Count != 0)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: LAPS history must be empty, non-secret, read-only and horizontally scrollable at startup.");
                }

                if (historyGrid.Columns["Type"] is null ||
                    historyGrid.Columns["Account"] is null ||
                    historyGrid.Columns["Updated"] is null ||
                    historyGrid.Columns["Status"] is null)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: LAPS history requires Type, Account, Updated and Status columns.");
                }
            }

            if (FindByName(startPage, "HomeBitLockerDiagnosticLink") is LinkLabel help &&
                (!help.Text.Contains("diagnostic", StringComparison.OrdinalIgnoreCase) ||
                 help.Enabled))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: inline BitLocker diagnostic commands link must start disabled until selection.");
            }

            if (FindByName(startPage, "HomeDeviceDetailsButton") is not null ||
                FindByName(startPage, "HomeRotateBitLockerButton") is not null ||
                FindByName(startPage, "HomeBitLockerDiagnosticButton") is not null)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start must not contain navigation-only device or BitLocker diagnostic buttons.");
            }

            foreach (var controlName in
                     requiredStartControls)
            {
                if (FindByName(
                        startPage,
                        controlName) is null)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start workspace is missing '{controlName}'.");
                }
            }

            if (FindByName(startPage, "HomeConnectionStatus") is UiStatusLabel connectionStatus &&
                FindByName(startPage, "HomeAdAccountStatus") is UiStatusLabel accountStatus &&
                (connectionStatus.Parent is not TableLayoutPanel connectionLayout ||
                 !ReferenceEquals(accountStatus.Parent, connectionLayout) ||
                 connectionLayout.GetRow(accountStatus) != connectionLayout.GetRow(connectionStatus) + 1 ||
                 accountStatus.Margin.Top < UiStyle.SectionGap))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start must have a full DPI-scaled section gap between Connected and Connected as.");
            }

            if (tabs.SelectedIndex != 0)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start workspace is not selected by default.");
            }

            if (FindByName(
                    startPage,
                    "AdCredentialStatus") is UiStatusLabel credentialStatus &&
                credentialStatus.MaximumSize.Width < 300)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: hidden Advanced layout collapsed the AD credential status width to {credentialStatus.MaximumSize.Width}px.");
            }

            if (FindByName(startPage, "HomeSearchResultsArea")
                    is TableLayoutPanel resultsArea &&
                FindByName(startPage, "HomeServiceCards")
                    is TableLayoutPanel threeCards &&
                FindByName(startPage, "HomeUnifiedSearchResults")
                    is DataGridView resultsGrid &&
                FindByName(startPage, "HomeSelectionPanel")
                    is TableLayoutPanel selectionPanel &&
                FindByName(startPage, "HomeComputersCard")
                    is UiRecoveryCard computersCard &&
                FindByName(startPage, "HomeBitLockerPaneHost")
                    is TableLayoutPanel bitLockerHost &&
                FindByName(startPage, "HomeLapsPaneHost")
                    is TableLayoutPanel lapsHost &&
                FindByName(startPage, "HomeComputersPaneHost")
                    is TableLayoutPanel computersHost)
            {
                if (!ReferenceEquals(threeCards.Parent, resultsArea) ||
                    !ReferenceEquals(selectionPanel.Parent, resultsArea) ||
                    !ReferenceEquals(resultsGrid.Parent, computersCard) ||
                    !ReferenceEquals(bitLockerHost.Parent, threeCards) ||
                    !ReferenceEquals(lapsHost.Parent, threeCards) ||
                    !ReferenceEquals(computersHost.Parent, threeCards) ||
                    threeCards.Controls.Count != 3)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start must show three equal draggable BitLocker, LAPS and Computers panes, even before selection.");
                }

                if (selectionPanel.Visible)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: secret/selection status must stay hidden until a computer is selected.");
                }
            }
            else
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: three-pane Start recovery/results container is missing.");
            }

        }

        // Administrator-only LAPS Coverage remains discoverable with the
        // same central UiStyle across compact, 150% and 200% scaling.
        if (FindByName(form, "LapsCoverageScanButton") is not Button ||
            FindByName(form, "LapsCoverageDevices") is not DataGridView ||
            FindByName(form, "LapsCoverageChanges") is not DataGridView ||
            FindByName(form, "LapsCoverageCompareSnapshotsButton") is not Button ||
            FindByName(form, "LapsCoverageRequestRotation") is not Button lapsRotate ||
            FindByName(form, "LapsRotationOptIn") is not CheckBox lapsOptIn)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS Coverage and optional rotation controls are missing.");
        }
        else if (lapsRotate.Enabled || lapsOptIn.Checked)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS expiry writes must remain disabled by default.");
        }

        if (FindByName(
                form,
                "AutomaticUpdateInstall") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Updates workspace is missing the automatic-install control.");
        }

        if (FindByName(
                form,
                "UpdateNowButton") is null ||
            FindByName(
                form,
                "RequireTrustedUpdateSignature") is null ||
            FindByName(
                form,
                "TrustedUpdatePublisher") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Updates workspace is missing retry or Authenticode policy controls.");
        }

        // Compact Helpdesk settings still expose the actual policy state
        // and always retain discoverable controls for profiles and audit.
        if (FindByName(form, "HelpdeskProfilesToggle") is not Button ||
            FindByName(form, "HelpdeskProfilesGrid") is not TableLayoutPanel ||
            FindByName(form, "HelpdeskRecoveryPolicyToggle") is not Button ||
            FindByName(form, "HelpdeskRecoveryPolicyGrid") is not TableLayoutPanel ||
            FindByName(form, "HelpdeskRecoveryPolicyOverview")
                is not UiStatusLabel overview ||
            !overview.Text.Contains("Saved recovery policy:",
                StringComparison.Ordinal) ||
            !overview.Text.Contains("does not grant secret access",
                StringComparison.Ordinal))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: compact Settings must display saved audit policy and retain both expandable sections.");
        }

        // Verify the collapsed policy can be opened reliably even when its
        // nested Settings TabPage started life at 200x100 (or was hidden
        // during a DPI/RDP switch). Avoid Control.Visible as a state flag:
        // WinForms combines it with visibility of all ancestors.
        if (form is MainForm settingsMain &&
            FindByName(form, "HelpdeskRecoveryPolicyToggle") is Button policyToggle &&
            FindByName(form, "HelpdeskRecoveryPolicyGrid") is TableLayoutPanel policyGrid &&
            tabs.TabPages.Cast<TabPage>().FirstOrDefault(
                page => page.Name == "SettingsWorkspace") is TabPage settingsTab &&
            settingsTab.Controls.OfType<TabControl>().FirstOrDefault() is TabControl settingsSections &&
            settingsSections.TabPages.Cast<TabPage>().FirstOrDefault(
                page => page.Name == "HelpdeskBasicsTab") is TabPage helpdeskSettings)
        {
            var previous = tabs.SelectedTab;
            var previousSection = settingsSections.SelectedTab;
            try
            {
                tabs.SelectedTab = settingsTab;
                settingsSections.SelectedTab = helpdeskSettings;
                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(true);
                settingsMain.ReflowMainViewport();
                if (policyToggle.Text != "Hide recovery access policy")
                    failures.Add($"{formName}/{scenario.Name}: expanding recovery policy did not update its controls.");

                // Calling Setup & Permissions twice is idempotent rather
                // than accidentally hiding the policy on its second visit.
                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(true);
                if (policyToggle.Text != "Hide recovery access policy")
                    failures.Add($"{formName}/{scenario.Name}: repeated Settings navigation collapsed an open policy.");

                if (policyGrid.Parent is FlowLayoutPanel viewport &&
                    viewport.ClientSize.Width > 600 &&
                    (policyGrid.Width < viewport.ClientSize.Width / 2 ||
                     policyGrid.Right > viewport.ClientSize.Width +
                         SystemInformation.VerticalScrollBarWidth))
                {
                    failures.Add($"{formName}/{scenario.Name}: expanded policy was squeezed or clipped by its viewport.");
                }

                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(false);
                if (policyToggle.Text != "Show recovery access policy...")
                    failures.Add($"{formName}/{scenario.Name}: Settings policy could not be collapsed.");
            }
            finally
            {
                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(false);
                settingsSections.SelectedTab = previousSection;
                tabs.SelectedTab = previous;
            }
        }

        if (FindByName(form, "ResetVisualLayoutButton") is not Button)
            failures.Add($"{formName}/{scenario.Name}: visual-only layout reset action is missing.");

        if (form is MainForm visualMain)
        {
            // Disturb only visual preferences, then verify that a reset
            // restores defaults without editing source authorization or
            // saved recovery/audit policies.
            var securityBefore =
                visualMain.PreviewNonVisualSettingsForUiSelfTest();
            visualMain.ExchangeHomePanesForUiSelfTest("BitLocker", "Computers");
            var compact = FindByName(form, "UpdateCenterCompact") as CheckBox;
            if (compact is not null)
                compact.Checked = false;
            visualMain.PreviewSetConnectionOptionsExpandedForUiTest(true);
            visualMain.PreviewSetHelpdeskProfilesExpandedForUiTest(true);
            visualMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(true);
            visualMain.PreviewResetVisualLayoutForUiSelfTest();
            if (visualMain.PreviewHomePaneOrderForUiSelfTest() !=
                "BitLocker,Laps,Computers")
                failures.Add($"{formName}/{scenario.Name}: reset did not restore the default three-pane order.");
            if (compact is not { Checked: true })
                failures.Add($"{formName}/{scenario.Name}: reset did not restore compact Updates.");
            if (FindByName(form, "HomeConnectionOptionsToggle") is not Button
                { Text: "More connection options..." } ||
                FindByName(form, "HelpdeskProfilesToggle") is not Button
                { Text: "Show environment profiles..." } ||
                FindByName(form, "HelpdeskRecoveryPolicyToggle") is not Button
                { Text: "Show recovery access policy..." })
            {
                failures.Add($"{formName}/{scenario.Name}: visual reset did not collapse optional Connection and Settings sections.");
            }
            if (securityBefore !=
                visualMain.PreviewNonVisualSettingsForUiSelfTest())
                failures.Add($"{formName}/{scenario.Name}: visual reset changed authentication source or recovery policy.");
            // No second swap: that would silently undo the visual reset.
        }

        if (FindByName(
                form,
                "HelpdeskBasicsTab") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Administration is missing the Helpdesk basics workspace.");
        }

        if (FindByName(form, "EnterpriseSettingsTab") is null ||
            FindByName(form, "EnterpriseSettingsSections") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: advanced enterprise features must remain accessible from Settings.");
        }

        if (!tabs.TabPages.Cast<TabPage>().Any(page => page.Text == "Diagnostics") ||
            FindByName(form, "AccessHealthTab") is null ||
            FindByName(form, "DiagnosticToolsTab") is null ||
            FindByName(form, "RunEnvironmentCheckButton") is null ||
            FindByName(form, "RunAdvancedDeviceDiagnosticsButton") is null ||
            FindByName(form, "RunBitLockerCommandsButton") is null ||
            FindByName(form, "RunLapsCommandsButton") is null ||
            FindByName(form, "RunUiPerformanceDiagnosticsButton") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Diagnostics must provide Access Health and the existing recovery runbooks.");
        }

        if (FindByName(
                form,
                "UpdateHistory") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Updates workspace is missing update history.");
        }

        if (FindByName(
                form,
                "GlobalStatusBar") is not StatusStrip globalStatusBar)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: global connection status bar was not found.");
        }
        else if (globalStatusBar.Items["GlobalAdAccountStatus"] is not ToolStripStatusLabel)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: global Active Directory account indicator was not found.");
        }

        if (FindByName(
                form,
                "RecoveryBackToStart") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Recovery Back to Start button was not found.");
        }

        if (FindByName(
                form,
                "LapsBackToStart") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS Back to Start button was not found.");
        }

        var lapsWarningPanel =
            FindByName(form, "LapsZeroRecordWarningPanel") as TableLayoutPanel;
        var lapsWarning =
            FindByName(form, "LapsZeroRecordWarning") as UiStatusLabel;
        var lapsCommands =
            FindByName(form, "LapsDiagnosticCommandsButton") as Button;
        var lapsKeyGrid =
            FindByName(form, "LapsKeyResults") as DataGridView;

        if (lapsWarningPanel is null ||
            lapsWarning is null ||
            lapsCommands is null ||
            lapsKeyGrid is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS warning, key grid or diagnostic commands button is missing.");
        }
        else
        {
            if (lapsWarningPanel.Visible)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: LAPS warning is visible before a failed read.");
            }

            if (lapsWarning.StatusKind != UiStatusKind.Warning)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: the LAPS warning is not using the shared warning palette.");
            }

            if (lapsWarningPanel.Parent is not TableLayoutPanel workspace ||
                workspace.GetRow(lapsWarningPanel) >= workspace.GetRow(lapsKeyGrid))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: LAPS warning must be positioned above the key results grid.");
            }
        }

        var bitLockerWarningPanel =
            FindByName(form, "BitLockerZeroRecordWarningPanel") as TableLayoutPanel;
        var bitLockerWarning =
            FindByName(form, "BitLockerZeroRecordWarning") as UiStatusLabel;
        var bitLockerCommands =
            FindByName(form, "BitLockerDiagnosticCommandsButton") as Button;
        var bitLockerResults =
            FindByName(form, "BitLockerSearchResults") as DataGridView;

        if (bitLockerWarningPanel is null ||
            bitLockerWarning is null ||
            bitLockerCommands is null ||
            bitLockerResults is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: BitLocker warning/results/diagnostic controls missing.");
        }
        else
        {
            if (bitLockerWarningPanel.Visible)
                failures.Add(
                    $"{formName}/{scenario.Name}: BitLocker warning must start hidden.");

            if (bitLockerWarning.StatusKind != UiStatusKind.Warning)
                failures.Add(
                    $"{formName}/{scenario.Name}: BitLocker warning bypasses shared warning palette.");

            if (bitLockerWarningPanel.Parent is not TableLayoutPanel workspace ||
                workspace.GetRow(bitLockerWarningPanel) >= workspace.GetRow(bitLockerResults))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: BitLocker warning must be above the results grid.");
            }
        }

        VerifyTabControlPages(
            tabs,
            formName,
            scenario,
            failures,
            form,
            checkRecoveryConnection: true);

        VerifyComputerQueryNavigation(
            (MainForm)form,
            tabs,
            formName,
            scenario,
            failures);
    }

    private static void VerifyComputerQueryNavigation(
        MainForm main,
        TabControl tabs,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var start = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "StartWorkspace");
        var recovery = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "RecoveryWorkspace");
        var laps = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "LapsWorkspace");
        var helpdesk = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "HelpdeskDashboardTab");

        var homeQuery = start is null
            ? null : FindByName(start, "HomeUnifiedQuery") as TextBox;
        var recoveryQuery = recovery is null
            ? null : FindByName(recovery, "BitLockerSearchQuery") as TextBox;
        var lapsQuery = laps is null
            ? null : FindByName(laps, "LapsLiveSearchQuery") as TextBox;

        if (start is null || recovery is null || laps is null || helpdesk is null ||
            homeQuery is null || recoveryQuery is null || lapsQuery is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: computer query navigation controls are missing.");
            return;
        }

        var initialTab = tabs.SelectedTab;
        try
        {
            tabs.SelectedTab = start;
            homeQuery.Text = "PC-NAV-001";
            tabs.SelectedTab = recovery;
            if (recoveryQuery.Text != "PC-NAV-001")
                failures.Add($"{formName}/{scenario.Name}: Start to BitLocker computer was lost.");

            tabs.SelectedTab = laps;
            if (lapsQuery.Text != "PC-NAV-001")
                failures.Add($"{formName}/{scenario.Name}: BitLocker to LAPS computer was lost.");

            lapsQuery.Text = "PC-NAV-002";
            tabs.SelectedTab = recovery;
            if (recoveryQuery.Text != "PC-NAV-002")
                failures.Add($"{formName}/{scenario.Name}: LAPS to BitLocker computer was lost.");

            tabs.SelectedTab = start;
            if (homeQuery.Text != "PC-NAV-002")
                failures.Add($"{formName}/{scenario.Name}: LAPS to Start computer was lost.");

            homeQuery.Clear();
            tabs.SelectedTab = laps;
            if (lapsQuery.Text.Length != 0)
                failures.Add($"{formName}/{scenario.Name}: clearing computer must clear stale tab queries.");

            tabs.SelectedTab = start;
            homeQuery.Text = "PC";
            var results =
                FindByName(start, "HomeUnifiedSearchResults") as DataGridView;
            if (results is not null)
            {
                var index = results.Rows.Add(
                    "PC-SELECTED", "AD", "Available", DBNull.Value, "Detected", DBNull.Value);
                results.Rows[index].Tag = new HomeSearchResult
                {
                    ComputerName = "PC-SELECTED",
                    BitLockerKeyCount = 1,
                    LapsCandidateFound = true,
                    LapsStatus = "Detected"
                };
                results.ClearSelection();
                results.Rows[index].Selected = true;
                results.CurrentCell = results.Rows[index].Cells[0];
                tabs.SelectedTab = helpdesk;
                var selectedSummary = FindByName(
                    helpdesk, "HelpdeskSelectedDeviceSummary") as UiStatusLabel;
                if (selectedSummary is null ||
                    !selectedSummary.Text.Contains("PC-SELECTED",
                        StringComparison.Ordinal))
                {
                    failures.Add($"{formName}/{scenario.Name}: Helpdesk must show the exact selected Start metadata without a second search.");
                }

                main.PreviewHelpdeskDeviceWorkspaceForUiSelfTest();
                if (!ReferenceEquals(tabs.SelectedTab, start) ||
                    results.SelectedRows.Count != 1 ||
                    results.SelectedRows[0].Tag is not HomeSearchResult selected ||
                    selected.ComputerName != "PC-SELECTED")
                {
                    failures.Add($"{formName}/{scenario.Name}: returning from Helpdesk lost the source-qualified Start selection.");
                }

                // The same selected row can become ambiguous when the
                // metadata result includes another AD object with this name.
                // Helpdesk must never imply it can authorize recovery by name.
                if (results.Rows[index].Tag is HomeSearchResult tested)
                {
                    tested.IdentityAmbiguous = true;
                    tabs.SelectedTab = helpdesk;
                    if (selectedSummary is null ||
                        selectedSummary.StatusKind != UiStatusKind.Warning ||
                        !selectedSummary.Text.Contains("ambiguous",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"{formName}/{scenario.Name}: ambiguous AD device must be clearly unverified in Helpdesk.");
                    }

                    // Likewise, a cloud result lacking its source-qualified
                    // Entra identity must not be displayed as verified.
                    tested.IdentityAmbiguous = false;
                    tested.IsCloudRecord = true;
                    tested.SourceIdentity = string.Empty;
                    tabs.SelectedTab = start;
                    tabs.SelectedTab = helpdesk;
                    if (selectedSummary is null ||
                        selectedSummary.StatusKind != UiStatusKind.Warning ||
                        !selectedSummary.Text.Contains("unverified",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"{formName}/{scenario.Name}: cloud device without an exact identity must remain unverified in Helpdesk.");
                    }

                    // Restore normal AD metadata before the existing
                    // BitLocker/LAPS query navigation regression.
                    tested.IsCloudRecord = false;
                    tabs.SelectedTab = start;
                }

                tabs.SelectedTab = recovery;
                if (recoveryQuery.Text != "PC-SELECTED")
                    failures.Add($"{formName}/{scenario.Name}: selected Start computer was not retained.");

                // A *new* query must invalidate the selected old device
                // immediately, even before the debounce or a network call.
                tabs.SelectedTab = start;
                homeQuery.Text = "PC-NEW-DEVICE";
                if (results.Rows.Count != 0 ||
                    (selectedSummary is not null &&
                     selectedSummary.Text.Contains("PC-SELECTED",
                         StringComparison.Ordinal)))
                {
                    failures.Add($"{formName}/{scenario.Name}: editing Start search retained stale device metadata or Helpdesk selection.");
                }
            }
        }
        finally
        {
            // The synthetic result was added only to check navigation.
            // Clear its selection before the next assertion checks that
            // Recovery Steps starts disabled without a selected computer.
            // Otherwise the test itself leaves a false-positive enabled
            // action behind, even though production navigation is correct.
            var testGrid = FindByName(start, "HomeUnifiedSearchResults") as DataGridView;
            testGrid?.Rows.Clear();
            homeQuery.Clear();
            tabs.SelectedTab = initialTab;
        }
    }

    private static void VerifyTabControlPages(
        TabControl tabs,
        string path,
        Scenario scenario,
        List<string> failures,
        DpiAwareForm form,
        bool checkRecoveryConnection = false)
    {
        var originalIndex =
            tabs.SelectedIndex;

        try
        {
            for (var index = 0;
                 index < tabs.TabPages.Count;
                 index++)
            {
                tabs.SelectedIndex =
                    index;

                var page =
                    tabs.TabPages[index];
                var pagePath =
                    $"{path}/{page.Text}";

                page.CreateControl();

                if (checkRecoveryConnection &&
                    string.Equals(
                        page.Text,
                        "Start",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var advanced =
                        FindByName(
                            page,
                            "AdvancedConnectionGroup");

                    if (advanced is not null)
                    {
                        advanced.Visible =
                            true;
                    }
                }

                // Tab selection and Dock=Fill layout are message-loop driven in
                // WinForms. A headless WinExe can retain the framework's default
                // 200x100 child size, so normalize Dock=Fill geometry before and
                // after pumping pending layout work.
                NormalizeDockFillSizes(
                    form);
                Application.DoEvents();
                form.PerformLayout();
                tabs.PerformLayout();
                page.PerformLayout();
                NormalizeDockFillSizes(
                    form);
                form.PrepareResponsiveLayoutForTesting();
                Application.DoEvents();
                form.PerformLayout();
                tabs.PerformLayout();
                page.PerformLayout();
                NormalizeDockFillSizes(
                    form);

                VerifyControlTree(
                    page,
                    pagePath,
                    scenario,
                    failures);

                if (checkRecoveryConnection &&
                    string.Equals(page.Text, "LAPS", StringComparison.OrdinalIgnoreCase))
                {
                    VerifyLapsCompactFooter(
                        page,
                        pagePath,
                        scenario,
                        failures);
                }

                if (checkRecoveryConnection &&
                    string.Equals(
                        page.Text,
                        "BitLocker",
                        StringComparison.OrdinalIgnoreCase))
                {
                    VerifyMainConnectionBar(
                        page,
                        pagePath,
                        scenario,
                        failures);
                }
            }
        }
        finally
        {
            if (originalIndex >= 0 &&
                originalIndex <
                tabs.TabPages.Count)
            {
                tabs.SelectedIndex =
                    originalIndex;
            }
        }
    }

    private static void VerifyLapsCompactFooter(
        TabPage page,
        string path,
        Scenario scenario,
        List<string> failures)
    {
        var layout =
            FindByName(page, "LapsWorkspaceLayout") as TableLayoutPanel;
        var viewport =
            FindByName(page, "LapsScrollViewport") as Panel;
        var scroll =
            FindByName(page, "LapsScrollableContent") as TableLayoutPanel;
        var footer =
            FindByName(page, "LapsPasswordPanel") as TableLayoutPanel;
        var password =
            FindByName(page, "LapsPasswordValue") as TextBox;
        var reveal =
            FindByName(page, "LapsRevealPasswordButton") as Button;
        var copy =
            FindByName(page, "LapsCopyPasswordButton") as Button;
        var summary =
            FindByName(page, "LapsSelectedRecordSummary") as UiStatusLabel;
        var status =
            FindByName(page, "LapsOperationStatus") as UiStatusLabel;
        var grid =
            FindByName(page, "LapsKeyResults") as DataGridView;
        var progress =
            FindByName(page, "LapsProgress") as ProgressBar;
        var zeroWarning =
            FindByName(page, "LapsZeroRecordWarning") as UiStatusLabel;
        var zeroWarningPanel =
            FindByName(page, "LapsZeroRecordWarningPanel") as TableLayoutPanel;
        var searchGrid =
            FindByName(page, "LapsSearchResults") as DataGridView;
        var actions =
            FindByName(page, "LapsReadActions") as FlowLayoutPanel;
        var detailsPanel =
            FindByName(page, "LapsDetailsPanel") as TableLayoutPanel;
        var details =
            FindByName(page, "LapsRecordDetails") as RichTextBox;

        if (layout is null || viewport is null || scroll is null || footer is null ||
            password is null || reveal is null || copy is null ||
            summary is null || status is null || grid is null ||
            progress is null || zeroWarning is null || zeroWarningPanel is null ||
            searchGrid is null || actions is null || detailsPanel is null ||
            details is null)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS compact password footer or record grid is missing.");
            return;
        }

        if (!ReferenceEquals(viewport.Parent, layout) ||
            !ReferenceEquals(scroll.Parent, viewport) ||
            !ReferenceEquals(footer.Parent, layout) ||
            layout.GetRow(viewport) != 0 ||
            layout.GetRow(footer) != 1 ||
            layout.RowStyles[0].SizeType != SizeType.Percent ||
            layout.RowStyles[1].SizeType != SizeType.AutoSize ||
            !viewport.AutoScroll ||
            scroll.AutoScroll ||
            !scroll.AutoSize ||
            scroll.AutoSizeMode != AutoSizeMode.GrowAndShrink ||
            scroll.Dock != DockStyle.Top)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS content must use an outer scroll viewport, an AutoSize content table and a separately pinned password footer.");
        }

        // Regression for the actual LAPS decryption screenshot: a percentage
        // row inside an AutoScroll TableLayoutPanel allowed selected-record
        // details to overprint the warning and action row. Every child now
        // consumes its own measured AutoSize row, while both grids scroll
        // internally within an explicit, non-collapsing height.
        if (scroll.RowStyles.Count != 8 ||
            scroll.RowStyles.Cast<RowStyle>().Any(
                row => row.SizeType != SizeType.AutoSize) ||
            !ReferenceEquals(actions.Parent, scroll) ||
            !ReferenceEquals(zeroWarningPanel.Parent, scroll) ||
            !ReferenceEquals(grid.Parent, scroll) ||
            !ReferenceEquals(detailsPanel.Parent, scroll) ||
            scroll.GetRow(actions) != 3 ||
            scroll.GetRow(zeroWarningPanel) != 4 ||
            scroll.GetRow(grid) != 5 ||
            scroll.GetRow(detailsPanel) != 6 ||
            grid.Dock != DockStyle.Top ||
            grid.Height < grid.MinimumSize.Height ||
            searchGrid.Dock != DockStyle.Top ||
            searchGrid.Height < searchGrid.MinimumSize.Height ||
            details.Height < details.MinimumSize.Height ||
            !actions.WrapContents)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS warning, grids, actions and details must occupy non-overlapping AutoSize rows, with wrapping actions and bounded grid heights.");
        }

        if (!ReferenceEquals(password.Parent, footer) ||
            !ReferenceEquals(status.Parent, footer) ||
            !ReferenceEquals(summary.Parent, footer) ||
            !ReferenceEquals(reveal.Parent, copy.Parent) ||
            !ReferenceEquals(reveal.Parent?.Parent, footer) ||
            footer.GetRow(password) != 3 ||
            footer.GetRow(status) != 1 ||
            footer.GetRow(summary) != 2)
        {
            failures.Add(
                $"{path}/{scenario.Name}: selected password, record summary and buttons must be inside the fixed footer.");
        }

        if (!ReferenceEquals(progress.Parent, footer) ||
            footer.GetRow(progress) != 0 ||
            footer.GetColumnSpan(progress) != 2 ||
            progress.Dock != DockStyle.Fill ||
            progress.Style != ProgressBarStyle.Marquee)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS progress must be a full-width bar in the pinned footer, never a clipped fragment inside the scrollable results grid.");
        }

        if (!status.AutoSize ||
            !summary.AutoSize ||
            !zeroWarning.AutoSize ||
            status.AutoEllipsis ||
            summary.AutoEllipsis ||
            zeroWarning.AutoEllipsis ||
            status.MaximumSize.Width <= 0 ||
            summary.MaximumSize.Width <= 0 ||
            zeroWarning.MaximumSize.Width <= 0)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS operation, record summary and zero-record warnings must use width-constrained multiline status labels.");
        }

        if (!password.UseSystemPasswordChar ||
            !password.ReadOnly ||
            password.AccessibleName != "Selected LAPS password")
        {
            failures.Add(
                $"{path}/{scenario.Name}: password field must start masked and accessible.");
        }

        // WinForms Visible includes ancestor visibility. A headless test
        // form is not shown, so even correctly laid out children can have
        // Visible=false. Test the actual geometry and parent structure.
        if (footer.Height <= 0 ||
            footer.Bottom > layout.ClientSize.Height + 3 ||
            viewport.Bottom > footer.Top + 3)
        {
            failures.Add(
                $"{path}/{scenario.Name}: pinned LAPS footer is outside the visible tab viewport " +
                $"(Footer: Y:{footer.Top}, Height:{footer.Height}, Bottom:{footer.Bottom}; " +
                $"Layout: Height={layout.ClientSize.Height}, Scroll: Height={scroll.Height}).");
        }

        foreach (var name in new[] { "Account", "Expires", "Status" })
        {
            if (grid.Columns[name] is not DataGridViewColumn column ||
                column.MinimumWidth < 130)
            {
                failures.Add(
                    $"{path}/{scenario.Name}: LAPS column '{name}' has insufficient readable width.");
            }
        }

        if (grid.ScrollBars != ScrollBars.Both)
        {
            failures.Add(
                $"{path}/{scenario.Name}: narrow LAPS result grids must allow horizontal scrolling.");
        }
    }

    private static void VerifyControlTree(
        Control root,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        foreach (Control control in
                 root.Controls)
        {
            if (!control.Visible)
                continue;

            if (control is UiStatusLabel statusLabel)
            {
                VerifyStatusSurface(
                    statusLabel,
                    formName,
                    scenario,
                    failures);
            }

            if (control is ListView listView &&
                listView.Columns.Count > 0)
            {
                if (!UiStyle.HasConfiguredListViewColumns(
                        listView))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: ListView '{ControlName(listView)}' bypasses shared DPI-aware column configuration.");
                }

                foreach (ColumnHeader column in
                         listView.Columns)
                {
                    var minimumHeaderWidth =
                        UiStyle.GetMinimumListViewHeaderWidth(
                            listView,
                            column.Text);

                    if (column.Width <
                        minimumHeaderWidth)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}: ListView header '{column.Text}' may clip at the active text/DPI scale " +
                            $"(Width={column.Width}, Minimum={minimumHeaderWidth}).");
                    }
                }
            }

            if (control is FlowLayoutPanel flow &&
                flow.FlowDirection is
                    FlowDirection.LeftToRight or
                    FlowDirection.RightToLeft &&
                !flow.WrapContents)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: horizontal FlowLayoutPanel '{ControlName(flow)}' cannot wrap.");
            }

            if (control is Button button &&
                !string.IsNullOrWhiteSpace(
                    button.Text))
            {
                var preferred =
                    button.GetPreferredSize(
                        Size.Empty);

                if (!button.AutoSize)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: button '{button.Text}' is not using shared AutoSize behavior.");
                }

                if (button.MinimumSize.Height <
                    UiStyle.MinimumButtonHeight)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: button '{button.Text}' minimum height is {button.MinimumSize.Height}, expected at least {UiStyle.MinimumButtonHeight}.");
                }

                if (!button.AutoSize &&
                    button.Dock !=
                    DockStyle.Fill &&
                    (button.ClientSize.Width <
                         preferred.Width ||
                     button.ClientSize.Height <
                         preferred.Height))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: button '{button.Text}' may clip its caption.");
                }
            }

            if (control is Label label &&
                label.AutoSize &&
                label.MaximumSize.Width > 0 &&
                label.Parent is not null &&
                label.Parent.ClientSize.Width > 0)
            {
                var parentRight =
                    label.Parent.ClientSize.Width -
                    label.Parent.Padding.Right;

                if (label.Right >
                    parentRight +
                    2)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: wrapping label '{ShortText(label.Text)}' exceeds the visible parent width " +
                        $"(X={label.Left}, Width={label.Width}, Right={label.Right}, ParentWidth={label.Parent.ClientSize.Width}; " +
                        $"Ancestors={DescribeAncestors(label)}).");
                }
            }

            if (control is TabControl nestedTabs)
            {
                var form =
                    nestedTabs.FindForm() as
                    DpiAwareForm;

                if (form is not null)
                {
                    VerifyTabControlPages(
                        nestedTabs,
                        formName,
                        scenario,
                        failures,
                        form);
                }

                continue;
            }

            VerifyControlTree(
                control,
                formName,
                scenario,
                failures);
        }
    }

    private static void VerifyStatusGlyphAlignmentInset(
        List<string> failures)
    {
        using var bordered = new UiStatusLabel();
        UiStyle.ConfigureStatusLabel(bordered);
        UiStyle.SetStatus(bordered, "Connected", UiStatusKind.Success);
        var baseWidth = bordered.GetPreferredSize(Size.Empty).Width;
        var baseX = bordered.GlyphLeftInset;
        bordered.GlyphColumnInset = UiStyle.ControlGap * 2;
        var alignedWidth = bordered.GetPreferredSize(Size.Empty).Width;

        using var inline = new UiStatusLabel();
        UiStyle.ConfigureInlineStatusLabel(inline);
        UiStyle.SetStatus(inline, "Connected as: helpdesk", UiStatusKind.Success);

        if (bordered.GlyphLeftInset - baseX != UiStyle.ControlGap * 2 ||
            alignedWidth < baseWidth + UiStyle.ControlGap * 2 - 1 ||
            inline.GlyphBaseLeftInset >= bordered.GlyphBaseLeftInset)
        {
            failures.Add(
                "Status glyph column offset must change both paint position and measured width, including inline Connected as labels.");
        }
    }

    private static void VerifyHomeStatusGlyphColumn(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var page = FindByName(main, "HomeLayoutRoot");
        if (page is null)
            return;

        var names = new[]
        {
            "HomeConnectionStatus", "HomeAdAccountStatus",
            "HomeCloudConnectionStatus", "StartSetupStatus",
            "HomeUnifiedSearchStatus", "HomeSelectionSummary",
            "SecretLifetimeStatus"
        };
        var visible = names
            .Select(name => FindByName(main, name) as UiStatusLabel)
            .Where(label => label is { Visible: true, Width: > 0, Image: not null })
            .Select(label => new
            {
                Label = label!,
                Painted = MainForm.HomeGlyphPaintedX(page, label!)
            })
            .Where(item => item.Painted.HasValue)
            .ToArray();

        var tolerance = (int)Math.Ceiling(
            UiStyle.HomeStatusGlyphGroupingDistance *
            Math.Max(main.DeviceDpi / (double)UiStyle.BaselineDpi,
                main.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size)));

        var sorted = visible.OrderBy(item =>
            item.Painted!.Value - item.Label.GlyphColumnInset).ToArray();
        for (var first = 0; first < sorted.Length;)
        {
            var start = sorted[first].Painted!.Value -
                sorted[first].Label.GlyphColumnInset;
            var end = first + 1;
            while (end < sorted.Length &&
                   sorted[end].Painted!.Value -
                   sorted[end].Label.GlyphColumnInset - start <= tolerance)
                end++;
            for (var i = first + 1; i < end; i++)
            {
                if (Math.Abs(sorted[i].Painted!.Value -
                             sorted[first].Painted!.Value) > 1)
                    failures.Add($"{formName}/{scenario.Name}: Start status icons are not in a vertical column.");
            }
            first = end;
        }
    }

    private static void VerifyWrappingStatusMeasurement(
        List<string> failures)
    {
        using var label = new UiStatusLabel();
        UiStyle.ConfigureWrappingStatusLabel(label);
        label.Text = string.Concat(
            Enumerable.Repeat(
                "The current LAPS expiration may be overdue; check the password history and authorization. ",
                7));

        label.MaximumSize = new Size(280, 0);
        var wrapped = label.GetPreferredSize(Size.Empty);

        label.MaximumSize = new Size(2200, 0);
        var wide = label.GetPreferredSize(Size.Empty);

        if (wrapped.Width > 280 ||
            wrapped.Height <= wide.Height)
        {
            failures.Add(
                "UiStatusLabel must honor MaximumSize.Width when the parent requests an unconstrained preferred size, so long LAPS warnings wrap and grow vertically.");
        }
    }

    private static void VerifyStatusSurface(
        UiStatusLabel status,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (status.Image is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' has no DPI-safe glyph.");
        }

        if (status.TabStop)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' must not participate in keyboard tab navigation.");
        }

        if (string.IsNullOrWhiteSpace(
                status.AccessibleName) ||
            string.IsNullOrWhiteSpace(
                status.AccessibleDescription))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' is missing accessibility metadata.");
        }

        if (!string.IsNullOrWhiteSpace(
                status.Text) &&
            status.AccessibleDescription is not null &&
            !status.AccessibleDescription.Contains(
                status.Text,
                StringComparison.Ordinal))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' accessibility description is stale.");
        }
    }

    private static void VerifyHighContrastStatusPalette(
        List<string> failures)
    {
        foreach (var kind in
                 Enum.GetValues<UiStatusKind>())
        {
            var highContrast =
                UiStyle.ResolveStatusColors(
                    kind,
                    highContrast: true);

            if (highContrast.BackColor !=
                    SystemColors.Window ||
                highContrast.ForeColor !=
                    SystemColors.WindowText)
            {
                failures.Add(
                    $"High Contrast status palette for {kind} does not use Windows system colors.");
            }
        }
    }

    private static void VerifyListViewColumnScaling(
        List<string> failures)
    {
        const int logical =
            150;

        var scale100 =
            UiStyle.ScaleLogicalPixels(
                logical,
                UiStyle.BaselineDpi);
        var scale150 =
            UiStyle.ScaleLogicalPixels(
                logical,
                144);
        var scale200 =
            UiStyle.ScaleLogicalPixels(
                logical,
                192);
        var roundTrip =
            UiStyle.ToLogicalPixels(
                scale200,
                192);

        if (scale100 != 150 ||
            scale150 != 225 ||
            scale200 != 300 ||
            roundTrip != logical)
        {
            failures.Add(
                $"ListView column scaling failed: logical={logical}; 100%={scale100}; 150%={scale150}; 200%={scale200}; round-trip={roundTrip}.");
        }
    }

    private static void VerifyStatusGlyphScaling(
        List<string> failures)
    {
        var normal =
            UiStatusGlyphs.Get(
                UiStatusKind.Success,
                UiStyle.BaselineDpi,
                Color.DarkGreen);
        var scale200 =
            UiStatusGlyphs.Get(
                UiStatusKind.Success,
                UiStyle.BaselineDpi * 2,
                Color.DarkGreen);

        if (normal.Width <= 0 ||
            normal.Height <= 0 ||
            scale200.Width <
                normal.Width * 2 - 1 ||
            scale200.Height <
                normal.Height * 2 - 1)
        {
            failures.Add(
                $"Status glyph scaling is not DPI-safe: 96-DPI={normal.Width}x{normal.Height}; 192-DPI={scale200.Width}x{scale200.Height}.");
        }
    }

    private static void VerifyMainConnectionBar(
        Control root,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var source =
            FindByName(
                root,
                "RecoverySourceSelector");
        var openStart =
            FindByName(
                root,
                "RecoveryOpenStartButton");

        if (source is null ||
            openStart is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Recovery source / Start connection shortcut was not found.");
            return;
        }

        var forbidden =
            new[]
            {
                "ConnectAdButton",
                "ConnectAdCancelButton",
                "DisconnectAdButton",
                "AdvancedConnectionGroup"
            };

        foreach (var controlName in
                 forbidden)
        {
            if (FindByName(
                    root,
                    controlName) is not null)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Recovery must not own '{controlName}'; AD connection is managed on Start.");
            }
        }
    }

    private static T? FindFirst<T>(
        Control root)
        where T : Control
    {
        if (root is T typed)
            return typed;

        foreach (Control child in
                 root.Controls)
        {
            var found =
                FindFirst<T>(
                    child);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static Control? FindByName(
        Control root,
        string name)
    {
        if (string.Equals(
                root.Name,
                name,
                StringComparison.Ordinal))
        {
            return root;
        }

        foreach (Control child in
                 root.Controls)
        {
            var found =
                FindByName(
                    child,
                    name);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static string DescribeAncestors(
        Control control)
    {
        var parts =
            new List<string>();
        var current =
            control.Parent;

        while (current is not null &&
               parts.Count < 8)
        {
            parts.Add(
                $"{current.GetType().Name}[{current.ClientSize.Width}x{current.ClientSize.Height}]");

            current =
                current.Parent;
        }

        return string.Join(
            " > ",
            parts);
    }

    private static string ControlName(
        Control control) =>
        string.IsNullOrWhiteSpace(
            control.Name)
            ? control.GetType().Name
            : control.Name;

    private static string ShortText(
        string value)
    {
        var text =
            (value ?? string.Empty)
                .Replace(
                    Environment.NewLine,
                    " ",
                    StringComparison.Ordinal)
                .Trim();

        return text.Length <= 48
            ? text
            : text[..48] + "...";
    }
}
