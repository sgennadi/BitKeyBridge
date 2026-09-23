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
    private readonly Label _jitStatus = new();

    private readonly CheckBox _approvalEnabled = new();
    private readonly NumericUpDown _approvalMinutes = new();
    private readonly TextBox _approvers = new();
    private readonly TextBox _approvalSession = new();
    private readonly TextBox _approvalComment = new();
    private readonly Label _approvalStatus = new();

    private readonly CheckBox _siemEnabled = new();
    private readonly ComboBox _siemMode = new();
    private readonly TextBox _siemFile = new();
    private readonly TextBox _siemWebhook = new();
    private readonly TextBox _siemCertificate = new();
    private readonly NumericUpDown _siemTimeout = new();
    private readonly NumericUpDown _siemFlushMinutes = new();
    private readonly NumericUpDown _siemMaxOutbox = new();
    private readonly CheckBox _siemFailClosed = new();
    private readonly Label _siemStatus = new();

    public PrivilegedAccessSettingsDialog(
        AppConfig config)
    {
        _config = config;

        Text = "Privileged Recovery Access";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(980, 790);
        MinimumSize = new Size(900, 700);
        Font = new Font("Segoe UI", 9F);

        var title = new Label
        {
            Text = "Optional privileged recovery controls",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Left = 18,
            Top = 14
        };
        Controls.Add(title);

        var note = new Label
        {
            Text =
                "JIT recovery, two-person approval, and SIEM forwarding are opt-in. " +
                "Upgrades never enable them automatically. Recovery passwords are never forwarded to SIEM.",
            Left = 20,
            Top = 50,
            Width = 935,
            Height = 42
        };
        Controls.Add(note);

        BuildJitGroup();
        BuildApprovalGroup();
        BuildSiemGroup();

        var save = new Button
        {
            Text = "Save & Apply",
            Left = 752,
            Top = 742,
            Width = 105,
            Height = 34,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Left = 867,
            Top = 742,
            Width = 90,
            Height = 34,
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        var refresh = new Button
        {
            Text = "Refresh Status",
            Left = 20,
            Top = 742,
            Width = 125,
            Height = 34,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        Controls.AddRange([save, cancel, refresh]);
        AcceptButton = save;
        CancelButton = cancel;

        save.Click += (_, _) => SaveSettings();
        refresh.Click += (_, _) => RefreshStatus();

        LoadSettings();
        RefreshStatus();
    }

    private void BuildJitGroup()
    {
        var group = new GroupBox
        {
            Text = "JIT recovery grant",
            Left = 20,
            Top = 100,
            Width = 935,
            Height = 205,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(group);

        _jitEnabled.Text = "Enable JIT recovery requirement";
        _jitEnabled.SetBounds(16, 28, 240, 25);
        group.Controls.Add(_jitEnabled);

        group.Controls.Add(new Label
        {
            Text = "Grant lifetime (min):",
            Left = 280,
            Top = 31,
            Width = 120,
            Height = 24
        });
        _jitMinutes.SetBounds(405, 27, 80, 27);
        _jitMinutes.Minimum = 1;
        _jitMinutes.Maximum = 1440;
        group.Controls.Add(_jitMinutes);

        group.Controls.Add(new Label
        {
            Text = "Authorized grantors:",
            Left = 16,
            Top = 67,
            Width = 125,
            Height = 24
        });
        _jitGrantors.SetBounds(145, 63, 330, 54);
        _jitGrantors.Multiline = true;
        _jitGrantors.ScrollBars = ScrollBars.Vertical;
        group.Controls.Add(_jitGrantors);

        group.Controls.Add(new Label
        {
            Text = "Grant subject:",
            Left = 500,
            Top = 67,
            Width = 95,
            Height = 24
        });
        _jitSubject.SetBounds(600, 63, 305, 27);
        group.Controls.Add(_jitSubject);

        group.Controls.Add(new Label
        {
            Text = "Reason:",
            Left = 500,
            Top = 101,
            Width = 95,
            Height = 24
        });
        _jitReason.SetBounds(600, 97, 305, 27);
        group.Controls.Add(_jitReason);

        var grant = new Button
        {
            Text = "Grant",
            Left = 600,
            Top = 132,
            Width = 90,
            Height = 30
        };
        _jitGrantId.SetBounds(145, 130, 330, 27);
        _jitGrantId.PlaceholderText = "Grant ID for revoke";
        var revoke = new Button
        {
            Text = "Revoke ID",
            Left = 500,
            Top = 132,
            Width = 90,
            Height = 30
        };
        group.Controls.AddRange([_jitGrantId, revoke, grant]);

        _jitStatus.SetBounds(16, 169, 889, 28);
        group.Controls.Add(_jitStatus);

        grant.Click += (_, _) => GrantJit();
        revoke.Click += (_, _) => RevokeJit();
    }

    private void BuildApprovalGroup()
    {
        var group = new GroupBox
        {
            Text = "Two-person approval",
            Left = 20,
            Top = 315,
            Width = 935,
            Height = 180,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(group);

        _approvalEnabled.Text = "Require second authorized Windows user";
        _approvalEnabled.SetBounds(16, 28, 280, 25);
        group.Controls.Add(_approvalEnabled);

        group.Controls.Add(new Label
        {
            Text = "Approval lifetime (min):",
            Left = 315,
            Top = 31,
            Width = 135,
            Height = 24
        });
        _approvalMinutes.SetBounds(455, 27, 80, 27);
        _approvalMinutes.Minimum = 1;
        _approvalMinutes.Maximum = 1440;
        group.Controls.Add(_approvalMinutes);

        group.Controls.Add(new Label
        {
            Text = "Authorized approvers:",
            Left = 16,
            Top = 67,
            Width = 130,
            Height = 24
        });
        _approvers.SetBounds(150, 63, 325, 54);
        _approvers.Multiline = true;
        _approvers.ScrollBars = ScrollBars.Vertical;
        group.Controls.Add(_approvers);

        group.Controls.Add(new Label
        {
            Text = "Session ID:",
            Left = 500,
            Top = 67,
            Width = 85,
            Height = 24
        });
        _approvalSession.SetBounds(590, 63, 315, 27);
        group.Controls.Add(_approvalSession);

        group.Controls.Add(new Label
        {
            Text = "Comment:",
            Left = 500,
            Top = 101,
            Width = 85,
            Height = 24
        });
        _approvalComment.SetBounds(590, 97, 315, 27);
        group.Controls.Add(_approvalComment);

        var approve = new Button
        {
            Text = "Approve",
            Left = 590,
            Top = 132,
            Width = 90,
            Height = 30
        };
        var deny = new Button
        {
            Text = "Deny",
            Left = 690,
            Top = 132,
            Width = 90,
            Height = 30
        };
        group.Controls.AddRange([approve, deny]);

        _approvalStatus.SetBounds(16, 137, 555, 30);
        group.Controls.Add(_approvalStatus);

        approve.Click += (_, _) => DecideApproval(true);
        deny.Click += (_, _) => DecideApproval(false);
    }

    private void BuildSiemGroup()
    {
        var group = new GroupBox
        {
            Text = "SIEM forwarding",
            Left = 20,
            Top = 505,
            Width = 935,
            Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(group);

        _siemEnabled.Text = "Enable SIEM forwarding";
        _siemEnabled.SetBounds(16, 28, 180, 25);
        group.Controls.Add(_siemEnabled);

        group.Controls.Add(new Label
        {
            Text = "Mode:",
            Left = 215,
            Top = 31,
            Width = 45,
            Height = 24
        });
        _siemMode.SetBounds(265, 27, 135, 27);
        _siemMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _siemMode.Items.AddRange(["FileJsonl", "Webhook"]);
        group.Controls.Add(_siemMode);

        _siemFailClosed.Text = "Fail closed when SIEM is not ready";
        _siemFailClosed.SetBounds(430, 28, 245, 25);
        group.Controls.Add(_siemFailClosed);

        group.Controls.Add(new Label
        {
            Text = "JSONL file:",
            Left = 16,
            Top = 67,
            Width = 85,
            Height = 24
        });
        _siemFile.SetBounds(105, 63, 345, 27);
        group.Controls.Add(_siemFile);

        group.Controls.Add(new Label
        {
            Text = "HTTPS webhook:",
            Left = 475,
            Top = 67,
            Width = 100,
            Height = 24
        });
        _siemWebhook.SetBounds(580, 63, 325, 27);
        group.Controls.Add(_siemWebhook);

        group.Controls.Add(new Label
        {
            Text = "Client cert thumbprint:",
            Left = 16,
            Top = 101,
            Width = 135,
            Height = 24
        });
        _siemCertificate.SetBounds(155, 97, 295, 27);
        group.Controls.Add(_siemCertificate);

        group.Controls.Add(new Label
        {
            Text = "Timeout:",
            Left = 475,
            Top = 101,
            Width = 55,
            Height = 24
        });
        _siemTimeout.SetBounds(535, 97, 65, 27);
        _siemTimeout.Minimum = 2;
        _siemTimeout.Maximum = 120;
        group.Controls.Add(_siemTimeout);

        group.Controls.Add(new Label
        {
            Text = "Flush min:",
            Left = 615,
            Top = 101,
            Width = 65,
            Height = 24
        });
        _siemFlushMinutes.SetBounds(685, 97, 65, 27);
        _siemFlushMinutes.Minimum = 1;
        _siemFlushMinutes.Maximum = 1440;
        group.Controls.Add(_siemFlushMinutes);

        group.Controls.Add(new Label
        {
            Text = "Max outbox:",
            Left = 765,
            Top = 101,
            Width = 75,
            Height = 24
        });
        _siemMaxOutbox.SetBounds(840, 97, 65, 27);
        _siemMaxOutbox.Minimum = 100;
        _siemMaxOutbox.Maximum = 100000;
        group.Controls.Add(_siemMaxOutbox);

        var check = new Button
        {
            Text = "Test Readiness",
            Left = 580,
            Top = 137,
            Width = 115,
            Height = 30
        };
        var flush = new Button
        {
            Text = "Flush Now",
            Left = 705,
            Top = 137,
            Width = 100,
            Height = 30
        };
        group.Controls.AddRange([check, flush]);

        _siemStatus.SetBounds(16, 137, 545, 65);
        group.Controls.Add(_siemStatus);

        check.Click += (_, _) => CheckSiem();
        flush.Click += async (_, _) => await FlushSiemAsync();
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
            MessageBox.Show(
                this,
                ex.Message,
                "Privileged Access",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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

            MessageBox.Show(
                this,
                removed
                    ? "JIT grant revoked."
                    : "JIT grant was not found.",
                "Privileged Access",
                MessageBoxButtons.OK,
                removed
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);

            RefreshStatus();
        }
        catch (Exception ex)
        {
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

            MessageBox.Show(
                this,
                approved
                    ? $"Session {decision.SessionId} approved."
                    : $"Session {decision.SessionId} denied.",
                "Privileged Access",
                MessageBoxButtons.OK,
                approved
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);

            RefreshStatus();
        }
        catch (Exception ex)
        {
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

            _siemStatus.Text =
                $"Mode={status.Mode}; Ready={status.Ready}; " +
                $"Pending={status.PendingEvents}; " +
                (string.IsNullOrWhiteSpace(status.LastError)
                    ? "No error."
                    : status.LastError);
        }
        catch (Exception ex)
        {
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

            _siemStatus.Text =
                $"Mode={status.Mode}; Ready={status.Ready}; " +
                $"Pending={status.PendingEvents}; " +
                (string.IsNullOrWhiteSpace(status.LastError)
                    ? "Flush completed."
                    : status.LastError);
        }
        catch (Exception ex)
        {
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

            _jitStatus.Text =
                $"Enabled={status.JitEnabled}; Active valid grants={status.ActiveJitGrants}; " +
                $"Current identity={AuthorizationService.CurrentIdentityName()}";

            _approvalStatus.Text =
                $"Enabled={status.TwoPersonApprovalEnabled}; Pending={status.PendingApprovalRequests}; " +
                $"Active approvals={status.ActiveApprovals}";

            var siem =
                new SiemForwardingService(_config)
                    .CheckReadiness();

            _siemStatus.Text =
                $"Enabled={_config.SiemEnabled}; Mode={siem.Mode}; Ready={siem.Ready}; " +
                $"Pending={siem.PendingEvents}; " +
                (string.IsNullOrWhiteSpace(siem.LastError)
                    ? "No error."
                    : siem.LastError);
        }
        catch (Exception ex)
        {
            _jitStatus.Text = "Status error: " + ex.Message;
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
        catch
        {
            // Settings are already persisted. Service restart can be performed
            // manually if SCM access is temporarily unavailable.
        }
    }

    private void ShowError(Exception ex)
    {
        MessageBox.Show(
            this,
            ex.Message,
            "Privileged Access",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
