using System.Diagnostics;

namespace BitKeyBridge;

public sealed class RecoveryIncidentVerificationDialog : DpiAwareForm
{
    private readonly ComboBox _session = new();
    private readonly TextBox _result = new();

    public RecoveryIncidentVerificationDialog(
        string? initialSessionId = null)
    {
        Text = "Recovery Incident Verification";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 590);
        MinimumSize = new Size(650, 460);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Verify Recovery Incident Bundle",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text =
                "The verifier matches incident actions to retained audit EntryHash values, CorrelationId and metadata. It never reads or stores a recovery password.",
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);

        var sessionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Margin = new Padding(0, 0, 0, 12)
        };
        sessionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sessionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        sessionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sessionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        sessionRow.Controls.Add(new Label
        {
            Text = "Session ID:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0)
        }, 0, 0);

        _session.Dock = DockStyle.Fill;
        _session.DropDownStyle = ComboBoxStyle.DropDown;
        sessionRow.Controls.Add(_session, 1, 0);

        var verify = new Button
        {
            Text = "Verify",
            AutoSize = true,
            MinimumSize = new Size(100, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        var openBundle = new Button
        {
            Text = "Open Bundle",
            AutoSize = true,
            MinimumSize = new Size(120, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        sessionRow.Controls.Add(verify, 2, 0);
        sessionRow.Controls.Add(openBundle, 3, 0);
        root.Controls.Add(sessionRow, 0, 2);

        _result.Dock = DockStyle.Fill;
        _result.Multiline = true;
        _result.ReadOnly = true;
        _result.ScrollBars = ScrollBars.Both;
        _result.WordWrap = false;
        _result.Font = new Font("Consolas", 9F);
        root.Controls.Add(_result, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var close = new Button
        {
            Text = "Close",
            AutoSize = true,
            MinimumSize = new Size(100, 32)
        };
        var openFolder = new Button
        {
            Text = "Open Incident Folder",
            AutoSize = true,
            MinimumSize = new Size(160, 32)
        };
        footer.Controls.Add(close);
        footer.Controls.Add(openFolder);
        root.Controls.Add(footer, 0, 4);

        verify.Click += (_, _) => VerifyCurrent();
        openBundle.Click += (_, _) => OpenCurrentBundle();
        openFolder.Click += (_, _) => OpenFolder();
        close.Click += (_, _) => Close();

        LoadRecentSessions(initialSessionId);
    }

    private void LoadRecentSessions(
        string? initialSessionId)
    {
        try
        {
            if (Directory.Exists(
                    AppPaths.IncidentsDirectory))
            {
                foreach (var path in
                         Directory
                             .EnumerateFiles(
                                 AppPaths.IncidentsDirectory,
                                 "*.json",
                                 SearchOption.TopDirectoryOnly)
                             .OrderByDescending(
                                 File.GetLastWriteTimeUtc)
                             .Take(100))
                {
                    _session.Items.Add(
                        Path.GetFileNameWithoutExtension(
                            path));
                }
            }
        }
        catch
        {
        }

        if (!string.IsNullOrWhiteSpace(
                initialSessionId))
        {
            _session.Text =
                initialSessionId.Trim();
        }
        else if (_session.Items.Count > 0)
        {
            _session.SelectedIndex = 0;
        }
    }

    private void VerifyCurrent()
    {
        var sessionId =
            _session.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                sessionId))
        {
            MessageBox.Show(
                this,
                "Enter or select a recovery Session ID.",
                "Recovery Incident Verification",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            var verification =
                new RecoveryIncidentVerificationService()
                    .Verify(sessionId);

            var lines =
                new List<string>
                {
                    $"Status: {verification.Status}",
                    $"Bundle found: {verification.BundleFound}",
                    $"Audit chain valid: {verification.AuditChainValid}",
                    $"Stable audit snapshot: {verification.StableAuditSnapshot}",
                    $"Actions: {verification.ActionsAnchored}/{verification.ActionsTotal} anchored",
                    $"Not retained: {verification.ActionsMissingFromRetainedAudit}",
                    $"Metadata mismatches: {verification.MetadataMismatches}",
                    $"Sensitive data detected: {verification.SensitiveDataDetected}",
                    $"Rotation state valid: {verification.RotationStateValid}"
                };

            if (verification.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("WARNINGS:");
                lines.AddRange(
                    verification.Warnings
                        .Select(x => " - " + x));
            }

            if (verification.Errors.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("ERRORS:");
                lines.AddRange(
                    verification.Errors
                        .Select(x => " - " + x));
            }

            _result.Text =
                string.Join(
                    Environment.NewLine,
                    lines);
        }
        catch (Exception ex)
        {
            _result.Text =
                "Verification failed:" +
                Environment.NewLine +
                ex.Message;
        }
    }

    private void OpenCurrentBundle()
    {
        try
        {
            var sessionId =
                _session.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                    sessionId))
            {
                return;
            }

            var path =
                new RecoveryIncidentService()
                    .GetPathForSession(
                        sessionId);

            if (!File.Exists(path))
            {
                MessageBox.Show(
                    this,
                    "Incident bundle does not exist.",
                    "Recovery Incident Verification",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Process.Start(
                new ProcessStartInfo(
                    "notepad.exe",
                    $"\\\"{path}\\\"")
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Recovery Incident Verification",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(
                AppPaths.IncidentsDirectory);

            Process.Start(
                new ProcessStartInfo(
                    AppPaths.IncidentsDirectory)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Recovery Incident Verification",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
