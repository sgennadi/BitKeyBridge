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
        ClientSize = new Size(900, 660);
        MinimumSize = new Size(820, 600);
        Font = new Font("Segoe UI", 9F);

        Controls.Add(new Label
        {
            Text = "Housekeeping / Protected Storage",
            Font = new Font(
                "Segoe UI Semibold",
                15F),
            AutoSize = true,
            Left = 18,
            Top = 16
        });

        Controls.Add(new Label
        {
            Text =
                "Incident deletion is evidence-aware: only fully verified bundles are eligible. " +
                "NotFullyRetained or mismatched incidents are preserved. Audit-signing transition history is never automatically deleted.",
            Left = 20,
            Top = 54,
            Width = 845,
            Height = 48
        });

        var policy = new GroupBox
        {
            Text = "Retention Policy",
            Left = 20,
            Top = 108,
            Width = 855,
            Height = 218,
            Anchor =
                AnchorStyles.Top |
                AnchorStyles.Left |
                AnchorStyles.Right
        };
        Controls.Add(policy);

        _enabled.Text =
            "Enable scheduled housekeeping";
        _enabled.SetBounds(
            18,
            28,
            250,
            26);
        policy.Controls.Add(_enabled);

        _runOnStart.Text =
            "Run when Windows Service starts";
        _runOnStart.SetBounds(
            300,
            28,
            250,
            26);
        policy.Controls.Add(_runOnStart);

        AddNumeric(
            policy,
            "Interval (hours):",
            _intervalHours,
            18,
            68,
            1,
            168);

        AddNumeric(
            policy,
            "Incident retention days:",
            _incidentRetentionDays,
            430,
            68,
            0,
            36500);

        AddNumeric(
            policy,
            "Backup retention days:",
            _backupRetentionDays,
            18,
            108,
            0,
            36500);

        AddNumeric(
            policy,
            "Minimum backups to keep:",
            _backupMinimumFiles,
            430,
            108,
            0,
            1000);

        AddNumeric(
            policy,
            "Temporary-file retention days:",
            _tempRetentionDays,
            18,
            148,
            0,
            3650);

        policy.Controls.Add(new Label
        {
            Text =
                "0 incident/backup days = keep forever. 0 temporary-file days = disable temporary-file cleanup.",
            Left = 430,
            Top = 153,
            Width = 390,
            Height = 42
        });

        var save = new Button
        {
            Text = "Save Policy",
            Left = 20,
            Top = 342,
            Width = 120,
            Height = 34
        };
        var dryRun = new Button
        {
            Text = "Dry Run",
            Left = 150,
            Top = 342,
            Width = 110,
            Height = 34
        };
        var runNow = new Button
        {
            Text = "Run Now",
            Left = 270,
            Top = 342,
            Width = 110,
            Height = 34
        };
        var aclStatus = new Button
        {
            Text = "ACL Status",
            Left = 400,
            Top = 342,
            Width = 115,
            Height = 34
        };
        var hardenAcl = new Button
        {
            Text = "Harden ACLs",
            Left = 525,
            Top = 342,
            Width = 125,
            Height = 34
        };
        var openIncidents = new Button
        {
            Text = "Incidents",
            Left = 670,
            Top = 342,
            Width = 95,
            Height = 34
        };
        var openBackups = new Button
        {
            Text = "Backups",
            Left = 775,
            Top = 342,
            Width = 95,
            Height = 34
        };

        Controls.AddRange([
            save,
            dryRun,
            runNow,
            aclStatus,
            hardenAcl,
            openIncidents,
            openBackups
        ]);

        _aclEnforcement.SetBounds(
            20,
            388,
            850,
            26);
        Controls.Add(_aclEnforcement);

        _status.SetBounds(
            20,
            420,
            850,
            185);
        _status.ReadOnly = true;
        _status.WordWrap = false;
        _status.ScrollBars =
            RichTextBoxScrollBars.Both;
        _status.Font =
            new Font(
                "Consolas",
                9F);
        _status.Anchor =
            AnchorStyles.Top |
            AnchorStyles.Bottom |
            AnchorStyles.Left |
            AnchorStyles.Right;
        Controls.Add(_status);

        var close = new Button
        {
            Text = "Close",
            Left = 770,
            Top = 616,
            Width = 100,
            Height = 32,
            Anchor =
                AnchorStyles.Bottom |
                AnchorStyles.Right
        };
        close.Click += (_, _) =>
            Close();
        Controls.Add(close);

        save.Click += (_, _) =>
            SavePolicy();
        dryRun.Click += (_, _) =>
            RunHousekeeping(
                dryRun: true);
        runNow.Click += (_, _) =>
            RunHousekeeping(
                dryRun: false);
        aclStatus.Click += (_, _) =>
            CheckAcl();
        hardenAcl.Click += (_, _) =>
            HardenAcl();
        openIncidents.Click += (_, _) =>
            OpenDirectory(
                AppPaths.IncidentsDirectory);
        openBackups.Click += (_, _) =>
            OpenDirectory(
                AppPaths.BackupsDirectory);

        LoadValues();
        ShowLastStatus();
    }

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
