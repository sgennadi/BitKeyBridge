using System.Diagnostics;

namespace BitKeyBridge;

public sealed class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad = new();
    private readonly CheckedListBox _scopes = new();
    private readonly RichTextBox _exportLog = new();
    private readonly Label _lastSuccess = new();
    private readonly Label _exportSummary = new();
    private readonly ListView _scopeResults = new();
    private readonly CheckBox _forcePublish = new();
    private readonly Button _runExport = new();
    private readonly Button _dryRun = new();

    private readonly ListView _dcResults = new();
    private readonly RichTextBox _dcDetails = new();
    private readonly Button _dcTest = new();

    private readonly TextBox _localQuery = new();
    private readonly ListView _localResults = new();
    private readonly TextBox _localKey = new();
    private readonly Button _localShow = new();
    private string? _localCurrentKey;

    private readonly TextBox _cloudTenant = new();
    private readonly TextBox _cloudClient = new();
    private readonly TextBox _cloudUsername = new();
    private readonly TextBox _cloudPassword = new();
    private readonly TextBox _cloudThumbprint = new();
    private readonly ComboBox _cloudAuthMode = new();
    private readonly TextBox _cloudQuery = new();
    private readonly ListView _cloudResults = new();
    private readonly TextBox _cloudKey = new();
    private readonly Button _cloudShow = new();
    private readonly Label _cloudStatus = new();
    private CloudAuthConfig _cloudConfig;
    private GraphToken? _cloudToken;
    private string? _cloudCurrentKey;

    public MainForm(AppConfig config)
    {
        _config = config;
        _cloudConfig = ConfigService.LoadCloudConfig();
        Text = "BitKeyBridge (.NET)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1220, 820);
        MinimumSize = new Size(1000, 700);
        Font = new Font("Segoe UI", 9F);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildExportTab());
        tabs.TabPages.Add(BuildDcTab());
        tabs.TabPages.Add(BuildLocalSearchTab());
        tabs.TabPages.Add(BuildCloudTab());
        Controls.Add(tabs);

        LoadDefaultScopes();
        RefreshLastSuccess();
        LoadCloudFields();
    }

    private TabPage BuildExportTab()
    {
        var tab = new TabPage("Export");
        var header = new Label
        {
            Text = "BitLocker AD Recovery Export",
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Left = 18,
            Top = 16
        };
        tab.Controls.Add(header);

        _lastSuccess.SetBounds(20, 52, 1145, 24);
        tab.Controls.Add(_lastSuccess);

        var scopeGroup = new GroupBox { Text = "Export scopes (no selection = defaults)", Left = 20, Top = 82, Width = 1145, Height = 155, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _scopes.SetBounds(12, 23, 885, 115);
        _scopes.CheckOnClick = true;
        _scopes.HorizontalScrollbar = true;
        scopeGroup.Controls.Add(_scopes);

        var addOu = new Button { Text = "Add OU...", Left = 910, Top = 25, Width = 105, Height = 31 };
        var defaults = new Button { Text = "Use defaults", Left = 1025, Top = 25, Width = 105, Height = 31 };
        var remove = new Button { Text = "Remove", Left = 910, Top = 66, Width = 105, Height = 31 };
        var checkAll = new Button { Text = "Check all", Left = 1025, Top = 66, Width = 105, Height = 31 };
        var saveDefaults = new Button { Text = "Save defaults", Left = 910, Top = 107, Width = 220, Height = 31 };
        scopeGroup.Controls.AddRange([addOu, defaults, remove, checkAll, saveDefaults]);
        tab.Controls.Add(scopeGroup);

        addOu.Click += async (_, _) => await AddOuAsync();
        defaults.Click += (_, _) => LoadDefaultScopes();
        remove.Click += (_, _) => { if (_scopes.SelectedIndex >= 0) _scopes.Items.RemoveAt(_scopes.SelectedIndex); };
        checkAll.Click += (_, _) => { for (var i = 0; i < _scopes.Items.Count; i++) _scopes.SetItemChecked(i, true); };
        saveDefaults.Click += (_, _) => SaveSelectedScopesAsDefaults();

        _runExport.Text = "Run Export";
        _runExport.SetBounds(20, 250, 120, 36);
        _dryRun.Text = "Dry Run";
        _dryRun.SetBounds(150, 250, 105, 36);
        var openCsv = new Button { Text = "Open CSV", Left = 265, Top = 250, Width = 100, Height = 36 };
        var openLog = new Button { Text = "Open Log", Left = 375, Top = 250, Width = 100, Height = 36 };
        var openFolder = new Button { Text = "Open Folder", Left = 485, Top = 250, Width = 110, Height = 36 };
        _forcePublish.Text = "Override publish guards (row drop / scope change)";
        _forcePublish.AutoSize = true;
        _forcePublish.SetBounds(620, 260, 400, 24);
        tab.Controls.AddRange([_runExport, _dryRun, openCsv, openLog, openFolder, _forcePublish]);

        _exportSummary.SetBounds(20, 298, 1145, 42);
        _exportSummary.Text = "No run in this GUI session yet.";
        tab.Controls.Add(_exportSummary);

        _scopeResults.View = View.Details;
        _scopeResults.FullRowSelect = true;
        _scopeResults.GridLines = true;
        _scopeResults.SetBounds(20, 345, 1145, 165);
        _scopeResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_scopeResults, ("Scope", 180), ("Status", 80), ("Objects", 80), ("Valid", 70), ("Duplicates", 85), ("Invalid", 70), ("Error", 520));
        tab.Controls.Add(_scopeResults);

        _exportLog.ReadOnly = true;
        _exportLog.WordWrap = false;
        _exportLog.Font = new Font("Consolas", 9F);
        _exportLog.SetBounds(20, 522, 1145, 220);
        _exportLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tab.Controls.Add(_exportLog);

        _runExport.Click += async (_, _) => await RunExportAsync(false);
        _dryRun.Click += async (_, _) => await RunExportAsync(true);
        openCsv.Click += (_, _) => OpenPath(_config.OutputCsv);
        openLog.Click += (_, _) => OpenPath(_config.ErrorLog, "notepad.exe");
        openFolder.Click += (_, _) => OpenPath(_config.OutputDirectory);
        return tab;
    }

    private TabPage BuildDcTab()
    {
        var tab = new TabPage("DC Comparison");
        _dcTest.Text = "Discover and Test DCs";
        _dcTest.SetBounds(16, 16, 170, 36);
        var hint = new Label
        {
            Text = "Domain controllers are discovered dynamically from Active Directory. Uses the checked OUs from Export; if none are checked, defaults are used.",
            AutoSize = true,
            Left = 205,
            Top = 26
        };
        tab.Controls.AddRange([_dcTest, hint]);

        _dcResults.View = View.Details;
        _dcResults.FullRowSelect = true;
        _dcResults.GridLines = true;
        _dcResults.SetBounds(16, 65, 1155, 315);
        _dcResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_dcResults,
            ("DC", 120), ("Site", 140), ("RODC", 60), ("Reachable", 75), ("Status", 80),
            ("Objects", 75), ("Delta", 60), ("Repl errors", 80), ("Warnings", 75), ("Max age h", 80), ("IPv4", 105), ("OS", 190));
        tab.Controls.Add(_dcResults);

        _dcDetails.ReadOnly = true;
        _dcDetails.Font = new Font("Consolas", 9F);
        _dcDetails.SetBounds(16, 392, 1155, 345);
        _dcDetails.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tab.Controls.Add(_dcDetails);
        _dcTest.Click += async (_, _) => await RunDcTestAsync();
        return tab;
    }

    private TabPage BuildLocalSearchTab()
    {
        var tab = new TabPage("Recovery Search");
        var label = new Label { Text = "Computer name or Recovery ID:", Left = 16, Top = 22, AutoSize = true };
        _localQuery.SetBounds(200, 18, 520, 27);
        var search = new Button { Text = "Search Local CSV", Left = 735, Top = 16, Width = 135, Height = 32 };
        tab.Controls.AddRange([label, _localQuery, search]);

        _localResults.View = View.Details;
        _localResults.FullRowSelect = true;
        _localResults.GridLines = true;
        _localResults.SetBounds(16, 62, 1155, 330);
        _localResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_localResults, ("Computer", 220), ("Recovery ID", 330), ("Last checked", 160), ("Source", 90));
        tab.Controls.Add(_localResults);

        var keyLabel = new Label { Text = "Recovery key:", Left = 16, Top = 415, AutoSize = true };
        _localKey.SetBounds(110, 410, 510, 28);
        _localKey.ReadOnly = true;
        _localKey.UseSystemPasswordChar = true;
        _localShow.Text = "Show Key";
        _localShow.SetBounds(635, 408, 100, 32);
        var copy = new Button { Text = "Copy Key", Left = 745, Top = 408, Width = 100, Height = 32 };
        tab.Controls.AddRange([keyLabel, _localKey, _localShow, copy]);

        var note = new Label
        {
            Text = "The recovery password is hidden until a row is selected. Clipboard is cleared after 60 seconds if it still contains that key.",
            Left = 16,
            Top = 455,
            AutoSize = true
        };
        tab.Controls.Add(note);

        search.Click += (_, _) => SearchLocal();
        _localQuery.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) SearchLocal(); };
        _localResults.SelectedIndexChanged += (_, _) => SelectLocalRecord();
        _localShow.Click += (_, _) => ToggleKey(_localKey, _localShow);
        copy.Click += (_, _) => CopyKeyWithAutoClear(_localCurrentKey);
        return tab;
    }

    private TabPage BuildCloudTab()
    {
        var tab = new TabPage("Entra / Intune Cloud");
        var authGroup = new GroupBox { Text = "Microsoft Graph authentication", Left = 16, Top = 14, Width = 1155, Height = 205, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        tab.Controls.Add(authGroup);

        AddLabeled(authGroup, "Tenant ID/domain:", _cloudTenant, 14, 28, 135, 510);
        AddLabeled(authGroup, "Client ID:", _cloudClient, 14, 62, 135, 510);
        AddLabeled(authGroup, "Username:", _cloudUsername, 14, 96, 135, 510);
        AddLabeled(authGroup, "Password:", _cloudPassword, 14, 130, 135, 510);
        _cloudPassword.UseSystemPasswordChar = true;
        AddLabeled(authGroup, "Certificate thumbprint:", _cloudThumbprint, 585, 28, 150, 390);
        AddLabeled(authGroup, "Authentication:", _cloudAuthMode, 585, 62, 150, 390);
        _cloudAuthMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cloudAuthMode.Items.AddRange(["Username + Password (ROPC)", "App registration + certificate"]);

        var save = new Button { Text = "Save Config", Left = 585, Top = 103, Width = 115, Height = 32 };
        var connect = new Button { Text = "Connect / Test", Left = 710, Top = 103, Width = 120, Height = 32 };
        var autoSetup = new Button { Text = "Native Auto Setup", Left = 840, Top = 103, Width = 140, Height = 32 };
        authGroup.Controls.AddRange([save, connect, autoSetup]);
        var ropcNote = new Label
        {
            Text = "ROPC is for temporary/manual use and does not work when MFA/Conditional Access requires interactive authentication. Password is never saved.",
            Left = 585,
            Top = 148,
            Width = 545,
            Height = 42
        };
        authGroup.Controls.Add(ropcNote);

        _cloudStatus.SetBounds(16, 228, 1155, 24);
        _cloudStatus.Text = "Not connected.";
        tab.Controls.Add(_cloudStatus);

        var searchLabel = new Label { Text = "Computer name or Recovery ID:", Left = 16, Top = 267, AutoSize = true };
        _cloudQuery.SetBounds(205, 262, 515, 27);
        var search = new Button { Text = "Cloud Search", Left = 735, Top = 260, Width = 115, Height = 32 };
        tab.Controls.AddRange([searchLabel, _cloudQuery, search]);

        _cloudResults.View = View.Details;
        _cloudResults.FullRowSelect = true;
        _cloudResults.GridLines = true;
        _cloudResults.SetBounds(16, 305, 1155, 280);
        _cloudResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AddColumns(_cloudResults, ("Computer", 220), ("Recovery ID", 330), ("Device ID", 290), ("Volume", 100), ("Created", 160));
        tab.Controls.Add(_cloudResults);

        var keyLabel = new Label { Text = "Cloud recovery key:", Left = 16, Top = 607, AutoSize = true };
        _cloudKey.SetBounds(145, 602, 475, 28);
        _cloudKey.ReadOnly = true;
        _cloudKey.UseSystemPasswordChar = true;
        var getKey = new Button { Text = "Get Key from Entra", Left = 635, Top = 600, Width = 135, Height = 32 };
        _cloudShow.Text = "Show Key";
        _cloudShow.SetBounds(780, 600, 95, 32);
        var copy = new Button { Text = "Copy Key", Left = 885, Top = 600, Width = 95, Height = 32 };
        tab.Controls.AddRange([keyLabel, _cloudKey, getKey, _cloudShow, copy]);

        save.Click += (_, _) => SaveCloudFields();
        connect.Click += async (_, _) => await ConnectCloudAsync();
        search.Click += async (_, _) => await SearchCloudAsync();
        _cloudQuery.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) await SearchCloudAsync(); };
        getKey.Click += async (_, _) => await GetCloudKeyAsync();
        _cloudShow.Click += (_, _) => ToggleKey(_cloudKey, _cloudShow);
        copy.Click += (_, _) => CopyKeyWithAutoClear(_cloudCurrentKey);
        autoSetup.Click += async (_, _) => await RunNativeAutoSetupAsync();
        _cloudAuthMode.SelectedIndexChanged += (_, _) => UpdateCloudAuthUi();
        return tab;
    }

    private void LoadDefaultScopes()
    {
        _scopes.Items.Clear();
        foreach (var scope in _config.DefaultScopes) _scopes.Items.Add(scope, true);
    }

    private List<BitLockerScope> GetSelectedScopes()
    {
        var selected = _scopes.CheckedItems.OfType<BitLockerScope>().ToList();
        return selected.Count == 0 ? _config.DefaultScopes.ToList() : selected;
    }


    private void SaveSelectedScopesAsDefaults()
    {
        var selected = _scopes.CheckedItems.OfType<BitLockerScope>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Check at least one OU before saving defaults.", "Default Scopes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            _config.DefaultScopes = selected;
            ConfigService.SaveAppConfig(_config);
            MessageBox.Show(this, $"Saved {selected.Count} default OU scope(s) to:{Environment.NewLine}{AppPaths.AppSettingsFile}", "Default Scopes", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Default Scopes", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task AddOuAsync()
    {
        try
        {
            UseWaitCursor = true;
            var dc = _ad.GetPreferredWritableDc();
            var ous = await Task.Run(() => _ad.ListOrganizationalUnits(dc));
            using var browser = new OuBrowserForm();
            browser.LoadScopes(ous);
            if (browser.ShowDialog(this) == DialogResult.OK && browser.SelectedScope is { } scope)
            {
                var existing = _scopes.Items.OfType<BitLockerScope>().Any(x => x.SearchBase.Equals(scope.SearchBase, StringComparison.OrdinalIgnoreCase));
                if (!existing) _scopes.Items.Add(scope, true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "OU Browser Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async Task RunExportAsync(bool dryRun)
    {
        SetExportRunning(true);
        _exportLog.Clear();
        _scopeResults.Items.Clear();
        try
        {
            var progress = new Progress<string>(AppendExportLog);
            var service = new ExportService(_config);
            var result = await service.RunAsync(dryRun, _forcePublish.Checked, GetSelectedScopes(), progress);
            ShowExportResult(result);
        }
        finally
        {
            SetExportRunning(false);
            RefreshLastSuccess();
        }
    }

    private void ShowExportResult(ExportResult result)
    {
        var state = result.Success ? (result.DryRun ? "DRY RUN OK" : "SUCCESS") : "FAILED";
        _exportSummary.Text = $"Status: {state}    DC: {result.AdServer}    Previous: {result.PreviousRows}    New: {result.ValidRows}    Objects: {result.ObjectsFound}    Duplicates: {result.Duplicates}    Invalid: {result.InvalidObjects}    Duration: {result.DurationSeconds:0.00}s";
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) _exportSummary.Text += Environment.NewLine + "Error: " + result.ErrorMessage;
        else if (!string.IsNullOrWhiteSpace(result.RowCountWarning)) _exportSummary.Text += Environment.NewLine + "Warning: " + result.RowCountWarning;
        else if (!string.IsNullOrWhiteSpace(result.ScopeWarning)) _exportSummary.Text += Environment.NewLine + "Warning: " + result.ScopeWarning;

        _scopeResults.Items.Clear();
        foreach (var scope in result.Containers)
        {
            var item = new ListViewItem(scope.Name);
            item.SubItems.Add(scope.Status);
            item.SubItems.Add(scope.ObjectsFound.ToString());
            item.SubItems.Add(scope.ValidRows.ToString());
            item.SubItems.Add(scope.Duplicates.ToString());
            item.SubItems.Add(scope.Invalid.ToString());
            item.SubItems.Add(scope.Error ?? string.Empty);
            _scopeResults.Items.Add(item);
        }
    }

    private void SetExportRunning(bool running)
    {
        _runExport.Enabled = !running;
        _dryRun.Enabled = !running;
        _forcePublish.Enabled = !running;
    }

    private void AppendExportLog(string message)
    {
        _exportLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _exportLog.SelectionStart = _exportLog.TextLength;
        _exportLog.ScrollToCaret();
    }

    private void RefreshLastSuccess()
    {
        try
        {
            var last = JsonStore.Read<LastSuccessInfo>(_config.LastSuccessFile);
            if (last is null)
            {
                _lastSuccess.Text = "Last successful export: not recorded yet.";
                _lastSuccess.ForeColor = SystemColors.ControlText;
                return;
            }
            var age = (DateTime.Now - last.Finished).TotalHours;
            var stale = age > _config.StaleSuccessHours;
            _lastSuccess.Text = $"Last successful export: {last.Finished:yyyy-MM-dd HH:mm:ss} ({age:0.0}h ago) | Rows: {last.ValidRows} | DC: {last.AdServer}" + (stale ? " | STALE" : string.Empty);
            _lastSuccess.ForeColor = stale ? Color.DarkRed : Color.DarkGreen;
        }
        catch { }
    }

    private async Task RunDcTestAsync()
    {
        _dcTest.Enabled = false;
        _dcResults.Items.Clear();
        _dcDetails.Clear();
        try
        {
            var progress = new Progress<string>(m => _dcDetails.AppendText($"[{DateTime.Now:HH:mm:ss}] {m}{Environment.NewLine}"));
            var service = new DomainControllerComparisonService(_config);
            var rows = await service.RunAsync(GetSelectedScopes(), progress);
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.Name);
                item.SubItems.Add(row.Site);
                item.SubItems.Add(row.IsReadOnly ? "Yes" : "No");
                item.SubItems.Add(row.Reachable ? "Yes" : "No");
                item.SubItems.Add(row.Status);
                item.SubItems.Add(row.ObjectsFound.ToString());
                item.SubItems.Add(row.DifferenceFromMax?.ToString() ?? "-");
                item.SubItems.Add(row.ReplicationErrors.ToString());
                item.SubItems.Add(row.ReplicationWarnings.ToString());
                item.SubItems.Add(row.MaxReplicationAgeHours?.ToString("0.00") ?? "-");
                item.SubItems.Add(row.IPv4Address);
                item.SubItems.Add(row.OperatingSystem);
                item.Tag = row;
                _dcResults.Items.Add(item);
            }
            _dcDetails.AppendText(Environment.NewLine + $"Discovered {rows.Count} domain controller(s)." + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _dcDetails.AppendText("ERROR: " + ex + Environment.NewLine);
        }
        finally { _dcTest.Enabled = true; }
    }

    private void SearchLocal()
    {
        try
        {
            var q = _localQuery.Text.Trim();
            var rows = CsvUtility.ReadRecoveryCsv(_config.OutputCsv);
            if (!string.IsNullOrWhiteSpace(q))
                rows = rows.Where(x => x.ComputerName.Contains(q, StringComparison.OrdinalIgnoreCase) || x.BitLockerId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            rows = rows.Take(1000).ToList();
            _localResults.Items.Clear();
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.ComputerName);
                item.SubItems.Add(row.BitLockerId);
                item.SubItems.Add(row.LastChecked.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(row.Source);
                item.Tag = row;
                _localResults.Items.Add(item);
            }
            _localCurrentKey = null;
            _localKey.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Recovery Search", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SelectLocalRecord()
    {
        if (_localResults.SelectedItems.Count == 0 || _localResults.SelectedItems[0].Tag is not RecoveryRecord row) return;
        _localCurrentKey = row.RecoveryKey;
        _localKey.Text = row.RecoveryKey;
        _localKey.UseSystemPasswordChar = true;
        _localShow.Text = "Show Key";
    }

    private void LoadCloudFields()
    {
        _cloudConfig = ConfigService.LoadCloudConfig();
        _cloudTenant.Text = _cloudConfig.TenantId;
        _cloudClient.Text = _cloudConfig.ClientId;
        _cloudUsername.Text = _cloudConfig.Username;
        _cloudThumbprint.Text = _cloudConfig.CertificateThumbprint;
        _cloudAuthMode.SelectedIndex = _cloudConfig.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        UpdateCloudAuthUi();
    }

    private void SaveCloudFields()
    {
        _cloudConfig.TenantId = _cloudTenant.Text.Trim();
        _cloudConfig.ClientId = _cloudClient.Text.Trim();
        _cloudConfig.Username = _cloudUsername.Text.Trim();
        _cloudConfig.CertificateThumbprint = _cloudThumbprint.Text.Trim();
        _cloudConfig.AuthMode = _cloudAuthMode.SelectedIndex == 1 ? "Certificate" : "Password";
        ConfigService.SaveCloudConfig(_cloudConfig);
        _cloudStatus.Text = "Cloud config saved. Password was not saved.";
    }

    private void UpdateCloudAuthUi()
    {
        var cert = _cloudAuthMode.SelectedIndex == 1;
        _cloudUsername.Enabled = !cert;
        _cloudPassword.Enabled = !cert;
        _cloudThumbprint.Enabled = cert;
    }

    private async Task<bool> ConnectCloudAsync()
    {
        try
        {
            SaveCloudFields();
            _cloudStatus.Text = "Connecting...";
            using var graph = new CloudGraphService();
            _cloudToken = _cloudAuthMode.SelectedIndex == 1
                ? await graph.AcquireCertificateTokenAsync(_cloudTenant.Text, _cloudClient.Text, _cloudThumbprint.Text)
                : await graph.AcquirePasswordTokenAsync(_cloudTenant.Text, _cloudClient.Text, _cloudUsername.Text, _cloudPassword.Text);
            var count = await graph.TestAccessAsync(_cloudToken.AccessToken);
            _cloudStatus.Text = $"Connected using {_cloudToken.AuthMode}. First Graph page returned {count} recovery metadata item(s).";
            return true;
        }
        catch (Exception ex)
        {
            _cloudToken = null;
            _cloudStatus.Text = "Connection failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Microsoft Graph", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task<bool> EnsureCloudTokenAsync()
    {
        if (_cloudToken is not null && _cloudToken.ExpiresAt > DateTime.Now.AddMinutes(1)) return true;
        return await ConnectCloudAsync();
    }

    private async Task SearchCloudAsync()
    {
        if (!await EnsureCloudTokenAsync()) return;
        try
        {
            _cloudStatus.Text = "Searching cloud BitLocker metadata...";
            using var graph = new CloudGraphService();
            var rows = await graph.SearchAsync(_cloudToken!.AccessToken, _cloudQuery.Text);
            _cloudResults.Items.Clear();
            foreach (var row in rows)
            {
                var item = new ListViewItem(row.ComputerName);
                item.SubItems.Add(row.RecoveryId);
                item.SubItems.Add(row.DeviceId);
                item.SubItems.Add(row.VolumeType);
                item.SubItems.Add(row.CreatedDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                item.Tag = row;
                _cloudResults.Items.Add(item);
            }
            _cloudCurrentKey = null;
            _cloudKey.Clear();
            _cloudStatus.Text = $"Cloud search returned {rows.Count} metadata record(s). Recovery passwords were not requested.";
        }
        catch (Exception ex)
        {
            _cloudStatus.Text = "Cloud search failed: " + ex.Message;
        }
    }

    private async Task GetCloudKeyAsync()
    {
        if (_cloudResults.SelectedItems.Count == 0 || _cloudResults.SelectedItems[0].Tag is not CloudRecoveryMetadata row)
        {
            MessageBox.Show(this, "Select a cloud recovery record first.", "Cloud Recovery", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!await EnsureCloudTokenAsync()) return;
        try
        {
            _cloudStatus.Text = "Retrieving recovery password from Entra (this access is audited)...";
            using var graph = new CloudGraphService();
            _cloudCurrentKey = await graph.GetRecoveryKeyValueAsync(_cloudToken!.AccessToken, row.RecoveryId);
            _cloudKey.Text = _cloudCurrentKey;
            _cloudKey.UseSystemPasswordChar = true;
            _cloudShow.Text = "Show Key";
            _cloudStatus.Text = "Recovery password retrieved. This key-read operation is auditable in Microsoft Entra.";
        }
        catch (Exception ex)
        {
            _cloudStatus.Text = "Key retrieval failed: " + ex.Message;
        }
    }

    private async Task RunNativeAutoSetupAsync()
    {
        SaveCloudFields();
        using var input = new InputDialog(
            "Native Entra Auto Setup",
            "Bootstrap public-client Application (Client) ID. It must already be allowed to request Application.ReadWrite.All, AppRoleAssignment.ReadWrite.All and DelegatedPermissionGrant.ReadWrite.All:",
            _cloudConfig.BootstrapClientId);
        if (input.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(input.Value)) return;
        _cloudConfig.BootstrapClientId = input.Value;
        ConfigService.SaveCloudConfig(_cloudConfig);

        try
        {
            Enabled = false;
            using var setup = new EntraSetupService();
            var progress = new Progress<string>(m => _cloudStatus.Text = m);
            var result = await setup.RunAsync(
                _cloudTenant.Text,
                input.Value,
                "BitKeyBridge",
                _cloudConfig,
                async info =>
                {
                    try { Clipboard.SetText(info.UserCode); } catch { }
                    try { Process.Start(new ProcessStartInfo(info.VerificationUri) { UseShellExecute = true }); } catch { }
                    MessageBox.Show(this,
                        info.Message + Environment.NewLine + Environment.NewLine +
                        "The code has also been copied to the clipboard. Sign in with an Entra administrator account and approve the requested management permissions, then close this dialog.",
                        "Microsoft Entra Device Code",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    await Task.CompletedTask;
                },
                progress);
            LoadCloudFields();
            _cloudStatus.Text = $"Auto Setup complete. Client ID: {result.ClientId}; certificate: {result.CertificateThumbprint}";
        }
        catch (Exception ex)
        {
            _cloudStatus.Text = "Auto Setup failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Native Entra Auto Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Enabled = true; }
    }

    private static void ToggleKey(TextBox box, Button button)
    {
        box.UseSystemPasswordChar = !box.UseSystemPasswordChar;
        button.Text = box.UseSystemPasswordChar ? "Show Key" : "Hide Key";
    }

    private void CopyKeyWithAutoClear(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        Clipboard.SetText(key);
        var timer = new System.Windows.Forms.Timer { Interval = 60000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                if (Clipboard.ContainsText() && string.Equals(Clipboard.GetText(), key, StringComparison.Ordinal)) Clipboard.Clear();
            }
            catch { }
            timer.Dispose();
        };
        timer.Start();
    }

    private static void AddColumns(ListView view, params (string Name, int Width)[] columns)
    {
        foreach (var (name, width) in columns) view.Columns.Add(name, width);
    }

    private static void AddLabeled(Control parent, string labelText, Control control, int x, int y, int labelWidth, int controlWidth)
    {
        var label = new Label { Text = labelText, Left = x, Top = y + 4, Width = labelWidth, Height = 23 };
        control.SetBounds(x + labelWidth, y, controlWidth, 27);
        parent.Controls.Add(label);
        parent.Controls.Add(control);
    }

    private void OpenPath(string path, string? executable = null)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show(this, $"Path does not exist:{Environment.NewLine}{path}", "Open", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (executable is null)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(executable, $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
