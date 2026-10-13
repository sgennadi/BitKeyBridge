namespace BitKeyBridge;

public sealed partial class MainForm
{
    private bool _startupConnectionWizardShown;
    private bool _connectionWizardOpening;
    private bool _lastAdConnectionFailed;
    private bool _adRetryWizardAutoOffered;

    /// <summary>
    /// On every interactive launch present the source chooser after the updater.
    /// The same wizard can be reopened any time without restarting BitKeyBridge.
    /// </summary>
    private async Task ShowStartupConnectionWizardAsync()
    {
        if (_startupConnectionWizardShown || _layoutSelfTest || IsDisposed)
            return;

        _startupConnectionWizardShown = true;
        RestoreRecoveryUiState();
        RefreshHomeConnectionUi();
        UpdateStartConnectionSourceControls();
        await ShowConnectionWizardAsync();
    }

    private async Task ShowConnectionWizardAsync(bool preferOtherAdAccount = false)
    {
        if (_connectionWizardOpening || _directoryConnecting ||
            _cloudConnecting || IsDisposed)
            return;

        _connectionWizardOpening = true;
        UpdateStartConnectionSourceControls();
        try
        {
            using var dialog = new StartupConnectionWizardDialog(
                SelectedStartSource, preferOtherAdAccount);
            if (dialog.ShowDialog(this) != DialogResult.OK || IsDisposed)
                return;

            var source = dialog.SelectedSource;
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

            // Switching source cancels old searches and erases all secrets.
            await OnStartConnectionSourceSelectedAsync(openCloudWizardIfNeeded: false);
            if (source == StartConnectionPolicy.Choose || IsDisposed)
            {
                UiStyle.SetStatus(_homeCloudConnectionStatus,
                    "Connection postponed. Select AD or Microsoft 365 when ready.",
                    UiStatusKind.Neutral);
                return;
            }

            if (StartConnectionPolicy.NeedsActiveDirectory(source))
            {
                var anotherAccount = dialog.UseOtherAdAccount;
                _homeUseOtherAccount.Checked = anotherAccount;
                _homeUseWindowsIdentity.Checked = !anotherAccount;
                _adExplicitCredentials.Checked = anotherAccount;
                UpdateDirectoryConnectionUi();

                // The explicit account path always asks for a FRESH password.
                await TestDirectoryConnectionAsync(
                    promptForOu: false,
                    promptForSessionCredentials: anotherAccount,
                    forceManualCredentials: anotherAccount);
                if (IsDisposed)
                    return;
            }

            if (StartConnectionPolicy.NeedsMicrosoftCloud(source))
            {
                // Delegated Device Code + MFA, using the existing consent and
                // Graph permission checks. No silent admin elevation.
                await ConnectStartCloudAsync();
            }
        }
        finally
        {
            _connectionWizardOpening = false;
            if (!IsDisposed)
            {
                UpdateStartConnectionSourceControls();
                RefreshHomeConnectionUi();
            }
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
        BeginInvoke(new MethodInvoker(async () =>
        {
            if (!IsDisposed && !_directoryConnected && !_directoryConnecting)
                await ShowConnectionWizardAsync(preferOtherAdAccount: true);
        }));
    }

    private static bool IsAdLogonFailure(Exception error)
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
