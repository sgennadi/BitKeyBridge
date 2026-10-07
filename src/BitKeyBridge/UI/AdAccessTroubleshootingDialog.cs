namespace BitKeyBridge;

public sealed class AdAccessTroubleshootingDialog : DpiAwareForm
{
    private readonly TextBox _ou = new();
    private readonly TextBox _principal = new();
    private readonly RichTextBox _commands = new();
    private readonly UiStatusLabel _status = new();
    private readonly Button _copy =
        UiStyle.CreateActionButton(
            "Copy guidance");
    private readonly Button _close =
        UiStyle.CreateActionButton(
            "Close",
            DialogResult.Cancel);

    public AdAccessTroubleshootingDialog(
        string? ouDistinguishedName,
        string? principal,
        string? context = null)
    {
        Text =
            "AD Access Troubleshooting";
        StartPosition =
            FormStartPosition.CenterParent;
        ClientSize =
            new Size(
                980,
                820);
        MinimumSize =
            new Size(
                760,
                620);
        Font =
            UiStyle.BodyFont;
        AutoScroll =
            true;

        var root =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Fill,
                AutoScroll =
                    true,
                Padding =
                    new Padding(
                        UiStyle.PagePadding),
                ColumnCount =
                    1,
                RowCount =
                    8
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
                SizeType.AutoSize));
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
        root.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        Controls.Add(
            root);

        var title =
            new Label
            {
                Text =
                    "Scoped Active Directory access",
                Font =
                    UiStyle.CreateDialogTitleFont(),
                AutoSize =
                    true,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };
        root.Controls.Add(
            title,
            0,
            0);

        var intro =
            new Label
            {
                Text =
                    "Generate narrowly scoped delegation examples for BitLocker, Windows LAPS and Legacy LAPS. " +
                    "Prefer a dedicated AD security group and the smallest computer OU. " +
                    "These are administrator-side commands for AD configuration; BitKeyBridge never runs PowerShell as part of its runtime.",
                AutoSize =
                    true,
                MaximumSize =
                    new Size(
                        910,
                        0),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            };
        root.Controls.Add(
            intro,
            0,
            1);

        var fields =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    2,
                RowCount =
                    2,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            };
        fields.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        fields.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        fields.Controls.Add(
            CreateFieldLabel(
                "Computer OU DN:"),
            0,
            0);
        _ou.Dock =
            DockStyle.Fill;
        _ou.Text =
            ouDistinguishedName ??
            string.Empty;
        _ou.PlaceholderText =
            "OU=Workstations,DC=example,DC=com";
        fields.Controls.Add(
            _ou,
            1,
            0);

        fields.Controls.Add(
            CreateFieldLabel(
                "User / group:"),
            0,
            1);
        _principal.Dock =
            DockStyle.Fill;
        _principal.Text =
            principal ??
            string.Empty;
        _principal.PlaceholderText =
            @"DOMAIN\BitKeyBridge-Recovery-Readers";
        fields.Controls.Add(
            _principal,
            1,
            1);

        root.Controls.Add(
            fields,
            0,
            2);

        var contextLabel =
            new Label
            {
                Text =
                    string.IsNullOrWhiteSpace(
                        context)
                        ? "Context: general BitLocker / LAPS access troubleshooting."
                        : "Context: " +
                          context,
                AutoSize =
                    true,
                MaximumSize =
                    new Size(
                        910,
                        0),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };
        root.Controls.Add(
            contextLabel,
            0,
            3);

        var warning =
            new UiStatusLabel();
        UiStyle.ConfigureStatusLabel(
            warning);
        warning.AccessibleName =
            "AD access troubleshooting warning";
        UiStyle.SetStatus(
            warning,
            "Windows LAPS read ACL, encrypted-password decryption, and password-history retention are separate controls. " +
            "Set-LapsADReadPasswordPermission alone does not grant DPAPI-NG decrypt rights.",
            UiStatusKind.Warning);
        root.Controls.Add(
            warning,
            0,
            4);

        _commands.ReadOnly =
            true;
        _commands.Dock =
            DockStyle.Fill;
        _commands.DetectUrls =
            false;
        _commands.WordWrap =
            false;
        _commands.BackColor =
            SystemColors.Window;
        _commands.MinimumSize =
            new Size(
                0,
                320);
        root.Controls.Add(
            _commands,
            0,
            5);

        UiStyle.ConfigureStatusLabel(
            _status);
        _status.AccessibleName =
            "AD access guidance status";
        UiStyle.SetStatus(
            _status,
            "Enter the target OU DN and the delegated AD user/group.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _status,
            0,
            6);

        var actions =
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
        actions.Controls.Add(
            _close);
        actions.Controls.Add(
            _copy);
        root.Controls.Add(
            actions,
            0,
            7);

        AcceptButton =
            _copy;
        CancelButton =
            _close;

        _ou.TextChanged +=
            (_, _) =>
                RefreshGuidance();
        _principal.TextChanged +=
            (_, _) =>
                RefreshGuidance();
        _copy.Click +=
            (_, _) =>
                CopyGuidance();

        RefreshGuidance();
    }

    private static Label CreateFieldLabel(
        string text) =>
        new()
        {
            Text =
                text,
            AutoSize =
                true,
            Anchor =
                AnchorStyles.Left,
            Margin =
                new Padding(
                    0,
                    7,
                    UiStyle.SectionGap,
                    4)
        };

    private void RefreshGuidance()
    {
        var ou =
            _ou.Text.Trim();
        var principal =
            _principal.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                ou) ||
            string.IsNullOrWhiteSpace(
                principal))
        {
            _commands.Text =
                BuildGuidance(
                    string.IsNullOrWhiteSpace(
                        ou)
                        ? "OU=Workstations,DC=example,DC=com"
                        : ou,
                    string.IsNullOrWhiteSpace(
                        principal)
                        ? @"DOMAIN\BitKeyBridge-Recovery-Readers"
                        : principal);

            UiStyle.SetStatus(
                _status,
                "Example values are shown until both OU DN and user/group are filled in.",
                UiStatusKind.Warning);
            return;
        }

        _commands.Text =
            BuildGuidance(
                ou,
                principal);

        UiStyle.SetStatus(
            _status,
            "Guidance generated. Review the scope and group before an AD administrator applies any command.",
            UiStatusKind.Success);
    }

    private void CopyGuidance()
    {
        try
        {
            Clipboard.SetText(
                _commands.Text);

            UiStyle.SetStatus(
                _status,
                "Guidance copied. It contains no recovery password.",
                UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _status,
                "Could not copy guidance: " +
                DiagnosticRedaction.Sanitize(
                    ex.Message),
                UiStatusKind.Error);
        }
    }

    internal static string BuildGuidance(
        string ou,
        string principal)
    {
        var safeOu =
            ou.Replace(
                """,
                """");
        var safePrincipal =
            principal.Replace(
                """,
                """");

        return
            "BITLOCKER — AD DS recovery password" +
            Environment.NewLine +
            "If recovery metadata is visible but the 48-digit password is blank/Access denied:" +
            Environment.NewLine +
            $"dsacls \"{safeOu}\" /I:S /G \"{safePrincipal}:CA;msFVE-RecoveryPassword;msFVE-RecoveryInformation\"" +
            Environment.NewLine +
            Environment.NewLine +
            "If the identity also cannot enumerate/read the recovery child objects:" +
            Environment.NewLine +
            $"dsacls \"{safeOu}\" /I:S /G \"{safePrincipal}:RP;;msFVE-RecoveryInformation\"" +
            Environment.NewLine +
            Environment.NewLine +
            "Do not grant Full Control or All Extended Rights just to solve recovery-password access." +
            Environment.NewLine +
            Environment.NewLine +
            "WINDOWS LAPS — query permission" +
            Environment.NewLine +
            $"Set-LapsADReadPasswordPermission -Identity \"{safeOu}\" -AllowedPrincipals @(\"{safePrincipal}\")" +
            Environment.NewLine +
            $"Find-LapsADExtendedRights -Identity \"{safeOu}\"" +
            Environment.NewLine +
            Environment.NewLine +
            "WINDOWS LAPS — encrypted current password / history" +
            Environment.NewLine +
            "Read permission is NOT decrypt permission. In the Windows LAPS policy for the target computers configure:" +
            Environment.NewLine +
            "  BackupDirectory = Active Directory" +
            Environment.NewLine +
            "  ADPasswordEncryptionEnabled = Enabled" +
            Environment.NewLine +
            $"  ADPasswordEncryptionPrincipal = {safePrincipal}" +
            Environment.NewLine +
            "  ADEncryptedPasswordHistorySize > 0   (only if history is required)" +
            Environment.NewLine +
            "  ADBackupDSRMPassword = Enabled       (only if DSRM backup is required on DCs)" +
            Environment.NewLine +
            "Changing ADPasswordEncryptionPrincipal does not retroactively re-encrypt existing protected values. " +
            "Apply policy and create a new password backup/rotation on a test computer before validating the new decryptor." +
            Environment.NewLine +
            Environment.NewLine +
            "LEGACY MICROSOFT LAPS — ms-Mcs-AdmPwd" +
            Environment.NewLine +
            $"Set-AdmPwdReadPasswordPermission -Identity \"{safeOu}\" -AllowedPrincipals \"{safePrincipal}\"" +
            Environment.NewLine +
            $"Find-AdmPwdExtendedRights -Identity \"{safeOu}\"" +
            Environment.NewLine +
            "Legacy Microsoft LAPS has no password history." +
            Environment.NewLine +
            Environment.NewLine +
            "ENTRA LAPS" +
            Environment.NewLine +
            "AD ACL commands do not apply. Configure DeviceLocalCredential.Read.All with the Intune / Entra Setup Wizard " +
            "and verify the operator also has an applicable Microsoft Entra role.";
    }
}
