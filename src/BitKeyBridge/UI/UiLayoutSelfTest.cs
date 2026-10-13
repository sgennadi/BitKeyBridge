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

        VerifyStatusGlyphScaling(
            failures);

        VerifyWrappingStatusMeasurement(
            failures);

        VerifyHighContrastStatusPalette(
            failures);

        VerifyListViewColumnScaling(
            failures);

        VerifyLapsHelpdeskStates(
            failures);

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
            VerifyMainFormTabs(form, formName, scenario, failures);
            VerifySelectedHomeRecoveryLayout(main, formName, scenario, failures);
        }
        else
        {
            if (!form.AutoScroll)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: dialog scrolling is disabled.");
            }
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

    private static void VerifySelectedHomeRecoveryLayout(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var root = FindByName(main, "HomeLayoutRoot") as TableLayoutPanel;
        var area = FindByName(main, "HomeSearchResultsArea") as TableLayoutPanel;
        var grid = FindByName(main, "HomeUnifiedSearchResults") as DataGridView;
        var panel = FindByName(main, "HomeSelectionPanel") as TableLayoutPanel;
        var cards = FindByName(main, "HomeServiceCards") as TableLayoutPanel;
        var recoveryHost = FindByName(main, "HomeRecoveryPaneHost") as TableLayoutPanel;
        var computersHost = FindByName(main, "HomeComputersPaneHost") as TableLayoutPanel;
        var swap = FindByName(main, "HomeSwapPanesButton") as Button;

        if (root is null || area is null || grid is null ||
            panel is null || cards is null || recoveryHost is null ||
            computersHost is null || swap is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: selected recovery layout is missing.");
            return;
        }

        if (root.RowStyles[5].SizeType != SizeType.AutoSize ||
            root.RowStyles[7].SizeType != SizeType.Percent)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: selected recovery cards must not be placed below a percentage-height results grid.");
        }

        // Metadata-only mock: no AD bind, Graph call or secret is performed.
        try
        {
            var index = grid.Rows.Add(
                "UI-TEST-PC", "AD", "Available", DateTime.UtcNow,
                "Detected", DateTime.UtcNow);
            grid.Rows[index].Tag = new HomeSearchResult
            {
                ComputerName = "UI-TEST-PC",
                ComputerId = Guid.Empty.ToString(),
                BitLockerKeyCount = 1,
                BitLockerStatus = "Available",
                LapsCandidateFound = true,
                LapsStatus = "Detected"
            };
            grid.ClearSelection();
            grid.Rows[index].Selected = true;
            grid.CurrentCell = grid.Rows[index].Cells[0];
            main.ReflowMainViewport();

            var pairWidth = (int)Math.Ceiling(
                UiStyle.HomeResultsSideBySideMinimumWidth *
                main.DeviceDpi / (double)UiStyle.BaselineDpi);
            var available = area.Parent?.ClientSize.Width ?? 0;
            bool expectedSideBySide = available -
                (area.Parent?.Padding.Horizontal ?? 0) >= pairWidth;
            var computerCell = area.GetPositionFromControl(computersHost);
            var recoveryCell = area.GetPositionFromControl(recoveryHost);
            var correct = expectedSideBySide
                ? area.ColumnCount == 2 && area.RowCount == 1 &&
                  recoveryCell.Row == 0 && computerCell.Row == 0 &&
                  recoveryCell.Column != computerCell.Column &&
                  recoveryCell.Column is 0 or 1 &&
                  computerCell.Column is 0 or 1 &&
                  area.ColumnStyles.Count == 2 &&
                  Math.Abs(area.ColumnStyles[recoveryCell.Column].Width -
                           UiStyle.HomeRecoveryColumnPercent) < 0.1F &&
                  Math.Abs(area.ColumnStyles[computerCell.Column].Width -
                           (100F - UiStyle.HomeRecoveryColumnPercent)) < 0.1F
                : area.ColumnCount == 1 && area.RowCount == 2 &&
                  computerCell.Column == 0 && computerCell.Row == 0 &&
                  recoveryCell.Column == 0 && recoveryCell.Row == 1 &&
                  area.RowStyles[0].SizeType == SizeType.Absolute;

            if (!correct || !ReferenceEquals(cards.Parent, panel) ||
                cards.Controls.Count != 2)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: wide Start must keep the recovery pane at 66% on either side of the 34% computer pane; compact Start must show the list above the recovery cards.");
            }

            if (expectedSideBySide)
            {
                // Swap the two real WinForms pane hosts without altering source
                // connections, selected results, secrets or saved user settings.
                var originalRecoveryColumn = recoveryCell.Column;
                if (!swap.Enabled)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: the pane swap action must be enabled for a wide selected-computer layout.");
                }
                else
                {
                    main.SwapHomePanesForUiSelfTest();
                    main.ReflowMainViewport();
                    var newRecoveryColumn = area.GetPositionFromControl(recoveryHost).Column;
                    var newComputerColumn = area.GetPositionFromControl(computersHost).Column;
                    if (newRecoveryColumn == originalRecoveryColumn ||
                        newComputerColumn == newRecoveryColumn ||
                        grid.SelectedRows.Count != 1 ||
                        !ReferenceEquals(cards.Parent, panel))
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}: pane swap failed: recovery column {originalRecoveryColumn}->{newRecoveryColumn}, computer column {newComputerColumn}, selected rows {grid.SelectedRows.Count}, card parent intact {ReferenceEquals(cards.Parent, panel)}.");
                    }

                    // Restore initial order. Layout tests never write profile preferences.
                    main.SwapHomePanesForUiSelfTest();
                    main.ReflowMainViewport();
                }
            }
            else if (swap.Enabled)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: swap is disabled when panes are stacked for narrow displays.");
            }

            if (FindByName(cards, "HomeRevealBitLockerButton") is Button { Enabled: true } ||
                FindByName(cards, "HomeRevealLapsButton") is Button { Enabled: true })
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: metadata selection must not enable secret actions without authenticated source access.");
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
        if (StartConnectionPolicy.Normalize("unknown") != StartConnectionPolicy.Choose ||
            StartConnectionPolicy.NeedsActiveDirectory(StartConnectionPolicy.MicrosoftCloud) ||
            StartConnectionPolicy.NeedsMicrosoftCloud(StartConnectionPolicy.ActiveDirectory) ||
            !StartConnectionPolicy.NeedsActiveDirectory(StartConnectionPolicy.Both) ||
            !StartConnectionPolicy.NeedsMicrosoftCloud(StartConnectionPolicy.Both))
        {
            failures.Add("Start connection source must isolate AD from cloud-only searches.");
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

        if (tabs.TabPages.Count == 0 ||
            !string.Equals(
                tabs.TabPages[0].Text,
                "Start",
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Start must be the first main workspace tab.");
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
                    "HomeReconnectWizardButton",
                    "HomeRecoveryPaneHost",
                    "HomeComputersPaneHost",
                    "HomeSwapPanesButton",
                    "HomeConnectCloudButton",
                    "HomeConfigureCloudButton",
                    "HomeCloudConnectionStatus",
                    "HomeConnectionStatus",
                    "HomeAdAccountStatus",
                    "HomeConnectAdButton",
                    "HomeDisconnectAdButton",
                    "HomeEnvironmentCheckButton",
                    "HomeAdvancedDiagnosticsButton",
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
                    "HomeConnectionSettingsButton",
                    "AdvancedConnectionGroup"
                };

            if (FindByName(startPage, "HomeServiceCards") is TableLayoutPanel cards)
            {
                if (FindByName(cards, "HomeBitLockerCard") is not UiRecoveryCard bitLocker ||
                    FindByName(cards, "HomeLapsCard") is not UiRecoveryCard laps)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start must contain BitLocker and LAPS recovery cards.");
                }
                else if (cards.GetPositionFromControl(bitLocker).Column != 0 ||
                         cards.GetPositionFromControl(laps).Column != 1)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: BitLocker must be left of LAPS at startup.");
                }

                if (cards.Visible ||
                    FindByName(startPage, "HomeRevealLapsButton") is Button { Enabled: true } ||
                    FindByName(startPage, "HomeCopyLapsButton") is Button { Enabled: true } ||
                    FindByName(startPage, "HomeLoadLapsHistoryButton") is Button { Enabled: true })
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start cannot reveal or copy LAPS without a selected computer and explicit action.");
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

            if (FindByName(
                    startPage,
                    "HomeSearchResultsArea") is TableLayoutPanel resultsArea &&
                FindByName(
                    startPage,
                    "HomeUnifiedSearchResults") is DataGridView resultsGrid &&
                FindByName(
                    startPage,
                    "HomeSelectionPanel") is TableLayoutPanel selectionPanel)
            {
                var computersHost =
                    FindByName(startPage, "HomeComputersPaneHost") as TableLayoutPanel;
                var recoveryHost =
                    FindByName(startPage, "HomeRecoveryPaneHost") as TableLayoutPanel;
                if (computersHost is null || recoveryHost is null ||
                    !ReferenceEquals(resultsGrid.Parent, computersHost) ||
                    !ReferenceEquals(selectionPanel.Parent, recoveryHost) ||
                    resultsArea.GetPositionFromControl(computersHost).Column != 0 ||
                    resultsArea.GetPositionFromControl(computersHost).Row != 0 ||
                    resultsArea.GetPositionFromControl(recoveryHost).Column != 0 ||
                    resultsArea.GetPositionFromControl(recoveryHost).Row != 1)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: before a computer is selected, the bounded computer picker must be above the hidden recovery pane.");
                }

                if (selectionPanel.Visible)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start selection actions must stay hidden until a result is selected.");
                }
            }
            else
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start results/action layout container is missing.");
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
                "RetryUpdateButton") is null ||
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

        if (FindByName(
                form,
                "HelpdeskBasicsTab") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Administration is missing the Helpdesk basics workspace.");
        }

        if (FindByName(
                form,
                "AccessHealthTab") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Health & Audit is missing the Access Health workspace.");
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
            tabs,
            formName,
            scenario,
            failures);
    }

    private static void VerifyComputerQueryNavigation(
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

        var homeQuery = start is null
            ? null : FindByName(start, "HomeUnifiedQuery") as TextBox;
        var recoveryQuery = recovery is null
            ? null : FindByName(recovery, "BitLockerSearchQuery") as TextBox;
        var lapsQuery = laps is null
            ? null : FindByName(laps, "LapsLiveSearchQuery") as TextBox;

        if (start is null || recovery is null || laps is null ||
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
                    "PC-SELECTED", "AD", "Available", null, "Detected", null);
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
                tabs.SelectedTab = recovery;
                if (recoveryQuery.Text != "PC-SELECTED")
                    failures.Add($"{formName}/{scenario.Name}: selected Start computer was not retained.");
            }
        }
        finally
        {
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
