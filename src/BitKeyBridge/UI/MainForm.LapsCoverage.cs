namespace BitKeyBridge;

/// <summary>
/// Administrator-only native WinForms metadata dashboard. Every scan,
/// diff and export excludes LAPS password attributes and plaintext secrets.
/// </summary>
public sealed partial class MainForm
{
    private readonly DataGridView _lapsCoverageResults = new();
    private readonly DataGridView _lapsCoverageChanges = new();
    private readonly TextBox _lapsCoverageScope = new();
    private readonly UiStatusLabel _lapsCoverageStatus = new();
    private readonly UiStatusLabel _lapsCoverageSummary = new();
    private readonly Button _lapsCoverageScanButton = new();
    private readonly Button _lapsCoverageCancelButton = new();
    private readonly Button _lapsCoverageExportButton = new();
    private readonly Button _lapsCoverageCompareButton = new();
    private readonly CheckBox _lapsCoverageRotationOptIn = new();
    private readonly Button _lapsCoverageRotateButton = new();
    private CancellationTokenSource? _lapsCoverageCancellation;
    private LapsCoverageSnapshot? _lapsCoverageCurrent;
    private LapsCoverageSnapshot? _lapsCoveragePrevious;
    private LapsCoverageComparison? _lapsCoverageComparison;

    private TabPage BuildLapsCoverageTab()
    {
        var tab = new TabPage("LAPS Coverage");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(UiStyle.SectionGap),
            ColumnCount = 1,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(root);

        var heading = new Label
        {
            Text = "LAPS Coverage | AD metadata only",
            Font = UiStyle.CreateSectionTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        root.Controls.Add(heading, 0, 0);

        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        var scopeLabel = new Label
        {
            Text = "OU / DN (blank = domain):",
            AutoSize = true,
            Margin = new Padding(0, UiStyle.ControlGap, UiStyle.ControlGap, 0)
        };
        tools.Controls.Add(scopeLabel);
        _lapsCoverageScope.Name = "LapsCoverageSearchBase";
        _lapsCoverageScope.Width = 310;
        _lapsCoverageScope.PlaceholderText = "OU=Computers,DC=example,DC=org";
        tools.Controls.Add(_lapsCoverageScope);

        _lapsCoverageScanButton.Name = "LapsCoverageScanButton";
        _lapsCoverageScanButton.Text = "Scan metadata";
        UiStyle.ConfigureActionButton(_lapsCoverageScanButton);
        tools.Controls.Add(_lapsCoverageScanButton);

        _lapsCoverageCancelButton.Text = "Cancel";
        UiStyle.ConfigureActionButton(_lapsCoverageCancelButton);
        _lapsCoverageCancelButton.Enabled = false;
        tools.Controls.Add(_lapsCoverageCancelButton);

        _lapsCoverageCompareButton.Name = "LapsCoverageCompareSnapshotsButton";
        _lapsCoverageCompareButton.Text = "Compare snapshots";
        UiStyle.ConfigureActionButton(_lapsCoverageCompareButton);
        _lapsCoverageCompareButton.Enabled = false;
        tools.Controls.Add(_lapsCoverageCompareButton);

        _lapsCoverageExportButton.Text = "Export CSV";
        UiStyle.ConfigureActionButton(_lapsCoverageExportButton);
        _lapsCoverageExportButton.Enabled = false;
        tools.Controls.Add(_lapsCoverageExportButton);

        _lapsCoverageRotationOptIn.Text =
            "Enable manual AD LAPS expiration requests (this session)";
        _lapsCoverageRotationOptIn.AutoSize = true;
        _lapsCoverageRotationOptIn.Name = "LapsRotationOptIn";
        _lapsCoverageRotationOptIn.Checked = false;
        tools.Controls.Add(_lapsCoverageRotationOptIn);

        _lapsCoverageRotateButton.Text = "Request LAPS rotation...";
        _lapsCoverageRotateButton.Name = "LapsCoverageRequestRotation";
        UiStyle.ConfigureActionButton(_lapsCoverageRotateButton);
        _lapsCoverageRotateButton.Enabled = false;
        tools.Controls.Add(_lapsCoverageRotateButton);
        root.Controls.Add(tools, 0, 1);

        UiStyle.ConfigureStatusLabel(_lapsCoverageSummary);
        _lapsCoverageSummary.Name = "LapsCoverageSummary";
        _lapsCoverageSummary.MaximumSize = new Size(1050, 0);
        UiStyle.SetStatus(_lapsCoverageSummary,
            "LAPS expiration metadata can be hidden by permissions. " +
            "Not verified is NOT proof of missing backups.", UiStatusKind.Neutral);
        root.Controls.Add(_lapsCoverageSummary, 0, 2);

        UiStyle.ConfigureDataGridView(_lapsCoverageResults);
        _lapsCoverageResults.Name = "LapsCoverageDevices";
        _lapsCoverageResults.Dock = DockStyle.Fill;
        _lapsCoverageResults.MinimumSize = new Size(0, 140);
        _lapsCoverageResults.Columns.AddRange([
            UiStyle.CreateSortableTextColumn("Computer", "Computer", 160),
            UiStyle.CreateSortableTextColumn("Guid", "AD object GUID", 235),
            UiStyle.CreateSortableTextColumn("OU", "OU / distinguished name", 260),
            UiStyle.CreateSortableTextColumn("Backup", "Visible LAPS metadata", 145),
            UiStyle.CreateSortableTextColumn("Expiry", "Expiration (UTC)", 145),
            UiStyle.CreateSortableTextColumn("State", "Expiry state", 110),
            UiStyle.CreateSortableTextColumn("LastLogon", "Last AD logon (UTC)", 145)
        ]);
        _lapsCoverageResults.Columns["Expiry"]!.DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        _lapsCoverageResults.Columns["LastLogon"]!.DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        root.Controls.Add(_lapsCoverageResults, 0, 3);

        UiStyle.ConfigureDataGridView(_lapsCoverageChanges);
        _lapsCoverageChanges.Name = "LapsCoverageChanges";
        _lapsCoverageChanges.Dock = DockStyle.Fill;
        _lapsCoverageChanges.MinimumSize = new Size(0, 105);
        _lapsCoverageChanges.Columns.AddRange([
            UiStyle.CreateSortableTextColumn("Computer", "Computer", 140),
            UiStyle.CreateSortableTextColumn("Change", "Change type", 220),
            UiStyle.CreateSortableTextColumn("Previous", "Previous", 210),
            UiStyle.CreateSortableTextColumn("Current", "Current", 210)
        ]);
        root.Controls.Add(_lapsCoverageChanges, 0, 4);

        UiStyle.ConfigureStatusLabel(_lapsCoverageStatus);
        _lapsCoverageStatus.Name = "LapsCoverageStatus";
        _lapsCoverageStatus.MaximumSize = new Size(1050, 0);
        UiStyle.SetStatus(_lapsCoverageStatus,
            "Connect to AD on Start to scan. No passwords are ever read.",
            UiStatusKind.Neutral);
        root.Controls.Add(_lapsCoverageStatus, 0, 5);

        _lapsCoverageScanButton.Click +=
            async (_, _) => await ScanLapsCoverageAsync();
        _lapsCoverageCancelButton.Click += (_, _) =>
            _lapsCoverageCancellation?.Cancel();
        _lapsCoverageCompareButton.Click += (_, _) =>
            RenderLapsCoverageComparison();
        _lapsCoverageExportButton.Click += (_, _) =>
            ExportLapsCoverage();
        _lapsCoverageResults.SelectionChanged +=
            (_, _) => RefreshLapsCoverageRotationButton();
        _lapsCoverageRotationOptIn.CheckedChanged +=
            (_, _) => RefreshLapsCoverageRotationButton();
        _lapsCoverageRotateButton.Click +=
            async (_, _) => await RequestSelectedAdLapsRotationAsync();
        return tab;
    }

    private LapsCoverageDevice? SelectedLapsCoverageDevice()
    {
        if (_lapsCoverageResults.SelectedRows.Count == 0)
            return null;
        return _lapsCoverageResults.SelectedRows[0].Tag as LapsCoverageDevice;
    }

    private void RefreshLapsCoverageRotationButton()
    {
        _lapsCoverageRotateButton.Enabled =
            _lapsCoverageCancellation is null &&
            _lapsCoverageRotationOptIn.Checked &&
            _directoryConnected &&
            SelectedLapsCoverageDevice()?.CanRequestAdExpiration == true;
    }

    private async Task ScanLapsCoverageAsync()
    {
        if (_lapsCoverageCancellation is not null)
            return;
        if (!_directoryConnected)
        {
            UiStyle.SetStatus(_lapsCoverageStatus,
                "Connect to AD on Start before scanning LAPS metadata.",
                UiStatusKind.Warning);
            return;
        }
        if (!AuthorizeAction(BitKeyBridgePermission.RecoveryRead,
                "LapsCoverageScan", source: "AD"))
            return;

        using var cancellation = new CancellationTokenSource();
        _lapsCoverageCancellation = cancellation;
        _lapsCoverageScanButton.Enabled = false;
        _lapsCoverageCancelButton.Enabled = true;
        _lapsCoverageExportButton.Enabled = false;
        _lapsCoverageCompareButton.Enabled = false;
        _lapsCoverageRotateButton.Enabled = false;
        _lapsCoverageResults.Rows.Clear();
        _lapsCoverageChanges.Rows.Clear();
        UiStyle.SetStatus(_lapsCoverageStatus,
            "Scanning AD LAPS expiration metadata (no passwords)...",
            UiStatusKind.Busy);
        try
        {
            var progress = new Progress<string>(message =>
                UiStyle.SetStatus(_lapsCoverageStatus, message,
                    UiStatusKind.Busy));
            var scanner = new LapsCoverageService(_config);
            var snapshot = await scanner.ScanAsync(
                _lapsCoverageScope.Text.Trim(),
                progress: progress,
                ct: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            _lapsCoveragePrevious = LapsCoverageSnapshotService.LoadPrevious();
            _lapsCoverageCurrent = snapshot;
            _lapsCoverageComparison = null;
            if (!snapshot.Truncated)
            {
                if (_lapsCoveragePrevious is not null)
                {
                    try
                    {
                        _lapsCoverageComparison =
                            LapsCoverageSnapshotService.Compare(
                                _lapsCoveragePrevious, snapshot);
                    }
                    catch (InvalidOperationException)
                    {
                        // Incompatible OU/scope; save a fresh baseline.
                    }
                }
                LapsCoverageSnapshotService.SaveCurrent(snapshot);
            }

            RenderLapsCoverageRows(snapshot);
            _lapsCoverageCompareButton.Enabled =
                _lapsCoverageComparison is not null;
            _lapsCoverageExportButton.Enabled = true;
            UiStyle.SetStatus(_lapsCoverageStatus,
                snapshot.Truncated
                    ? "Scan limit reached: incomplete snapshot not saved or compared."
                    : $"Metadata scan complete. DC={snapshot.DomainController}; " +
                      $"changes={_lapsCoverageComparison?.Changes.Count ?? 0}; " +
                      "no passwords requested or stored.",
                snapshot.Truncated ? UiStatusKind.Warning : UiStatusKind.Success);

            _audit.Write("LapsCoverageScan", source: "AD",
                details: $"Scope={snapshot.SearchBaseDn}; Records={snapshot.Devices.Count}; " +
                    $"Truncated={snapshot.Truncated}");
        }
        catch (OperationCanceledException)
        {
            UiStyle.SetStatus(_lapsCoverageStatus,
                "LAPS coverage scan canceled; no snapshot replaced.",
                UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(_lapsCoverageStatus,
                "LAPS coverage scan failed; previous snapshot preserved.",
                UiStatusKind.Error);
            _appDiagnostics.ShowError(
                "LAPS coverage metadata scan failed.", "LapsCoverageScan", ex);
        }
        finally
        {
            _lapsCoverageCancellation = null;
            _lapsCoverageScanButton.Enabled = true;
            _lapsCoverageCancelButton.Enabled = false;
            RefreshLapsCoverageRotationButton();
        }
    }

    private void RenderLapsCoverageRows(LapsCoverageSnapshot snapshot)
    {
        _lapsCoverageResults.Rows.Clear();
        foreach (var row in snapshot.Devices)
        {
            var index = _lapsCoverageResults.Rows.Add(
                row.ComputerName,
                row.ObjectGuid,
                row.OrganizationalUnit,
                row.BackupEvidence,
                row.EffectiveExpirationUtc,
                row.ExpirationState(DateTime.UtcNow),
                row.AdLastLogonUtc);
            _lapsCoverageResults.Rows[index].Tag = row;
        }
        _lapsCoverageResults.ClearSelection();
        _lapsCoverageResults.CurrentCell = null;
        var s = snapshot.Summary;
        UiStyle.SetStatus(_lapsCoverageSummary,
            $"Scanned={s.Scanned} | Windows={s.WindowsMetadata} | " +
            $"Legacy={s.LegacyMetadata} | Both={s.BothMetadata} | " +
            $"Not verified={s.Unverified} | Expired={s.Expired} | " +
            $"Expiring soon={s.ExpiringSoon}. " +
            "Metadata visibility does not prove a readable password exists.",
            s.Expired > 0 || s.Unverified > 0
                ? UiStatusKind.Warning : UiStatusKind.Success);
    }

    private void RenderLapsCoverageComparison()
    {
        _lapsCoverageChanges.Rows.Clear();
        if (_lapsCoverageComparison is not { } comparison)
            return;
        foreach (var change in comparison.Changes)
        {
            _lapsCoverageChanges.Rows.Add(
                change.ComputerName, change.ChangeType,
                change.PreviousState, change.CurrentState);
        }
        UiStyle.SetStatus(_lapsCoverageStatus,
            $"Compared against {comparison.PreviousAtUtc:yyyy-MM-dd HH:mm} UTC; " +
            $"{comparison.Changes.Count} metadata change(s). " +
            "Visibility changes do not prove passwords were deleted.",
            comparison.Changes.Count > 0
                ? UiStatusKind.Warning : UiStatusKind.Success);
    }

    private void ExportLapsCoverage()
    {
        if (_lapsCoverageCurrent is not { } snapshot)
            return;
        using var dialog = new SaveFileDialog
        {
            Title = "Export LAPS metadata coverage",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"BitKeyBridge-LAPS-Coverage-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = "csv",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            LapsCoverageCsvService.Export(dialog.FileName, snapshot);
            _audit.Write("ExportLapsCoverage", source: "AD",
                details: $"Rows={snapshot.Devices.Count}; File={dialog.FileName}");
            UiStyle.SetStatus(_lapsCoverageStatus,
                $"Exported {snapshot.Devices.Count} non-secret metadata row(s).",
                UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            _appDiagnostics.ShowError(
                "LAPS coverage CSV export failed.", "ExportLapsCoverage", ex);
        }
    }
}
