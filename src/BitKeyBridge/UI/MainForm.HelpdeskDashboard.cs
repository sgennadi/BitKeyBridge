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
        var probe = NewActionButton("Run Access Checks");
        var diagnostics = CreateActionFlow();
        diagnostics.Controls.AddRange([refresh, probe, setup]);
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

        search.Click += (_, _) =>
        {
            if (_homeWorkspaceTab is not null)
            {
                _mainTabs.SelectedTab = _homeWorkspaceTab;
                _homeQuery.Focus();
            }
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
