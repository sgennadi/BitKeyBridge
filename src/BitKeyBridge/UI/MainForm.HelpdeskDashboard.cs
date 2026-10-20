namespace BitKeyBridge;

public sealed partial class MainForm
{
    // The landing page is intentionally read-only. Existing authorization
    // checks remain the only authority for revealing recovery secrets.
    private TabPage BuildHelpdeskDashboardTab()
    {
        var page = new TabPage("Helpdesk")
        {
            Name = "HelpdeskDashboardTab",
            AutoScroll = false
        };
        var root = CreateVerticalWorkspace();
        page.Controls.Add(root);
        root.Controls.Add(CreateSectionTitle(
            "Helpdesk workspace",
            "Search and select a device once on Start. Helpdesk contains access checks, recovery guidance and support tools; it never performs a second device search."));

        var actions = CreateActionFlow();
        var search = NewActionButton("Device Workspace");
        search.Name = "HelpdeskOpenDeviceWorkspaceButton";
        search.AccessibleName = "Open the existing Start device search and selection";
        var prepare = NewActionButton("Recovery Steps");
        prepare.Name = "HelpdeskRecoveryStepsButton";
        prepare.AccessibleName = "Review recovery steps without revealing a password";
        // BitLocker and LAPS already have main tabs and selected-device
        // cards on Start; do not duplicate those navigation controls here.
        var access = NewActionButton("Access Health");
        var support = NewActionButton("Support Bundle");
        actions.Controls.AddRange([search, prepare, access, support]);
        root.Controls.Add(actions);

        _helpdeskSelectedDeviceSummary = new UiStatusLabel
        {
            Name = "HelpdeskSelectedDeviceSummary",
            AccessibleName = "Selected device metadata from Start; no recovery secrets",
            AutoSize = true,
            MaximumSize = new Size(1000, 0)
        };
        UiStyle.ConfigureStatusLabel(_helpdeskSelectedDeviceSummary);
        root.Controls.Add(_helpdeskSelectedDeviceSummary);

        var status = new UiStatusLabel
        {
            Name = "HelpdeskDashboardStatus",
            AccessibleName = "Current Helpdesk readiness",
            AutoSize = true,
            MaximumSize = new Size(1000, 0)
        };
        UiStyle.ConfigureStatusLabel(status);
        root.Controls.Add(status);

        var refresh = NewActionButton("Refresh Status");
        var setup = NewActionButton("Setup & Permissions");
        var checklist = NewActionButton("Deployment Checklist");
        var probe = NewActionButton("Run Access Checks");
        var diagnostics = CreateActionFlow();
        diagnostics.Controls.AddRange([refresh, probe, setup, checklist]);
        root.Controls.Add(diagnostics);

        void UpdateStatus()
        {
            if (_layoutSelfTest)
            {
                UiStyle.SetStatus(status, "Layout preview - checks not run.", UiStatusKind.Neutral);
                return;
            }

            bool searchAllowed;
            try
            {
                searchAllowed = new AuthorizationService(_config)
                    .Check(BitKeyBridgePermission.SearchDevices)
                    .Allowed;
            }
            catch (Exception ex)
            {
                // A restricted Windows token must not crash the landing tab
                // or imply that access has been granted.
                System.Diagnostics.Debug.WriteLine(
                    "Helpdesk search readiness unavailable: " + ex.GetType().Name);
                UiStyle.SetStatus(status,
                    "Search permissions could not be verified. Open Access Health; " +
                    "recovery access remains subject to its normal authorization checks.",
                    UiStatusKind.Warning);
                return;
            }
            var audit = _config.RequireProtectedAuditForRecovery
                ? "Protected audit required"
                : "Protected audit not required by current policy";
            // Reflect the selected connection source. A cloud-only Helpdesk
            // session must not be presented as simply "AD disconnected".
            // HasCloudSearchSession checks in-memory token state only.
            var adStatus = _directoryConnected ? "AD connected" : "AD disconnected";
            var cloudStatus = HasCloudSearchSession()
                ? "Microsoft 365 connected" : "Microsoft 365 disconnected";
            var connection = StartConnectionPolicy.Normalize(SelectedStartSource) switch
            {
                StartConnectionPolicy.ActiveDirectory => adStatus,
                StartConnectionPolicy.MicrosoftCloud => cloudStatus,
                StartConnectionPolicy.Both => adStatus + " / " + cloudStatus,
                _ => "Choose a connection source on Start"
            };
            UiStyle.SetStatus(status,
                $"{connection} | {audit} | Search: {(searchAllowed ? "permitted" : "denied")}. " +
                "Use Access Health for detailed checks; status alone does not authorize secret reads.",
                searchAllowed ? UiStatusKind.Neutral : UiStatusKind.Warning);
        }

        // The same selected HomeSearchResult and source identity are reused.
        // Helpdesk does not create an independent query or fetch secrets.
        search.Click += (_, _) => OpenHelpdeskDeviceWorkspace();
        prepare.Click += (_, _) => ShowRecoveryWorkflowSteps();
        access.Click += (_, _) => OpenHelpdeskAccessHealth();
        support.Click += (_, _) =>
        {
            if (!_layoutSelfTest)
                CreateQuickDiagnosticsBundleGui();
        };
        refresh.Click += (_, _) => UpdateStatus();
        setup.Click += (_, _) => OpenHelpdeskSetup();
        checklist.Click += (_, _) => ShowHelpdeskDeploymentChecklist();
        probe.Click += async (_, _) =>
        {
            OpenHelpdeskAccessHealth();
            if (!_layoutSelfTest && _accessHealthRun.Enabled)
                await RunAccessHealthAsync();
        };
        page.Enter += (_, _) =>
        {
            UpdateStatus();
            UpdateHelpdeskSelectedDeviceSummary();
        };
        UpdateStatus();
        UpdateHelpdeskSelectedDeviceSummary();
        return page;
    }

    private void ShowHelpdeskDeploymentChecklist()
    {
        using var wizard = CreateHelpdeskDeploymentWizard();
        wizard.ShowDialog(this);
    }

    // Shared by the real dialog and the offline 100-200% UI regression test.
    internal DpiAwareForm CreateHelpdeskDeploymentWizard()
    {
        // Read-only guided preparation. All privileged changes remain in the
        // existing administrator UI / CLI with their own authorization gates.
        var wizard = new DpiAwareForm
        {
            Text = "Helpdesk deployment - preparation",
            Name = "HelpdeskDeploymentWizard",
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(570, 460),
            Size = new Size(780, 590),
            AutoScaleMode = AutoScaleMode.Dpi,
            FormBorderStyle = FormBorderStyle.Sizable,
            ShowInTaskbar = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            AutoScroll = true,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        wizard.Controls.Add(layout);

        var heading = new Label
        {
            Text = "Prepare a Helpdesk workstation",
            AutoSize = true,
            Font = UiStyle.CreateEmphasisFont(15F),
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(heading, 0, 0);

        var steps = new CheckedListBox
        {
            Name = "HelpdeskDeploymentSteps",
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            AccessibleName = "Helpdesk deployment preparation checklist"
        };
        steps.Items.AddRange(new object[]
        {
            "Install the Windows service on a test workstation (administrator)",
            "Configure a scoped RecoveryReaders group; disable admin bypass",
            "Verify AD and Entra / Intune delegation independently",
            "Provision protected audit and restart the service (administrator)",
            "Sign in as an authorized non-admin and run Access Health",
            "Verify unauthorized access is denied and secrets are never logged",
            "Validate protected journal, RDP, 100-200% DPI and rollback"
        });
        layout.Controls.Add(steps, 0, 1);

        // Live, non-privileged checks: these never read recovery secrets or
        // mutate service configuration. They do not replace Access Health.
        var readiness = new Label
        {
            Name = "HelpdeskDeploymentReadiness",
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(900, 0),
            Text = "Readiness has not been checked."
        };
        var healthResults = new ListView
        {
            Name = "HelpdeskHealthDashboard",
            AccessibleName = "Helpdesk readiness results",
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Height = 165,
            Dock = DockStyle.Top
        };
        UiStyle.ConfigureListViewColumns(healthResults, ("Check", 190), ("Result", 360));
        void ShowHealth(string name, string result)
        {
            if (wizard.IsDisposed || healthResults.IsDisposed)
                return;
            var item = healthResults.Items.Cast<ListViewItem>()
                .FirstOrDefault(entry => entry.Text == name);
            if (item is null)
            {
                item = new ListViewItem(name);
                item.SubItems.Add(result);
                healthResults.Items.Add(item);
            }
            else
                item.SubItems[1].Text = result;
        }
        var readinessRunning = false;
        async void CheckReadiness()
        {
            if (readinessRunning || wizard.IsDisposed)
                return;
            readinessRunning = true;
            readiness.Text = "Checking readiness...";
            healthResults.Items.Clear();
            try
            {
                var authorization = new AuthorizationService(_config)
                    .Check(BitKeyBridgePermission.SearchDevices);
                string serviceStatus;
                try
                {
                    var service = WindowsServiceHost.GetInfo();
                    serviceStatus = !service.Installed
                        ? "not installed"
                        : $"{service.State} / {service.Identity}";
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                    or UnauthorizedAccessException or InvalidOperationException)
                {
                    serviceStatus = "unavailable (query failed)";
                }
                var brokerStatus = !_config.AuditBrokerEnabled
                    ? "disabled in configuration"
                    : !_config.RequireProtectedAuditForRecovery
                        ? "enabled; strict audit not required"
                        : "enabled; strict audit required (runtime unverified)";
                string journalStatus;
                try
                {
                    journalStatus = ProtectedAuditStoragePolicy.IsTrustedBrokerJournal(
                        AppPaths.BrokerAuditLogFile)
                        ? "protected ACL verified"
                        : "not verified / untrusted";
                }
                catch (Exception)
                {
                    journalStatus = "verification unavailable";
                }
                var integrityStatus = "not checked";
                // The protected journal is normally readable only by SYSTEM/admins.
                // Never treat access denied as a successful integrity check.
                if (journalStatus == "protected ACL verified")
                {
                    try
                    {
                        // Journal verification may scan a large file and wait for its lock.
                        // Keep the WinForms UI responsive while it runs.
                        var integrity = await Task.Run(() =>
                            AuditIntegrityService.Verify(AppPaths.BrokerAuditLogFile));
                        integrityStatus = integrity.Valid
                            ? "hash chain valid"
                            : "hash chain invalid or unavailable";
                    }
                    catch (Exception)
                    {
                        integrityStatus = "unavailable (insufficient access or I/O error)";
                    }
                }
                if (wizard.IsDisposed || readiness.IsDisposed)
                    return;
                ShowHealth("Windows service", serviceStatus);
                ShowHealth("Active Directory", _directoryConnected ? "Connected" : "Not connected");
                ShowHealth("Search authorization", authorization.Allowed ? "Allowed" : "Denied");
                ShowHealth("Audit broker policy", brokerStatus);
                ShowHealth("Protected journal ACL", journalStatus);
                ShowHealth("Audit hash chain", integrityStatus);
                readiness.Text =
                    "Local readiness checks completed. Review each result below; " +
                    "Access Health and an actual delegated account are required " +
                    "before recovery can be approved.";
            }
            catch (Exception)
            {
                if (!wizard.IsDisposed)
                    readiness.Text = "Readiness check failed. Open Access Health for diagnostics.";
            }
            finally
            {
                readinessRunning = false;
            }
        }


        var progress = new Label
        {
            Name = "HelpdeskDeploymentProgress",
            AutoSize = true,
            AccessibleName = "Completed deployment preparation steps",
            Text = "0 of 7 steps marked complete",
            Margin = new Padding(0, 8, 0, 8)
        };
        // The checklist is a manual aid, not a security validation result.
        steps.ItemCheck += (_, args) =>
        {
            var count = steps.CheckedItems.Count;
            if (args.NewValue == CheckState.Checked && args.CurrentValue != CheckState.Checked)
                count++;
            else if (args.NewValue != CheckState.Checked && args.CurrentValue == CheckState.Checked)
                count--;
            progress.Text = $"{count} of {steps.Items.Count} steps marked complete";
        };

        var guidance = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = "Checking an item records progress in this window only. " +
                   "It does not install services, modify RBAC, grant directory access, " +
                   "or assert that a security check has passed.",
            Margin = new Padding(0, 12, 0, 12),
            MaximumSize = new Size(900, 0)
        };
        var notes = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        notes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        notes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notes.Controls.Add(progress, 0, 0);
        notes.Controls.Add(guidance, 0, 1);
        notes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notes.Controls.Add(readiness, 0, 2);
        notes.RowCount = 4;
        notes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notes.Controls.Add(healthResults, 0, 3);
        layout.Controls.Add(notes, 0, 2);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        var check = NewActionButton("Check Readiness");
        var settings = NewActionButton("Open Admin Settings");
        var health = NewActionButton("Open Access Health");
        var close = NewActionButton("Close");
        settings.Enabled = AdministrationAllowed;
        actions.Controls.AddRange([check, settings, health, close]);
        check.Click += (_, _) => CheckReadiness();
        if (!_layoutSelfTest)
            CheckReadiness();
        layout.Controls.Add(actions, 0, 3);
        settings.Click += (_, _) =>
        {
            wizard.Close();
            OpenHelpdeskSetup();
        };
        health.Click += (_, _) =>
        {
            wizard.Close();
            OpenHelpdeskAccessHealth();
        };
        close.Click += (_, _) => wizard.Close();
        wizard.CancelButton = close;
        wizard.AcceptButton = check;
        return wizard;
    }

    private void OpenHelpdeskSetup()
    {
        if (!AdministrationAllowed)
        {
            MessageBox.Show(this,
                "Helpdesk setup requires administrator authorization. " +
                "Ask an administrator to configure the protected audit broker and AD delegation. " +
                "Use Access Health to verify your existing permissions.",
                "Helpdesk setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenHelpdeskAccessHealth();
            return;
        }

        foreach (TabPage page in _mainTabs.TabPages)
        {
            // Text changes with language: Settings is a stable control ID.
            if (!string.Equals(page.Name, "SettingsWorkspace",
                    StringComparison.Ordinal))
                continue;
            _mainTabs.SelectedTab = page;
            foreach (Control control in page.Controls)
            {
                if (control is not TabControl nested)
                    continue;
                foreach (TabPage section in nested.TabPages)
                {
                    if (section.Name == "HelpdeskBasicsTab")
                    {
                        nested.SelectedTab = section;
                        return;
                    }
                }
            }
        }
    }

    private void OpenHelpdeskAccessHealth()
    {
        // Reuse the exact Start AD selection without copying any recovery
        // key, LAPS password, ticket or cloud token into the diagnostic UI.
        // Never carry an ambiguous/cloud hostname into the AD query.
        if (!_layoutSelfTest && _accessHealthRun.Enabled &&
            !_accessHealthComputer.IsDisposed)
        {
            var selected = GetSelectedHomeSearchResult();
            var safeName = AccessHealthGuidanceService.SafeAdComputerName(selected);
            if (safeName is not null)
            {
                _accessHealthComputer.Text = safeName;
                UiStyle.SetStatus(_accessHealthStatus,
                    "Using selected Start computer " + safeName +
                    ". Tests have not run; this is not recovery authorization.",
                    UiStatusKind.Neutral);
            }
            else if (selected is not null)
            {
                // An old AD name must never masquerade as the newly selected
                // cloud/ambiguous object during a cross-workspace switch.
                _accessHealthComputer.Clear();
                UiStyle.SetStatus(_accessHealthStatus,
                    "Selected device cannot be safely reused as an AD computer. " +
                    "Confirm its unique directory ID before entering a diagnostic query.",
                    UiStatusKind.Warning);
            }
        }

        foreach (TabPage page in _mainTabs.TabPages)
        {
            // Works under English, Russian and mirrored Hebrew navigation.
            if (!string.Equals(page.Name, "DiagnosticsWorkspace",
                    StringComparison.Ordinal))
                continue;
            _mainTabs.SelectedTab = page;
            foreach (Control control in page.Controls)
            {
                if (control is not TabControl nested)
                    continue;
                foreach (TabPage section in nested.TabPages)
                {
                    if (section.Name == "AccessHealthTab")
                    {
                        nested.SelectedTab = section;
                        return;
                    }
                }
            }
        }
    }
}
