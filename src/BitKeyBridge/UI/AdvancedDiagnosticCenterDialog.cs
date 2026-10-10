namespace BitKeyBridge;

/// <summary>
/// Consolidated, metadata-only diagnostic center for ordinary Windows workstations.
/// All source and privilege-changing actions remain outside this form.
/// </summary>
public sealed class AdvancedDiagnosticCenterDialog : DpiAwareForm
{
    private readonly AppConfig _config;
    private readonly Func<Task<string?>> _getGraphToken;
    private readonly Action<string> _selectDc;
    private readonly Action _protectedCreated;
    private readonly TextBox _computer = new();
    private readonly TextBox _entraId = new();
    private readonly UiStatusLabel _status = new();
    private readonly ProgressBar _progress = new();
    private readonly RichTextBox _report = new();
    private readonly Button _cancel;
    private readonly Button _copy;
    private readonly Button _useDc;
    private readonly Button _close;
    private readonly List<Button> _runActions = [];
    private CancellationTokenSource? _cancelToken;
    private string _recommendedDc = string.Empty;

    public AdvancedDiagnosticCenterDialog(
        AppConfig config,
        Func<Task<string?>> getGraphToken,
        Action<string> selectDc,
        Action protectedCreated,
        string initialComputer)
    {
        _config = config;
        _getGraphToken = getGraphToken;
        _selectDc = selectDc;
        _protectedCreated = protectedCreated;
        Name = "AdvancedDiagnosticCenterDialog";
        Text = "BitKeyBridge - Advanced Device Diagnostics";
        Size = new Size(1110, 810);
        MinimumSize = new Size(690, 480);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 6; i++)
            layout.RowStyles.Add(new RowStyle(i == 4
                ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100F : 0F));

        var intro = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "DC consistency, rights evidence, smart diagnosis, strict LDAP/LDAPS, " +
                   "AD/Entra metadata comparison, endpoint ZIP and user-DPAPI cache. " +
                   "No password attributes are read or policies modified by these checks."
        };
        UiStyle.ConfigureWrappingLabel(intro);
        layout.Controls.Add(intro, 0, 0);

        var inputs = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2,
            Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.SectionGap)
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        inputs.Controls.Add(new Label
        {
            Text = "Exact AD computer:", AutoSize = true, Anchor = AnchorStyles.Left
        }, 0, 0);
        _computer.Name = "AdvancedDiagnosticComputer";
        _computer.Dock = DockStyle.Fill;
        _computer.Text = initialComputer;
        _computer.PlaceholderText = "PC-12345";
        inputs.Controls.Add(_computer, 1, 0);

        inputs.Controls.Add(new Label
        {
            Text = "Entra deviceId (optional):", AutoSize = true, Anchor = AnchorStyles.Left
        }, 0, 1);
        _entraId.Name = "AdvancedDiagnosticEntraId";
        _entraId.Dock = DockStyle.Fill;
        _entraId.PlaceholderText = "Entra deviceId GUID, not object ID";
        inputs.Controls.Add(_entraId, 1, 1);
        layout.Controls.Add(inputs, 0, 1);

        var tools = new FlowLayoutPanel
        {
            Name = "AdvancedDiagnosticActions",
            Dock = DockStyle.Top, AutoSize = true,
            WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        AddAction(tools, "Compare DCs", () => RunAsync(async ct =>
        {
            var report = await new AdvancedAdDiagnosticService(_config)
                .CompareControllersAsync(_computer.Text.Trim(), ct);
            var lines = new List<string>
            {
                "PER-DEVICE MULTI-DC CONSISTENCY — " + report.Computer,
                "Domain: " + report.Domain,
                "DC checks succeeded: " + report.Queried + "; failed: " + report.Failed,
                "Does not retrieve any BitLocker or LAPS secret."
            };
            foreach (var dc in report.Controllers)
            {
                lines.Add("");
                lines.Add(dc.Server + " (" + dc.Site + "; " +
                          (dc.IsReadOnly ? "RODC" : "writable") + "): " +
                          (dc.QuerySucceeded ? dc.ComputerFound ? "Computer visible" :
                           "Computer not visible" : "Error"));
                lines.Add("  BitLocker Recovery IDs: " + string.Join(", ", dc.RecoveryIds));
                lines.Add("  LAPS Windows expiry: " + dc.WindowsLapsExpiry?.ToString("O"));
                lines.Add("  LAPS legacy expiry: " + dc.LegacyLapsExpiry?.ToString("O"));
                lines.Add("  LAPS version: " + dc.WindowsLapsVersion);
                lines.Add("  Error: " + dc.Error);
            }
            AddFindings(lines, report.Findings);
            return string.Join(Environment.NewLine, lines);
        }));
        AddAction(tools, "Permission analysis", () => RunAsync(async ct =>
        {
            var r = await new AdvancedAdDiagnosticService(_config)
                .InspectPermissionsAsync(_computer.Text.Trim(), ct);
            var lines = new List<string>
            {
                "AD PERMISSIONS / CONFIGURED DACL (NOT AN EFFECTIVE ACCESS PROOF)",
                "Computer: " + r.Computer,
                "DC: " + r.QueriedDc,
                "Identity configured: " + r.IdentityUsed +
                  (r.ExplicitCredentials ? " (explicit AD credentials)" : " (Windows token)"),
                "Computer DN: " + r.ComputerDn, "OU/container: " + r.Scope,
                "Status: " + r.Status, "",
                "DACL ENTRIES (ACE SID, access mask, type; inherited or explicit)"
            };
            lines.AddRange(r.DescriptorRules.Take(250));
            AddFindings(lines, r.Findings);
            return string.Join(Environment.NewLine, lines);
        }));
        AddAction(tools, "Connection / LDAPS", () => RunAsync(async ct =>
        {
            var rows = await new AdvancedConnectionDiagnosticService(_config).RunAsync(ct);
            var winner = rows.Where(x => x.Check == "Authenticated LDAP" &&
                                           x.Status == "OK" &&
                                           x.Detail.Contains("writable DC", StringComparison.Ordinal))
                .OrderBy(x => x.LatencyMs ?? double.MaxValue).FirstOrDefault();
            _recommendedDc = winner?.Server ?? string.Empty;
            _useDc.Enabled = !string.IsNullOrWhiteSpace(_recommendedDc);
            var lines = new List<string>
            {
                "AD DNS SRV / SITE / LDAP / LDAPS VALIDATION",
                "TLS validation is strict; certificate errors are never bypassed.",
                "An authenticated LDAP connection tests the selected credentials; not password read permissions.",
                ""
            };
            lines.AddRange(rows.Select(x => x.Check + " | " + x.Server + " | " + x.Status +
                " | latency(ms): " + x.LatencyMs + " | " + x.Detail));
            lines.Add("");
            lines.Add("Suggested writable DC: " +
                (string.IsNullOrWhiteSpace(_recommendedDc) ? "None verified" : _recommendedDc));
            lines.Add("DC selection is never automatic; the operator must explicitly choose to apply the suggestion.");
            return string.Join(Environment.NewLine, lines);
        }));
        AddAction(tools, "AD / Entra comparison", () => RunAsync(async ct =>
        {
            var token = await _getGraphToken();
            var r = await new RecoverySourceConsistencyService(_config).CompareAsync(
                _computer.Text.Trim(), _entraId.Text.Trim(), token, ct);
            var lines = new List<string>
            {
                "AD / MICROSOFT ENTRA BITLOCKER METADATA",
                "Computer: " + r.Computer,
                "AD query successful: " + r.AdQueried + "; DC: " + r.DomainController,
                "Entra query successful: " + r.EntraQueried + "; deviceId: " + r.EntraDeviceId,
                "AD Recovery IDs: " + string.Join(", ", r.AdRecoveryIds),
                "Entra Recovery IDs: " + string.Join(", ", r.EntraRecoveryIds),
                "Different or one-sided keys can be legitimate; source-specific escrow policies may differ."
            };
            AddFindings(lines, r.Findings);
            return string.Join(Environment.NewLine, lines);
        }));
        AddAction(tools, "Collect endpoint ZIP", CollectEndpoint);
        AddAction(tools, "Open endpoint ZIP", OpenEndpoint);
        AddAction(tools, "Protect recovery CSV", ProtectCache);
        AddAction(tools, "Verify EXE signature", VerifySignature);

        _cancel = UiStyle.CreateActionButton("Cancel check");
        _cancel.Name = "AdvancedDiagnosticCancel";
        _cancel.Enabled = false;
        _cancel.Click += (_, _) => _cancelToken?.Cancel();
        tools.Controls.Add(_cancel);

        _useDc = UiStyle.CreateActionButton("Use suggested DC");
        _useDc.Name = "AdvancedDiagnosticUseDc";
        _useDc.Enabled = false;
        _useDc.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_recommendedDc)) return;
            if (MessageBox.Show(this,
                    "Set Start > Advanced to use " + _recommendedDc +
                    " as explicit DC? You must reconnect for a new LDAP session. " +
                    "No domain changes will be made.",
                    "Choose DC", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                == DialogResult.Yes)
            {
                _selectDc(_recommendedDc);
                UiStyle.SetStatus(_status,
                    "Selected " + _recommendedDc + " for next AD connection. Reconnect from Start.",
                    UiStatusKind.Warning);
            }
        };
        tools.Controls.Add(_useDc);
        layout.Controls.Add(tools, 0, 2);

        UiStyle.ConfigureStatusLabel(_status);
        _status.Name = "AdvancedDiagnosticStatus";
        UiStyle.SetStatus(_status, "Choose a check. Results contain metadata only.", UiStatusKind.Neutral);
        _progress = new ProgressBar { Dock = DockStyle.Top, Style = ProgressBarStyle.Marquee, Visible = false };
        var indicators = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2
        };
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        indicators.Controls.Add(_status, 0, 0);
        indicators.Controls.Add(_progress, 0, 1);
        layout.Controls.Add(indicators, 0, 3);

        _report.Name = "AdvancedDiagnosticReport";
        _report.Dock = DockStyle.Fill;
        _report.ReadOnly = true;
        _report.WordWrap = false;
        _report.DetectUrls = false;
        _report.ScrollBars = RichTextBoxScrollBars.Both;
        _report.Font = UiStyle.CreateMonospaceFont();
        _report.Text = "Run Compare DCs or another check. No changes are made automatically.";
        layout.Controls.Add(_report, 0, 4);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true
        };
        _close = UiStyle.CreateActionButton("Close");
        _close.DialogResult = DialogResult.Cancel;
        _close.Click += (_, _) => Close();
        _copy = UiStyle.CreateActionButton("Copy sanitized report");
        _copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(DiagnosticRedaction.Sanitize(_report.Text)); }
            catch (Exception) { UiStyle.SetStatus(_status, "Clipboard unavailable.", UiStatusKind.Warning); }
        };
        footer.Controls.Add(_close);
        footer.Controls.Add(_copy);
        layout.Controls.Add(footer, 0, 5);
        Controls.Add(layout);
        CancelButton = _close;
        FormClosing += (_, _) => _cancelToken?.Cancel();
    }

    private void AddAction(FlowLayoutPanel tools, string title, Action action)
    {
        var button = UiStyle.CreateActionButton(title);
        button.Click += (_, _) => action();
        tools.Controls.Add(button);
        _runActions.Add(button);
    }

    private async void CollectEndpoint()
    {
        using var chooser = new SaveFileDialog
        {
            Filter = "ZIP archive (*.zip)|*.zip",
            FileName = "BitKeyBridge-Endpoint-" + Environment.MachineName + "-" +
                       DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip"
        };
        if (chooser.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(ct => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var path = EndpointDiagnosticCollector.Write(chooser.FileName);
            return "Offline metadata-only endpoint ZIP saved:\n" + path +
                   "\n\nNo password, token, BitLocker protector or event message text was collected.";
        }, ct));
    }

    private void OpenEndpoint()
    {
        using var chooser = new OpenFileDialog
        {
            Filter = "BitKeyBridge endpoint ZIP (*.zip)|*.zip"
        };
        if (chooser.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var data = EndpointDiagnosticCollector.Read(chooser.FileName);
            _report.Text = EndpointDiagnosticCollector.SafeSummary(data);
            UiStyle.SetStatus(_status, "Endpoint metadata report opened locally.", UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_status, "Unable to read report: " +
                DiagnosticRedaction.Sanitize(ex.Message), UiStatusKind.Error);
        }
    }

    private void ProtectCache()
    {
        if (!File.Exists(_config.OutputCsv))
        {
            UiStyle.SetStatus(_status,
                "No existing export CSV found at " + _config.OutputCsv,
                UiStatusKind.Warning);
            return;
        }
        if (MessageBox.Show(this,
                "Create/update a DPAPI CurrentUser sidecar in your LocalAppData? " +
                "Only this Windows user can decrypt it. Original plaintext export remains and " +
                "is NOT deleted. Continue?",
                "Protect existing recovery CSV", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) !=
            DialogResult.Yes) return;
        try
        {
            var path = ProtectedRecoveryCacheService.EncryptExistingCsv(_config.OutputCsv);
            _protectedCreated();
            _report.Text = "Protected sidecar created and verified:\n" + path +
                "\nUse Recovery > Protected cache to read it without writing decrypted CSV to disk." +
                "\n\nIMPORTANT: The original plaintext CSV STILL EXISTS. Handle its retirement separately.";
            UiStyle.SetStatus(_status, "Protected user-DPAPI cache ready.", UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_status,
                DiagnosticRedaction.Sanitize("Cache protection failed: " + ex.Message),
                UiStatusKind.Error);
        }
    }

    private void VerifySignature()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) throw new InvalidOperationException("Cannot identify running EXE.");
            var result = AuthenticodeVerificationService.Verify(exe);
            _report.Text =
                "Authenticode / WinVerifyTrust status\n" +
                "EXE: " + exe + "\nSigned: " + result.Signed +
                "\nTrusted: " + result.Trusted +
                "\nPublisher: " + result.Publisher + "\nStatus: " + result.Status +
                "\n\nIf unsigned, supply an organizational code-signing provider. " +
                "BitKeyBridge does not create certificates or sign itself.";
            UiStyle.SetStatus(_status, result.Trusted
                ? "Executable signature trusted by Windows."
                : "Executable signature is absent or not trusted.", result.Trusted
                    ? UiStatusKind.Success : UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_status,
                DiagnosticRedaction.Sanitize(ex.Message), UiStatusKind.Error);
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task<string>> action)
    {
        if (_cancelToken is not null) return;
        _cancelToken = new CancellationTokenSource();
        var ct = _cancelToken.Token;
        _runActions.ForEach(b => b.Enabled = false);
        _useDc.Enabled = false;
        _cancel.Enabled = true;
        _progress.Visible = true;
        UiStyle.SetStatus(_status, "Running read-only diagnostic checks...", UiStatusKind.Busy);
        try
        {
            var content = await action(ct);
            if (ct.IsCancellationRequested || IsDisposed) return;
            _report.Text = DiagnosticRedaction.Sanitize(content);
            UiStyle.SetStatus(_status,
                "Diagnostic completed. Unverified checks are not reported as permissions granted.",
                UiStatusKind.Neutral);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) UiStyle.SetStatus(_status, "Diagnostic canceled.", UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) UiStyle.SetStatus(_status,
                "Diagnostic failed: " + DiagnosticRedaction.Sanitize(ex.Message), UiStatusKind.Error);
        }
        finally
        {
            _cancelToken.Dispose();
            _cancelToken = null;
            if (!IsDisposed)
            {
                _runActions.ForEach(b => b.Enabled = true);
                _useDc.Enabled = !string.IsNullOrWhiteSpace(_recommendedDc);
                _cancel.Enabled = false;
                _progress.Visible = false;
            }
        }
    }

    private static void AddFindings(List<string> lines, IEnumerable<DiagnosticFinding> findings)
    {
        lines.Add("");
        lines.Add("SMART DIAGNOSTICS / EVIDENCE AND NEXT ACTIONS");
        foreach (var f in findings)
        {
            lines.Add("");
            lines.Add(f.Code + " | " + f.Severity + " | " + f.Evidence);
            lines.Add(f.Summary);
            lines.Add("Evidence: " + f.Explanation);
            lines.Add("Action: " + f.NextAction);
            lines.Add("Where: " + f.RunOn);
        }
    }
}
