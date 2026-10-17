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
            AutoScroll = true
        };
        var root = CreateVerticalWorkspace();
        page.Controls.Add(root);
        root.Controls.Add(CreateSectionTitle(
            "Helpdesk workspace",
            "Find a device, review recovery options, or diagnose access. No administrator rights are required to open this page."));

        var actions = CreateActionFlow();
        var search = NewActionButton("Find Device");
        var bitlocker = NewActionButton("BitLocker");
        var laps = NewActionButton("LAPS");
        var access = NewActionButton("Access Health");
        var support = NewActionButton("Support Bundle");
        actions.Controls.AddRange([search, bitlocker, laps, access, support]);
        root.Controls.Add(actions);

        var quickQuery = new TextBox
        {
            Name = "HelpdeskQuickSearch",
            AccessibleName = "Find a computer or device",
            PlaceholderText = "Computer name or device ID",
            Dock = DockStyle.Top
        };
        root.Controls.Add(quickQuery);

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

            var permission = new AuthorizationService(_config)
                .Check(BitKeyBridgePermission.SearchDevices);
            var audit = _config.RequireProtectedAuditForRecovery
                ? "Protected audit required"
                : "Protected audit not required by current policy";
            var connection = _directoryConnected
                ? "AD connected"
                : "AD not connected";
            UiStyle.SetStatus(status,
                $"{connection} | {audit} | Search: {(permission.Allowed ? "permitted" : "denied")}. " +
                "Use Access Health for detailed checks; status alone does not authorize secret reads.",
                permission.Allowed ? UiStatusKind.Neutral : UiStatusKind.Warning);
        }

        void OpenSearch()
        {
            if (_homeWorkspaceTab is null)
                return;
            _mainTabs.SelectedTab = _homeWorkspaceTab;
            if (!string.IsNullOrWhiteSpace(quickQuery.Text))
                _homeQuery.Text = quickQuery.Text.Trim();
            _homeQuery.Focus();
        }
        search.Click += (_, _) => OpenSearch();
        quickQuery.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            OpenSearch();
        };
        bitlocker.Click += (_, _) =>
        {
            if (_recoveryWorkspaceTab is not null)
                _mainTabs.SelectedTab = _recoveryWorkspaceTab;
        };
        laps.Click += (_, _) =>
        {
            if (_lapsWorkspaceTab is not null)
                _mainTabs.SelectedTab = _lapsWorkspaceTab;
        };
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
        page.Enter += (_, _) => UpdateStatus();
        UpdateStatus();
        return page;
    }

    private void ShowHelpdeskDeploymentChecklist()
    {
        // Read-only guided preparation. All privileged changes remain in the
        // existing administrator UI / CLI with their own authorization gates.
        using var wizard = new Form
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
            MaximumSize = new Size(0, 0),
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
                    $"Protected journal: {journalStatus} | Integrity: {integrityStatus} | " +
                    $"Audit broker: {brokerStatus} | " +
                    $"Service: {serviceStatus} | " +
                    $"Directory: {(_directoryConnected ? "connected" : "not connected")} | " +
                    $"Search authorization: {(authorization.Allowed ? "allowed" : "denied")} | " +
                    $"Protected audit policy: {(_config.RequireProtectedAuditForRecovery ? "required" : "optional")}. " +
                    "Use Access Health for protected broker and delegation checks.";
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
            Margin = new Padding(0, 12, 0, 12)
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
        wizard.ShowDialog(this);
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
            if (page.Text != "Settings")
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
        foreach (TabPage page in _mainTabs.TabPages)
        {
            if (page.Text != "Diagnostics")
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
