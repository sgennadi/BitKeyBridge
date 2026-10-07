using System.Security.Cryptography;
using System.Text;

namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly ComboBox _lapsSource = new();
    private readonly TextBox _lapsQuery = new();
    private readonly CheckBox _lapsHistory = new();
    private readonly Button _lapsRead = new();
    private readonly Button _lapsCancel = new();
    private readonly Button _lapsReveal = new();
    private readonly Button _lapsCopy = new();
    private readonly ListView _lapsRows = new();
    private readonly TextBox _lapsPassword = new();
    private readonly RichTextBox _lapsDetails = new();
    private readonly UiStatusLabel _lapsStatus = new();
    private readonly ProgressBar _lapsProgress = new();
    private readonly UiDiagnosticPanel _lapsDiagnostics = new();
    private LapsReadResult? _lapsResult;
    private CancellationTokenSource? _lapsReadCancellation;
    private System.Windows.Forms.Timer? _lapsClearTimer;
    private int _lapsGeneration;
    private bool _lapsReading;
    private string _lapsResourceId = string.Empty;
    private string _lapsAuditSource = string.Empty;

    private TabPage BuildLapsWorkspaceTab()
    {
        var tab = new TabPage("LAPS");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1, RowCount = 9
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 9; i++)
            root.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize, i == 3 ? 100F : 0F));
        tab.Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = "LAPS passwords and history", AutoSize = true, Font = UiStyle.CreatePageTitleFont(),
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        }, 0, 0);

        var search = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        search.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        search.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        search.Controls.Add(new Label { Text = "Source:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _lapsSource.DropDownStyle = ComboBoxStyle.DropDownList;
        _lapsSource.Items.AddRange(["Active Directory (Legacy / Windows LAPS / DSRM)", "Microsoft Entra ID"]);
        _lapsSource.Dock = DockStyle.Fill;
        _lapsSource.SelectedIndex = 0;
        search.Controls.Add(_lapsSource, 1, 0);
        search.Controls.Add(new Label { Text = "Computer:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _lapsQuery.Dock = DockStyle.Fill;
        _lapsQuery.PlaceholderText = "Exact computer name, DNS name, AD object GUID or DN";
        search.Controls.Add(_lapsQuery, 1, 1);
        root.Controls.Add(search, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        _lapsHistory.Text = "Include password history";
        _lapsHistory.AutoSize = true;
        _lapsHistory.Checked = true;
        actions.Controls.Add(_lapsHistory);
        _lapsRead.Text = "Read LAPS";
        UiStyle.ConfigureActionButton(_lapsRead);
        actions.Controls.Add(_lapsRead);
        _lapsCancel.Text = "Cancel";
        UiStyle.ConfigureActionButton(_lapsCancel);
        _lapsCancel.Enabled = false;
        actions.Controls.Add(_lapsCancel);
        var connect = UiStyle.CreateActionButton("Connect to AD");
        var clear = UiStyle.CreateActionButton("Clear passwords");
        actions.Controls.Add(connect);
        actions.Controls.Add(clear);
        root.Controls.Add(actions, 0, 2);

        _lapsRows.Dock = DockStyle.Fill;
        _lapsRows.MinimumSize = new Size(0, 160);
        _lapsRows.View = View.Details;
        _lapsRows.FullRowSelect = true;
        _lapsRows.MultiSelect = false;
        _lapsRows.HideSelection = false;
        _lapsRows.GridLines = true;
        AddColumns(_lapsRows, ("Source / version", 210), ("Current / history", 150),
            ("Account", 190), ("Updated (UTC)", 175), ("Expires (UTC)", 175), ("Status", 150));
        root.Controls.Add(_lapsRows, 0, 3);

        _lapsDetails.Dock = DockStyle.Top;
        _lapsDetails.ReadOnly = true;
        _lapsDetails.MinimumSize = new Size(0, 90);
        _lapsDetails.Font = UiStyle.CreateMonospaceFont(9F);
        root.Controls.Add(_lapsDetails, 0, 4);

        var secret = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2,
            Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap)
        };
        secret.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        secret.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        secret.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        secret.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        secret.Controls.Add(new Label { Text = "Password:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _lapsPassword.Dock = DockStyle.Fill;
        _lapsPassword.ReadOnly = true;
        _lapsPassword.UseSystemPasswordChar = true;
        _lapsPassword.Font = UiStyle.CreateMonospaceFont(10F);
        secret.Controls.Add(_lapsPassword, 1, 0);
        var secretActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        _lapsReveal.Text = "Reveal password";
        _lapsCopy.Text = "Copy password";
        UiStyle.ConfigureActionButton(_lapsReveal);
        UiStyle.ConfigureActionButton(_lapsCopy);
        _lapsReveal.Enabled = _lapsCopy.Enabled = false;
        secretActions.Controls.Add(_lapsReveal);
        secretActions.Controls.Add(_lapsCopy);
        secret.Controls.Add(secretActions, 1, 1);
        root.Controls.Add(secret, 0, 5);
        _lapsStatus.Dock = DockStyle.Top;
        UiStyle.SetStatus(_lapsStatus, "Choose a source and computer. Read LAPS checks access and reads current passwords and optional history.", UiStatusKind.Neutral);
        root.Controls.Add(_lapsStatus, 0, 6);

        _lapsProgress.Name = "LapsProgress";
        _lapsProgress.AccessibleName = "LAPS read progress";
        _lapsProgress.Dock = DockStyle.Top;
        _lapsProgress.Style = ProgressBarStyle.Marquee;
        _lapsProgress.MarqueeAnimationSpeed = 25;
        _lapsProgress.Visible = false;
        _lapsProgress.Margin = new Padding(0, UiStyle.ControlGap, 0, 0);
        root.Controls.Add(_lapsProgress, 0, 7);

        root.Controls.Add(_lapsDiagnostics, 0, 8);

        _lapsRead.Click += async (_, _) => await ReadLapsAsync();
        _lapsCancel.Click += (_, _) => CancelLapsRead();
        _lapsQuery.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await ReadLapsAsync();
        };
        _lapsQuery.TextChanged += (_, _) => ClearLapsResult();
        _adUsername.TextChanged += (_, _) => ClearLapsResult();
        _adServer.TextChanged += (_, _) => ClearLapsResult();
        _adDomain.TextChanged += (_, _) => ClearLapsResult();
        _adExplicitCredentials.CheckedChanged += (_, _) => ClearLapsResult();
        _adCredentialStorage.SelectedIndexChanged += (_, _) => ClearLapsResult();
        _cloudTenant.TextChanged += (_, _) => ClearLapsResult();
        _cloudClient.TextChanged += (_, _) => ClearLapsResult();
        _cloudUsername.TextChanged += (_, _) => ClearLapsResult();
        _cloudThumbprint.TextChanged += (_, _) => ClearLapsResult();
        _cloudAuthMode.SelectedIndexChanged += (_, _) => ClearLapsResult();
        _lapsSource.SelectedIndexChanged += (_, _) =>
        {
            ClearLapsResult();
            var cloud = _lapsSource.SelectedIndex == 1;
            connect.Enabled = !cloud;
            _lapsQuery.PlaceholderText = cloud ? "Exact Entra device name or Entra device ID (not object ID)" :
                "Exact computer name, DNS name, AD object GUID or DN";
        };
        connect.Click += async (_, _) => await TestDirectoryConnectionAsync(promptForOu: false, forceManualCredentials: true);
        clear.Click += (_, _) => ClearLapsResult();
        _lapsRows.SelectedIndexChanged += (_, _) => SelectLapsEntry();
        _lapsReveal.Click += (_, _) => RevealLapsPassword(copy: false);
        _lapsCopy.Click += (_, _) => RevealLapsPassword(copy: true);
        FormClosed += (_, _) => ClearLapsResult();
        return tab;
    }

    private async Task ReadLapsAsync()
    {
        if (_lapsReading) return;
        var query = _lapsQuery.Text.Trim();
        if (query.Length == 0)
        {
            UiStyle.SetStatus(_lapsStatus, "Enter the exact computer name or device ID.", UiStatusKind.Warning);
            return;
        }
        var cloud = _lapsSource.SelectedIndex == 1;
        if (!cloud)
        {
            if (!EnsureSessionAdCredentialForConnection()) return;
            SaveDirectorySettings(showConfirmation: false, allowInMemoryFallback: true);
        }
        var source = cloud ? "LAPS-Entra" : "LAPS-AD";
        var resourceId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(source + ":" + query.ToUpperInvariant()))[..16]).ToString("D");
        var context = AuthorizeLapsAccess("ReadLapsPasswords", query, resourceId, source);
        if (context is null) return;

        ClearLapsResult();
        var generation = _lapsGeneration;
        var history = _lapsHistory.Checked;
        using var cancellation = new CancellationTokenSource();
        _lapsReadCancellation = cancellation;
        _lapsReading = true;
        _lapsRead.Enabled = false;
        _lapsCancel.Enabled = true;
        _lapsProgress.Visible = true;
        _lapsDiagnostics.Clear();
        _lapsSource.Enabled = false;
        _lapsQuery.Enabled = false;
        _lapsHistory.Enabled = false;
        LapsReadResult? pending = null;
        try
        {
            UiStyle.SetStatus(_lapsStatus, "Reading LAPS passwords and history...", UiStatusKind.Busy);
            if (cloud)
            {
                if (!await EnsureCloudTokenAsync(forLaps: true))
                {
                    UiStyle.SetStatus(_lapsStatus, "Entra authentication was not completed. Configure the cloud connection and try again.", UiStatusKind.Warning);
                    return;
                }
                using var graph = new CloudGraphService();
                pending = await graph.ReadLapsPasswordsAsync(_cloudToken!.AccessToken, query, history, cancellation.Token);
            }
            else
            {
                // Freeze the target and credentials before leaving the UI thread.
                var snapshot = new AppConfig
                {
                    AdConnectionMode = _config.AdConnectionMode, AdDomain = _config.AdDomain,
                    AdServer = _config.AdServer, AdPort = _config.AdPort, AdUseLdaps = _config.AdUseLdaps
                };
                var credential = AdSessionCredentials.CreateNetworkCredential(_config);
                var service = new LapsDirectoryService(snapshot, credential);
                pending = await Task.Run(() => service.Read(query, history, cancellation.Token), cancellation.Token);
            }
            if (IsDisposed || cancellation.IsCancellationRequested || generation != _lapsGeneration) return;
            _lapsResult = pending;
            pending = null;
            _lapsResourceId = resourceId;
            _lapsAuditSource = source;
            foreach (var row in _lapsResult.OrderedEntries)
            {
                var item = new ListViewItem(row.Source);
                item.SubItems.Add(row.IsHistory ? "History" : "Current");
                item.SubItems.Add(row.AccountName);
                item.SubItems.Add(row.UpdatedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not stored");
                item.SubItems.Add(row.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not stored");
                item.SubItems.Add(row.Status.ToString());
                item.Tag = row;
                _lapsRows.Items.Add(item);
            }
            var available = _lapsResult.Entries.Count(x => x.HasPassword);
            WriteRecoveryAudit("ReadLapsPasswords", context,
                result: available == 0 ? "NoReadablePasswords" : available == _lapsResult.Entries.Count ? "Success" : "Partial",
                computerName: _lapsResult.ComputerName,
                recoveryId: resourceId, source: source,
                details: $"Records={_lapsResult.Entries.Count}; Available={available}; IncludeHistory={history}; DC={_lapsResult.DirectoryServer}");
            _lapsDetails.Text = $"Computer: {_lapsResult.ComputerName} | Directory: {_lapsResult.DirectoryServer}\r\n" +
                $"Device / AD object ID: {_lapsResult.ComputerId} | Password version: {_lapsResult.PasswordVersion}\r\n{_lapsResult.Note}";
            UiStyle.SetStatus(_lapsStatus, $"{_lapsResult.Entries.Count} record(s), {available} readable password(s). " +
                "Select a record to reveal or copy. Passwords clear after two minutes.",
                available == _lapsResult.Entries.Count && available > 0 ? UiStatusKind.Success : UiStatusKind.Warning);
            _lapsClearTimer = new System.Windows.Forms.Timer { Interval = 120000 };
            _lapsClearTimer.Tick += (_, _) => ClearLapsResult();
            _lapsClearTimer.Start();
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && generation == _lapsGeneration)
                UiStyle.SetStatus(_lapsStatus, "LAPS read canceled.", UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            if (IsDisposed || generation != _lapsGeneration) return;
            // Secret parsers and native decryption emit fixed, secret-free errors.
            _audit.Write("ReadLapsPasswords", "Failed", query, resourceId, source, details: ex.Message);
            UiStyle.SetStatus(_lapsStatus, "LAPS read failed. Review diagnostics below.", UiStatusKind.Error);
            _lapsDiagnostics.ShowError(
                "LAPS read failed.",
                "ReadLapsPasswords",
                ex,
                ("Source", source),
                ("Lookup", query),
                ("IncludeHistory", history.ToString()),
                ("Server", _config.AdServer),
                ("Domain", _config.AdDomain));
        }
        finally
        {
            pending?.Dispose();
            if (ReferenceEquals(_lapsReadCancellation, cancellation)) _lapsReadCancellation = null;
            _lapsReading = false;
            if (!IsDisposed)
            {
                _lapsRead.Enabled = true;
                _lapsCancel.Enabled = false;
                _lapsSource.Enabled = true;
                _lapsQuery.Enabled = true;
                _lapsHistory.Enabled = true;
                _lapsProgress.Visible = false;
            }
        }
    }

    private void CancelLapsRead()
    {
        if (!_lapsReading || _lapsReadCancellation is null)
            return;

        _lapsCancel.Enabled = false;
        UiStyle.SetStatus(_lapsStatus, "Cancelling LAPS read...", UiStatusKind.Busy);
        _lapsReadCancellation.Cancel();
    }

    private RecoveryAccessContext? AuthorizeLapsAccess(string action, string computer, string resourceId, string source)
    {
        if (!AuthorizeAction(BitKeyBridgePermission.RecoveryRead, action, computer, resourceId, source)) return null;
        var context = GetOrRequestRecoveryAccessContext(source, resourceId, computer, allowRotationReminder: false);
        return context is not null && AuthorizePrivilegedRecoveryAccess(context, action, computer, resourceId, source)
            ? context : null;
    }

    private void SelectLapsEntry()
    {
        _lapsPassword.Clear();
        _lapsPassword.UseSystemPasswordChar = true;
        _lapsReveal.Text = "Reveal password";
        var row = _lapsRows.SelectedItems.Count == 1 ? _lapsRows.SelectedItems[0].Tag as LapsPasswordEntry : null;
        _lapsReveal.Enabled = _lapsCopy.Enabled = row?.HasPassword == true;
        if (row is null || _lapsResult is null) return;
        _lapsDetails.Text = $"Computer: {_lapsResult.ComputerName} | Directory: {_lapsResult.DirectoryServer}\r\n" +
            $"Attribute: {row.Attribute} | Account SID: {row.AccountSid} | Password version: {_lapsResult.PasswordVersion}\r\n" +
            $"{row.StatusDetail}\r\n{_lapsResult.Note}";
    }

    private void RevealLapsPassword(bool copy)
    {
        if (!copy && !_lapsPassword.UseSystemPasswordChar)
        {
            _lapsPassword.Clear();
            _lapsPassword.UseSystemPasswordChar = true;
            _lapsReveal.Text = "Reveal password";
            return;
        }
        if (_lapsResult is null || _lapsRows.SelectedItems.Count != 1 ||
            _lapsRows.SelectedItems[0].Tag is not LapsPasswordEntry { HasPassword: true } row) return;
        var action = copy ? "CopyLapsPassword" : "RevealLapsPassword";
        var context = AuthorizeLapsAccess(action, _lapsResult.ComputerName, _lapsResourceId, _lapsAuditSource);
        if (context is null) return;
        WriteRecoveryAudit(action, context, computerName: _lapsResult.ComputerName, recoveryId: _lapsResourceId,
            source: _lapsAuditSource, details: $"Attribute={row.Attribute}; Account={row.AccountName}; History={row.IsHistory}; Updated={row.UpdatedAtUtc:O}");
        if (copy)
        {
            try { CopyKeyWithAutoClear(row.CopyPassword()); }
            catch { UiStyle.SetStatus(_lapsStatus, "The password could not be copied to the clipboard.", UiStatusKind.Warning); }
        }
        else
        {
            _lapsPassword.Text = row.CopyPassword();
            _lapsPassword.UseSystemPasswordChar = false;
            _lapsReveal.Text = "Hide password";
        }
    }

    private void ClearLapsResult()
    {
        _lapsGeneration++;
        _lapsReadCancellation?.Cancel();
        _lapsClearTimer?.Stop();
        _lapsClearTimer?.Dispose();
        _lapsClearTimer = null;
        if (_clipboardRecoveryKey is { } clipboard && _lapsResult?.Entries.Any(x => x.PasswordEquals(clipboard)) == true)
            ClearTrackedRecoveryClipboard();
        _lapsPassword.Clear();
        _lapsPassword.UseSystemPasswordChar = true;
        _lapsRows.Items.Clear();
        _lapsDetails.Clear();
        _lapsDiagnostics.Clear();
        _lapsReveal.Text = "Reveal password";
        _lapsReveal.Enabled = _lapsCopy.Enabled = false;
        _lapsResult?.Dispose();
        _lapsResult = null;
        _lapsResourceId = _lapsAuditSource = string.Empty;
        if (!IsDisposed)
            UiStyle.SetStatus(_lapsStatus, "Passwords cleared. Enter a computer and click Read LAPS.", UiStatusKind.Neutral);
    }
}
