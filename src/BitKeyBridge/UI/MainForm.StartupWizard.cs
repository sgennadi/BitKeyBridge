namespace BitKeyBridge;

public sealed partial class MainForm
{
    private bool _startupConnectionWizardShown;
    private bool _connectionWizardOpening;
    private bool _lastAdConnectionFailed;
    private bool _adRetryWizardAutoOffered;
    private bool _adRetryWizardPending;

    /// <summary>
    /// Show the connection chooser at startup unless the current user has
    /// deliberately checked Remember this connection. Existing saved sources
    /// (from older versions) never implicitly enable automatic connection.
    /// </summary>
    private async Task ShowStartupConnectionWizardAsync()
    {
        if (_startupConnectionWizardShown || _layoutSelfTest || IsDisposed)
            return;

        _startupConnectionWizardShown = true;
        RestoreRecoveryUiState();
        RefreshHomeConnectionUi();
        UpdateStartConnectionSourceControls();

        await ShowConnectionWizardAsync(
            automaticallyUseRemembered: StartConnectionPolicy.ShouldAutoConnect(
                _startConnectionPreferences, _config.AutoConnectOnStart));
    }

    private async Task ShowConnectionWizardAsync(
        bool preferOtherAdAccount = false, bool automaticallyUseRemembered = false)
    {
        if (_connectionWizardOpening || _directoryConnecting ||
            _cloudConnecting || IsDisposed)
            return;

        _connectionWizardOpening = true;
        UpdateStartConnectionSourceControls();
        try
        {
            string source;
            bool anotherAccount;
            if (automaticallyUseRemembered)
            {
                // The saved source selects only the connection flow. It NEVER
                // represents a credential, authorization or Graph consent.
                source = StartConnectionPolicy.Normalize(SelectedStartSource);
                anotherAccount = _startConnectionPreferences.PreferOtherAdAccountOnStart &&
                    StartConnectionPolicy.NeedsActiveDirectory(source);
            }
            else
            {
                using var dialog = new StartupConnectionWizardDialog(
                    SelectedStartSource,
                    preferOtherAdAccount ||
                        _startConnectionPreferences.PreferOtherAdAccountOnStart,
                    _startConnectionPreferences.RememberLastConnectionOnStart,
                    _config.AutoConnectOnStart);
                if (dialog.ShowDialog(this) != DialogResult.OK || IsDisposed)
                    return;

                source = dialog.SelectedSource;
                anotherAccount = dialog.UseOtherAdAccount;

                // Store only the source and chosen authentication METHOD.
                // Checking the box explicitly opts this user into startup
                // reconnection; unchecking disables it immediately.
                _startConnectionPreferences.RememberLastConnectionOnStart =
                    dialog.RememberLastConnectionOnStart;
                _startConnectionPreferences.PreferOtherAdAccountOnStart =
                    dialog.RememberLastConnectionOnStart && anotherAccount;

                _initializingStartSource = true;
                try
                {
                    _homeConnectionSource.SelectedIndex = source switch
                    {
                        StartConnectionPolicy.ActiveDirectory => 1,
                        StartConnectionPolicy.MicrosoftCloud => 2,
                        StartConnectionPolicy.Both => 3,
                        _ => 0
                    };
                }
                finally
                {
                    _initializingStartSource = false;
                }

                // Source changes also invalidate old searches and revealed
                // secrets; persist the opt-in with the confirmed choice.
                await OnStartConnectionSourceSelectedAsync(
                    openCloudWizardIfNeeded: false);
            }

            await ConnectSelectedSourceAsync(source, anotherAccount);
        }
        catch (Exception ex) when (automaticallyUseRemembered)
        {
            WindowsEventLogService.TryWrite(
                "BitKeyBridge remembered startup connection failed: " +
                DiagnosticRedaction.Sanitize(ex.Message),
                EventLogSeverity.Warning, 4190, "Connection");
            if (!IsDisposed)
            {
                UiStyle.SetStatus(_homeSearchStatus,
                    "Remembered connection failed. Use Reconnect / Change connection " +
                    "to review the source or account.",
                    UiStatusKind.Warning);
            }
        }
        finally
        {
            _connectionWizardOpening = false;
            if (!IsDisposed)
            {
                UpdateStartConnectionSourceControls();
                RefreshHomeConnectionUi();
                if (_adRetryWizardPending)
                {
                    _adRetryWizardPending = false;
                    QueueAdRetryWizard();
                }
            }
        }
    }

    private async Task ConnectSelectedSourceAsync(string source, bool anotherAccount)
    {
        if (source == StartConnectionPolicy.Choose || IsDisposed)
        {
            if (!IsDisposed)
            {
                UiStyle.SetStatus(_homeCloudConnectionStatus,
                    "Connection postponed. Select AD or Microsoft 365 when ready.",
                    UiStatusKind.Neutral);
            }
            return;
        }

        if (StartConnectionPolicy.NeedsActiveDirectory(source))
        {
            _homeUseOtherAccount.Checked = anotherAccount;
            _homeUseWindowsIdentity.Checked = !anotherAccount;
            _adExplicitCredentials.Checked = anotherAccount;
            UpdateDirectoryConnectionUi();

            // Never reuse an alternate AD password on the next launch.
            // This requests fresh credentials when another account was chosen.
            await TestDirectoryConnectionAsync(
                promptForOu: false,
                promptForSessionCredentials: anotherAccount,
                forceManualCredentials: anotherAccount);
            if (IsDisposed)
                return;
        }

        if (StartConnectionPolicy.NeedsMicrosoftCloud(source))
        {
            // Delegated Device Code + MFA and existing Graph RBAC/consent,
            // not a silent tenant-admin or application-only login.
            await ConnectStartCloudAsync();
        }
    }

    /// <summary>
    /// Offer one immediate retry for an invalid password. Repeated failures
    /// never create an endless modal prompt loop; the Reconnect button remains.
    /// </summary>
    private void ScheduleAdCredentialRetryWizard()
    {
        if (_adRetryWizardAutoOffered || !_startupConnectionWizardShown ||
            _layoutSelfTest || !IsHandleCreated || IsDisposed)
            return;

        _adRetryWizardAutoOffered = true;
        if (_connectionWizardOpening)
        {
            // Both-source wizard can still be completing cloud sign-in.
            // Wait until the original wizard is closed before prompting.
            _adRetryWizardPending = true;
            return;
        }

        QueueAdRetryWizard();
    }

    private void QueueAdRetryWizard()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(new MethodInvoker(async () =>
        {
            if (!IsDisposed && !_directoryConnected && !_directoryConnecting)
                await ShowConnectionWizardAsync(preferOtherAdAccount: true);
        }));
    }

    internal static bool IsAdLogonFailure(Exception error)
    {
        for (Exception? ex = error; ex is not null; ex = ex.InnerException)
        {
            if (ex is System.DirectoryServices.Protocols.LdapException ldap &&
                ldap.ErrorCode == 49)
                return true;

            var hresult = unchecked((uint)ex.HResult);
            if (hresult is 0x8007052Eu or 0x8009030Cu)
                return true;

            if (ex is System.ComponentModel.Win32Exception win32 &&
                win32.NativeErrorCode == 1326)
                return true;

            if (ex.Message.Contains("invalid credentials",
                    StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("user name or password is incorrect",
                    StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("username or password is incorrect",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
