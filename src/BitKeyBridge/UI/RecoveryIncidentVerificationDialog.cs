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
        MinimumSize = new Size(780, 520);
        Font = new Font("Segoe UI", 9F);

        Controls.Add(new Label
        {
            Text = "Verify Recovery Incident Bundle",
            Font = new Font("Segoe UI Semibold", 15F),
            Left = 18,
            Top = 16,
            AutoSize = true
        });

        Controls.Add(new Label
        {
            Text =
                "The verifier matches incident actions to retained audit EntryHash values, CorrelationId and metadata. It never reads or stores a recovery password.",
            Left = 20,
            Top = 54,
            Width = 810,
            Height = 42
        });

        Controls.Add(new Label
        {
            Text = "Session ID:",
            Left = 20,
            Top = 112,
            Width = 90,
            Height = 24
        });

        _session.SetBounds(
            110,
            108,
            470,
            28);
        _session.DropDownStyle =
            ComboBoxStyle.DropDown;
        Controls.Add(_session);

        var verify = new Button
        {
            Text = "Verify",
            Left = 595,
            Top = 106,
            Width = 100,
            Height = 32
        };
        var openBundle = new Button
        {
            Text = "Open Bundle",
            Left = 705,
            Top = 106,
            Width = 120,
            Height = 32
        };

        Controls.AddRange([
            verify,
            openBundle
        ]);

        _result.SetBounds(
            20,
            158,
            805,
            365);
        _result.Multiline = true;
        _result.ReadOnly = true;
        _result.ScrollBars =
            ScrollBars.Both;
        _result.WordWrap = false;
        _result.Font =
            new Font(
                "Consolas",
                9F);
        _result.Anchor =
            AnchorStyles.Top |
            AnchorStyles.Bottom |
            AnchorStyles.Left |
            AnchorStyles.Right;
        Controls.Add(_result);

        var openFolder = new Button
        {
            Text = "Open Incident Folder",
            Left = 20,
            Top = 538,
            Width = 160,
            Height = 32,
            Anchor =
                AnchorStyles.Bottom |
                AnchorStyles.Left
        };
        var close = new Button
        {
            Text = "Close",
            Left = 725,
            Top = 538,
            Width = 100,
            Height = 32,
            Anchor =
                AnchorStyles.Bottom |
                AnchorStyles.Right
        };

        Controls.AddRange([
            openFolder,
            close
        ]);

        verify.Click += (_, _) =>
            VerifyCurrent();
        openBundle.Click += (_, _) =>
            OpenCurrentBundle();
        openFolder.Click += (_, _) =>
            OpenFolder();
        close.Click += (_, _) =>
            Close();

        LoadRecentSessions(
            initialSessionId);
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
                    $"\"{path}\"")
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
