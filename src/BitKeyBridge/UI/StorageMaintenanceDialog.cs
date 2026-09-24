using System.Diagnostics;
using System.Text;

namespace BitKeyBridge;

public sealed class StorageMaintenanceDialog : DpiAwareForm
{
    private readonly AppConfig _config;

    private readonly CheckBox _enabled = new();
    private readonly CheckBox _runOnStart = new();
    private readonly NumericUpDown _intervalHours = new();
    private readonly NumericUpDown _incidentRetentionDays = new();
    private readonly NumericUpDown _backupRetentionDays = new();
    private readonly NumericUpDown _backupMinimumFiles = new();
    private readonly NumericUpDown _tempRetentionDays = new();
    private readonly Label _aclEnforcement = new();
    private readonly RichTextBox _status = new();

    public StorageMaintenanceDialog(
        AppConfig config)
    {
        _config = config;

        Text = "Housekeeping / Protected Storage";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(920, 700);
        MinimumSize = new Size(680, 560);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 7
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Housekeeping / Protected Storage",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text =
                "Incident deletion is evidence-aware: only fully verified bundles are eligible. " +
                "NotFullyRetained or mismatched incidents are preserved. Audit-signing transition history is never automatically deleted.",
            AutoSize = true,
            MaximumSize = new Size(850, 0),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 1);

        var policy = new GroupBox
        {
            Text = "Retention Policy",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        var policyGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 5
        };
        policyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        policyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        policyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        policyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (var row = 0; row < 5; row++)
            policyGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        policy.Controls.Add(policyGrid);
        root.Controls.Add(policy, 0, 2);

        _enabled.Text = "Enable scheduled housekeeping";
        _enabled.AutoSize = true;
        _enabled.Anchor = AnchorStyles.Left;
        policyGrid.Controls.Add(_enabled, 0, 0);
        policyGrid.SetColumnSpan(_enabled, 2);

        _runOnStart.Text = "Run when Windows Service starts";
        _runOnStart.AutoSize = true;
        _runOnStart.Anchor = AnchorStyles.Left;
        policyGrid.Controls.Add(_runOnStart, 2, 0);
        policyGrid.SetColumnSpan(_runOnStart, 2);

        AddNumeric(
            policyGrid,
            "Interval (hours):",
            _intervalHours,
            0,
            1,
            1,
            168);

        AddNumeric(
            policyGrid,
            "Incident retention days:",
            _incidentRetentionDays,
            2,
            1,
            0,
            36500);

        AddNumeric(
            policyGrid,
            "Backup retention days:",
            _backupRetentionDays,
            0,
            2,
            0,
            36500);

        AddNumeric(
            policyGrid,
            "Minimum backups to keep:",
            _backupMinimumFiles,
            2,
            2,
            0,
            1000);

        AddNumeric(
            policyGrid,
            "Temporary-file retention days:",
            _tempRetentionDays,
            0,
            3,
            0,
            3650);

        var hint = new Label
        {
            Text =
                "0 incident/backup days = keep forever. 0 temporary-file days = disable temporary-file cleanup.",
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 6, 0, 4)
        };
        policyGrid.Controls.Add(hint, 2, 3);
        policyGrid.SetColumnSpan(hint, 2);

        var actionBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        var save = ActionButton("Save Policy", 120);
        var dryRun = ActionButton("Dry Run", 100);
        var runNow = ActionButton("Run Now", 100);
        var aclStatus = ActionButton("ACL Status", 110);
        var hardenAcl = ActionButton("Harden ACLs", 120);
        var openIncidents = ActionButton("Incidents", 95);
        var openBackups = ActionButton("Backups", 95);

        actionBar.Controls.AddRange([
            save,
            dryRun,
            runNow,
            aclStatus,
            hardenAcl,
            openIncidents,
            openBackups
        ]);
        root.Controls.Add(actionBar, 0, 3);

        _aclEnforcement.AutoSize = true;
        _aclEnforcement.MaximumSize = new Size(850, 0);
        _aclEnforcement.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(_aclEnforcement, 0, 4);

        _status.Dock = DockStyle.Fill;
        _status.ReadOnly = true;
        _status.WordWrap = false;
        _status.ScrollBars = RichTextBoxScrollBars.Both;
        _status.Font = new Font("Consolas", 9F);
        _status.MinimumSize = new Size(0, 190);
        root.Controls.Add(_status, 0, 5);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var close = ActionButton("Close", 100);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 6);

        save.Click += (_, _) => SavePolicy();
        dryRun.Click += (_, _) => RunHousekeeping(dryRun: true);
        runNow.Click += (_, _) => RunHousekeeping(dryRun: false);
        aclStatus.Click += (_, _) => CheckAcl();
        hardenAcl.Click += (_, _) => HardenAcl();
        openIncidents.Click += (_, _) =>
            OpenDirectory(AppPaths.IncidentsDirectory);
        openBackups.Click += (_, _) =>
            OpenDirectory(AppPaths.BackupsDirectory);

        LoadValues();
        ShowLastStatus();
    }

    private static Button ActionButton(
        string text,
        int minimumWidth) =>
        new()
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(minimumWidth, 34),
            Margin = new Padding(0, 0, 8, 6)
        };

    private void LoadValues()
    {
        _enabled.Checked =
            _config.HousekeepingEnabled;
        _runOnStart.Checked =
            _config.HousekeepingRunOnStart;
        _intervalHours.Value =
            ClampDecimal(
                _config.HousekeepingIntervalHours,
                _intervalHours.Minimum,
                _intervalHours.Maximum);
        _incidentRetentionDays.Value =
            ClampDecimal(
                _config.IncidentRetentionDays,
                _incidentRetentionDays.Minimum,
                _incidentRetentionDays.Maximum);
        _backupRetentionDays.Value =
            ClampDecimal(
                _config.BackupRetentionDays,
                _backupRetentionDays.Minimum,
                _backupRetentionDays.Maximum);
        _backupMinimumFiles.Value =
            ClampDecimal(
                _config.BackupMinimumFiles,
                _backupMinimumFiles.Minimum,
                _backupMinimumFiles.Maximum);
        _tempRetentionDays.Value =
            ClampDecimal(
                _config.TemporaryFileRetentionDays,
                _tempRetentionDays.Minimum,
                _tempRetentionDays.Maximum);

        RefreshAclEnforcement();
    }

    private void SavePolicy()
    {
        try
        {
            _config.HousekeepingEnabled =
                _enabled.Checked;
            _config.HousekeepingRunOnStart =
                _runOnStart.Checked;
            _config.HousekeepingIntervalHours =
                (int)_intervalHours.Value;
            _config.IncidentRetentionDays =
                (int)_incidentRetentionDays.Value;
            _config.BackupRetentionDays =
                (int)_backupRetentionDays.Value;
            _config.BackupMinimumFiles =
                (int)_backupMinimumFiles.Value;
            _config.TemporaryFileRetentionDays =
                (int)_tempRetentionDays.Value;

            ConfigurationMaintenanceService
                .ValidateAppConfig(
                    _config);

            ConfigService.SaveAppConfig(
                _config);

            RestartServiceIfRunning();

            _status.Text =
                "Housekeeping policy saved." +
                Environment.NewLine +
                $"Incidents: {FormatRetention(_config.IncidentRetentionDays)}" +
                Environment.NewLine +
                $"Backups: {FormatRetention(_config.BackupRetentionDays)}; minimum {_config.BackupMinimumFiles}" +
                Environment.NewLine +
                $"Temporary files: {FormatTempRetention(_config.TemporaryFileRetentionDays)}";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RunHousekeeping(
        bool dryRun)
    {
        try
        {
            SaveValuesWithoutPersisting();

            var result =
                new HousekeepingService()
                    .Run(
                        _config,
                        dryRun);

            _status.Text =
                FormatHousekeeping(
                    result);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void CheckAcl()
    {
        try
        {
            var result =
                new StorageSecurityService()
                    .Check();

            _status.Text =
                FormatStorageSecurity(
                    result);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void HardenAcl()
    {
        try
        {
            var answer =
                MessageBox.Show(
                    this,
                    "Replace inherited ACLs on Incidents, Backups and AuditSigningTransitions with the BitKeyBridge protected-storage policy?" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "SYSTEM and local Administrators keep FullControl. The installed service identity receives only the access required by each directory.",
                    "Harden Protected Storage ACLs",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

            if (answer !=
                DialogResult.Yes)
            {
                return;
            }

            var result =
                new StorageSecurityService()
                    .Repair();

            if (result.RepairSucceeded)
            {
                _config.StorageAclHardeningEnabled =
                    true;
                ConfigService.SaveAppConfig(
                    _config);
                RestartServiceIfRunning();
            }

            RefreshAclEnforcement();

            _status.Text =
                FormatStorageSecurity(
                    result);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ShowLastStatus()
    {
        try
        {
            var last =
                new HousekeepingService()
                    .ReadLastStatus();

            if (last is null)
            {
                _status.Text =
                    "No completed housekeeping run is recorded yet.";
                return;
            }

            _status.Text =
                FormatHousekeeping(
                    last);
        }
        catch (Exception ex)
        {
            _status.Text =
                ex.Message;
        }
    }

    private void SaveValuesWithoutPersisting()
    {
        _config.HousekeepingEnabled =
            _enabled.Checked;
        _config.HousekeepingRunOnStart =
            _runOnStart.Checked;
        _config.HousekeepingIntervalHours =
            (int)_intervalHours.Value;
        _config.IncidentRetentionDays =
            (int)_incidentRetentionDays.Value;
        _config.BackupRetentionDays =
            (int)_backupRetentionDays.Value;
        _config.BackupMinimumFiles =
            (int)_backupMinimumFiles.Value;
        _config.TemporaryFileRetentionDays =
            (int)_tempRetentionDays.Value;

        ConfigurationMaintenanceService
            .ValidateAppConfig(
                _config);
    }

    private void RefreshAclEnforcement()
    {
        _aclEnforcement.Text =
            "Protected storage ACL health enforcement: " +
            (_config.StorageAclHardeningEnabled
                ? "ENABLED"
                : "DISABLED (Harden ACLs enables it)");
    }

    private static string FormatHousekeeping(
        HousekeepingStatus result)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            $"Success: {result.Success}");
        builder.AppendLine(
            $"Dry run: {result.DryRun}");
        builder.AppendLine(
            $"Started: {result.StartedAtUtc:O}");
        builder.AppendLine(
            $"Finished: {result.FinishedAtUtc:O}");
        builder.AppendLine();
        builder.AppendLine(
            $"Incidents scanned/candidates/deleted/preserved: " +
            $"{result.IncidentFilesScanned}/{result.IncidentCandidates}/{result.IncidentDeleted}/{result.IncidentPreserved}");
        builder.AppendLine(
            $"Incidents preserved NotFullyRetained: {result.IncidentPreservedNotFullyRetained}");
        builder.AppendLine(
            $"Backups scanned/candidates/deleted/minimum-preserved: " +
            $"{result.BackupFilesScanned}/{result.BackupCandidates}/{result.BackupDeleted}/{result.BackupPreservedMinimum}");
        builder.AppendLine(
            $"Temporary files scanned/deleted: {result.TemporaryFilesScanned}/{result.TemporaryFilesDeleted}");
        builder.AppendLine(
            $"Bytes freed: {result.BytesFreed}");
        builder.AppendLine(
            $"Errors: {result.Errors.Count + result.IncidentErrors + result.BackupErrors + result.TemporaryFileErrors}");

        foreach (var warning in
                 result.Warnings.Take(20))
        {
            builder.AppendLine(
                "WARNING: " + warning);
        }

        foreach (var error in
                 result.Errors.Take(20))
        {
            builder.AppendLine(
                "ERROR: " + error);
        }

        return builder.ToString();
    }

    private static string FormatStorageSecurity(
        StorageSecurityStatus result)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            $"Valid: {result.Valid}");
        builder.AppendLine(
            $"Repair attempted: {result.RepairAttempted}");
        builder.AppendLine(
            $"Repair succeeded: {result.RepairSucceeded}");
        builder.AppendLine();

        foreach (var directory in
                 result.Directories)
        {
            builder.AppendLine(
                $"{directory.Name}: Valid={directory.Valid}; " +
                $"Exists={directory.Exists}; Protected={directory.InheritanceProtected}; " +
                $"SYSTEM={directory.SystemFullControl}; Admins={directory.AdministratorsFullControl}; " +
                $"ServiceRequired={directory.ServiceAccessRequired}; ServiceAccess={directory.ServiceAccessPresent}; " +
                $"Mode={directory.ServiceAccessMode}; UnexpectedAllow={directory.UnexpectedAllowRules}");

            foreach (var problem in
                     directory.Problems.Take(10))
            {
                builder.AppendLine(
                    "  - " + problem);
            }
        }

        foreach (var error in
                 result.Errors.Take(20))
        {
            builder.AppendLine(
                "ERROR: " + error);
        }

        return builder.ToString();
    }

    private static string FormatRetention(
        int days) =>
        days == 0
            ? "keep forever"
            : $"{days} day(s)";

    private static string FormatTempRetention(
        int days) =>
        days == 0
            ? "disabled"
            : $"{days} day(s)";

    private static decimal ClampDecimal(
        int value,
        decimal minimum,
        decimal maximum) =>
        Math.Min(
            maximum,
            Math.Max(
                minimum,
                value));

    private static void AddNumeric(
        Control parent,
        string label,
        NumericUpDown control,
        int left,
        int top,
        int minimum,
        int maximum)
    {
        parent.Controls.Add(
            new Label
            {
                Text = label,
                Left = left,
                Top = top + 4,
                Width = 205,
                Height = 24
            });

        control.SetBounds(
            left + 210,
            top,
            120,
            27);
        control.Minimum = minimum;
        control.Maximum = maximum;

        parent.Controls.Add(
            control);
    }

    private static void OpenDirectory(
        string path)
    {
        Directory.CreateDirectory(
            path);

        Process.Start(
            new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
    }

    private static void RestartServiceIfRunning()
    {
        var service =
            WindowsServiceHost.GetInfo();

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

    private void ShowError(
        Exception ex)
    {
        MessageBox.Show(
            this,
            ex.Message,
            "Housekeeping / Protected Storage",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
