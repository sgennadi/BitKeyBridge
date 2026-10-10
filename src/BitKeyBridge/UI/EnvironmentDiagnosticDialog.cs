namespace BitKeyBridge;

/// <summary>
/// Workstation-visible prerequisites probe. Network and AD checks are read-only.
/// </summary>
public sealed class EnvironmentDiagnosticDialog : DpiAwareForm
{
    private readonly AppConfig _config;
    private readonly bool _cloudConfigured;
    private readonly TextBox _computer = new();
    private readonly Button _run = UiStyle.CreateActionButton("Run checks");
    private readonly Button _cancel = UiStyle.CreateActionButton("Cancel checks");
    private readonly Button _copy = UiStyle.CreateActionButton("Copy report");
    private readonly Button _close = UiStyle.CreateActionButton("Close");
    private readonly UiStatusLabel _status = new();
    private readonly ProgressBar _progress = new();
    private readonly ListView _results = new();
    private CancellationTokenSource? _cancellation;
    private EnvironmentDiagnosticReport? _report;

    public EnvironmentDiagnosticDialog(AppConfig config, bool cloudConfigured, string computer)
    {
        _config = config;
        _cloudConfigured = cloudConfigured;
        Name = "EnvironmentDiagnosticDialog";
        Text = "BitKeyBridge - Environment / Prerequisites Check";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Size = new Size(1050, 760);
        MinimumSize = new Size(680, 470);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            Text = "Checks the workstation, DC DNS, TCP ports, authenticated LDAP bind, BitLocker recovery metadata " +
                   "and LAPS backup indicators. Does not read secrets, open RPC dynamic ports, modify ACLs or start services. " +
                   "Remote DC services and password/decryption rights remain NotVerified unless independently tested.",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        UiStyle.ConfigureWrappingLabel(intro);
        root.Controls.Add(intro, 0, 0);

        var query = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1
        };
        query.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        query.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        query.Controls.Add(new Label
        {
            Text = "Exact AD computer (optional):",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, UiStyle.ControlGap, 0)
        }, 0, 0);
        _computer.Name = "EnvironmentComputer";
        _computer.Dock = DockStyle.Fill;
        _computer.Text = computer;
        _computer.PlaceholderText = "PC-12345";
        query.Controls.Add(_computer, 1, 0);
        root.Controls.Add(query, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap)
        };
        _run.Name = "EnvironmentRunButton";
        _cancel.Name = "EnvironmentCancelButton";
        _copy.Name = "EnvironmentCopyReportButton";
        _cancel.Enabled = false;
        _copy.Enabled = false;
        _close.DialogResult = DialogResult.Cancel;
        actions.Controls.AddRange([_run, _cancel, _copy, _close]);
        root.Controls.Add(actions, 0, 2);

        var indicator = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2
        };
        indicator.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        UiStyle.ConfigureStatusLabel(_status);
        _status.Name = "EnvironmentStatus";
        UiStyle.SetStatus(_status, "Checks have not started.", UiStatusKind.Neutral);
        indicator.Controls.Add(_status, 0, 0);
        _progress.Dock = DockStyle.Top;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.Visible = false;
        indicator.Controls.Add(_progress, 0, 1);
        root.Controls.Add(indicator, 0, 3);

        _results.Name = "EnvironmentCheckResults";
        _results.AccessibleName = "Read-only readiness checks and troubleshooting details";
        _results.View = View.Details;
        _results.FullRowSelect = true;
        _results.GridLines = true;
        _results.HideSelection = false;
        _results.Dock = DockStyle.Fill;
        UiStyle.ConfigureListViewColumns(_results,
            ("Area", 120),
            ("Check", 260),
            ("State", 110),
            ("Details and next action", 690));
        root.Controls.Add(_results, 0, 4);
        var footer = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "A reachable port is not proof of AD rights. LAPS encrypted-password decryption requires a separate authorized test. " +
                   "Copy report excludes passwords and tokens."
        };
        UiStyle.ConfigureWrappingLabel(footer);
        root.Controls.Add(footer, 0, 5);
        Controls.Add(root);

        _run.Click += async (_, _) => await RunChecksAsync();
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        _copy.Click += (_, _) =>
        {
            if (_report is null) return;
            try
            {
                Clipboard.SetText(DiagnosticRedaction.Sanitize(_report.SafeText()));
                UiStyle.SetStatus(_status, "Secret-free environment report copied.", UiStatusKind.Success);
            }
            catch (Exception)
            {
                UiStyle.SetStatus(_status, "Could not copy report; select the rows manually.", UiStatusKind.Warning);
            }
        };
        _close.Click += (_, _) => Close();
        FormClosing += (_, _) => _cancellation?.Cancel();
        Shown += async (_, _) => await RunChecksAsync();
        CancelButton = _close;
    }

    private async Task RunChecksAsync()
    {
        if (_cancellation is not null)
            return;
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _run.Enabled = false;
        _computer.Enabled = false;
        _copy.Enabled = false;
        _cancel.Enabled = true;
        _progress.Visible = true;
        _results.Items.Clear();
        _report = null;
        UiStyle.SetStatus(_status, "Checking network/DC and metadata; no password attributes will be read...",
            UiStatusKind.Busy);

        try
        {
            var report = await new EnvironmentDiagnosticService()
                .CheckAsync(_config, _computer.Text.Trim(), _cloudConfigured, token);
            if (IsDisposed || Disposing || token.IsCancellationRequested) return;

            _report = report;
            foreach (var entry in report.Checks)
            {
                var item = new ListViewItem(entry.Area);
                item.SubItems.Add(entry.Check);
                item.SubItems.Add(entry.State.ToString());
                item.SubItems.Add(entry.Detail);
                var kind = entry.State switch
                {
                    EnvironmentCheckState.Failed => UiStatusKind.Error,
                    EnvironmentCheckState.Warning => UiStatusKind.Warning,
                    EnvironmentCheckState.Available => UiStatusKind.Success,
                    _ => UiStatusKind.Neutral
                };
                item.ForeColor = UiStyle.ResolveStatusColors(kind, SystemInformation.HighContrast).ForeColor;
                _results.Items.Add(item);
            }

            _copy.Enabled = true;
            UiStyle.SetStatus(_status,
                "Checks finished: " + report.Failures + " failed, " + report.Warnings +
                " warnings. 'NotVerified' never means a permission was granted.",
                report.Failures > 0 ? UiStatusKind.Error :
                report.Warnings > 0 ? UiStatusKind.Warning : UiStatusKind.Success);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
                UiStyle.SetStatus(_status, "Environment checks canceled.", UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                UiStyle.SetStatus(_status,
                    "Environment check failed: " + DiagnosticRedaction.Sanitize(ex.Message),
                    UiStatusKind.Error);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            if (!IsDisposed && !Disposing)
            {
                _run.Enabled = true;
                _computer.Enabled = true;
                _cancel.Enabled = false;
                _progress.Visible = false;
            }
        }
    }
}
