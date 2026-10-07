using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class IntuneSetupWizardDialog : DpiAwareForm
{
    private readonly CloudAuthConfig _existingConfig;
    private readonly Func<DeviceCodeInfo, Task> _showDeviceCode;

    private readonly Label _stepCaption = new();
    private readonly Label _stepDescription = new();
    private readonly Panel _pageHost = new();
    private readonly TextBox _tenant = new();
    private readonly CheckBox _bitLocker = new();
    private readonly CheckBox _devices = new();
    private readonly CheckBox _intuneRotation = new();
    private readonly CheckBox _laps = new();
    private readonly CheckBox _certificate = new();
    private readonly RichTextBox _review = new();
    private readonly UiStatusLabel _status = new();
    private readonly ProgressBar _progress = new();
    private readonly UiDiagnosticPanel _diagnostics = new();
    private readonly Button _back =
        UiStyle.CreateActionButton("Back");
    private readonly Button _next =
        UiStyle.CreateActionButton("Next");
    private readonly Button _cancel =
        UiStyle.CreateActionButton(
            "Cancel",
            DialogResult.Cancel);

    private readonly Control[] _pages;
    private CancellationTokenSource? _cancellation;
    private int _step;
    private bool _running;
    private bool _completed;

    public EntraSetupResult? SetupResult { get; private set; }

    public IntuneSetupWizardDialog(
        CloudAuthConfig existingConfig,
        Func<DeviceCodeInfo, Task> showDeviceCode)
    {
        _existingConfig =
            existingConfig ??
            new CloudAuthConfig();
        _showDeviceCode =
            showDeviceCode ??
            throw new ArgumentNullException(
                nameof(showDeviceCode));

        Text =
            "Intune / Entra Setup Wizard";
        StartPosition =
            FormStartPosition.CenterParent;
        ClientSize =
            new Size(
                920,
                720);
        MinimumSize =
            new Size(
                720,
                600);
        Font =
            UiStyle.BodyFont;
        AutoScroll =
            true;

        var root =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Fill,
                Padding =
                    new Padding(
                        UiStyle.PagePadding),
                ColumnCount =
                    1,
                RowCount =
                    4
            };
        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        Controls.Add(
            root);

        _stepCaption.AutoSize =
            true;
        _stepCaption.Font =
            UiStyle.CreateDialogTitleFont();
        _stepCaption.Margin =
            new Padding(
                0,
                0,
                0,
                UiStyle.ControlGap);
        root.Controls.Add(
            _stepCaption,
            0,
            0);

        _stepDescription.AutoSize =
            true;
        _stepDescription.MaximumSize =
            new Size(
                850,
                0);
        _stepDescription.Margin =
            new Padding(
                0,
                0,
                0,
                UiStyle.SectionGap);
        root.Controls.Add(
            _stepDescription,
            0,
            1);

        _pageHost.Dock =
            DockStyle.Fill;
        _pageHost.AutoScroll =
            true;
        root.Controls.Add(
            _pageHost,
            0,
            2);

        var footer =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Fill,
                AutoSize =
                    true,
                ColumnCount =
                    2,
                Margin =
                    new Padding(
                        0,
                        UiStyle.SectionGap,
                        0,
                        0)
            };
        footer.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        footer.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        var navigation =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Fill,
                AutoSize =
                    true,
                WrapContents =
                    true,
                FlowDirection =
                    FlowDirection.RightToLeft
            };
        navigation.Controls.Add(
            _cancel);
        navigation.Controls.Add(
            _next);
        navigation.Controls.Add(
            _back);
        footer.Controls.Add(
            navigation,
            1,
            0);
        root.Controls.Add(
            footer,
            0,
            3);

        AcceptButton =
            _next;
        CancelButton =
            _cancel;

        _pages =
        [
            BuildWelcomePage(),
            BuildCapabilitiesPage(),
            BuildReviewPage()
        ];

        _back.Click +=
            (_, _) =>
                MoveBack();
        _next.Click +=
            async (_, _) =>
                await MoveNextAsync();
        _cancel.Click +=
            (_, _) =>
                CancelOrClose();

        FormClosing +=
            (_, e) =>
            {
                if (!_running)
                    return;

                e.Cancel =
                    true;
                _cancellation?.Cancel();
                UiStyle.SetStatus(
                    _status,
                    "Cancelling setup...",
                    UiStatusKind.Busy);
            };

        ShowStep(
            0);
    }

    private Control BuildWelcomePage()
    {
        var page =
            CreatePage();

        page.Controls.Add(
            CreateHeading(
                "What this wizard configures"));

        page.Controls.Add(
            CreateBody(
                "BitKeyBridge will create or repair its Microsoft Entra App Registration and Enterprise Application, grant the required Microsoft Graph permissions with tenant-wide admin consent, create or reuse a LocalMachine certificate, and verify app-only Microsoft Graph access. No administrator password is stored."));

        var admin =
            SecurityContext.IsAdministrator();

        var status =
            new UiStatusLabel();
        UiStyle.ConfigureStatusLabel(
            status);
        status.AccessibleName =
            "Wizard administrator status";
        UiStyle.SetStatus(
            status,
            admin
                ? "Administrator rights are available. The local certificate can be created and configured."
                : "Administrator rights are required. Restart BitKeyBridge elevated before running this wizard.",
            admin
                ? UiStatusKind.Success
                : UiStatusKind.Error);
        page.Controls.Add(
            status);

        page.Controls.Add(
            CreateBody(
                "During setup, Microsoft Device Code authentication will open automatically. Sign in with an Entra administrator account that can create applications and grant the requested Microsoft Graph permissions."));

        return page;
    }

    private Control BuildCapabilitiesPage()
    {
        var page =
            CreatePage();

        page.Controls.Add(
            CreateHeading(
                "Tenant and capabilities"));

        var tenantLabel =
            CreateBody(
                "Tenant ID or verified domain:");
        page.Controls.Add(
            tenantLabel);

        _tenant.Dock =
            DockStyle.Top;
        _tenant.Text =
            string.IsNullOrWhiteSpace(
                _existingConfig.TenantId)
                ? "organizations"
                : _existingConfig.TenantId;
        _tenant.Margin =
            new Padding(
                0,
                0,
                0,
                UiStyle.SectionGap);
        page.Controls.Add(
            _tenant);

        page.Controls.Add(
            CreateBody(
                "Required capabilities"));

        ConfigureRequiredCapability(
            _bitLocker,
            "BitLocker recovery metadata and recovery-key read (BitlockerKey.Read.All)");
        ConfigureRequiredCapability(
            _devices,
            "Microsoft Entra device lookup (Device.Read.All)");
        ConfigureRequiredCapability(
            _intuneRotation,
            "Microsoft Intune managed-device access and BitLocker key rotation (DeviceManagementManagedDevices.ReadWrite.All)");
        ConfigureRequiredCapability(
            _certificate,
            "Certificate authentication for unattended/service operation");

        page.Controls.Add(
            _bitLocker);
        page.Controls.Add(
            _devices);
        page.Controls.Add(
            _intuneRotation);
        page.Controls.Add(
            _certificate);

        _laps.Text =
            "Also configure Entra LAPS read access (DeviceLocalCredential.Read.All)";
        _laps.AutoSize =
            true;
        _laps.Checked =
            _existingConfig.EnableLapsPermissions;
        _laps.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        page.Controls.Add(
            _laps);

        page.Controls.Add(
            CreateBody(
                "Entra LAPS is optional. The wizard does not retrieve any LAPS password while configuring permissions. Per-device access can be checked later from the LAPS workspace."));

        return page;
    }

    private Control BuildReviewPage()
    {
        var page =
            CreatePage();

        page.Controls.Add(
            CreateHeading(
                "Review, configure and verify"));

        _review.ReadOnly =
            true;
        _review.Dock =
            DockStyle.Top;
        _review.MinimumSize =
            new Size(
                0,
                210);
        _review.Height =
            240;
        _review.DetectUrls =
            false;
        _review.BackColor =
            SystemColors.Window;
        _review.Margin =
            new Padding(
                0,
                0,
                0,
                UiStyle.ControlGap);
        page.Controls.Add(
            _review);

        UiStyle.ConfigureStatusLabel(
            _status);
        _status.AccessibleName =
            "Intune setup wizard status";
        UiStyle.SetStatus(
            _status,
            "Review the settings, then choose Configure & Verify.",
            UiStatusKind.Neutral);
        page.Controls.Add(
            _status);

        _progress.Dock =
            DockStyle.Top;
        _progress.Style =
            ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed =
            25;
        _progress.Visible =
            false;
        _progress.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        page.Controls.Add(
            _progress);

        page.Controls.Add(
            _diagnostics);

        return page;
    }

    private static FlowLayoutPanel CreatePage() =>
        new()
        {
            Dock =
                DockStyle.Top,
            AutoSize =
                true,
            AutoSizeMode =
                AutoSizeMode.GrowAndShrink,
            FlowDirection =
                FlowDirection.TopDown,
            WrapContents =
                false,
            Padding =
                new Padding(
                    0),
            Margin =
                Padding.Empty
        };

    private static Label CreateHeading(
        string text) =>
        new()
        {
            Text =
                text,
            AutoSize =
                true,
            Font =
                UiStyle.CreateSectionTitleFont(),
            Margin =
                new Padding(
                    0,
                    0,
                    0,
                    UiStyle.ControlGap)
        };

    private static Label CreateBody(
        string text) =>
        new()
        {
            Text =
                text,
            AutoSize =
                true,
            MaximumSize =
                new Size(
                    820,
                    0),
            Margin =
                new Padding(
                    0,
                    0,
                    0,
                    UiStyle.ControlGap)
        };

    private static void ConfigureRequiredCapability(
        CheckBox checkBox,
        string text)
    {
        checkBox.Text =
            text;
        checkBox.AutoSize =
            true;
        checkBox.Checked =
            true;
        checkBox.Enabled =
            false;
        checkBox.Margin =
            new Padding(
                0,
                2,
                0,
                2);
    }

    private void ShowStep(
        int step)
    {
        _step =
            Math.Clamp(
                step,
                0,
                _pages.Length - 1);

        _pageHost.Controls.Clear();
        var page =
            _pages[_step];
        page.Dock =
            DockStyle.Top;
        _pageHost.Controls.Add(
            page);

        (_stepCaption.Text, _stepDescription.Text) =
            _step switch
            {
                0 =>
                    (
                        "Step 1 of 3 — Prerequisites",
                        "Confirm the local and Entra requirements before changing tenant configuration."),
                1 =>
                    (
                        "Step 2 of 3 — Capabilities",
                        "Choose the tenant and optional Entra LAPS capability. Core BitLocker/Intune capabilities are required by BitKeyBridge."),
                _ =>
                    (
                        "Step 3 of 3 — Configure & verify",
                        "Review the exact permissions, run the native setup, then verify certificate authentication without rotating any device key.")
            };

        if (_step == 2)
            UpdateReview();

        UpdateNavigation();
    }

    private void UpdateReview()
    {
        _review.Text =
            "Tenant: " +
            _tenant.Text.Trim() +
            Environment.NewLine +
            Environment.NewLine +
            "Microsoft Graph permissions:" +
            Environment.NewLine +
            "  • BitlockerKey.Read.All" +
            Environment.NewLine +
            "  • Device.Read.All" +
            Environment.NewLine +
            "  • DeviceManagementManagedDevices.ReadWrite.All" +
            (_laps.Checked
                ? Environment.NewLine +
                  "  • DeviceLocalCredential.Read.All"
                : string.Empty) +
            Environment.NewLine +
            Environment.NewLine +
            "Setup actions:" +
            Environment.NewLine +
            "  • Create or repair App Registration" +
            Environment.NewLine +
            "  • Create or repair Enterprise Application" +
            Environment.NewLine +
            "  • Grant application permissions and tenant-wide delegated admin consent" +
            Environment.NewLine +
            "  • Create/reuse a LocalMachine certificate and upload its public key" +
            Environment.NewLine +
            "  • Save only tenant/client/certificate metadata" +
            Environment.NewLine +
            "  • Test BitLocker metadata and Intune managed-device read access" +
            Environment.NewLine +
            Environment.NewLine +
            "The wizard does not rotate a BitLocker key and does not retrieve a LAPS password.";
    }

    private async Task MoveNextAsync()
    {
        if (_running)
            return;

        if (_completed)
        {
            DialogResult =
                DialogResult.OK;
            Close();
            return;
        }

        if (_step == 0)
        {
            if (!SecurityContext.IsAdministrator())
                return;

            ShowStep(
                1);
            return;
        }

        if (_step == 1)
        {
            if (string.IsNullOrWhiteSpace(
                    _tenant.Text))
            {
                _tenant.Focus();
                return;
            }

            ShowStep(
                2);
            return;
        }

        await RunSetupAsync();
    }

    private void MoveBack()
    {
        if (_running ||
            _step <= 0)
        {
            return;
        }

        ShowStep(
            _step - 1);
    }

    private void CancelOrClose()
    {
        if (_running)
        {
            _cancel.Enabled =
                false;
            _cancellation?.Cancel();
            UiStyle.SetStatus(
                _status,
                "Cancelling setup...",
                UiStatusKind.Busy);
            return;
        }

        DialogResult =
            DialogResult.Cancel;
        Close();
    }

    private void UpdateNavigation()
    {
        _back.Enabled =
            !_running &&
            !_completed &&
            _step > 0;

        _next.Enabled =
            !_running &&
            (_step != 0 ||
             SecurityContext.IsAdministrator());

        _next.Text =
            _completed
                ? "Finish"
                : _step == 2
                    ? "Configure & Verify"
                    : "Next";

        _cancel.Text =
            _running
                ? "Cancel operation"
                : "Cancel";
        _cancel.Enabled =
            true;
    }

    private static HashSet<string> ReadApplicationRoles(
        string accessToken)
    {
        var parts =
            accessToken.Split(
                '.');

        if (parts.Length < 2)
        {
            throw new InvalidOperationException(
                "Microsoft Graph returned an invalid access token.");
        }

        var payload =
            parts[1]
                .Replace(
                    '-',
                    '+')
                .Replace(
                    '_',
                    '/');

        payload =
            payload.PadRight(
                payload.Length +
                ((4 - payload.Length % 4) % 4),
                '=');

        var json =
            Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    payload));

        using var document =
            JsonDocument.Parse(
                json);

        var roles =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.TryGetProperty(
                "roles",
                out var roleElement) &&
            roleElement.ValueKind ==
                JsonValueKind.Array)
        {
            foreach (var role in
                     roleElement.EnumerateArray())
            {
                var value =
                    role.GetString();

                if (!string.IsNullOrWhiteSpace(
                        value))
                {
                    roles.Add(
                        value);
                }
            }
        }

        return roles;
    }

    private static void RequireRole(
        IReadOnlySet<string> roles,
        string role)
    {
        if (!roles.Contains(
                role))
        {
            throw new InvalidOperationException(
                $"Certificate authentication succeeded, but the access token does not contain the required Microsoft Graph application role '{role}'. Verify admin consent and rerun the wizard.");
        }
    }

    private async Task RunSetupAsync()
    {
        using var cancellation =
            new CancellationTokenSource();
        _cancellation =
            cancellation;
        _running =
            true;
        _diagnostics.Clear();
        _progress.Visible =
            true;
        UpdateNavigation();

        try
        {
            UiStyle.SetStatus(
                _status,
                "Starting native Entra / Intune setup...",
                UiStatusKind.Busy);

            using var setup =
                new EntraSetupService();

            var progress =
                new Progress<string>(
                    message =>
                        UiStyle.SetStatus(
                            _status,
                            message,
                            UiStatusKind.Busy));

            SetupResult =
                await setup.RunAsync(
                    _tenant.Text.Trim(),
                    _existingConfig.BootstrapClientId,
                    "BitKeyBridge",
                    _existingConfig,
                    _showDeviceCode,
                    progress,
                    ct:
                        cancellation.Token,
                    includeLapsPermissions:
                        _laps.Checked);

            cancellation.Token
                .ThrowIfCancellationRequested();

            UiStyle.SetStatus(
                _status,
                "Configuration written. Verifying certificate authentication...",
                UiStatusKind.Busy);

            using var graph =
                new CloudGraphService();

            var token =
                await graph.AcquireCertificateTokenAsync(
                    SetupResult.TenantId,
                    SetupResult.ClientId,
                    SetupResult.CertificateThumbprint,
                    cancellation.Token);

            var grantedRoles =
                ReadApplicationRoles(
                    token.AccessToken);

            RequireRole(
                grantedRoles,
                "BitlockerKey.Read.All");
            RequireRole(
                grantedRoles,
                "Device.Read.All");
            RequireRole(
                grantedRoles,
                "DeviceManagementManagedDevices.ReadWrite.All");

            if (_laps.Checked)
            {
                RequireRole(
                    grantedRoles,
                    "DeviceLocalCredential.Read.All");
            }

            var recoveryObjects =
                await graph.TestAccessAsync(
                    token.AccessToken,
                    cancellation.Token);

            _ =
                await graph.SearchManagedDevicesAsync(
                    token.AccessToken,
                    string.Empty,
                    1,
                    cancellation.Token);

            cancellation.Token
                .ThrowIfCancellationRequested();

            _completed =
                true;

            UiStyle.SetStatus(
                _status,
                "Setup and non-destructive Graph verification completed successfully.",
                UiStatusKind.Success);

            _diagnostics.ShowMessage(
                "Intune / Entra setup completed.",
                "Tenant: " +
                SetupResult.TenantId +
                Environment.NewLine +
                "Client ID: " +
                SetupResult.ClientId +
                Environment.NewLine +
                "Certificate: " +
                SetupResult.CertificateThumbprint +
                Environment.NewLine +
                "Certificate expires: " +
                SetupResult.CertificateNotAfter.ToString(
                    "yyyy-MM-dd") +
                Environment.NewLine +
                "Verified app-only roles: " +
                string.Join(
                    ", ",
                    grantedRoles
                        .OrderBy(
                            role =>
                                role,
                            StringComparer.OrdinalIgnoreCase)) +
                Environment.NewLine +
                "BitLocker metadata objects visible during test: " +
                recoveryObjects +
                Environment.NewLine +
                "Intune managed-device endpoint: accessible" +
                Environment.NewLine +
                (_laps.Checked
                    ? "Entra LAPS app role: verified. Use LAPS > Check access for per-device metadata/role validation."
                    : "Entra LAPS permission: not requested."));
        }
        catch (OperationCanceledException)
        {
            UiStyle.SetStatus(
                _status,
                "Setup canceled. Re-running the wizard is safe and repairs partially completed configuration.",
                UiStatusKind.Warning);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _status,
                "Setup failed. Review diagnostics below; the wizard can be run again to repair configuration.",
                UiStatusKind.Error);

            _diagnostics.ShowError(
                "Intune / Entra setup failed.",
                "IntuneSetupWizard",
                ex,
                ("Tenant", _tenant.Text.Trim()),
                ("IncludeLaps", _laps.Checked.ToString()));
        }
        finally
        {
            if (ReferenceEquals(
                    _cancellation,
                    cancellation))
            {
                _cancellation =
                    null;
            }

            _running =
                false;
            _progress.Visible =
                false;
            UpdateNavigation();
        }
    }
}
