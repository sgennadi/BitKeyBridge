namespace BitKeyBridge;

public sealed partial class MainForm
{
    private bool _startupConnectionWizardShown;

    /// <summary>
    /// Called once per interactive launch, after the updater completes.
    /// No AD credentials or Graph scopes are requested until the user chooses.
    /// </summary>
    private async Task ShowStartupConnectionWizardAsync()
    {
        if (_startupConnectionWizardShown || _layoutSelfTest || IsDisposed)
            return;

        _startupConnectionWizardShown = true;
        RestoreRecoveryUiState();
        RefreshHomeConnectionUi();
        UpdateStartConnectionSourceControls();

        using var dialog = new StartupConnectionWizardDialog(SelectedStartSource);
        var accepted = dialog.ShowDialog(this) == DialogResult.OK;
        if (IsDisposed)
            return;

        var source = accepted ? dialog.SelectedSource : StartConnectionPolicy.Choose;
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

        // Changing source invalidates previous source-specific selections,
        // session secrets and in-flight searches before authentication.
        await OnStartConnectionSourceSelectedAsync(openCloudWizardIfNeeded: false);
        if (source == StartConnectionPolicy.Choose || IsDisposed)
        {
            UiStyle.SetStatus(_homeCloudConnectionStatus,
                "No connection attempted. Select AD or Microsoft 365 on Start when ready.",
                UiStatusKind.Neutral);
            return;
        }

        if (StartConnectionPolicy.NeedsActiveDirectory(source))
        {
            var anotherAdAccount = dialog.UseOtherAdAccount;
            _homeUseOtherAccount.Checked = anotherAdAccount;
            _homeUseWindowsIdentity.Checked = !anotherAdAccount;
            _adExplicitCredentials.Checked = anotherAdAccount;
            UpdateDirectoryConnectionUi();

            await TestDirectoryConnectionAsync(
                promptForOu: false,
                promptForSessionCredentials: anotherAdAccount,
                forceManualCredentials: anotherAdAccount);
            if (IsDisposed)
                return;
        }

        if (StartConnectionPolicy.NeedsMicrosoftCloud(source))
        {
            // An ordinary helpdesk user can authenticate using delegated MFA.
            // If tenant/client are missing, this opens the safe onboarding
            // wizard rather than the privileged app-registration setup.
            await ConnectStartCloudAsync();
        }

        if (!IsDisposed)
        {
            UpdateStartConnectionSourceControls();
            RefreshHomeConnectionUi();
        }
    }
}
