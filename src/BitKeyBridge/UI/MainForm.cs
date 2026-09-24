using System.Diagnostics;

namespace BitKeyBridge;

public sealed partial class MainForm : DpiAwareForm
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;
    private readonly AuditService _audit = new();
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

    private readonly TextBox _cloudTenant = new();
    private readonly TextBox _cloudClient = new();
    private readonly TextBox _cloudUsername = new();
    private readonly TextBox _cloudPassword = new();
    private readonly TextBox _cloudThumbprint = new();
    private readonly ComboBox _cloudAuthMode = new();
    private readonly Label _cloudStatus = new();
    private CloudAuthConfig _cloudConfig;
    private GraphToken? _cloudToken;
    private readonly ListView _coverageResults = new();
    private readonly Label _coverageSummary = new();
    private readonly Label _coverageStatus = new();
    private readonly ComboBox _coverageFilter = new();
    private readonly NumericUpDown _coverageStaleDays = new();
    private readonly NumericUpDown _coverageOldKeyDays = new();
    private CoverageResult? _coverageCurrent;

    private readonly TextBox _unifiedQuery = new();
    private readonly ListView _unifiedResults = new();
    private readonly RichTextBox _unifiedDetails = new();
    private readonly Label _unifiedStatus = new();

    private readonly ListView _auditResults = new();
    private readonly Label _auditSigningStatus = new();

    private readonly ComboBox _adMode = new();
    private readonly TextBox _adServer = new();
    private readonly TextBox _adDomain = new();
    private readonly TextBox _adUsername = new();
    private readonly TextBox _adPassword = new();
    private readonly NumericUpDown _adPort = new();
    private readonly CheckBox _adUseLdaps = new();
    private readonly CheckBox _adExplicitCredentials = new();
    private readonly ComboBox _adCredentialStorage = new();
    private readonly Label _credentialVaultStatus = new();
    private readonly TextBox _outputRoot = new();
    private readonly TextBox _outputSubdirectory = new();
    private readonly Label _adConnectionStatus = new();

    private readonly Label _startPurposeStatus = new();
    private readonly Label _startOuStatus = new();
    private readonly Button _startSelectOu = new();
    private readonly TextBox _startQuery = new();
    private readonly Button _startSearch = new();
    private readonly ListView _startResults = new();
    private readonly TextBox _startKey = new();
    private readonly Button _startShow = new();
    private BitLockerScope? _startScope;
    private string _startDomainDn = string.Empty;
    private string? _startCurrentKey;
    private string? _clipboardRecoveryKey;
    private System.Windows.Forms.Timer? _clipboardClearTimer;

    private readonly Label _dashboardStatus = new();
    private readonly RichTextBox _dashboardDetails = new();
    private readonly NumericUpDown _serviceInterval = new();
    private readonly NumericUpDown _healthPort = new();
    private readonly CheckBox _healthEnabled = new();
    private readonly CheckBox _serviceRunOnStart = new();
    private readonly CheckBox _serviceCoverageEnabled = new();
    private readonly NumericUpDown _serviceCoverageInterval = new();
    private readonly CheckBox _serviceCoverageRunOnStart = new();
    private readonly Label _machineCloudStatus = new();
    private readonly ComboBox _serviceIdentityMode = new();
    private readonly TextBox _serviceIdentityAccount = new();
    private readonly TextBox _serviceIdentityPassword = new();
    private readonly Label _serviceIdentityStatus = new();

    private readonly Label _updateStatus = new();
    private readonly TextBox _updateRepository = new();
    private readonly CheckBox _checkUpdatesOnStart = new();
    private readonly CheckBox _allowPrereleaseUpdates = new();
    private UpdateInfo? _lastUpdateInfo;

    private readonly Label _remoteApiStatus = new();
    private readonly NumericUpDown _remoteApiPort = new();
    private readonly CheckBox _remoteApiManagement = new();

    private readonly CheckBox _requireRecoveryReference = new();
    private readonly CheckBox _suggestRotationAfterRecovery = new();
    private readonly Dictionary<string, RecoveryAccessContext> _recoveryAccessContexts =
        new(StringComparer.OrdinalIgnoreCase);

    public MainForm(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService(config);
        _cloudConfig = ConfigService.LoadCloudConfig();
        try
        {
            if (SecurityContext.IsAdministrator())
                WindowsEventLogService.EnsureSource();
        }
        catch { }
        var assemblyVersion = GetType().Assembly.GetName().Version;
        Text = $"BitKeyBridge {assemblyVersion?.ToString(3) ?? "unknown"} (.NET)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1220, 820);
        MinimumSize = new Size(1000, 700);
        Font = new Font("Segoe UI", 9F);

        Controls.Add(BuildMainShell());

        LoadDirectorySettings();
        LoadDefaultScopes();
        RefreshLastSuccess();
        LoadCloudFields();
        LoadDashboardSettings();
        LoadOperationsSettings();
        RefreshDashboard();
        FormClosing += (_, _) => ClearSensitiveState();
        Shown += async (_, _) =>
        {
            await InitializeRecoveryWorkspaceAsync();

            if (_config.CheckForUpdatesOnStart &&
                AdministrationAllowed)
            {
                await CheckForUpdatesGuiAsync(
                    silentWhenCurrent: true);
            }
        };
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

    private void LoadCloudFields()
    {
        if (_cloudAuthMode.Items.Count == 0)
        {
            _cloudAuthMode.DropDownStyle =
                ComboBoxStyle.DropDownList;
            _cloudAuthMode.Items.AddRange([
                "Device Code (MFA / Conditional Access)",
                "Username + Password (ROPC legacy)",
                "App registration + certificate"
            ]);
        }

        _cloudConfig = ConfigService.LoadCloudConfig();
        _cloudTenant.Text = _cloudConfig.TenantId;
        _cloudClient.Text = _cloudConfig.ClientId;
        _cloudUsername.Text = _cloudConfig.Username;
        _cloudThumbprint.Text = _cloudConfig.CertificateThumbprint;
        _cloudAuthMode.SelectedIndex = _cloudConfig.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase)
            ? 2
            : _cloudConfig.AuthMode.Equals("Password", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
        UpdateCloudAuthUi();
        if (string.IsNullOrWhiteSpace(_cloudConfig.ClientId))
        {
            _cloudStatus.Text =
                "No BitKeyBridge App Registration is configured yet. Click First-Run / Repair Setup; no pre-created App Registration is required.";
        }
    }

    private void SaveCloudFields()
    {
        _cloudConfig.TenantId = _cloudTenant.Text.Trim();
        _cloudConfig.ClientId = _cloudClient.Text.Trim();
        _cloudConfig.Username = _cloudUsername.Text.Trim();
        _cloudConfig.CertificateThumbprint = _cloudThumbprint.Text.Trim();
        _cloudConfig.AuthMode = _cloudAuthMode.SelectedIndex switch
        {
            2 => "Certificate",
            1 => "Password",
            _ => "DeviceCode"
        };
        ConfigService.SaveCloudConfig(_cloudConfig);
        _cloudStatus.Text = "Cloud config saved. Password was not saved.";
    }

    private void UpdateCloudAuthUi()
    {
        var deviceCode = _cloudAuthMode.SelectedIndex == 0;
        var password = _cloudAuthMode.SelectedIndex == 1;
        var cert = _cloudAuthMode.SelectedIndex == 2;
        _cloudUsername.Enabled = password;
        _cloudPassword.Enabled = password;
        _cloudThumbprint.Enabled = cert;

        if (!password)
            _cloudPassword.Clear();
    }

    private async Task<bool> ConnectCloudAsync()
    {
        var passwordMode =
            _cloudAuthMode.SelectedIndex == 1;

        try
        {
            SaveCloudFields();
            _cloudStatus.Text =
                "Connecting...";

            using var graph =
                new CloudGraphService();

            _cloudToken =
                _cloudAuthMode.SelectedIndex switch
                {
                    2 =>
                        await graph
                            .AcquireCertificateTokenAsync(
                                _cloudTenant.Text,
                                _cloudClient.Text,
                                _cloudThumbprint.Text),
                    1 =>
                        await graph
                            .AcquirePasswordTokenAsync(
                                _cloudTenant.Text,
                                _cloudClient.Text,
                                _cloudUsername.Text,
                                _cloudPassword.Text),
                    _ =>
                        await graph
                            .AcquireDeviceCodeTokenAsync(
                                _cloudTenant.Text,
                                _cloudClient.Text,
                                ShowDeviceCodeAsync)
                };

            var count =
                await graph.TestAccessAsync(
                    _cloudToken.AccessToken);

            _cloudStatus.Text =
                $"Connected using {_cloudToken.AuthMode}. First Graph page returned {count} recovery metadata item(s).";

            return true;
        }
        catch (Exception ex)
        {
            _cloudToken = null;
            _cloudStatus.Text =
                "Connection failed: " +
                ex.Message;

            MessageBox.Show(
                this,
                ex.Message,
                "Microsoft Graph",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }
        finally
        {
            if (passwordMode)
                _cloudPassword.Clear();
        }
    }

    private async Task<bool> EnsureCloudTokenAsync()
    {
        if (_cloudToken is not null && _cloudToken.ExpiresAt > DateTime.Now.AddMinutes(1)) return true;
        return await ConnectCloudAsync();
    }

    private async Task RunNativeAutoSetupAsync()
    {
        SaveCloudFields();

        var usingDefaultBootstrap = string.IsNullOrWhiteSpace(_cloudConfig.BootstrapClientId) ||
            string.Equals(
                _cloudConfig.BootstrapClientId,
                EntraSetupService.DefaultBootstrapClientId,
                StringComparison.OrdinalIgnoreCase);

        var bootstrapDescription = usingDefaultBootstrap
            ? $"Microsoft first-party '{EntraSetupService.DefaultBootstrapDisplayName}'"
            : $"custom bootstrap Client ID {_cloudConfig.BootstrapClientId}";

        var answer = MessageBox.Show(
            this,
            "BitKeyBridge will create or repair its dedicated Microsoft Entra App Registration automatically." +
            Environment.NewLine + Environment.NewLine +
            $"Bootstrap: {bootstrapDescription}" + Environment.NewLine +
            "You will be asked to sign in with an Entra administrator account using Device Code and approve the requested management permissions." +
            Environment.NewLine + Environment.NewLine +
            "BitKeyBridge will then create/update the application, Enterprise Application, Graph permissions, admin-consent grants and local certificate. No administrator password is stored." +
            Environment.NewLine + Environment.NewLine +
            "Continue?",
            "First-Run / Repair Entra Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1);
        if (answer != DialogResult.Yes) return;

        try
        {
            Enabled = false;
            _cloudStatus.Text = "Starting first-run Entra setup...";
            using var setup = new EntraSetupService();
            var progress = new Progress<string>(m => _cloudStatus.Text = m);
            var result = await setup.RunAsync(
                _cloudTenant.Text,
                _cloudConfig.BootstrapClientId,
                "BitKeyBridge",
                _cloudConfig,
                ShowDeviceCodeAsync,
                progress);
            LoadCloudFields();
            _cloudAuthMode.SelectedIndex = 0;
            SaveCloudFields();
            _audit.Write(
                "EntraAutoSetup",
                computerName: Environment.MachineName,
                source: "Entra",
                authMode: "DeviceCode",
                details: $"ApplicationId={result.ClientId}; ServicePrincipalId={result.ServicePrincipalId}");
            _cloudStatus.Text =
                $"First-run setup complete. BitKeyBridge App Client ID: {result.ClientId}; certificate: {result.CertificateThumbprint}";
            MessageBox.Show(
                this,
                "Microsoft Entra setup completed successfully." + Environment.NewLine + Environment.NewLine +
                $"BitKeyBridge Client ID: {result.ClientId}" + Environment.NewLine +
                $"Certificate: {result.CertificateThumbprint}" + Environment.NewLine +
                $"Certificate expires: {result.CertificateNotAfter:yyyy-MM-dd}" + Environment.NewLine + Environment.NewLine +
                "Authentication was switched to Device Code. You can now use Connect / Test, Cloud Search and Unified Devices.",
                "BitKeyBridge Entra Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "EntraAutoSetup",
                "Failed",
                source: "Entra",
                authMode: "DeviceCode",
                details: ex.Message);
            _cloudStatus.Text = "Auto Setup failed: " + ex.Message;
            MessageBox.Show(
                this,
                ex.Message + Environment.NewLine + Environment.NewLine +
                "If your tenant blocks the Microsoft first-party bootstrap through Conditional Access, use Bootstrap... to configure a tenant-approved public-client Application ID and run setup again.",
                "First-Run / Repair Entra Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally { Enabled = true; }
    }

    private async Task RolloverEntraCertificateGuiAsync()
    {
        SaveCloudFields();

        if (string.IsNullOrWhiteSpace(
                _cloudConfig.ClientId) ||
            string.IsNullOrWhiteSpace(
                _cloudConfig.CertificateThumbprint))
        {
            MessageBox.Show(
                this,
                "Run First-Run / Repair Setup first. A managed Client ID and certificate are required before rollover.",
                "Entra Certificate Rollover",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Roll over the BitKeyBridge Entra certificate?" +
            Environment.NewLine +
            Environment.NewLine +
            "A new LocalMachine certificate will be added to the existing App Registration and tested with app-only Graph authentication before BitKeyBridge switches its configuration." +
            Environment.NewLine +
            Environment.NewLine +
            "The previous Graph credential and local certificate will be retained for rollback/grace.",
            "Entra Certificate Rollover",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        try
        {
            Enabled = false;
            _cloudStatus.Text =
                "Starting staged Entra certificate rollover...";

            using var lifecycle =
                new EntraCertificateLifecycleService();

            var progress =
                new Progress<string>(
                    message =>
                        _cloudStatus.Text =
                            message);

            var result =
                await lifecycle.RolloverAsync(
                    _cloudConfig,
                    ShowDeviceCodeAsync,
                    progress);

            LoadCloudFields();
            RefreshMachineCloudStatus();
            RefreshDashboard();

            _audit.Write(
                "EntraCertificateRollover",
                source: "Entra",
                authMode: "DeviceCode",
                details:
                    $"Previous={result.PreviousThumbprint}; New={result.NewThumbprint}; Verified={result.CertificateAuthenticationVerified}; MachineConfigUpdated={result.MachineCloudConfigUpdated}; ServiceKeyAccess={result.ServiceKeyAccessStatus}");

            _cloudStatus.Text =
                $"Certificate rollover completed. New certificate: {result.NewThumbprint}; expires {result.NewCertificateNotAfter:yyyy-MM-dd}.";

            MessageBox.Show(
                this,
                "Entra certificate rollover completed successfully." +
                Environment.NewLine +
                Environment.NewLine +
                $"Previous: {result.PreviousThumbprint}" +
                Environment.NewLine +
                $"New: {result.NewThumbprint}" +
                Environment.NewLine +
                $"Expires: {result.NewCertificateNotAfter:yyyy-MM-dd}" +
                Environment.NewLine +
                $"Certificate auth verified: {result.CertificateAuthenticationVerified}" +
                Environment.NewLine +
                $"Machine service config updated: {result.MachineCloudConfigUpdated}" +
                Environment.NewLine +
                Environment.NewLine +
                "The previous credential/certificate was retained for rollback/grace.",
                "Entra Certificate Rollover",
                MessageBoxButtons.OK,
                result.Warnings.Count == 0
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "EntraCertificateRollover",
                "Failed",
                source: "Entra",
                authMode: "DeviceCode",
                details: ex.Message);

            _cloudStatus.Text =
                "Certificate rollover failed: " +
                ex.Message;

            MessageBox.Show(
                this,
                ex.Message,
                "Entra Certificate Rollover",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Enabled = true;
        }
    }

    private void ConfigureCustomBootstrap()
    {
        var current = string.IsNullOrWhiteSpace(_cloudConfig.BootstrapClientId)
            ? EntraSetupService.DefaultBootstrapClientId
            : _cloudConfig.BootstrapClientId;

        using var input = new InputDialog(
            "Advanced Bootstrap Client",
            "Optional advanced override. Leave the Microsoft default Client ID below, or enter a tenant-approved public-client Application ID. Clear the field to restore the Microsoft default bootstrap:",
            current);

        if (input.ShowDialog(this) != DialogResult.OK) return;

        var value = input.Value.Trim();
        _cloudConfig.BootstrapClientId =
            string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, EntraSetupService.DefaultBootstrapClientId, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
        ConfigService.SaveCloudConfig(_cloudConfig);

        _cloudStatus.Text = string.IsNullOrWhiteSpace(_cloudConfig.BootstrapClientId)
            ? $"Bootstrap reset to Microsoft first-party '{EntraSetupService.DefaultBootstrapDisplayName}'."
            : $"Custom bootstrap saved: {_cloudConfig.BootstrapClientId}";
    }

    private async Task ShowDeviceCodeAsync(DeviceCodeInfo info)
    {
        try { Clipboard.SetText(info.UserCode); } catch { }
        try { Process.Start(new ProcessStartInfo(info.VerificationUri) { UseShellExecute = true }); } catch { }
        MessageBox.Show(this,
            info.Message + Environment.NewLine + Environment.NewLine +
            "The code has been copied to the clipboard. Complete sign-in in the browser, then return to BitKeyBridge.",
            "Microsoft Entra Device Code",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        await Task.CompletedTask;
    }

    private async Task RunCoverageAsync()
    {
        if (!await EnsureCloudTokenAsync())
            return;

        try
        {
            _config.CoverageStaleIntuneDays =
                (int)_coverageStaleDays.Value;
            _config.CoverageOldCloudKeyDays =
                (int)_coverageOldKeyDays.Value;
            ConfigService.SaveAppConfig(_config);

            _coverageStatus.Text =
                "Starting metadata-only coverage analysis...";
            UseWaitCursor = true;

            var progress = new Progress<string>(
                message => _coverageStatus.Text = message);

            var service = new CoverageService(_config);
            _coverageCurrent = await service.RunAsync(
                _cloudToken!.AccessToken,
                GetSelectedScopes(),
                progress);

            RenderCoverageSummary();
            RenderCoverageRows();

            var s = _coverageCurrent.Summary;
            _audit.Write(
                "RunCoverageReport",
                source: "AD+Entra+Intune",
                authMode: _cloudToken.AuthMode,
                details:
                    $"Devices={s.TotalDevices}; Both={s.BothSources}; ADOnly={s.AdOnly}; EntraOnly={s.EntraOnly}; NoKey={s.NoRecoveryKey}; Multiple={s.MultipleKeys}; IntuneNotEncrypted={s.IntuneNotEncrypted}; Stale={s.IntuneStale}; OldCloudKey={s.OldCloudKey}");

            _coverageStatus.Text =
                $"Coverage completed at {_coverageCurrent.GeneratedAt:yyyy-MM-dd HH:mm:ss}. " +
                $"DC={_coverageCurrent.DomainController}. Recovery passwords were not requested.";
        }
        catch (Exception ex)
        {
            _coverageStatus.Text =
                "Coverage failed: " + ex.Message;
            _audit.Write(
                "RunCoverageReport",
                "Failed",
                source: "AD+Entra+Intune",
                authMode: _cloudToken?.AuthMode,
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "BitLocker Coverage",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderCoverageSummary()
    {
        if (_coverageCurrent is null)
        {
            _coverageSummary.Text =
                "Coverage has not been generated yet.";
            return;
        }

        var s = _coverageCurrent.Summary;
        var policy = new CoveragePolicyService(_config)
            .Evaluate(s);
        _coverageSummary.Text =
            $"Devices: {s.TotalDevices}    AD+Entra: {s.BothSources}    " +
            $"AD only: {s.AdOnly}    Entra only: {s.EntraOnly}    " +
            $"No key: {s.NoRecoveryKey}    Multiple keys: {s.MultipleKeys}" +
            Environment.NewLine +
            $"Intune managed: {s.IntuneManaged}    Encrypted: {s.IntuneEncrypted}    " +
            $"Not encrypted: {s.IntuneNotEncrypted}    Stale: {s.IntuneStale}    " +
            $"Old cloud key: {s.OldCloudKey}" +
            Environment.NewLine +
            $"Policy: {(policy.Enabled ? (policy.Compliant ? "Compliant" : "Violated") : "Disabled")}    " +
            $"Errors: {policy.ErrorCount}    Warnings: {policy.WarningCount}";
    }

    private void ConfigureCoveragePolicyFromGui()
    {
        using var dialog = new DpiAwareForm
        {
            Text = "Coverage Policy",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(680, 430),
            MinimumSize = new Size(560, 390),
            MaximizeBox = false,
            MinimizeBox = false,
            Font = Font
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(root);

        var enabled = new CheckBox
        {
            Text = "Enable Coverage policy evaluation",
            AutoSize = true,
            Checked = _config.CoveragePolicyEnabled,
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(enabled, 0, 0);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var row = 1; row < 5; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        grid.Controls.Add(CoveragePolicyHeader("Metric"), 0, 0);
        grid.Controls.Add(CoveragePolicyHeader("Allowed maximum"), 1, 0);
        grid.Controls.Add(CoveragePolicyHeader("Severity"), 2, 0);

        var noKeyMax = CreatePolicyMaximum(
            grid,
            "No recovery metadata",
            1,
            _config.CoveragePolicyMaxNoRecoveryKey);
        var noKeySeverity = CreatePolicySeverity(
            grid,
            1,
            _config.CoveragePolicyNoRecoveryKeySeverity);

        var unencryptedMax = CreatePolicyMaximum(
            grid,
            "Intune not encrypted",
            2,
            _config.CoveragePolicyMaxIntuneNotEncrypted);
        var unencryptedSeverity = CreatePolicySeverity(
            grid,
            2,
            _config.CoveragePolicyIntuneNotEncryptedSeverity);

        var staleMax = CreatePolicyMaximum(
            grid,
            "Intune stale",
            3,
            _config.CoveragePolicyMaxIntuneStale);
        var staleSeverity = CreatePolicySeverity(
            grid,
            3,
            _config.CoveragePolicyIntuneStaleSeverity);

        var oldKeyMax = CreatePolicyMaximum(
            grid,
            "Old cloud key metadata",
            4,
            _config.CoveragePolicyMaxOldCloudKey);
        var oldKeySeverity = CreatePolicySeverity(
            grid,
            4,
            _config.CoveragePolicyOldCloudKeySeverity);

        root.Controls.Add(grid, 0, 1);

        root.Controls.Add(new Label
        {
            Text =
                "Policy evaluates metadata counts only. It never retrieves a BitLocker recovery password.",
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Margin = new Padding(0, 12, 0, 8)
        }, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(90, 32)
        };
        var save = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(90, 32)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        root.Controls.Add(buttons, 0, 3);

        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _config.CoveragePolicyEnabled = enabled.Checked;
        _config.CoveragePolicyMaxNoRecoveryKey = (int)noKeyMax.Value;
        _config.CoveragePolicyMaxIntuneNotEncrypted =
            (int)unencryptedMax.Value;
        _config.CoveragePolicyMaxIntuneStale = (int)staleMax.Value;
        _config.CoveragePolicyMaxOldCloudKey = (int)oldKeyMax.Value;
        _config.CoveragePolicyNoRecoveryKeySeverity =
            noKeySeverity.Text;
        _config.CoveragePolicyIntuneNotEncryptedSeverity =
            unencryptedSeverity.Text;
        _config.CoveragePolicyIntuneStaleSeverity =
            staleSeverity.Text;
        _config.CoveragePolicyOldCloudKeySeverity =
            oldKeySeverity.Text;

        ConfigService.SaveAppConfig(_config);

        var stored = CoverageReportService.ReadStatus();
        if (stored is not null)
        {
            stored.Policy =
                new CoveragePolicyService(_config)
                    .Evaluate(stored.Summary);
            try
            {
                JsonStore.WriteAtomic(
                    AppPaths.CoverageStatusFile,
                    stored);
            }
            catch
            {
            }
        }

        _audit.Write(
            "SaveCoveragePolicy",
            source: "Coverage",
            details:
                $"Enabled={_config.CoveragePolicyEnabled}; " +
                $"NoKeyMax={_config.CoveragePolicyMaxNoRecoveryKey}/{_config.CoveragePolicyNoRecoveryKeySeverity}; " +
                $"UnencryptedMax={_config.CoveragePolicyMaxIntuneNotEncrypted}/{_config.CoveragePolicyIntuneNotEncryptedSeverity}; " +
                $"StaleMax={_config.CoveragePolicyMaxIntuneStale}/{_config.CoveragePolicyIntuneStaleSeverity}; " +
                $"OldKeyMax={_config.CoveragePolicyMaxOldCloudKey}/{_config.CoveragePolicyOldCloudKeySeverity}");

        RenderCoverageSummary();
        RefreshDashboard();
    }

    private Label CoveragePolicyHeader(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 12, 8)
        };

    private static NumericUpDown CreatePolicyMaximum(
        TableLayoutPanel parent,
        string label,
        int row,
        int value)
    {
        parent.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 6)
        }, 0, row);

        var control = new NumericUpDown
        {
            Width = 120,
            Minimum = 0,
            Maximum = 1000000,
            Value = Math.Clamp(value, 0, 1000000),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 12, 6)
        };
        parent.Controls.Add(control, 1, row);
        return control;
    }

    private static ComboBox CreatePolicySeverity(
        TableLayoutPanel parent,
        int row,
        string configured)
    {
        var control = new ComboBox
        {
            Width = 130,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 0, 6)
        };
        control.Items.AddRange(["Error", "Warning", "Info"]);
        control.Text =
            CoveragePolicyService.NormalizeSeverity(configured);
        parent.Controls.Add(control, 2, row);
        return control;
    }

    private IReadOnlyList<CoverageDeviceRow> GetVisibleCoverageRows()
    {
        if (_coverageCurrent is null)
            return [];

        IEnumerable<CoverageDeviceRow> rows =
            _coverageCurrent.Rows;

        rows = _coverageFilter.SelectedIndex switch
        {
            1 => rows.Where(x =>
                x.CoverageStatus == "No recovery key"),
            2 => rows.Where(x =>
                x.CoverageStatus == "AD only"),
            3 => rows.Where(x =>
                x.CoverageStatus == "Entra only"),
            4 => rows.Where(x =>
                x.CoverageStatus == "AD + Entra"),
            5 => rows.Where(x =>
                x.MultipleRecoveryKeys),
            6 => rows.Where(x =>
                x.FoundInIntune && x.IsEncrypted == false),
            7 => rows.Where(x =>
                x.IntuneStale),
            8 => rows.Where(x =>
                x.CloudKeyOld),
            _ => rows
        };

        return rows.ToList();
    }

    private void RenderCoverageRows()
    {
        _coverageResults.BeginUpdate();
        try
        {
            _coverageResults.Items.Clear();

            foreach (var row in GetVisibleCoverageRows())
            {
                var item = new ListViewItem(row.ComputerName);
                item.SubItems.Add(row.CoverageStatus);
                item.SubItems.Add(row.AdRecoveryKeyCount.ToString());
                item.SubItems.Add(row.EntraRecoveryKeyCount.ToString());
                item.SubItems.Add(row.FoundInIntune ? "Yes" : "No");
                item.SubItems.Add(
                    row.IsEncrypted is null
                        ? "-"
                        : row.IsEncrypted.Value ? "Yes" : "No");
                item.SubItems.Add(row.ComplianceState);
                item.SubItems.Add(
                    row.IntuneLastSync?.ToString(
                        "yyyy-MM-dd HH:mm") ?? string.Empty);
                item.SubItems.Add(
                    row.AdLastLogon?.ToString(
                        "yyyy-MM-dd HH:mm") ?? string.Empty);
                item.SubItems.Add(
                    row.NewestEntraRecoveryKey?.ToString(
                        "yyyy-MM-dd HH:mm") ?? string.Empty);
                item.SubItems.Add(row.SerialNumber);
                item.SubItems.Add(row.UserPrincipalName);
                item.SubItems.Add(
                    (row.Manufacturer + " " + row.Model).Trim());
                item.Tag = row;
                _coverageResults.Items.Add(item);
            }
        }
        finally
        {
            _coverageResults.EndUpdate();
        }

        if (_coverageCurrent is not null)
        {
            _coverageStatus.Text =
                $"Showing {_coverageResults.Items.Count} of " +
                $"{_coverageCurrent.Rows.Count} device(s). " +
                "Recovery passwords were not requested.";
        }
    }

    private void ExportCoverageCsv()
    {
        if (_coverageCurrent is null)
        {
            MessageBox.Show(
                this,
                "Run Coverage first.",
                "BitLocker Coverage",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export BitLocker Coverage",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName =
                $"BitKeyBridge-Coverage-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            AddExtension = true,
            DefaultExt = "csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var rows = GetVisibleCoverageRows();
            CoverageService.ExportCsv(dialog.FileName, rows);
            _audit.Write(
                "ExportCoverageCsv",
                source: "Coverage",
                details:
                    $"Path={dialog.FileName}; Rows={rows.Count}; Filter={_coverageFilter.Text}");
            _coverageStatus.Text =
                $"Exported {rows.Count} visible coverage row(s) to {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Coverage CSV Export",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task SearchUnifiedDevicesAsync()
    {
        try
        {
            _unifiedStatus.Text =
                "Searching devices...";
            _unifiedResults.Items.Clear();
            _unifiedDetails.Clear();
            _deviceRecoveryIds.Items.Clear();
            _deviceCurrentKey = null;
            _deviceRecoveryKey.Clear();

            var service =
                new UnifiedDeviceService(
                    _config);

            var cloudConfigured =
                !string.IsNullOrWhiteSpace(
                    _cloudConfig.TenantId) &&
                !string.IsNullOrWhiteSpace(
                    _cloudConfig.ClientId);

            var cloudReady =
                cloudConfigured &&
                await EnsureCloudTokenAsync();

            List<UnifiedDeviceInfo> rows;

            if (cloudReady)
            {
                _unifiedStatus.Text =
                    "Searching Active Directory, Entra and Intune...";

                rows =
                    await service.SearchAsync(
                        _cloudToken!.AccessToken,
                        _unifiedQuery.Text);
            }
            else
            {
                _unifiedStatus.Text =
                    cloudConfigured
                        ? "Cloud is unavailable; continuing with Active Directory only..."
                        : "Cloud is not configured; searching Active Directory only...";

                rows =
                    await service.SearchAdOnlyAsync(
                        _unifiedQuery.Text);
            }

            foreach (var row in rows)
            {
                var item =
                    new ListViewItem(
                        row.ComputerName);

                item.SubItems.Add(
                    row.FoundInAd
                        ? "Yes"
                        : "No");
                item.SubItems.Add(
                    row.FoundInEntra
                        ? "Yes"
                        : "No");
                item.SubItems.Add(
                    row.FoundInIntune
                        ? "Yes"
                        : "No");
                item.SubItems.Add(
                    row.SerialNumber);
                item.SubItems.Add(
                    string.IsNullOrWhiteSpace(
                        row.UserPrincipalName)
                        ? row.UserDisplayName
                        : row.UserPrincipalName);
                item.SubItems.Add(
                    (row.Manufacturer +
                     " " +
                     row.Model).Trim());
                item.SubItems.Add(
                    (row.OperatingSystem +
                     " " +
                     row.OsVersion).Trim());
                item.SubItems.Add(
                    row.ComplianceState);
                item.SubItems.Add(
                    row.IsEncrypted is null
                        ? "-"
                        : row.IsEncrypted.Value
                            ? "Yes"
                            : "No");
                item.SubItems.Add(
                    row.LastSyncDateTime?
                        .ToString(
                            "yyyy-MM-dd HH:mm") ??
                    string.Empty);
                item.SubItems.Add(
                    row.RecoveryKeyCount.ToString());
                item.Tag =
                    row;
                _unifiedResults.Items.Add(
                    item);
            }

            var source =
                cloudReady
                    ? "AD+Entra+Intune"
                    : "AD";

            _unifiedStatus.Text =
                cloudReady
                    ? $"Unified search returned {rows.Count} device(s). Recovery passwords were not requested."
                    : $"AD search returned {rows.Count} device(s). Configure Cloud under Administration to add Entra/Intune data.";

            _audit.Write(
                "UnifiedDeviceSearch",
                computerName:
                    _unifiedQuery.Text.Trim(),
                source:
                    source,
                authMode:
                    cloudReady
                        ? _cloudToken?.AuthMode
                        : null,
                details:
                    $"Results={rows.Count}; CloudIncluded={cloudReady}");
        }
        catch (Exception ex)
        {
            _unifiedStatus.Text =
                "Device search failed: " +
                ex.Message;

            _audit.Write(
                "UnifiedDeviceSearch",
                "Failed",
                source: "Devices",
                authMode:
                    _cloudToken?.AuthMode,
                details:
                    ex.Message);
        }
    }

    private void ShowUnifiedDeviceDetails()
    {
        _unifiedDetails.Clear();
        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not UnifiedDeviceInfo row) return;

        _unifiedDetails.AppendText($"Computer:          {row.ComputerName}{Environment.NewLine}");
        _unifiedDetails.AppendText($"AD:                {row.FoundInAd}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Entra:             {row.FoundInEntra}  Device ID: {row.EntraDeviceId}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Intune:            {row.FoundInIntune}  Managed Device ID: {row.ManagedDeviceId}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Serial:            {row.SerialNumber}{Environment.NewLine}");
        _unifiedDetails.AppendText($"User:              {row.UserDisplayName}  {row.UserPrincipalName}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Hardware:          {(row.Manufacturer + " " + row.Model).Trim()}{Environment.NewLine}");
        _unifiedDetails.AppendText($"OS:                {(row.OperatingSystem + " " + row.OsVersion).Trim()}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Compliance:        {row.ComplianceState}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Encrypted:         {(row.IsEncrypted is null ? "-" : row.IsEncrypted.Value ? "Yes" : "No")}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Intune last sync:  {row.LastSyncDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
        _unifiedDetails.AppendText($"AD last logon:     {row.AdLastLogonTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
        _unifiedDetails.AppendText($"AD DN:             {row.AdDistinguishedName}{Environment.NewLine}");
        _unifiedDetails.AppendText($"Recovery IDs:      {row.RecoveryKeyCount}{Environment.NewLine}");
        foreach (var recoveryId in row.RecoveryIds)
            _unifiedDetails.AppendText($"  {recoveryId}{Environment.NewLine}");
    }

    private async Task RotateSelectedUnifiedDeviceAsync()
    {
        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not UnifiedDeviceInfo row)
        {
            MessageBox.Show(
                this,
                "Select an Intune-managed device first.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(row.ManagedDeviceId))
        {
            MessageBox.Show(
                this,
                "The selected device is not linked to an Intune managedDevice object.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        await RotateManagedDeviceAsync(
            row.ManagedDeviceId,
            row.ComputerName,
            row.RecoveryIds.FirstOrDefault());
    }

    private async Task RotateManagedDeviceAsync(
        string managedDeviceId,
        string computerName,
        string? recoveryId,
        RecoveryAccessContext? existingContext = null)
    {
        if (!AuthorizeAction(
                BitKeyBridgePermission.Rotate,
                "RotateBitLockerKey",
                computerName,
                recoveryId,
                "Intune"))
        {
            return;
        }

        var auditId = string.IsNullOrWhiteSpace(recoveryId)
            ? managedDeviceId
            : recoveryId;

        var context = existingContext ?? GetOrRequestRecoveryAccessContext(
            "Entra",
            auditId,
            computerName,
            allowRotationReminder: false);
        if (context is null) return;

        if (!await EnsureCloudTokenAsync()) return;

        var answer = MessageBox.Show(
            this,
            $"Request BitLocker recovery-key rotation for {computerName}?{Environment.NewLine}{Environment.NewLine}" +
            "Only rotate after the recovery operation is complete and the device can process the Intune action." +
            (string.IsNullOrWhiteSpace(context.Reference)
                ? string.Empty
                : Environment.NewLine + Environment.NewLine + "Reference: " + context.Reference),
            "Confirm BitLocker Key Rotation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            using var graph = new CloudGraphService();
            await graph.RotateBitLockerKeysAsync(
                _cloudToken!.AccessToken,
                managedDeviceId);

            WriteRecoveryAudit(
                "RotateBitLockerKey",
                context,
                computerName: computerName,
                recoveryId: recoveryId,
                source: "Intune",
                authMode: _cloudToken.AuthMode,
                details: $"ManagedDeviceId={managedDeviceId}",
                rotationRequested: true,
                rotationSucceeded: true);

            _cloudStatus.Text =
                $"Intune accepted the BitLocker key-rotation request for {computerName}.";
            _unifiedStatus.Text = _cloudStatus.Text;
            MessageBox.Show(
                this,
                "Intune accepted the rotation request. The new recovery key appears after the device processes the action and backs up the new key.",
                "Rotate BitLocker Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            WriteRecoveryAudit(
                "RotateBitLockerKey",
                context,
                result: "Failed",
                computerName: computerName,
                recoveryId: recoveryId,
                source: "Intune",
                authMode: _cloudToken?.AuthMode,
                details: ex.Message,
                rotationRequested: true,
                rotationSucceeded: false);
            throw;
        }
    }

    private void ConfigureRbacFromGui()
    {
        using var dialog = new RbacSettingsDialog(_config);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _config.RbacEnabled = dialog.RbacEnabled;
        _config.RbacAllowLocalAdministrators =
            dialog.AllowLocalAdministrators;
        _config.RbacRecoveryReaders = dialog.RecoveryReaders;
        _config.RbacRotationOperators = dialog.RotationOperators;
        _config.RbacAdministrators = dialog.Administrators;

        ConfigService.SaveAppConfig(_config);

        var auth = new AuthorizationService(_config);
        var read = auth.Check(BitKeyBridgePermission.RecoveryRead);
        var rotate = auth.Check(BitKeyBridgePermission.Rotate);
        var admin = auth.Check(BitKeyBridgePermission.Administrator);

        _audit.Write(
            "SaveRbacSettings",
            source: "Local",
            details:
                $"Enabled={_config.RbacEnabled}; AdminBypass={_config.RbacAllowLocalAdministrators}; " +
                $"Readers={string.Join("|", _config.RbacRecoveryReaders)}; " +
                $"Rotators={string.Join("|", _config.RbacRotationOperators)}; " +
                $"Administrators={string.Join("|", _config.RbacAdministrators)}; " +
                $"CurrentIdentity={AuthorizationService.CurrentIdentityName()}; " +
                $"CurrentRecoveryRead={read.Allowed}; CurrentRotate={rotate.Allowed}; CurrentAdminUI={admin.Allowed}");

        MessageBox.Show(
            this,
            $"RBAC settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Current identity: {AuthorizationService.CurrentIdentityName()}{Environment.NewLine}" +
            $"RecoveryRead: {(read.Allowed ? "Allowed" : "Denied")}{Environment.NewLine}" +
            $"Rotate: {(rotate.Allowed ? "Allowed" : "Denied")}{Environment.NewLine}" +
            $"Administration UI: {(admin.Allowed ? "Allowed" : "Denied")}{Environment.NewLine}{Environment.NewLine}" +
            "Navigation roles are evaluated when the GUI starts.",
            "BitKeyBridge RBAC",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ConfigurePrivilegedAccessFromGui()
    {
        using var dialog =
            new PrivilegedAccessSettingsDialog(
                _config);

        if (dialog.ShowDialog(this) !=
            DialogResult.OK)
        {
            return;
        }

        RefreshDashboard();
    }

    private void LoadOperationsSettings()
    {
        _updateRepository.Text = _config.UpdateRepository;
        _checkUpdatesOnStart.Checked = _config.CheckForUpdatesOnStart;
        _allowPrereleaseUpdates.Checked = _config.AllowPrereleaseUpdates;
        _remoteApiPort.Value = Math.Clamp(_config.RemoteApiPort, 1024, 65535);
        _remoteApiManagement.Checked = _config.RemoteApiAllowManagement;
        _requireRecoveryReference.Checked = _config.RequireRecoveryAccessReference;
        _suggestRotationAfterRecovery.Checked =
            _config.SuggestRotationAfterCloudKeyRetrieval;
        RefreshRemoteApiStatus();

        try
        {
            _lastUpdateInfo = JsonStore.Read<UpdateInfo>(AppPaths.UpdateStatusFile);
            if (_lastUpdateInfo is not null)
                DisplayUpdateInfo(_lastUpdateInfo);
        }
        catch { }
    }

    private void SaveOperationsSettings()
    {
        var repository = _updateRepository.Text.Trim();
        if (string.IsNullOrWhiteSpace(repository) || repository.Split('/').Length != 2)
        {
            MessageBox.Show(
                this,
                "Update repository must use owner/repository format.",
                "Update Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _config.UpdateRepository = repository;
        _config.CheckForUpdatesOnStart = _checkUpdatesOnStart.Checked;
        _config.AllowPrereleaseUpdates = _allowPrereleaseUpdates.Checked;
        _config.RemoteApiPort = (int)_remoteApiPort.Value;
        _config.RemoteApiAllowManagement = _remoteApiManagement.Checked;
        _config.RequireRecoveryAccessReference = _requireRecoveryReference.Checked;
        _config.SuggestRotationAfterCloudKeyRetrieval =
            _suggestRotationAfterRecovery.Checked;
        ConfigService.SaveAppConfig(_config);
        _audit.Write(
            "SaveOperationsSettings",
            source: "Local",
            details:
                $"UpdateRepository={_config.UpdateRepository}; CheckOnStart={_config.CheckForUpdatesOnStart}; AllowPrerelease={_config.AllowPrereleaseUpdates}; RemotePort={_config.RemoteApiPort}; RemoteManagement={_config.RemoteApiAllowManagement}; RequireReference={_config.RequireRecoveryAccessReference}; SuggestRotation={_config.SuggestRotationAfterCloudKeyRetrieval}");
        RefreshRemoteApiStatus();
    }

    private async Task CheckForUpdatesGuiAsync(bool silentWhenCurrent)
    {
        SaveOperationsSettings();
        try
        {
            _updateStatus.Text = "Checking GitHub Releases...";
            using var updater = new UpdateService(_config);
            _lastUpdateInfo = await updater.CheckAsync();
            DisplayUpdateInfo(_lastUpdateInfo);

            if (!string.IsNullOrWhiteSpace(_lastUpdateInfo.Error))
            {
                if (!silentWhenCurrent)
                    MessageBox.Show(
                        this,
                        _lastUpdateInfo.Error,
                        "BitKeyBridge Update",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                return;
            }

            if (!_lastUpdateInfo.UpdateAvailable && !silentWhenCurrent)
            {
                MessageBox.Show(
                    this,
                    $"BitKeyBridge {_lastUpdateInfo.CurrentVersion} is current.",
                    "BitKeyBridge Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            if (_lastUpdateInfo.UpdateAvailable)
            {
                WindowsEventLogService.TryWrite(
                    $"BitKeyBridge update {_lastUpdateInfo.LatestVersion} is available.",
                    EventLogSeverity.Information,
                    4101,
                    "Update");
            }
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            _updateStatus.Text = "Update check failed: " + ex.Message;
        }
    }

    private void DisplayUpdateInfo(UpdateInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.Error))
        {
            _updateStatus.Text =
                $"Current: {info.CurrentVersion}{Environment.NewLine}" +
                $"Update check failed: {info.Error}";
            return;
        }

        _updateStatus.Text =
            $"Current: {info.CurrentVersion}    Latest: {info.LatestVersion}    Architecture: {info.Architecture}{Environment.NewLine}" +
            (info.UpdateAvailable
                ? $"UPDATE AVAILABLE: {info.LatestVersion}    Asset: {info.AssetName}"
                : "No newer release is available.") +
            Environment.NewLine +
            $"Checked: {info.CheckedAtUtc:u}";
    }

    private async Task InstallLatestUpdateGuiAsync()
    {
        if (_lastUpdateInfo is null ||
            !string.IsNullOrWhiteSpace(_lastUpdateInfo.Error) ||
            !_lastUpdateInfo.UpdateAvailable)
        {
            await CheckForUpdatesGuiAsync(silentWhenCurrent: false);
        }

        var info = _lastUpdateInfo;
        if (info is null ||
            !string.IsNullOrWhiteSpace(info.Error) ||
            !info.UpdateAvailable)
            return;

        var answer = MessageBox.Show(
            this,
            $"Install BitKeyBridge {info.LatestVersion} for {info.Architecture}?{Environment.NewLine}{Environment.NewLine}" +
            "The ZIP SHA-256 will be verified against SHA256SUMS.txt and GitHub's asset digest when available. " +
            "The downloaded EXE must also pass --self-test before installation. " +
            "The GUI will close during replacement and reopen automatically.",
            "Install Verified Update",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            Enabled = false;
            _updateStatus.Text = $"Downloading and verifying BitKeyBridge {info.LatestVersion}...";
            using var updater = new UpdateService(_config);
            var prepared = await updater.PrepareAsync(info);
            _audit.Write(
                "PrepareVerifiedUpdate",
                source: "GitHub",
                details: $"Version={info.LatestVersion}; Asset={info.AssetName}; SHA256={info.ExpectedSha256}");
            updater.LaunchApplyHelper(prepared, restartGui: true);
            _audit.Write(
                "LaunchUpdateHelper",
                source: "Local",
                details: $"Version={info.LatestVersion}");
            Close();
        }
        catch (Exception ex)
        {
            Enabled = true;
            _updateStatus.Text = "Update preparation failed: " + ex.Message;
            _audit.Write(
                "PrepareVerifiedUpdate",
                "Failed",
                source: "GitHub",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Install Verified Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenLatestRelease()
    {
        var url = _lastUpdateInfo?.ReleaseUrl;
        if (string.IsNullOrWhiteSpace(url))
            url = $"https://github.com/{_config.UpdateRepository}/releases";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "GitHub Release", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnableOrReconfigureRemoteApi()
    {
        SaveOperationsSettings();

        var answer = MessageBox.Show(
            this,
            $"Enable the TLS Remote API on TCP {_config.RemoteApiPort}?{Environment.NewLine}{Environment.NewLine}" +
            $"Remote export management: {_config.RemoteApiAllowManagement}{Environment.NewLine}" +
            "A self-signed server certificate will be created in LocalMachine\\My if needed. " +
            "A new random bearer token will be generated and shown once.",
            "Enable Remote API",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            var setup = new RemoteApiSetupService();
            var result = setup.Enable(
                _config,
                (int)_remoteApiPort.Value,
                _remoteApiManagement.Checked);
            RestartServiceIfRunning();
            _audit.Write(
                "EnableRemoteApi",
                source: "RemoteAPI",
                details: $"Port={result.Port}; Management={_config.RemoteApiAllowManagement}; Certificate={result.CertificateThumbprint}");
            RefreshRemoteApiStatus();

            using var secret = new SecretDisplayDialog(
                "BitKeyBridge Remote API Token",
                "Save this bearer token in your monitoring/management system now. BitKeyBridge stores only its SHA-256 hash and cannot display the token again.",
                result.Token,
                $"TLS certificate thumbprint: {result.CertificateThumbprint}{Environment.NewLine}" +
                $"Certificate expires: {result.CertificateExpires:yyyy-MM-dd}{Environment.NewLine}" +
                $"API base URL: https://{Environment.MachineName}:{result.Port}/api/v1/");
            secret.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _audit.Write("EnableRemoteApi", "Failed", source: "RemoteAPI", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RotateRemoteApiToken()
    {
        if (!_config.RemoteApiEnabled)
        {
            MessageBox.Show(this, "Remote API is not enabled.", "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var setup = new RemoteApiSetupService();
            var result = setup.RegenerateToken(_config);
            RestartServiceIfRunning();
            _audit.Write(
                "RotateRemoteApiToken",
                source: "RemoteAPI",
                details: $"Port={result.Port}; Certificate={result.CertificateThumbprint}");
            RefreshRemoteApiStatus();

            using var secret = new SecretDisplayDialog(
                "New BitKeyBridge Remote API Token",
                "The previous bearer token is now invalid. Save this new token now.",
                result.Token,
                $"API base URL: https://{Environment.MachineName}:{result.Port}/api/v1/");
            secret.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _audit.Write("RotateRemoteApiToken", "Failed", source: "RemoteAPI", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisableRemoteApi()
    {
        if (!_config.RemoteApiEnabled) return;

        var answer = MessageBox.Show(
            this,
            "Disable the Remote API and invalidate the current bearer token?",
            "Disable Remote API",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            new RemoteApiSetupService().Disable(_config);
            RestartServiceIfRunning();
            _audit.Write("DisableRemoteApi", source: "RemoteAPI");
            RefreshRemoteApiStatus();
        }
        catch (Exception ex)
        {
            _audit.Write("DisableRemoteApi", "Failed", source: "RemoteAPI", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Remote API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshRemoteApiStatus()
    {
        _remoteApiStatus.Text = _config.RemoteApiEnabled
            ? $"ENABLED: https://{Environment.MachineName}:{_config.RemoteApiPort}/api/v1/{Environment.NewLine}" +
              $"Management: {_config.RemoteApiAllowManagement}    Certificate: {_config.RemoteApiCertificateThumbprint}{Environment.NewLine}" +
              $"Tokens: Admin={Configured(_config.RemoteApiTokenSha256)}  Read={Configured(_config.RemoteApiReadTokenSha256)}  CoverageRun={Configured(_config.RemoteApiCoverageRunTokenSha256)}  Export={Configured(_config.RemoteApiExportTokenSha256)}"
            : "DISABLED. Remote API does not listen on the network until explicitly enabled.";
    }

    private void ConfigureStorageMaintenanceGui()
    {
        using var dialog =
            new StorageMaintenanceDialog(
                _config);

        dialog.ShowDialog(this);
        RefreshDashboard();
    }

    private void BackupConfigurationGui()
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter =
                    "BitKeyBridge configuration (*.json)|*.json|All files (*.*)|*.*",
                FileName =
                    "BitKeyBridge-config-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss") +
                    ".json",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) !=
                DialogResult.OK)
            {
                return;
            }

            var path =
                new ConfigurationMaintenanceService()
                    .CreateBackup(
                        dialog.FileName);

            _audit.Write(
                "BackupConfiguration",
                source: "Configuration",
                details:
                    $"Path={path}");

            MessageBox.Show(
                this,
                "Configuration backup created." +
                Environment.NewLine +
                Environment.NewLine +
                path +
                Environment.NewLine +
                Environment.NewLine +
                "Credential blobs, access tokens, private keys, and BitLocker recovery passwords are not included.",
                "Configuration Backup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Configuration Backup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RestoreConfigurationGui()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Filter =
                    "BitKeyBridge configuration (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) !=
                DialogResult.OK)
            {
                return;
            }

            var answer = MessageBox.Show(
                this,
                "Restore this BitKeyBridge configuration?" +
                Environment.NewLine +
                Environment.NewLine +
                "A rollback backup of the current configuration will be created first.",
                "Configuration Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            var serviceBefore =
                WindowsServiceHost.GetInfo();

            var result =
                new ConfigurationMaintenanceService()
                    .Restore(
                        dialog.FileName);

            if (serviceBefore.Installed &&
                string.Equals(
                    serviceBefore.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            _audit.Write(
                "RestoreConfiguration",
                source: "Configuration",
                details:
                    $"Path={dialog.FileName}; Rollback={result.RollbackBackupPath}; Warnings={result.Warnings.Count}");

            MessageBox.Show(
                this,
                "Configuration restored." +
                Environment.NewLine +
                Environment.NewLine +
                "Rollback: " +
                result.RollbackBackupPath +
                (result.Warnings.Count == 0
                    ? string.Empty
                    : Environment.NewLine +
                      Environment.NewLine +
                      "Warnings:" +
                      Environment.NewLine +
                      string.Join(
                          Environment.NewLine,
                          result.Warnings.Select(
                              x => "- " + x))) +
                Environment.NewLine +
                Environment.NewLine +
                "Restart the BitKeyBridge GUI to reload all restored settings.",
                "Configuration Restore",
                MessageBoxButtons.OK,
                result.Warnings.Count == 0
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Configuration Restore",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CreateDiagnosticsBundleGui()
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter =
                    "ZIP archive (*.zip)|*.zip|All files (*.*)|*.*",
                FileName =
                    "BitKeyBridge-Diagnostics-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss") +
                    ".zip",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) !=
                DialogResult.OK)
            {
                return;
            }

            var result =
                new ConfigurationMaintenanceService()
                    .CreateDiagnosticsBundle(
                        dialog.FileName);

            _audit.Write(
                "CreateDiagnosticsBundle",
                source: "Diagnostics",
                details:
                    $"Path={result.ZipPath}; Files={result.IncludedFiles.Count}");

            MessageBox.Show(
                this,
                "Sanitized diagnostics bundle created." +
                Environment.NewLine +
                Environment.NewLine +
                result.ZipPath +
                Environment.NewLine +
                Environment.NewLine +
                "Recovery CSV/passwords, audit contents, credential blobs, bearer tokens/hashes, Graph tokens, and private keys are excluded.",
                "Diagnostics Bundle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Diagnostics Bundle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ConfigureRemoteApiScopedTokens()
    {
        if (!_config.RemoteApiEnabled)
        {
            MessageBox.Show(
                this,
                "Enable the Remote API before creating scoped tokens.",
                "Remote API",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog =
            new RemoteApiScopedTokenDialog(
                _config);
        dialog.ShowDialog(this);
        RefreshRemoteApiStatus();
    }

    private static string Configured(
        string hash) =>
        string.IsNullOrWhiteSpace(hash)
            ? "No"
            : "Yes";

    private void RestartServiceIfRunning()
    {
        var service = WindowsServiceHost.GetInfo();
        if (!service.Installed ||
            !string.Equals(service.State, "Running", StringComparison.OrdinalIgnoreCase))
            return;
        WindowsServiceHost.Stop();
        WindowsServiceHost.Start();
    }

    private void OpenWindowsEventLog()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "eventvwr.msc",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Windows Event Log", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadDirectorySettings()
    {
        _adMode.SelectedIndex = string.Equals(
            _config.AdConnectionMode,
            "Explicit",
            StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        _adServer.Text = _config.AdServer;
        _adDomain.Text = _config.AdDomain;
        _adUsername.Text = _config.AdUsername;
        _adPassword.Clear();
        _adPort.Value = Math.Clamp(_config.AdPort, 1, 65535);
        _adUseLdaps.Checked =
            _config.AdUseLdaps ||
            _config.AdPort == 636;
        _adExplicitCredentials.Checked =
            _config.AdUseExplicitCredentials;
        _adCredentialStorage.SelectedIndex =
            _config.AdCredentialStorageMode.Equals(
                "CurrentUser",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : _config.AdCredentialStorageMode.Equals(
                    "LocalMachine",
                    StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 0;

        _outputRoot.Text =
            _config.EffectiveOutputRoot;
        _outputSubdirectory.Text =
            _config.OutputSubdirectory;

        _autoConnectOnStart.Checked =
            _config.AutoConnectOnStart;

        _recoverySource.SelectedIndex =
            _config.RecoverySearchSource.Equals(
                "LocalCache",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        if (!string.IsNullOrWhiteSpace(
                _config.LastRecoveryScopeSearchBase))
        {
            _startScope =
                new BitLockerScope(
                    string.IsNullOrWhiteSpace(
                        _config.LastRecoveryScopeName)
                        ? "Last OU"
                        : _config.LastRecoveryScopeName,
                    _config.LastRecoveryScopeSearchBase);
        }

        UpdateDirectoryConnectionUi();
        RefreshCredentialVaultStatus();

        _adConnectionStatus.Text =
            _adMode.SelectedIndex == 1 &&
            !string.IsNullOrWhiteSpace(
                _adServer.Text)
                ? $"Ready to connect to {_adServer.Text}:{_adPort.Value}."
                : "Ready to auto-discover a writable domain controller.";

        _startPurposeStatus.Text =
            "Search loads metadata only. Recovery passwords are read on demand.";

        UpdateRecoverySourceUi();
    }

    private void UpdateDirectoryConnectionUi()
    {
        var explicitServer = _adMode.SelectedIndex == 1;
        _adServer.Enabled = explicitServer;
        _adDomain.Enabled = explicitServer || _adExplicitCredentials.Checked;
        _adPort.Enabled = explicitServer;
        _adUsername.Enabled = _adExplicitCredentials.Checked;
        _adPassword.Enabled = _adExplicitCredentials.Checked;
        _adCredentialStorage.Enabled = _adExplicitCredentials.Checked;
    }

    private void SaveDirectorySettings(
        bool showConfirmation,
        bool allowInMemoryFallback = false)
    {
        _config.AdConnectionMode =
            _adMode.SelectedIndex == 1
                ? "Explicit"
                : "Auto";
        _config.AdServer =
            _adServer.Text.Trim();
        _config.AdDomain =
            _adDomain.Text.Trim();
        _config.AdUsername =
            _adUsername.Text.Trim();
        _config.AdPort =
            (int)_adPort.Value;
        _config.AdUseLdaps =
            _adUseLdaps.Checked;
        _config.AdUseExplicitCredentials =
            _adExplicitCredentials.Checked;
        _config.AdCredentialStorageMode =
            GetSelectedCredentialStorageMode();

        _config.AutoConnectOnStart =
            _autoConnectOnStart.Checked;
        _config.RecoverySearchSource =
            _recoverySource.SelectedIndex == 1
                ? "LocalCache"
                : "LiveAD";

        if (_startScope is not null)
        {
            _config.LastRecoveryScopeName =
                _startScope.Name;
            _config.LastRecoveryScopeSearchBase =
                _startScope.SearchBase;
        }

        if (_config.AdUseExplicitCredentials)
        {
            if (_config.AdCredentialStorageMode.Equals(
                    "Session",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(
                        _adPassword.Text))
                {
                    AdSessionCredentials.SetPassword(
                        _adPassword.Text);
                }
            }
            else
            {
                AdSessionCredentials.Clear();
            }
        }
        else
        {
            AdSessionCredentials.Clear();
            _adPassword.Clear();
        }

        var outputRoot =
            _outputRoot.Text.Trim();

        _config.OutputRoot =
            string.IsNullOrWhiteSpace(outputRoot) ||
            string.Equals(
                outputRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                AppConfig.DefaultOutputRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : outputRoot;

        _config.OutputSubdirectory =
            _outputSubdirectory.Text.Trim();

        var persisted = true;
        try
        {
            ConfigService.SaveAppConfig(
                _config);
        }
        catch (Exception ex)
        {
            persisted = false;

            if (!allowInMemoryFallback)
                throw;

            _audit.Write(
                "SaveDirectorySettings",
                "SessionOnly",
                source: "Local",
                details:
                    "Machine configuration was not writable; current controls remain active in memory. " +
                    ex.Message);
        }

        _audit.Write(
            "SaveDirectorySettings",
            source: "Local",
            details:
                $"Mode={_config.AdConnectionMode}; Server={_config.AdServer}; " +
                $"Domain={_config.AdDomain}; User={_config.AdUsername}; " +
                $"ExplicitCredentials={_config.AdUseExplicitCredentials}; " +
                $"CredentialStorage={_config.AdCredentialStorageMode}; " +
                $"LDAPS={_config.AdUseLdaps}; Port={_config.AdPort}; " +
                $"AutoConnect={_config.AutoConnectOnStart}; " +
                $"RecoverySource={_config.RecoverySearchSource}; " +
                $"RecoveryScope={_config.LastRecoveryScopeSearchBase}");

        if (showConfirmation)
        {
            MessageBox.Show(
                this,
                persisted
                    ? "Active Directory and recovery-search settings saved."
                    : "Settings are active for this BitKeyBridge session, but the machine configuration could not be updated.",
                "BitKeyBridge",
                MessageBoxButtons.OK,
                persisted
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }
    }

    private string GetSelectedCredentialStorageMode() =>
        _adCredentialStorage.SelectedIndex switch
        {
            1 => "CurrentUser",
            2 => "LocalMachine",
            _ => "Session"
        };

    private void SaveSelectedCredential()
    {
        if (!_adExplicitCredentials.Checked)
        {
            MessageBox.Show(
                this,
                "Enable explicit AD credentials first.",
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var username = _adUsername.Text.Trim();
        var password = _adPassword.Text;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            MessageBox.Show(
                this,
                "Enter the AD user and password before saving the credential.",
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SaveDirectorySettings(
                showConfirmation: false,
                allowInMemoryFallback: true);
            var mode = GetSelectedCredentialStorageMode();
            var vault = new CredentialVaultService();

            if (mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                vault.SaveUserCredential(
                    _config.AdCredentialTarget,
                    username,
                    password);
                AdSessionCredentials.Clear();
                _audit.Write(
                    "SaveAdCredential",
                    source: "CredentialManager",
                    details: $"Storage=CurrentUser; User={username}; Target={_config.AdCredentialTarget}");
            }
            else if (mode.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase))
            {
                vault.SaveMachineCredential(
                    username,
                    _adDomain.Text.Trim(),
                    password);
                AdSessionCredentials.Clear();
                _audit.Write(
                    "SaveAdCredential",
                    source: "DPAPI",
                    details: $"Storage=LocalMachine; User={username}; File={AppPaths.MachineAdCredentialFile}");
            }
            else
            {
                AdSessionCredentials.SetPassword(password);
                _audit.Write(
                    "LoadAdSessionCredential",
                    source: "Memory",
                    details: $"Storage=Session; User={username}");
            }

            _config.AdCredentialStorageMode = mode;
            ConfigService.SaveAppConfig(_config);

            if (!mode.Equals("Session", StringComparison.OrdinalIgnoreCase))
                _adPassword.Clear();

            RefreshCredentialVaultStatus();
            MessageBox.Show(
                this,
                mode.Equals("Session", StringComparison.OrdinalIgnoreCase)
                    ? "Credential loaded for this BitKeyBridge process only."
                    : "Credential saved successfully in the selected protected Windows vault.",
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "SaveAdCredential",
                "Failed",
                source: "CredentialVault",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void DeleteSelectedCredential()
    {
        try
        {
            var mode = GetSelectedCredentialStorageMode();
            var vault = new CredentialVaultService();

            if (mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                vault.DeleteUserCredential(_config.AdCredentialTarget);
                _audit.Write(
                    "DeleteAdCredential",
                    source: "CredentialManager",
                    details: $"Storage=CurrentUser; Target={_config.AdCredentialTarget}");
            }
            else if (mode.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase))
            {
                vault.DeleteMachineCredential();
                _audit.Write(
                    "DeleteAdCredential",
                    source: "DPAPI",
                    details: $"Storage=LocalMachine; File={AppPaths.MachineAdCredentialFile}");
            }
            else
            {
                AdSessionCredentials.Clear();
                _audit.Write(
                    "DeleteAdCredential",
                    source: "Memory",
                    details: "Storage=Session");
            }

            _adPassword.Clear();
            RefreshCredentialVaultStatus();
        }
        catch (Exception ex)
        {
            _audit.Write(
                "DeleteAdCredential",
                "Failed",
                source: "CredentialVault",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Credential Vault",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshCredentialVaultStatus()
    {
        try
        {
            if (!_adExplicitCredentials.Checked)
            {
                _credentialVaultStatus.Text =
                    "Using current Windows identity; no explicit AD credential is required.";
                return;
            }

            var mode = GetSelectedCredentialStorageMode();
            if (mode.Equals("Session", StringComparison.OrdinalIgnoreCase))
            {
                _credentialVaultStatus.Text = AdSessionCredentials.HasPassword
                    ? $"Session credential loaded for {_adUsername.Text.Trim()}."
                    : "Session credential is not loaded.";
                return;
            }

            var vault = new CredentialVaultService();
            var metadata = mode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase)
                ? vault.GetUserMetadata(_config.AdCredentialTarget)
                : vault.GetMachineMetadata();

            var suffix =
                string.Empty;

            if (mode.Equals(
                    "CurrentUser",
                    StringComparison.OrdinalIgnoreCase))
            {
                suffix =
                    " Interactive user only; do not use this mode for the Windows Service.";
            }
            else if (metadata.Exists)
            {
                var service =
                    WindowsServiceHost.GetInfo();

                if (service.Installed &&
                    !string.IsNullOrWhiteSpace(
                        service.Identity))
                {
                    var access =
                        vault.GetMachineCredentialAccess(
                            service.Identity);

                    suffix =
                        $" ServiceAccess={access.Status}; Account={access.Account}.";
                }
            }

            _credentialVaultStatus.Text =
                metadata.Exists
                    ? $"Stored: {metadata.Storage}; User={metadata.Username}; Protected by {metadata.ProtectedBy}.{suffix}"
                    : $"No stored {metadata.Storage} credential. Protected by {metadata.ProtectedBy}.";
        }
        catch (Exception ex)
        {
            _credentialVaultStatus.Text = "Credential status error: " + ex.Message;
        }
    }

    private async Task TestDirectoryConnectionAsync(
        bool promptForOu = true)
    {
        try
        {
            SaveDirectorySettings(
                showConfirmation: false,
                allowInMemoryFallback: true);

            _adConnectionStatus.Text =
                "Connecting to Active Directory...";
            UseWaitCursor = true;

            var result =
                await Task.Run(
                    () =>
                    {
                        var service =
                            new ActiveDirectoryService(
                                _config);
                        var server =
                            service.GetPreferredWritableDc();
                        var root =
                            service.TestConnection(
                                server);
                        return (
                            Server: server,
                            Root: root);
                    });

            _adConnectionStatus.Text =
                $"Connected to {result.Server}. Ready to search BitLocker.";

            _startDomainDn =
                result.Root.GetValueOrDefault(
                    "defaultNamingContext",
                    string.Empty);

            _startSelectOu.Enabled = true;

            if (_startScope is not null &&
                !ActiveDirectoryService.IsSearchBaseWithinNamingContext(
                    _startScope.SearchBase,
                    _startDomainDn))
            {
                _startScope = null;
                _config.LastRecoveryScopeName = string.Empty;
                _config.LastRecoveryScopeSearchBase = string.Empty;
                TrySaveRecoveryUiState();

                TryUseDefaultRecoveryScope(
                    "The remembered OU belongs to a different Active Directory naming context; using the entire domain.");
            }

            if (_startScope is not null)
            {
                _startOuStatus.Text =
                    $"Selected: {_startScope.Name}    {_startScope.SearchBase}";
                _startSearch.Enabled = true;
                _startPurposeStatus.Text =
                    "Connected. Enter a computer name or Recovery ID.";
            }
            else if (promptForOu)
            {
                _startPurposeStatus.Text =
                    "Connected. Select the OU that contains the target computer.";
                await SelectStartOuAsync();
            }
            else
            {
                _startPurposeStatus.Text =
                    "Connected. Select an OU before searching.";
            }
        }
        catch (Exception ex)
        {
            _startDomainDn =
                string.Empty;
            _startSelectOu.Enabled =
                false;
            _startSearch.Enabled =
                false;
            _startResults.Items.Clear();
            _startCurrentKey =
                null;
            _startKey.Clear();
            _adConnectionStatus.Text =
                "Connection failed: " +
                ex.Message;
            _startPurposeStatus.Text =
                "Active Directory connection failed. Open Advanced settings if explicit DC/credentials are required.";

            if (promptForOu)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Active Directory Connection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SelectStartOuAsync()
    {
        try
        {
            UseWaitCursor = true;

            var service =
                new ActiveDirectoryService(
                    _config);
            var dc =
                service.GetPreferredWritableDc();

            if (string.IsNullOrWhiteSpace(
                    _startDomainDn))
            {
                var root =
                    service.GetRootDse(
                        dc);
                _startDomainDn =
                    root.GetValueOrDefault(
                        "defaultNamingContext",
                        string.Empty);
            }

            List<BitLockerScope> ous;
            try
            {
                ous =
                    await Task.Run(
                        () =>
                            service.ListOrganizationalUnits(
                                dc));
            }
            catch (Exception ex)
            {
                if (TryUseDefaultRecoveryScope(
                        "OU discovery failed; using the entire domain as the default search scope."))
                {
                    _adConnectionStatus.Text =
                        $"Connected to {dc}. OU enumeration warning: {ex.Message}";
                    return;
                }

                throw;
            }

            var scopes =
                new List<BitLockerScope>();

            if (!string.IsNullOrWhiteSpace(
                    _startDomainDn))
            {
                scopes.Add(
                    new BitLockerScope(
                        "Entire domain",
                        _startDomainDn));
            }

            scopes.AddRange(
                ous);

            using var browser =
                new OuBrowserForm();

            browser.LoadScopes(
                scopes);

            if (browser.ShowDialog(this) !=
                    DialogResult.OK ||
                browser.SelectedScope is not
                    { } selected)
            {
                if (_startScope is null)
                {
                    TryUseDefaultRecoveryScope(
                        "No OU selected; using the entire domain as the default search scope.");
                }

                return;
            }

            SetRecoveryScope(
                selected,
                "Ready — enter a computer name or Recovery ID.");
        }
        catch (Exception ex)
        {
            if (TryUseDefaultRecoveryScope(
                    "OU selection is unavailable; using the entire domain as the default search scope."))
            {
                _adConnectionStatus.Text =
                    "Connected to Active Directory. OU selection warning: " +
                    ex.Message;
                return;
            }

            _startOuStatus.Text =
                "OU discovery failed: " +
                ex.Message;

            MessageBox.Show(
                this,
                ex.Message,
                "Select Active Directory OU",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private bool TryUseDefaultRecoveryScope(
        string status)
    {
        if (string.IsNullOrWhiteSpace(
                _startDomainDn))
        {
            return false;
        }

        SetRecoveryScope(
            new BitLockerScope(
                "Entire domain",
                _startDomainDn),
            status);

        return true;
    }

    private void SetRecoveryScope(
        BitLockerScope selected,
        string status)
    {
        _startScope =
            selected;

        _startOuStatus.Text =
            $"Selected: {selected.Name}    {selected.SearchBase}";

        _startSearch.Enabled =
            true;
        _startPurposeStatus.Text =
            status;

        TrySaveRecoveryUiState();
        _startQuery.Focus();
    }

    private async Task<List<RecoverySearchResult>> SearchLiveAdRecoveryMetadataAsync(
        string query,
        BitLockerScope scope)
    {
        return await Task.Run(
            () =>
            {
                var service =
                    new ActiveDirectoryService(
                        _config);
                var dc =
                    service.GetPreferredWritableDc();

                var computers =
                    service.SearchComputersInScope(
                        dc,
                        scope,
                        query,
                        100);

                var found =
                    new List<RecoverySearchResult>();

                foreach (var computer in
                         computers)
                {
                    found.AddRange(
                        service.GetRecoveryMetadataForComputer(
                            dc,
                            computer.DistinguishedName));
                }

                if (computers.Count == 0)
                {
                    var idQuery =
                        query
                            .Trim()
                            .Trim(
                                '{',
                                '}');

                    if (idQuery.Length >= 4)
                    {
                        found.AddRange(
                            service.SearchRecoveryMetadataInScope(
                                dc,
                                scope,
                                idQuery,
                                200));
                    }
                }

                return found
                    .GroupBy(
                        x =>
                            x.ComputerName +
                            "|" +
                            x.RecoveryId,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(
                        x =>
                            x.First())
                    .OrderByDescending(
                        x =>
                            x.CreatedDateTime ??
                            x.LastChecked)
                    .Take(200)
                    .ToList();
            });
    }

    private async Task SearchStartRecoveryAsync()
    {
        var localCache =
            _recoverySource.SelectedIndex == 1;

        if (!localCache &&
            _startScope is null)
        {
            MessageBox.Show(
                this,
                "Connect to Active Directory and select an OU first.",
                "BitLocker Recovery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (localCache &&
            !File.Exists(
                _config.OutputCsv))
        {
            _startPurposeStatus.Text =
                "Local recovery cache is unavailable. Run an export first or switch to Live AD.";

            MessageBox.Show(
                this,
                "The local recovery export CSV does not exist." +
                Environment.NewLine +
                Environment.NewLine +
                _config.OutputCsv,
                "Local Recovery Cache",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var query =
            _startQuery.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                query))
        {
            MessageBox.Show(
                this,
                "Enter a computer name or Recovery ID.",
                "BitLocker Recovery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var searchSource =
            localCache
                ? "LocalCSV"
                : "AD";

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                "SearchRecoveryMetadata",
                source: searchSource))
        {
            return;
        }

        try
        {
            _startSearch.Enabled = false;
            _startResults.Items.Clear();
            _startResults.Visible = true;
            _startCurrentKey = null;
            _startKey.Clear();
            _startKey.UseSystemPasswordChar = true;
            _startShow.Text =
                "Reveal Recovery Key";
            ClearRecoveryCard();

            _startPurposeStatus.Text =
                localCache
                    ? "Searching local recovery metadata..."
                    : "Searching Active Directory recovery metadata...";

            UseWaitCursor = true;

            List<RecoverySearchResult> rows;

            if (localCache)
            {
                rows =
                    await Task.Run(
                        () =>
                            CsvUtility.ReadRecoveryMetadata(
                                _config.OutputCsv,
                                query,
                                200));
            }
            else
            {
                var scope =
                    _startScope!;

                try
                {
                    rows =
                        await SearchLiveAdRecoveryMetadataAsync(
                            query,
                            scope);
                }
                catch when (
                    !scope.SearchBase.Equals(
                        _startDomainDn,
                        StringComparison.OrdinalIgnoreCase) &&
                    TryUseDefaultRecoveryScope(
                        "The selected OU is no longer available; retrying the search across the entire domain."))
                {
                    rows =
                        await SearchLiveAdRecoveryMetadataAsync(
                            query,
                            _startScope!);
                }
            }

            foreach (var row in rows)
            {
                var item =
                    new ListViewItem(
                        row.ComputerName);

                item.SubItems.Add(
                    row.RecoveryId);
                item.SubItems.Add(
                    row.Source);
                item.SubItems.Add(
                    (row.CreatedDateTime ??
                     row.LastChecked)?
                        .ToString(
                            "yyyy-MM-dd HH:mm:ss") ??
                    "-");
                item.Tag =
                    row;

                _startResults.Items.Add(
                    item);
            }

            if (rows.Count == 1)
            {
                _startResults.Items[0].Selected =
                    true;
                _startResults.Items[0].Focused =
                    true;
                _startResults.Visible =
                    false;
                SelectStartRecoveryRecord();
            }

            var scopeLabel =
                localCache
                    ? "local cache"
                    : _startScope!.Name;

            _startPurposeStatus.Text =
                rows.Count switch
                {
                    0 =>
                        $"No BitLocker recovery metadata found in {scopeLabel}.",
                    1 =>
                        "One recovery record found. Review the card below and reveal/copy only when needed.",
                    _ =>
                        $"Found {rows.Count} recovery records in {scopeLabel}. Select a row."
                };
        }
        catch (Exception ex)
        {
            _startPurposeStatus.Text =
                "BitLocker search failed: " +
                ex.Message;

            MessageBox.Show(
                this,
                ex.Message,
                "BitLocker Recovery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _startSearch.Enabled =
                localCache
                    ? File.Exists(
                        _config.OutputCsv)
                    : _startScope is not null;
            UseWaitCursor = false;
        }
    }

    private void SelectStartRecoveryRecord()
    {
        if (_startResults.SelectedItems.Count == 0 ||
            _startResults.SelectedItems[0].Tag is not
                RecoverySearchResult row)
        {
            _startCurrentKey =
                null;
            _startKey.Clear();
            ClearRecoveryCard();
            return;
        }

        _startCurrentKey =
            null;
        _startKey.Clear();
        _startKey.UseSystemPasswordChar =
            true;
        _startShow.Text =
            "Reveal Recovery Key";

        _recoveryCardComputer.Text =
            row.ComputerName;
        _recoveryCardOu.Text =
            row.Source.Equals(
                "Local cache",
                StringComparison.OrdinalIgnoreCase)
                ? "Local cache"
                : _startScope?.SearchBase ??
                  row.ComputerDistinguishedName;
        _recoveryCardId.Text =
            row.RecoveryId;
        _recoveryCardTime.Text =
            (row.CreatedDateTime ??
             row.LastChecked)?
                .ToString(
                    "yyyy-MM-dd HH:mm:ss") ??
            "-";
        _recoveryCardSource.Text =
            row.Source;

        _startShow.Enabled =
            true;
        _startCopy.Enabled =
            true;
    }

    private void ClearRecoveryCard()
    {
        _recoveryCardComputer.Text = "-";
        _recoveryCardOu.Text = "-";
        _recoveryCardId.Text = "-";
        _recoveryCardTime.Text = "-";
        _recoveryCardSource.Text = "-";
        _startShow.Enabled = false;
        _startCopy.Enabled = false;
    }

    private async Task<string?> EnsureStartRecoveryKeyAsync(
        RecoverySearchResult row)
    {
        if (!string.IsNullOrWhiteSpace(
                _startCurrentKey))
        {
            return _startCurrentKey;
        }

        try
        {
            UseWaitCursor = true;

            if (row.Source.Equals(
                    "Local cache",
                    StringComparison.OrdinalIgnoreCase))
            {
                _startCurrentKey =
                    await Task.Run(
                        () =>
                            CsvUtility.GetRecoveryPassword(
                                _config.OutputCsv,
                                row.ComputerName,
                                row.RecoveryId));
            }
            else
            {
                _startCurrentKey =
                    await Task.Run(
                        () =>
                        {
                            var service =
                                new ActiveDirectoryService(
                                    _config);
                            var dc =
                                service.GetPreferredWritableDc();

                            return service
                                .GetRecoveryPasswordByDistinguishedName(
                                    dc,
                                    row.RecoveryDistinguishedName,
                                    row.RecoveryId);
                        });
            }

            return _startCurrentKey;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Recovery Key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return null;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task RevealStartRecoveryKeyAsync()
    {
        if (!_startKey.UseSystemPasswordChar &&
            !string.IsNullOrWhiteSpace(
                _startCurrentKey))
        {
            _startKey.UseSystemPasswordChar =
                true;
            _startShow.Text =
                "Reveal Recovery Key";
            return;
        }

        if (_startResults.SelectedItems.Count == 0 ||
            _startResults.SelectedItems[0].Tag is not
                RecoverySearchResult row)
        {
            return;
        }

        var auditSource =
            row.Source.Equals(
                "Local cache",
                StringComparison.OrdinalIgnoreCase)
                ? "LocalCSV"
                : "AD";

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                "RevealRecoveryKey",
                row.ComputerName,
                row.RecoveryId,
                auditSource))
        {
            return;
        }

        var context =
            GetOrRequestRecoveryAccessContext(
                auditSource,
                row.RecoveryId,
                row.ComputerName,
                allowRotationReminder: false);

        if (context is null)
            return;

        if (!AuthorizePrivilegedRecoveryAccess(
                context,
                "RevealRecoveryKey",
                row.ComputerName,
                row.RecoveryId,
                auditSource))
        {
            return;
        }

        var key =
            await EnsureStartRecoveryKeyAsync(
                row);

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return;
        }

        _startKey.Text =
            key;
        _startKey.UseSystemPasswordChar =
            false;
        _startShow.Text =
            "Hide Key";

        WriteRecoveryAudit(
            "RevealRecoveryKey",
            context,
            computerName: row.ComputerName,
            recoveryId: row.RecoveryId,
            source: auditSource);
    }

    private async Task CopyStartRecoveryKeyAsync()
    {
        if (_startResults.SelectedItems.Count == 0 ||
            _startResults.SelectedItems[0].Tag is not
                RecoverySearchResult row)
        {
            return;
        }

        var auditSource =
            row.Source.Equals(
                "Local cache",
                StringComparison.OrdinalIgnoreCase)
                ? "LocalCSV"
                : "AD";

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                "CopyRecoveryKey",
                row.ComputerName,
                row.RecoveryId,
                auditSource))
        {
            return;
        }

        var context =
            GetOrRequestRecoveryAccessContext(
                auditSource,
                row.RecoveryId,
                row.ComputerName,
                allowRotationReminder: false);

        if (context is null)
            return;

        if (!AuthorizePrivilegedRecoveryAccess(
                context,
                "CopyRecoveryKey",
                row.ComputerName,
                row.RecoveryId,
                auditSource))
        {
            return;
        }

        var key =
            await EnsureStartRecoveryKeyAsync(
                row);

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return;
        }

        WriteRecoveryAudit(
            "CopyRecoveryKey",
            context,
            computerName: row.ComputerName,
            recoveryId: row.RecoveryId,
            source: auditSource);

        CopyKeyWithAutoClear(
            key);
    }

    private void LoadDashboardSettings()
    {
        _serviceInterval.Value = Math.Clamp(_config.ServiceIntervalMinutes, 1, 10080);
        _healthPort.Value = Math.Clamp(_config.HealthEndpointPort, 1024, 65535);
        _healthEnabled.Checked = _config.HealthEndpointEnabled;
        _serviceRunOnStart.Checked = _config.ServiceRunExportOnStart;
        _serviceCoverageEnabled.Checked = _config.ServiceCoverageEnabled;
        _serviceCoverageInterval.Value =
            Math.Clamp(_config.ServiceCoverageIntervalMinutes, 15, 10080);
        _serviceCoverageRunOnStart.Checked = _config.ServiceRunCoverageOnStart;
        _serviceIdentityMode.SelectedIndex =
            _config.ServiceIdentityMode.Equals("gMSA", StringComparison.OrdinalIgnoreCase)
                ? 1
                : _config.ServiceIdentityMode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 0;
        _serviceIdentityAccount.Text = _config.ServiceIdentityAccount;
        _serviceIdentityPassword.Clear();
        UpdateServiceIdentityUi();
        RefreshServiceIdentityStatus();
        RefreshMachineCloudStatus();
    }

    private bool SaveDashboardSettings(
        bool restartRunningService)
    {
        try
        {
            var serviceBefore =
                WindowsServiceHost.GetInfo();

            _config.ServiceIntervalMinutes =
                (int)_serviceInterval.Value;
            _config.HealthEndpointPort =
                (int)_healthPort.Value;
            _config.HealthEndpointEnabled =
                _healthEnabled.Checked;
            _config.ServiceRunExportOnStart =
                _serviceRunOnStart.Checked;
            _config.ServiceCoverageEnabled =
                _serviceCoverageEnabled.Checked;
            _config.ServiceCoverageIntervalMinutes =
                (int)_serviceCoverageInterval.Value;
            _config.ServiceRunCoverageOnStart =
                _serviceCoverageRunOnStart.Checked;

            if (_config.ServiceCoverageEnabled &&
                !File.Exists(
                    AppPaths.MachineCloudConfigFile))
            {
                throw new InvalidOperationException(
                    "Scheduled Coverage requires machine cloud configuration. " +
                    "Click 'Save Cloud for Service' after configuring Tenant ID, Client ID, and certificate.");
            }

            ConfigService.SaveAppConfig(
                _config);

            _audit.Write(
                "SaveServiceSettings",
                source: "Local",
                details:
                    $"Interval={_config.ServiceIntervalMinutes}; " +
                    $"HealthEnabled={_config.HealthEndpointEnabled}; " +
                    $"Port={_config.HealthEndpointPort}; " +
                    $"RunOnStart={_config.ServiceRunExportOnStart}; " +
                    $"CoverageEnabled={_config.ServiceCoverageEnabled}; " +
                    $"CoverageInterval={_config.ServiceCoverageIntervalMinutes}; " +
                    $"CoverageRunOnStart={_config.ServiceRunCoverageOnStart}");

            if (restartRunningService &&
                serviceBefore.Installed &&
                string.Equals(
                    serviceBefore.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            RefreshDashboard();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Service Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }
    }

    private void InstallOrUpdateService()
    {
        try
        {
            if (!SaveDashboardSettings(
                    restartRunningService: false))
            {
                return;
            }

            if (_config.AdUseExplicitCredentials &&
                _config.AdCredentialStorageMode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Current User Credential Manager storage is intended for interactive GUI/CLI use. " +
                    "For the Windows Service, switch AD credential storage to Machine / Service DPAPI, " +
                    "or use integrated credentials with a gMSA/domain service identity.");
            }
            var before = WindowsServiceHost.GetInfo();
            if (before.Installed && string.Equals(before.State, "Running", StringComparison.OrdinalIgnoreCase))
                WindowsServiceHost.Stop();

            WindowsServiceHost.InstallOrUpdate();
            ApplyConfiguredServiceIdentity(restartIfRunning: false, allowExistingDomainPassword: true);
            WindowsServiceHost.Start();
            _audit.Write(
                "InstallOrUpdateService",
                source: "WindowsService",
                details: $"Executable={AppPaths.ServiceExecutable}; Identity={WindowsServiceHost.GetInfo().Identity}");
            RefreshDashboard();
            MessageBox.Show(
                this,
                $"BitKeyBridge service is installed and running.{Environment.NewLine}{Environment.NewLine}" +
                $"Executable: {AppPaths.ServiceExecutable}",
                "BitKeyBridge Service",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write("InstallOrUpdateService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshDashboard();
        }
    }

    private void RefreshMachineCloudStatus()
    {
        try
        {
            if (!File.Exists(AppPaths.MachineCloudConfigFile))
            {
                _machineCloudStatus.Text =
                    "Machine cloud config: not configured. Scheduled Coverage requires certificate mode.";
                return;
            }

            var cloud = ConfigService.LoadMachineCloudConfig();
            var cert = new CertificateService()
                .FindLocalMachineByThumbprint(cloud.CertificateThumbprint);

            var suffix = string.Empty;
            var service = WindowsServiceHost.GetInfo();
            if (service.Installed &&
                !string.IsNullOrWhiteSpace(service.Identity))
            {
                var access = new CertificatePrivateKeyAccessService()
                    .GetStatus(
                        cloud.CertificateThumbprint,
                        service.Identity);
                suffix =
                    $"; KeyAccess={access.Status}; Account={access.Account}; Provider={access.Provider}";
            }

            _machineCloudStatus.Text =
                $"Machine cloud: Tenant={cloud.TenantId}; Client={cloud.ClientId}; " +
                $"Certificate={cloud.CertificateThumbprint}; Expires={cert.NotAfter:yyyy-MM-dd}" +
                suffix;
        }
        catch (Exception ex)
        {
            _machineCloudStatus.Text =
                "Machine cloud config error: " + ex.Message;
        }
    }

    private void SaveMachineCloudFromGui()
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
                throw new InvalidOperationException(
                    "Administrator rights are required to save machine cloud configuration.");

            var tenant = _cloudTenant.Text.Trim();
            var client = _cloudClient.Text.Trim();
            var thumbprint = _cloudThumbprint.Text
                .Replace(" ", string.Empty)
                .Trim();

            if (string.IsNullOrWhiteSpace(tenant) ||
                string.IsNullOrWhiteSpace(client) ||
                string.IsNullOrWhiteSpace(thumbprint))
            {
                throw new InvalidOperationException(
                    "Configure Tenant ID, Client ID, and certificate in the Entra / Intune Cloud tab first.");
            }

            var cert = new CertificateService()
                .FindLocalMachineByThumbprint(thumbprint);

            ConfigService.SaveMachineCloudConfig(new CloudAuthConfig
            {
                TenantId = tenant,
                ClientId = client,
                CertificateThumbprint = thumbprint,
                AuthMode = "Certificate"
            });

            CertificateKeyAccessInfo? access = null;
            var service = WindowsServiceHost.GetInfo();
            if (service.Installed &&
                !string.IsNullOrWhiteSpace(service.Identity))
            {
                access =
                    WindowsServiceHost.EnsureConfiguredCloudCertificateAccess(
                        service.Identity,
                        required: _config.ServiceCoverageEnabled);
            }

            _audit.Write(
                "SaveMachineCloudConfig",
                source: "Local",
                details:
                    $"Tenant={tenant}; Client={client}; Certificate={thumbprint}; Expires={cert.NotAfter:yyyy-MM-dd}; KeyAccess={access?.Status ?? "NotApplicable"}; KeyAccount={access?.Account ?? string.Empty}");

            RefreshMachineCloudStatus();
            MessageBox.Show(
                this,
                "Machine cloud configuration saved for Windows Service / Task Scheduler." +
                Environment.NewLine + Environment.NewLine +
                "Only Tenant ID, Client ID, certificate thumbprint, and Certificate auth mode were stored. " +
                "No password, access token, or private key was written to the config file.",
                "Machine Cloud Configuration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "SaveMachineCloudConfig",
                "Failed",
                source: "Local",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Machine Cloud Configuration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshMachineCloudStatus();
        }
    }

    private void RepairMachineCertificateAccessFromGui()
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
                throw new InvalidOperationException(
                    "Administrator rights are required to repair certificate private-key access.");

            if (!File.Exists(AppPaths.MachineCloudConfigFile))
                throw new InvalidOperationException(
                    "Machine cloud configuration is not configured.");

            var service = WindowsServiceHost.GetInfo();
            if (!service.Installed ||
                string.IsNullOrWhiteSpace(service.Identity))
            {
                throw new InvalidOperationException(
                    "Install the BitKeyBridge Windows Service before repairing certificate access.");
            }

            var access =
                WindowsServiceHost.EnsureConfiguredCloudCertificateAccess(
                    service.Identity,
                    required: true)
                ?? throw new InvalidOperationException(
                    "Certificate private-key access could not be prepared.");

            _audit.Write(
                "RepairCertificatePrivateKeyAccess",
                source: "WindowsService",
                details:
                    $"Account={access.Account}; SID={access.Sid}; Provider={access.Provider}; Status={access.Status}; Certificate={access.Thumbprint}");

            RefreshMachineCloudStatus();
            RefreshDashboard();

            MessageBox.Show(
                this,
                $"Certificate private-key access: {access.Status}" +
                Environment.NewLine +
                $"Account: {access.Account}" +
                Environment.NewLine +
                $"Provider: {access.Provider}",
                "Certificate Access",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write(
                "RepairCertificatePrivateKeyAccess",
                "Failed",
                source: "WindowsService",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Certificate Access",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshMachineCloudStatus();
        }
    }

    private void DeleteMachineCloudFromGui()
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
                throw new InvalidOperationException(
                    "Administrator rights are required to delete machine cloud configuration.");

            if (_config.ServiceCoverageEnabled)
            {
                throw new InvalidOperationException(
                    "Disable scheduled Coverage and save service settings before deleting machine cloud configuration.");
            }

            ConfigService.DeleteMachineCloudConfig();
            _audit.Write(
                "DeleteMachineCloudConfig",
                source: "Local");
            RefreshMachineCloudStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Machine Cloud Configuration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshMachineCloudStatus();
        }
    }

    private string GetSelectedServiceIdentityMode() =>
        _serviceIdentityMode.SelectedIndex switch
        {
            1 => "gMSA",
            2 => "DomainAccount",
            _ => "LocalSystem"
        };

    private void UpdateServiceIdentityUi()
    {
        var mode = GetSelectedServiceIdentityMode();
        var localSystem = mode.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase);
        var domainAccount = mode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase);

        _serviceIdentityAccount.Enabled = !localSystem;
        _serviceIdentityPassword.Enabled = domainAccount;

        if (localSystem)
        {
            _serviceIdentityAccount.Clear();
            _serviceIdentityPassword.Clear();
        }
        else if (!domainAccount)
        {
            _serviceIdentityPassword.Clear();
        }
    }

    private void RefreshServiceIdentityStatus()
    {
        try
        {
            var info = WindowsServiceHost.GetInfo();
            _serviceIdentityStatus.Text = info.Installed
                ? $"Installed service identity: {(string.IsNullOrWhiteSpace(info.Identity) ? "Unknown" : info.Identity)}"
                : "Windows Service is not installed.";
        }
        catch (Exception ex)
        {
            _serviceIdentityStatus.Text = "Service identity status error: " + ex.Message;
        }
    }

    private void ApplyConfiguredServiceIdentity(
        bool restartIfRunning,
        bool allowExistingDomainPassword)
    {
        var mode =
            GetSelectedServiceIdentityMode();
        var account =
            _serviceIdentityAccount.Text.Trim();
        var password =
            _serviceIdentityPassword.Text;
        var current =
            WindowsServiceHost.GetInfo();

        if (!current.Installed)
        {
            throw new InvalidOperationException(
                "Install the BitKeyBridge Windows Service before applying its identity.");
        }

        if (mode.Equals(
                "DomainAccount",
                StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(
                password) &&
            allowExistingDomainPassword &&
            string.Equals(
                current.Identity,
                account,
                StringComparison.OrdinalIgnoreCase))
        {
            WindowsServiceHost
                .EnsureConfiguredServiceAccess(
                    current.Identity);

            _config.ServiceIdentityMode =
                "DomainAccount";
            _config.ServiceIdentityAccount =
                account;

            ConfigService.SaveAppConfig(
                _config);

            return;
        }

        WindowsServiceHost.ConfigureIdentity(
            mode,
            account,
            mode.Equals(
                "DomainAccount",
                StringComparison.OrdinalIgnoreCase)
                ? password
                : null,
            restartIfRunning);

        _config.ServiceIdentityMode =
            mode;
        _config.ServiceIdentityAccount =
            mode.Equals(
                "LocalSystem",
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : account;
    }

    private void ApplyServiceIdentityFromGui()
    {
        try
        {
            if (!SaveDashboardSettings(
                    restartRunningService: false))
            {
                return;
            }

            ApplyConfiguredServiceIdentity(
                restartIfRunning: true,
                allowExistingDomainPassword: false);

            _audit.Write(
                "ConfigureServiceIdentity",
                source: "WindowsService",
                details: $"Mode={_config.ServiceIdentityMode}; Account={_config.ServiceIdentityAccount}; EffectiveIdentity={WindowsServiceHost.GetInfo().Identity}");

            _serviceIdentityPassword.Clear();
            RefreshServiceIdentityStatus();
            RefreshMachineCloudStatus();
            RefreshDashboard();

            MessageBox.Show(
                this,
                "Windows Service identity updated successfully." +
                Environment.NewLine + Environment.NewLine +
                (GetSelectedServiceIdentityMode().Equals("gMSA", StringComparison.OrdinalIgnoreCase)
                    ? "The gMSA password is managed by Active Directory and is not stored by BitKeyBridge."
                    : "BitKeyBridge does not store the Windows Service account password."),
                "Service Identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _serviceIdentityPassword.Clear();
            _audit.Write(
                "ConfigureServiceIdentity",
                "Failed",
                source: "WindowsService",
                details: ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "Service Identity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshServiceIdentityStatus();
        }
    }

    private void StartServiceFromGui()
    {
        try
        {
            WindowsServiceHost.Start();
            _audit.Write("StartService", source: "WindowsService");
        }
        catch (Exception ex)
        {
            _audit.Write("StartService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshDashboard();
    }

    private void StopServiceFromGui()
    {
        try
        {
            WindowsServiceHost.Stop();
            _audit.Write("StopService", source: "WindowsService");
        }
        catch (Exception ex)
        {
            _audit.Write("StopService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshDashboard();
    }

    private void UninstallServiceFromGui()
    {
        var answer = MessageBox.Show(
            this,
            "Stop and remove the BitKeyBridge Windows Service? Configuration, audit logs and exported recovery data will be preserved.",
            "Uninstall BitKeyBridge Service",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            WindowsServiceHost.Uninstall();
            _audit.Write("UninstallService", source: "WindowsService");
        }
        catch (Exception ex)
        {
            _audit.Write("UninstallService", "Failed", source: "WindowsService", details: ex.Message);
            MessageBox.Show(this, ex.Message, "BitKeyBridge Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshDashboard();
    }

    private void OpenHealthEndpoint()
    {
        try
        {
            var url = $"http://127.0.0.1:{Math.Clamp(_config.HealthEndpointPort, 1024, 65535)}/health";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Health Endpoint", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshDashboard()
    {
        try
        {
            var health = new HealthService(_config).GetSnapshot();
            _dashboardStatus.Text =
                $"Overall: {health.OverallStatus}    Service: {health.ServiceState}    Last rows: {health.LastRunRows}    DC: {(string.IsNullOrWhiteSpace(health.LastRunDc) ? "-" : health.LastRunDc)}";

            _dashboardDetails.Clear();
            _dashboardDetails.AppendText($"Version:                  {health.Version}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Machine:                  {health.MachineName}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service installed:        {health.ServiceInstalled}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service state:            {health.ServiceState}{Environment.NewLine}");
            var serviceInfo = WindowsServiceHost.GetInfo();
            _dashboardDetails.AppendText($"Service identity:         {(serviceInfo.Installed ? serviceInfo.Identity : "-")}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service executable:       {AppPaths.ServiceExecutable}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Health endpoint:          {health.HealthEndpoint}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Service interval:         {_config.ServiceIntervalMinutes} minute(s){Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Output directory:         {health.OutputDirectory}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Output exists:            {health.OutputDirectoryExists}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Recovery CSV exists:      {health.CsvExists}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Recovery CSV rows:        {health.CsvRows}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Last run success:         {health.LastRunSuccess?.ToString() ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run finished:        {health.LastRunFinished?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run published:       {health.LastRunPublished}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run rows:            {health.LastRunRows}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last run DC:              {health.LastRunDc}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Replication healthy:      {health.ReplicationHealthy?.ToString() ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Replication errors:       {health.ReplicationErrors}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Replication warnings:     {health.ReplicationWarnings}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last successful export:   {health.LastSuccessfulExport?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Last success age (hours): {health.LastSuccessfulExportAgeHours?.ToString("0.0") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Cloud configured:         {health.CloudConfigured}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Cloud auth mode:          {health.CloudAuthMode}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Certificate status:       {health.CertificateStatus}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Certificate expires:      {health.CertificateExpires?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Certificate days left:    {health.CertificateDaysRemaining?.ToString("0.0") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Remote API enabled:       {health.RemoteApiEnabled}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Remote API port:          {(health.RemoteApiEnabled ? health.RemoteApiPort.ToString() : "-")}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Remote management:        {health.RemoteApiManagementEnabled}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Remote API certificate:   {health.RemoteApiCertificateThumbprint}{Environment.NewLine}");
            _dashboardDetails.AppendText(Environment.NewLine);
            _dashboardDetails.AppendText($"Update checked (UTC):     {health.UpdateCheckedAtUtc?.ToString("u") ?? "-"}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Latest release:           {health.LatestVersion}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Update available:         {health.UpdateAvailable}{Environment.NewLine}");
            _dashboardDetails.AppendText($"Update error:             {health.UpdateError}{Environment.NewLine}");

            if (health.Warnings.Count > 0)
            {
                _dashboardDetails.AppendText(Environment.NewLine + "WARNINGS" + Environment.NewLine);
                foreach (var warning in health.Warnings)
                    _dashboardDetails.AppendText("  - " + warning + Environment.NewLine);
            }

            if (health.Errors.Count > 0)
            {
                _dashboardDetails.AppendText(Environment.NewLine + "ERRORS" + Environment.NewLine);
                foreach (var error in health.Errors)
                    _dashboardDetails.AppendText("  - " + error + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _dashboardStatus.Text = "Dashboard error: " + ex.Message;
        }
    }

    private void RunSecureOutputWizard()
    {
        using var dialog = new SecureOutputDialog(_config.OutputDirectory);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var service = new SecureOutputService();
            var result = service.Create(dialog.DirectoryPath, dialog.ReaderPrincipals, dialog.ShareName);

            if (dialog.UpdateApplicationConfig)
            {
                var full = Path.GetFullPath(result.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar);
                var parent = Path.GetDirectoryName(full)
                    ?? throw new InvalidOperationException("The selected output directory has no parent directory.");
                var name = Path.GetFileName(full);
                _config.OutputRoot = parent;
                _config.OutputSubdirectory = name;
                _outputRoot.Text = parent;
                _outputSubdirectory.Text = name;
                ConfigService.SaveAppConfig(_config);

                var svc = WindowsServiceHost.GetInfo();
                if (svc.Installed && string.Equals(svc.State, "Running", StringComparison.OrdinalIgnoreCase))
                {
                    WindowsServiceHost.Stop();
                    WindowsServiceHost.Start();
                }
            }

            _audit.Write(
                "CreateSecureOutput",
                source: "WindowsACL",
                details: $"Path={result.DirectoryPath}; Share={result.ShareName}; Readers={string.Join(",", result.ReaderPrincipals)}; ConfigUpdated={dialog.UpdateApplicationConfig}");

            RefreshLastSuccess();
            RefreshDashboard();

            var shareText = result.ShareCreated
                ? $"SMB share: \\{Environment.MachineName}\\{result.ShareName}"
                : "SMB share: not created";
            MessageBox.Show(
                this,
                $"Protected output created successfully.{Environment.NewLine}{Environment.NewLine}" +
                $"Directory: {result.DirectoryPath}{Environment.NewLine}{shareText}{Environment.NewLine}{Environment.NewLine}" +
                (dialog.UpdateApplicationConfig
                    ? "BitKeyBridge export configuration was updated. If an existing WinPE workflow still reads NETLOGON/SYSVOL, update that consumer before switching production export."
                    : "BitKeyBridge export configuration was not changed."),
                "Secure BitLocker Output",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _audit.Write("CreateSecureOutput", "Failed", source: "WindowsACL", details: ex.Message);
            MessageBox.Show(this, ex.Message, "Secure BitLocker Output", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private AuditEntry? WriteRecoveryAudit(
        string action,
        RecoveryAccessContext context,
        string result = "Success",
        string? computerName = null,
        string? recoveryId = null,
        string? source = null,
        string? authMode = null,
        string? details = null,
        bool rotationRequested = false,
        bool rotationSucceeded = false)
    {
        var entry = _audit.Write(
            action,
            result,
            computerName,
            recoveryId,
            source,
            authMode,
            details,
            context.Reference,
            context.Reason,
            context.SessionId);

        try
        {
            _ = new RecoveryIncidentService()
                .Append(
                    context,
                    entry,
                    rotationRequested,
                    rotationSucceeded);
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                $"Recovery incident bundle update failed. Session={context.SessionId}; Action={action}; Error={ex.Message}",
                EventLogSeverity.Warning,
                4538,
                "RecoveryIncident");
        }

        return entry;
    }

    private bool AuthorizeAction(
        BitKeyBridgePermission permission,
        string action,
        string? computerName = null,
        string? recoveryId = null,
        string? source = null)
    {
        var decision = new AuthorizationService(_config).Check(permission);
        if (decision.Allowed)
            return true;

        _audit.Write(
            action,
            "Denied",
            computerName,
            recoveryId,
            source,
            details:
                $"Permission={decision.Permission}; Identity={decision.Identity}; Reason={decision.Reason}");

        WindowsEventLogService.TryWrite(
            $"RBAC denied {action}. Permission={decision.Permission}; Identity={decision.Identity}; Computer={computerName}; RecoveryId={recoveryId}; Source={source}.",
            EventLogSeverity.Warning,
            4501,
            "RBAC");

        MessageBox.Show(
            this,
            $"Access denied for {permission}.{Environment.NewLine}{Environment.NewLine}" +
            $"Windows identity: {decision.Identity}{Environment.NewLine}" +
            decision.Reason,
            "BitKeyBridge RBAC",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);

        return false;
    }

    private bool AuthorizePrivilegedRecoveryAccess(
        RecoveryAccessContext context,
        string action,
        string computerName,
        string recoveryId,
        string source)
    {
        PrivilegedAccessDecision decision;

        try
        {
            decision =
                new PrivilegedAccessPolicyService(
                    _config)
                    .EvaluateRecoveryAccess(
                        context,
                        computerName,
                        recoveryId,
                        source,
                        _audit);
        }
        catch (Exception ex)
        {
            decision =
                new PrivilegedAccessDecision
                {
                    Allowed = false,
                    Status =
                        "PolicyError",
                    Message =
                        ex.Message
                };
        }

        if (decision.Allowed)
            return true;

        WriteRecoveryAudit(
            action,
            context,
            result:
                "Denied",
            computerName:
                computerName,
            recoveryId:
                recoveryId,
            source:
                source,
            details:
                $"PrivilegedPolicy={decision.Status}; JIT={decision.Jit.Status}; Approval={decision.Approval.Status}; SIEM={decision.SiemStatus}; Message={decision.Message}");

        WindowsEventLogService.TryWrite(
            $"Privileged recovery policy denied {action}. Status={decision.Status}; " +
            $"Computer={computerName}; RecoveryId={recoveryId}; Source={source}; " +
            $"Session={context.SessionId}.",
            EventLogSeverity.Warning,
            4651,
            "PrivilegedAccess");

        var message =
            decision.Message;

        if (decision.Status ==
            "ApprovalPending")
        {
            message +=
                Environment.NewLine +
                Environment.NewLine +
                "Session ID:" +
                Environment.NewLine +
                context.SessionId +
                Environment.NewLine +
                Environment.NewLine +
                "A second authorized Windows user can approve this session from BitKeyBridge Privileged Access settings or with --approval-approve.";
        }

        MessageBox.Show(
            this,
            message,
            "Privileged Recovery Access",
            MessageBoxButtons.OK,
            decision.Status is
                "ApprovalPending" or
                "JitRequired"
                ? MessageBoxIcon.Information
                : MessageBoxIcon.Warning);

        return false;
    }

    private RecoveryAccessContext? GetOrRequestRecoveryAccessContext(
        string source,
        string recoveryId,
        string computerName,
        bool allowRotationReminder)
    {
        var normalizedId = string.IsNullOrWhiteSpace(recoveryId)
            ? computerName
            : recoveryId;
        var cacheKey = source + ":" + normalizedId;

        if (_recoveryAccessContexts.TryGetValue(cacheKey, out var existing))
            return existing;

        var prompt =
            _config.RequireRecoveryAccessReference ||
            (allowRotationReminder &&
             _config.SuggestRotationAfterCloudKeyRetrieval);

        if (!prompt)
        {
            var empty = new RecoveryAccessContext();
            _recoveryAccessContexts[cacheKey] = empty;
            return empty;
        }

        using var dialog = new RecoveryAccessDialog(
            computerName,
            normalizedId,
            _config.RequireRecoveryAccessReference,
            allowRotationReminder,
            allowRotationReminder &&
            _config.SuggestRotationAfterCloudKeyRetrieval);

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return null;

        var context = dialog.Context;
        _recoveryAccessContexts[cacheKey] = context;
        return context;
    }

    private void ClearTrackedRecoveryClipboard()
    {
        _clipboardClearTimer?.Stop();
        _clipboardClearTimer?.Dispose();
        _clipboardClearTimer = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(
                    _clipboardRecoveryKey) &&
                Clipboard.ContainsText() &&
                string.Equals(
                    Clipboard.GetText(),
                    _clipboardRecoveryKey,
                    StringComparison.Ordinal))
            {
                Clipboard.Clear();
            }
        }
        catch
        {
        }

        _clipboardRecoveryKey = null;
    }

    private void ResetRecoverySearchState()
    {
        ClearTrackedRecoveryClipboard();
        _startResults.Items.Clear();
        _startResults.Visible = true;
        _startCurrentKey = null;
        _startKey.Clear();
        _startKey.UseSystemPasswordChar = true;
        _startShow.Text = "Reveal Recovery Key";
        ClearRecoveryCard();
        _recoveryAccessContexts.Clear();
    }

    private void ClearSensitiveState()
    {
        ClearTrackedRecoveryClipboard();

        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if ((!string.IsNullOrWhiteSpace(_startCurrentKey) &&
                     string.Equals(text, _startCurrentKey, StringComparison.Ordinal)) ||
                    (!string.IsNullOrWhiteSpace(_deviceCurrentKey) &&
                     string.Equals(text, _deviceCurrentKey, StringComparison.Ordinal)))
                {
                    Clipboard.Clear();
                }
            }
        }
        catch { }

        _startCurrentKey = null;
        _startKey.Clear();
        _deviceCurrentKey = null;
        _deviceRecoveryKey.Clear();
        _cloudToken = null;
        AdSessionCredentials.Clear();
        _recoveryAccessContexts.Clear();
        _cloudPassword.Clear();
        _adPassword.Clear();
        _serviceIdentityPassword.Clear();
    }

    private void RefreshAuditSigningStatus()
    {
        try
        {
            var service =
                new AuditSigningService(_config);
            var result =
                service.VerifyCheckpoint();
            var transitions =
                service.VerifyTransitionHistory();

            _auditSigningStatus.Text =
                $"Signed audit: {result.Status}; Enabled={_config.AuditSigningEnabled}; " +
                $"SignatureValid={result.SignatureValid}; CurrentHeadSigned={result.CurrentHeadSigned}; " +
                $"Transitions={transitions.Status}/{transitions.ValidTransitions}" +
                (result.CertificateExpiresUtc is null
                    ? string.Empty
                    : $"; CertificateExpires={result.CertificateExpiresUtc:yyyy-MM-dd}") +
                (result.Checkpoint is null
                    ? string.Empty
                    : $"; Checkpoint={result.Checkpoint.CreatedAtUtc:u}; Entries={result.Checkpoint.TotalEntries}") +
                (string.IsNullOrWhiteSpace(transitions.FirstError)
                    ? string.Empty
                    : $"; TransitionError={transitions.FirstError}");
        }
        catch (Exception ex)
        {
            _auditSigningStatus.Text =
                "Signed audit status error: " + ex.Message;
        }
    }

    private void SetupAuditSigningGui()
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
            {
                throw new InvalidOperationException(
                    "Administrator rights are required to configure audit signing.");
            }

            var before = WindowsServiceHost.GetInfo();
            var signing =
                new AuditSigningService(_config);
            var cert = signing.Setup();

            _audit.Write(
                "AuditSigningSetup",
                source: "Local",
                details:
                    $"Certificate={cert.Thumbprint}; Expires={cert.NotAfter:O}");

            AuditSigningCheckpoint? checkpoint = null;
            try
            {
                checkpoint = signing.SignCheckpoint();
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains(
                    "no hash-chained audit entries",
                    StringComparison.OrdinalIgnoreCase))
            {
            }

            if (before.Installed &&
                string.Equals(
                    before.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            RefreshAudit();
            RefreshAuditSigningStatus();
            RefreshDashboard();

            MessageBox.Show(
                this,
                "Audit signing is enabled." +
                Environment.NewLine +
                $"Certificate: {cert.Thumbprint}" +
                Environment.NewLine +
                $"Expires: {cert.NotAfter:yyyy-MM-dd}" +
                Environment.NewLine +
                (checkpoint is null
                    ? "Checkpoint will be created after the first chained audit entry."
                    : $"Signed checkpoint entries: {checkpoint.TotalEntries}"),
                "Audit Signing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Audit Signing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshAuditSigningStatus();
        }
    }

    private void SignAuditCheckpointGui()
    {
        try
        {
            var checkpoint =
                new AuditSigningService(_config)
                    .SignCheckpoint();

            RefreshAuditSigningStatus();
            RefreshDashboard();

            MessageBox.Show(
                this,
                $"Signed {checkpoint.TotalEntries} audit entries." +
                Environment.NewLine +
                $"Hash: {checkpoint.LastHash}",
                "Audit Signing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Audit Signing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshAuditSigningStatus();
        }
    }

    private void VerifyAuditSignatureGui()
    {
        try
        {
            var result =
                new AuditSigningService(_config)
                    .VerifyCheckpoint();

            RefreshAuditSigningStatus();

            MessageBox.Show(
                this,
                $"Status: {result.Status}" +
                Environment.NewLine +
                $"Signature valid: {result.SignatureValid}" +
                Environment.NewLine +
                $"Audit chain valid: {result.AuditChainValid}" +
                Environment.NewLine +
                $"Checkpoint hash present: {result.CheckpointHashPresent}" +
                Environment.NewLine +
                $"Current head signed: {result.CurrentHeadSigned}" +
                (string.IsNullOrWhiteSpace(result.Error)
                    ? string.Empty
                    : Environment.NewLine +
                      "Error: " + result.Error),
                "Audit Signature Verification",
                MessageBoxButtons.OK,
                result.Valid
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Audit Signature Verification",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RolloverAuditSigningGui()
    {
        try
        {
            if (!_config.AuditSigningEnabled)
            {
                throw new InvalidOperationException(
                    "Enable audit signing before certificate rollover.");
            }

            var answer = MessageBox.Show(
                this,
                "Roll over the audit-signing certificate?" +
                Environment.NewLine +
                Environment.NewLine +
                "BitKeyBridge will verify/sign the current audit head, create a new non-exportable LocalMachine certificate, and create a transition signed by BOTH the old and new certificates." +
                Environment.NewLine +
                Environment.NewLine +
                "The previous certificate is retained so historical trust transitions remain verifiable.",
                "Audit Signing Rollover",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            var serviceBefore =
                WindowsServiceHost.GetInfo();

            if (serviceBefore.Installed &&
                string.Equals(
                    serviceBefore.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
            }

            try
            {
                var signing =
                    new AuditSigningService(_config);

                var result =
                    signing.Rollover();

                _audit.Write(
                    "AuditSigningRollover",
                    source: "Local",
                    details:
                        $"Previous={result.PreviousThumbprint}; New={result.NewThumbprint}; OldSignatureValid={result.PreviousSignatureValid}; NewSignatureValid={result.NewSignatureValid}; NewCheckpointValid={result.NewCheckpointValid}");

                _ = signing.SignCheckpoint();

                RefreshAudit();
                RefreshAuditSigningStatus();
                RefreshDashboard();

                MessageBox.Show(
                    this,
                    "Audit-signing certificate rollover completed." +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Previous: {result.PreviousThumbprint}" +
                    Environment.NewLine +
                    $"New: {result.NewThumbprint}" +
                    Environment.NewLine +
                    $"Expires: {result.NewCertificateNotAfterUtc:yyyy-MM-dd}" +
                    Environment.NewLine +
                    $"Old signature valid: {result.PreviousSignatureValid}" +
                    Environment.NewLine +
                    $"New signature valid: {result.NewSignatureValid}" +
                    Environment.NewLine +
                    $"Checkpoint valid: {result.NewCheckpointValid}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Transition history: {AppPaths.AuditSigningTransitionsDirectory}",
                    "Audit Signing Rollover",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            finally
            {
                if (serviceBefore.Installed &&
                    string.Equals(
                        serviceBefore.State,
                        "Running",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WindowsServiceHost.Start();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Audit Signing Rollover",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshAuditSigningStatus();
        }
    }

    private void DisableAuditSigningGui()
    {
        try
        {
            if (!_config.AuditSigningEnabled)
            {
                RefreshAuditSigningStatus();
                return;
            }

            var answer = MessageBox.Show(
                this,
                "Disable creation of new signed audit checkpoints?" +
                Environment.NewLine +
                Environment.NewLine +
                "The existing certificate and signed checkpoint will be retained.",
                "Disable Audit Signing",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            var before = WindowsServiceHost.GetInfo();
            var signing =
                new AuditSigningService(_config);

            _audit.Write(
                "AuditSigningDisable",
                source: "Local",
                details:
                    "Audit signing disabled by administrator. Existing trust material retained.");

            try
            {
                _ = signing.SignCheckpoint();
            }
            catch
            {
            }

            signing.Disable();

            if (before.Installed &&
                string.Equals(
                    before.State,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            RefreshAudit();
            RefreshAuditSigningStatus();
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Audit Signing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshAuditSigningStatus();
        }
    }

    private void VerifyAuditIntegrityGui()
    {
        try
        {
            var result =
                AuditIntegrityService.VerifyAndPersist(_audit.Path);
            var icon = result.Valid
                ? MessageBoxIcon.Information
                : MessageBoxIcon.Error;

            var message =
                $"Valid: {result.Valid}{Environment.NewLine}" +
                $"Files checked: {result.FilesChecked}{Environment.NewLine}" +
                $"Entries: {result.TotalEntries}{Environment.NewLine}" +
                $"Hash-chained: {result.ChainedEntries}{Environment.NewLine}" +
                $"Legacy: {result.LegacyEntries}" +
                (string.IsNullOrWhiteSpace(result.FirstError)
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine +
                      "First error: " + result.FirstError);

            MessageBox.Show(
                this,
                message,
                "Audit Integrity",
                MessageBoxButtons.OK,
                icon);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Audit Integrity",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void VerifyRecoveryIncidentGui()
    {
        string? sessionId = null;

        if (_auditResults.SelectedItems.Count > 0 &&
            _auditResults.SelectedItems[0].Tag
                is AuditEntry entry &&
            !string.IsNullOrWhiteSpace(
                entry.CorrelationId))
        {
            sessionId = entry.CorrelationId;
        }

        using var dialog =
            new RecoveryIncidentVerificationDialog(
                sessionId);
        dialog.ShowDialog(this);
    }

    private void RefreshAudit()
    {
        _auditResults.Items.Clear();
        foreach (var row in _audit.ReadRecent(1000))
        {
            var item = new ListViewItem(row.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(row.User);
            item.SubItems.Add(row.Host);
            item.SubItems.Add(row.Action);
            item.SubItems.Add(row.Result);
            item.SubItems.Add(row.ComputerName);
            item.SubItems.Add(row.RecoveryId);
            item.SubItems.Add(row.Source);
            item.SubItems.Add(row.AuthMode);
            item.SubItems.Add(row.Reference);
            item.SubItems.Add(row.CorrelationId);
            item.SubItems.Add(row.Reason);
            item.SubItems.Add(row.Details);
            item.Tag = row;
            _auditResults.Items.Add(item);
        }
    }

    private void CopyKeyWithAutoClear(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _clipboardClearTimer?.Stop();
        _clipboardClearTimer?.Dispose();

        Clipboard.SetText(key);
        _clipboardRecoveryKey = key;

        _clipboardClearTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 60000
            };

        _clipboardClearTimer.Tick +=
            (_, _) =>
            {
                _clipboardClearTimer?.Stop();

                try
                {
                    if (Clipboard.ContainsText() &&
                        string.Equals(
                            Clipboard.GetText(),
                            _clipboardRecoveryKey,
                            StringComparison.Ordinal))
                    {
                        Clipboard.Clear();
                    }
                }
                catch
                {
                }

                _clipboardRecoveryKey = null;
                _clipboardClearTimer?.Dispose();
                _clipboardClearTimer = null;
            };

        _clipboardClearTimer.Start();
    }

    private static void AddColumns(ListView view, params (string Name, int Width)[] columns)
    {
        foreach (var (name, width) in columns) view.Columns.Add(name, width);
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
