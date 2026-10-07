namespace BitKeyBridge;

public sealed class PrivilegedAccessSettingsDialog : DpiAwareForm
{
    private readonly AppConfig _config;

    private readonly CheckBox _jitEnabled = new();
    private readonly NumericUpDown _jitMinutes = new();
    private readonly TextBox _jitGrantors = new();
    private readonly TextBox _jitSubject = new();
    private readonly TextBox _jitReason = new();
    private readonly TextBox _jitGrantId = new();
    private readonly UiStatusLabel _jitStatus = new();

    private readonly CheckBox _approvalEnabled = new();
    private readonly NumericUpDown _approvalMinutes = new();
    private readonly TextBox _approvers = new();
    private readonly TextBox _approvalSession = new();
    private readonly TextBox _approvalComment = new();
    private readonly UiStatusLabel _approvalStatus = new();

    private readonly CheckBox _siemEnabled = new();
    private readonly ComboBox _siemMode = new();
    private readonly TextBox _siemFile = new();
    private readonly TextBox _siemWebhook = new();
    private readonly TextBox _siemCertificate = new();
    private readonly NumericUpDown _siemTimeout = new();
    private readonly NumericUpDown _siemFlushMinutes = new();
    private readonly NumericUpDown _siemMaxOutbox = new();
    private readonly CheckBox _siemFailClosed = new();
    private readonly UiStatusLabel _siemStatus = new();
    private readonly UiDiagnosticPanel _diagnostics = new();

    public PrivilegedAccessSettingsDialog(
        AppConfig config)
    {
        _config = config;

        Text = "Privileged Recovery Access";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 860);
        MinimumSize = new Size(760, 650);
        Font = UiStyle.BodyFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 7
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 7; row++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Optional privileged recovery controls",
            Font = UiStyle.CreateDialogTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text =
                "JIT recovery, two-person approval, and SIEM forwarding are opt-in. " +
                "Upgrades never enable them automatically. Recovery passwords are never forwarded to SIEM.",
            AutoSize = true,
            MaximumSize = new Size(960, 0),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 1);

        root.Controls.Add(BuildJitGroup(), 0, 2);
        root.Controls.Add(BuildApprovalGroup(), 0, 3);
        root.Controls.Add(BuildSiemGroup(), 0, 4);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var refresh =
            UiStyle.CreateActionButton(
                "Refresh Status");
        refresh.Anchor =
            AnchorStyles.Left;
        footer.Controls.Add(refresh, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };
        var cancel =
            UiStyle.CreateActionButton(
                "Cancel",
                DialogResult.Cancel);
        var save =
            UiStyle.CreateActionButton(
                "Save & Apply");
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        footer.Controls.Add(buttons, 1, 0);
        root.Controls.Add(_diagnostics, 0, 5);
        root.Controls.Add(footer, 0, 6);

        AcceptButton = save;
        CancelButton = cancel;

        save.Click += (_, _) => SaveSettings();
        refresh.Click += (_, _) => RefreshStatus();

        LoadSettings();
        RefreshStatus();
    }

    private GroupBox BuildJitGroup()
    {
        var group = CreateResponsiveGroup("JIT recovery grant");
        var layout = CreateFourColumnLayout();
        group.Controls.Add(layout);

        _jitEnabled.Text = "Enable JIT recovery requirement";
        _jitEnabled.AutoSize = true;
        _jitEnabled.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_jitEnabled, 0, 0);
        layout.SetColumnSpan(_jitEnabled, 2);

        layout.Controls.Add(FieldLabel("Grant lifetime (min):"), 2, 0);
        ConfigureNumeric(_jitMinutes, 1, 1440);
        layout.Controls.Add(_jitMinutes, 3, 0);

        layout.Controls.Add(FieldLabel("Authorized grantors:"), 0, 1);
        _jitGrantors.Multiline = true;
        _jitGrantors.ScrollBars = ScrollBars.Vertical;
        _jitGrantors.Dock = DockStyle.Fill;
        _jitGrantors.MinimumSize = new Size(0, 64);
        layout.Controls.Add(_jitGrantors, 1, 1);

        layout.Controls.Add(FieldLabel("Grant subject:"), 2, 1);
        _jitSubject.Dock = DockStyle.Fill;
        layout.Controls.Add(_jitSubject, 3, 1);

        layout.Controls.Add(FieldLabel("Reason:"), 2, 2);
        _jitReason.Dock = DockStyle.Fill;
        layout.Controls.Add(_jitReason, 3, 2);

        layout.Controls.Add(FieldLabel("Grant ID:"), 0, 3);
        _jitGrantId.Dock = DockStyle.Fill;
        _jitGrantId.PlaceholderText = "Grant ID for revoke";
        layout.Controls.Add(_jitGrantId, 1, 3);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true
        };
        var revoke =
            UiStyle.CreateActionButton(
                "Revoke ID");
        var grant =
            UiStyle.CreateActionButton(
                "Grant");
        actions.Controls.Add(revoke);
        actions.Controls.Add(grant);
        layout.Controls.Add(actions, 2, 3);
        layout.SetColumnSpan(actions, 2);

        UiStyle.ConfigureStatusLabel(
            _jitStatus);
        _jitStatus.AccessibleName =
            "JIT recovery status";
        _jitStatus.MaximumSize =
            new Size(
                940,
                0);
        _jitStatus.Margin =
            new Padding(
                0,
                6,
                0,
                0);
        layout.Controls.Add(_jitStatus, 0, 4);
        layout.SetColumnSpan(_jitStatus, 4);

        grant.Click += (_, _) => GrantJit();
        revoke.Click += (_, _) => RevokeJit();

        return group;
    }

    private GroupBox BuildApprovalGroup()
    {
        var group = CreateResponsiveGroup("Two-person approval");
        var layout = CreateFourColumnLayout();
        group.Controls.Add(layout);

        _approvalEnabled.Text = "Require second authorized Windows user";
        _approvalEnabled.AutoSize = true;
        _approvalEnabled.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_approvalEnabled, 0, 0);
        layout.SetColumnSpan(_approvalEnabled, 2);

        layout.Controls.Add(FieldLabel("Approval lifetime (min):"), 2, 0);
        ConfigureNumeric(_approvalMinutes, 1, 1440);
        layout.Controls.Add(_approvalMinutes, 3, 0);

        layout.Controls.Add(FieldLabel("Authorized approvers:"), 0, 1);
        _approvers.Multiline = true;
        _approvers.ScrollBars = ScrollBars.Vertical;
        _approvers.Dock = DockStyle.Fill;
        _approvers.MinimumSize = new Size(0, 64);
        layout.Controls.Add(_approvers, 1, 1);

        layout.Controls.Add(FieldLabel("Session ID:"), 2, 1);
        _approvalSession.Dock = DockStyle.Fill;
        layout.Controls.Add(_approvalSession, 3, 1);

        layout.Controls.Add(FieldLabel("Comment:"), 2, 2);
        _approvalComment.Dock = DockStyle.Fill;
        layout.Controls.Add(_approvalComment, 3, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true
        };
        var approve =
            UiStyle.CreateActionButton(
                "Approve");
        var deny =
            UiStyle.CreateActionButton(
                "Deny");
        actions.Controls.Add(approve);
        actions.Controls.Add(deny);
        layout.Controls.Add(actions, 2, 3);
        layout.SetColumnSpan(actions, 2);

        UiStyle.ConfigureStatusLabel(
            _approvalStatus);
        _approvalStatus.AccessibleName =
            "Two-person approval status";
        _approvalStatus.MaximumSize =
            new Size(
                940,
                0);
        _approvalStatus.Margin =
            new Padding(
                0,
                6,
                0,
                0);
        layout.Controls.Add(_approvalStatus, 0, 4);
        layout.SetColumnSpan(_approvalStatus, 4);

        approve.Click += (_, _) => DecideApproval(true);
        deny.Click += (_, _) => DecideApproval(false);

        return group;
    }

    private GroupBox BuildSiemGroup()
    {
        var group = CreateResponsiveGroup("SIEM forwarding");
        var layout = CreateFourColumnLayout();
        group.Controls.Add(layout);

        _siemEnabled.Text = "Enable SIEM forwarding";
        _siemEnabled.AutoSize = true;
        _siemEnabled.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_siemEnabled, 0, 0);

        layout.Controls.Add(FieldLabel("Mode:"), 1, 0);
        _siemMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _siemMode.Items.AddRange(["FileJsonl", "Webhook"]);
        _siemMode.Dock = DockStyle.Fill;
        layout.Controls.Add(_siemMode, 2, 0);

        _siemFailClosed.Text = "Fail closed when SIEM is not ready";
        _siemFailClosed.AutoSize = true;
        _siemFailClosed.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_siemFailClosed, 3, 0);

        layout.Controls.Add(FieldLabel("JSONL file:"), 0, 1);
        _siemFile.Dock = DockStyle.Fill;
        layout.Controls.Add(_siemFile, 1, 1);

        layout.Controls.Add(FieldLabel("HTTPS webhook:"), 2, 1);
        _siemWebhook.Dock = DockStyle.Fill;
        layout.Controls.Add(_siemWebhook, 3, 1);

        layout.Controls.Add(FieldLabel("Client cert thumbprint:"), 0, 2);
        _siemCertificate.Dock = DockStyle.Fill;
        layout.Controls.Add(_siemCertificate, 1, 2);

        var numericPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true
        };
        numericPanel.Controls.Add(FieldLabel("Timeout:"));
        ConfigureNumeric(_siemTimeout, 2, 120);
        numericPanel.Controls.Add(_siemTimeout);
        numericPanel.Controls.Add(FieldLabel("Flush min:"));
        ConfigureNumeric(_siemFlushMinutes, 1, 1440);
        numericPanel.Controls.Add(_siemFlushMinutes);
        numericPanel.Controls.Add(FieldLabel("Max outbox:"));
        ConfigureNumeric(_siemMaxOutbox, 100, 100000);
        numericPanel.Controls.Add(_siemMaxOutbox);
        layout.Controls.Add(numericPanel, 2, 2);
        layout.SetColumnSpan(numericPanel, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true
        };
        var check =
            UiStyle.CreateActionButton(
                "Test Readiness");
        var flush =
            UiStyle.CreateActionButton(
                "Flush Now");
        actions.Controls.Add(check);
        actions.Controls.Add(flush);
        layout.Controls.Add(actions, 2, 3);
        layout.SetColumnSpan(actions, 2);

        UiStyle.ConfigureStatusLabel(
            _siemStatus);
        _siemStatus.AccessibleName =
            "SIEM forwarding status";
        _siemStatus.MaximumSize =
            new Size(
                940,
                0);
        _siemStatus.Margin =
            new Padding(
                0,
                6,
                0,
                0);
        layout.Controls.Add(_siemStatus, 0, 4);
        layout.SetColumnSpan(_siemStatus, 4);

        check.Click += (_, _) => CheckSiem();
        flush.Click += async (_, _) => await FlushSiemAsync();

        return group;
    }

    private static GroupBox CreateResponsiveGroup(string title) =>
        new()
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };

    private static TableLayoutPanel CreateFourColumnLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (var row = 0; row < 5; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return layout;
    }

    private static Label FieldLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 4)
        };

    private static void ConfigureNumeric(
        NumericUpDown control,
        int minimum,
        int maximum)
    {
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.Width = 90;
        control.Anchor = AnchorStyles.Left;
        control.Margin = new Padding(0, 2, 8, 4);
    }

    private void LoadSettings()
    {
        _jitEnabled.Checked = _config.JitRecoveryEnabled;
        _jitMinutes.Value =
            Math.Clamp(_config.JitRecoveryGrantMinutes, 1, 1440);
        _jitGrantors.Text =
            string.Join(Environment.NewLine, _config.RbacJitGrantors);
        _jitSubject.Text =
            AuthorizationService.CurrentIdentityName();

        _approvalEnabled.Checked =
            _config.TwoPersonApprovalEnabled;
        _approvalMinutes.Value =
            Math.Clamp(_config.TwoPersonApprovalMinutes, 1, 1440);
        _approvers.Text =
            string.Join(Environment.NewLine, _config.RbacRecoveryApprovers);

        _siemEnabled.Checked = _config.SiemEnabled;
        _siemMode.SelectedItem =
            SiemForwardingService.NormalizeMode(_config.SiemMode);
        if (_siemMode.SelectedIndex < 0)
            _siemMode.SelectedIndex = 0;
        _siemFile.Text = _config.SiemFilePath;
        _siemWebhook.Text = _config.SiemWebhookUrl;
        _siemCertificate.Text =
            _config.SiemClientCertificateThumbprint;
        _siemTimeout.Value =
            Math.Clamp(_config.SiemWebhookTimeoutSeconds, 2, 120);
        _siemFlushMinutes.Value =
            Math.Clamp(_config.SiemFlushIntervalMinutes, 1, 1440);
        _siemMaxOutbox.Value =
            Math.Clamp(_config.SiemMaxOutboxEvents, 100, 100000);
        _siemFailClosed.Checked = _config.SiemFailClosed;
    }

    private void SaveSettings()
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
                throw new InvalidOperationException(
                    "Administrator rights are required to change privileged-access settings.");

            var grantors = ParsePrincipals(_jitGrantors.Text);
            var approvers = ParsePrincipals(_approvers.Text);

            ValidatePrincipals(grantors);
            ValidatePrincipals(approvers);

            _config.JitRecoveryEnabled = _jitEnabled.Checked;
            _config.JitRecoveryGrantMinutes = (int)_jitMinutes.Value;
            _config.RbacJitGrantors = grantors;

            _config.TwoPersonApprovalEnabled = _approvalEnabled.Checked;
            _config.TwoPersonApprovalMinutes =
                (int)_approvalMinutes.Value;
            _config.RbacRecoveryApprovers = approvers;

            _config.SiemEnabled = _siemEnabled.Checked;
            _config.SiemMode =
                SiemForwardingService.NormalizeMode(
                    _siemMode.SelectedItem?.ToString());
            _config.SiemFilePath = _siemFile.Text.Trim();
            _config.SiemWebhookUrl = _siemWebhook.Text.Trim();
            _config.SiemClientCertificateThumbprint =
                _siemCertificate.Text.Trim();
            _config.SiemWebhookTimeoutSeconds =
                (int)_siemTimeout.Value;
            _config.SiemFlushIntervalMinutes =
                (int)_siemFlushMinutes.Value;
            _config.SiemMaxOutboxEvents =
                (int)_siemMaxOutbox.Value;
            _config.SiemFailClosed =
                _siemFailClosed.Checked;

            ConfigurationMaintenanceService.ValidateAppConfig(_config);
            ConfigService.SaveAppConfig(_config);
            RestartServiceIfRunning();

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void GrantJit()
    {
        try
        {
            var grant =
                new JitRecoveryService(_config)
                    .CreateGrant(
                        _jitSubject.Text.Trim(),
                        _jitReason.Text.Trim());

            _jitGrantId.Text = grant.GrantId;
            RefreshStatus();
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _jitStatus,
                "JIT grant failed: " +
                ex.Message,
                UiStatusKind.Error);
            ShowError(ex);
        }
    }

    private void RevokeJit()
    {
        try
        {
            var removed =
                new JitRecoveryService(_config)
                    .RevokeGrant(
                        _jitGrantId.Text.Trim(),
                        _jitReason.Text.Trim());

            UiStyle.SetStatus(
                _jitStatus,
                removed
                    ? "JIT grant revoked."
                    : "JIT grant was not found.",
                removed
                    ? UiStatusKind.Success
                    : UiStatusKind.Warning);

            RefreshStatus();
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _jitStatus,
                "JIT revoke failed: " +
                ex.Message,
                UiStatusKind.Error);
            ShowError(ex);
        }
    }

    private void DecideApproval(bool approved)
    {
        try
        {
            var decision =
                new RecoveryApprovalService(_config)
                    .Decide(
                        _approvalSession.Text.Trim(),
                        approved,
                        _approvalComment.Text.Trim());

            UiStyle.SetStatus(
                _approvalStatus,
                approved
                    ? $"Session {decision.SessionId} approved."
                    : $"Session {decision.SessionId} denied.",
                approved
                    ? UiStatusKind.Success
                    : UiStatusKind.Warning);

            RefreshStatus();
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _approvalStatus,
                "Approval action failed: " +
                ex.Message,
                UiStatusKind.Error);
            ShowError(ex);
        }
    }

    private void CheckSiem()
    {
        try
        {
            var status =
                new SiemForwardingService(_config)
                    .CheckReadiness();

            UiStyle.SetStatus(
                _siemStatus,
                $"Mode={status.Mode}; Ready={status.Ready}; " +
                $"Pending={status.PendingEvents}; " +
                (string.IsNullOrWhiteSpace(status.LastError)
                    ? "No error."
                    : status.LastError),
                status.Ready
                    ? UiStatusKind.Success
                    : string.IsNullOrWhiteSpace(
                        status.LastError)
                        ? UiStatusKind.Warning
                        : UiStatusKind.Error);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _siemStatus,
                "SIEM readiness check failed: " +
                ex.Message,
                UiStatusKind.Error);
            ShowError(ex);
        }
    }

    private async Task FlushSiemAsync()
    {
        try
        {
            var status =
                await new SiemForwardingService(_config)
                    .FlushAsync();

            UiStyle.SetStatus(
                _siemStatus,
                $"Mode={status.Mode}; Ready={status.Ready}; " +
                $"Pending={status.PendingEvents}; " +
                (string.IsNullOrWhiteSpace(status.LastError)
                    ? "Flush completed."
                    : status.LastError),
                status.Ready &&
                string.IsNullOrWhiteSpace(
                    status.LastError)
                    ? UiStatusKind.Success
                    : UiStatusKind.Error);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _siemStatus,
                "SIEM flush failed: " +
                ex.Message,
                UiStatusKind.Error);
            ShowError(ex);
        }
    }

    private void RefreshStatus()
    {
        try
        {
            var status =
                new PrivilegedAccessPolicyService(_config)
                    .GetStatus();

            UiStyle.SetStatus(
                _jitStatus,
                $"Enabled={status.JitEnabled}; Active valid grants={status.ActiveJitGrants}; " +
                $"Current identity={AuthorizationService.CurrentIdentityName()}",
                status.JitEnabled
                    ? UiStatusKind.Success
                    : UiStatusKind.Neutral);

            UiStyle.SetStatus(
                _approvalStatus,
                $"Enabled={status.TwoPersonApprovalEnabled}; Pending={status.PendingApprovalRequests}; " +
                $"Active approvals={status.ActiveApprovals}",
                status.TwoPersonApprovalEnabled
                    ? UiStatusKind.Success
                    : UiStatusKind.Neutral);

            var siem =
                new SiemForwardingService(_config)
                    .CheckReadiness();

            UiStyle.SetStatus(
                _siemStatus,
                $"Enabled={_config.SiemEnabled}; Mode={siem.Mode}; Ready={siem.Ready}; " +
                $"Pending={siem.PendingEvents}; " +
                (string.IsNullOrWhiteSpace(siem.LastError)
                    ? "No error."
                    : siem.LastError),
                !_config.SiemEnabled
                    ? UiStatusKind.Neutral
                    : siem.Ready &&
                      string.IsNullOrWhiteSpace(
                          siem.LastError)
                        ? UiStatusKind.Success
                        : UiStatusKind.Error);
        }
        catch (Exception ex)
        {
            var message =
                "Status error: " +
                ex.Message;

            UiStyle.SetStatus(
                _jitStatus,
                message,
                UiStatusKind.Error);
            UiStyle.SetStatus(
                _approvalStatus,
                message,
                UiStatusKind.Error);
            UiStyle.SetStatus(
                _siemStatus,
                message,
                UiStatusKind.Error);
        }
    }

    private static List<string> ParsePrincipals(string text) =>
        text
            .Split(
                ['\r', '\n', ';'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void ValidatePrincipals(
        IEnumerable<string> principals)
    {
        foreach (var principal in principals)
            _ = AuthorizationService.ResolvePrincipalSid(principal);
    }

    private static void RestartServiceIfRunning()
    {
        try
        {
            var service = WindowsServiceHost.GetInfo();
            if (!service.Installed ||
                !string.Equals(
                    service.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            WindowsServiceHost.Stop();
            WindowsServiceHost.Start();
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Privileged-access settings were saved, but the running service could not be restarted: " +
                ex.Message,
                EventLogSeverity.Warning,
                4594,
                "PrivilegedAccess");
        }
    }

    private void ShowError(Exception ex)
    {
        _diagnostics.ShowError(
            "Privileged-access operation failed.",
            "PrivilegedAccess",
            ex);
    }
}
