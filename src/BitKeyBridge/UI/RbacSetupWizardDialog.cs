namespace BitKeyBridge;

public sealed class RbacSetupWizardDialog : DpiAwareForm
{
    private readonly AppConfig _source;

    private readonly Label _stepCaption = new();
    private readonly Label _stepDescription = new();
    private readonly Panel _pageHost = new();

    private readonly CheckBox _enableRbac = new();
    private readonly CheckBox _allowLocalAdmins = new();
    private readonly TextBox _readers = new();
    private readonly TextBox _rotators = new();
    private readonly TextBox _administrators = new();
    private readonly CheckBox _addCurrentReader = new();
    private readonly CheckBox _addCurrentRotator = new();
    private readonly CheckBox _addCurrentAdmin = new();

    private readonly RichTextBox _review = new();
    private readonly UiStatusLabel _status = new();
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
    private int _step;

    public bool RbacEnabled { get; private set; }
    public bool AllowLocalAdministrators { get; private set; }
    public List<string> RecoveryReaders { get; private set; } = [];
    public List<string> RotationOperators { get; private set; } = [];
    public List<string> Administrators { get; private set; } = [];

    public RbacSetupWizardDialog(
        AppConfig config)
    {
        _source =
            config ??
            throw new ArgumentNullException(
                nameof(config));

        Text =
            "RBAC Setup Wizard";
        StartPosition =
            FormStartPosition.CenterParent;
        ClientSize =
            new Size(
                920,
                760);
        MinimumSize =
            new Size(
                720,
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
            BuildModePage(),
            BuildRolesPage(),
            BuildReviewPage()
        ];

        _back.Click +=
            (_, _) =>
                MoveBack();
        _next.Click +=
            (_, _) =>
                MoveNext();
        _cancel.Click +=
            (_, _) =>
            {
                DialogResult =
                    DialogResult.Cancel;
                Close();
            };

        ShowStep(
            0);
    }

    private Control BuildModePage()
    {
        var page =
            CreatePage();

        page.Controls.Add(
            CreateHeading(
                "Enable and safety options"));

        page.Controls.Add(
            CreateBody(
                "RBAC controls who may read/reveal/copy BitLocker or LAPS secrets, request Intune BitLocker rotation, and open the BitKeyBridge Administration UI. This wizard does not enable JIT, two-person approval, or SIEM; those remain separate optional Privileged Access controls."));

        _enableRbac.Text =
            "Enable RBAC when I finish this wizard";
        _enableRbac.AutoSize =
            true;
        _enableRbac.Checked =
            _source.RbacEnabled;
        page.Controls.Add(
            _enableRbac);

        _allowLocalAdmins.Text =
            "Allow members of local Administrators to bypass RBAC";
        _allowLocalAdmins.AutoSize =
            true;
        _allowLocalAdmins.Checked =
            _source.RbacAllowLocalAdministrators;
        _allowLocalAdmins.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                UiStyle.ControlGap);
        page.Controls.Add(
            _allowLocalAdmins);

        var current =
            AuthorizationService.CurrentIdentityName();

        var identityStatus =
            new UiStatusLabel();
        UiStyle.ConfigureStatusLabel(
            identityStatus);
        identityStatus.AccessibleName =
            "Current Windows identity";
        UiStyle.SetStatus(
            identityStatus,
            "Current identity: " +
            current,
            UiStatusKind.Neutral);
        page.Controls.Add(
            identityStatus);

        page.Controls.Add(
            CreateBody(
                "Nothing is changed until the last step. If RBAC is currently disabled, leaving the first checkbox clear preserves the existing behavior."));

        return page;
    }

    private Control BuildRolesPage()
    {
        var page =
            CreatePage();

        page.Controls.Add(
            CreateHeading(
                "Assign Windows users and groups"));

        page.Controls.Add(
            CreateBody(
                "Enter DOMAIN\\group, DOMAIN\\user, local account/group, or SID — one per line. Using AD groups is recommended for normal administration."));

        page.Controls.Add(
            BuildRoleEditor(
                "Recovery Readers",
                "Can reveal/copy BitLocker and LAPS recovery secrets.",
                _readers,
                _source.RbacRecoveryReaders,
                _addCurrentReader,
                "Also grant the current identity Recovery Read"));

        page.Controls.Add(
            BuildRoleEditor(
                "Rotation Operators",
                "Can request BitLocker key rotation through Intune. This does not automatically grant Recovery Read.",
                _rotators,
                _source.RbacRotationOperators,
                _addCurrentRotator,
                "Also grant the current identity Intune rotation"));

        page.Controls.Add(
            BuildRoleEditor(
                "BitKeyBridge Administrators",
                "Can open Administration and Health & Audit without relying on local Windows Administrator membership.",
                _administrators,
                _source.RbacAdministrators,
                _addCurrentAdmin,
                "Also grant the current identity BitKeyBridge Administration"));

        return page;
    }

    private Control BuildReviewPage()
    {
        var page =
            CreatePage();

        page.Controls.Add(
            CreateHeading(
                "Validate and apply"));

        _review.ReadOnly =
            true;
        _review.Dock =
            DockStyle.Top;
        _review.MinimumSize =
            new Size(
                0,
                260);
        _review.Height =
            300;
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
            "RBAC wizard validation status";
        UiStyle.SetStatus(
            _status,
            "Review the effective permissions. Finish validates every configured principal before saving.",
            UiStatusKind.Neutral);
        page.Controls.Add(
            _status);

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

    private static GroupBox BuildRoleEditor(
        string title,
        string description,
        TextBox editor,
        IEnumerable<string> values,
        CheckBox addCurrent,
        string addCurrentText)
    {
        var group =
            new GroupBox
            {
                Text =
                    title,
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                AutoSizeMode =
                    AutoSizeMode.GrowAndShrink,
                Padding =
                    new Padding(
                        UiStyle.SectionGap),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };

        var layout =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    1,
                RowCount =
                    3
            };
        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.Controls.Add(
            CreateBody(
                description),
            0,
            0);

        editor.Multiline =
            true;
        editor.ScrollBars =
            ScrollBars.Vertical;
        editor.Dock =
            DockStyle.Top;
        editor.MinimumSize =
            new Size(
                0,
                82);
        editor.Height =
            92;
        editor.Text =
            string.Join(
                Environment.NewLine,
                values);
        layout.Controls.Add(
            editor,
            0,
            1);

        addCurrent.Text =
            addCurrentText;
        addCurrent.AutoSize =
            true;
        addCurrent.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        layout.Controls.Add(
            addCurrent,
            0,
            2);

        group.Controls.Add(
            layout);
        return group;
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
                        "Step 1 of 3 — Policy mode",
                        "Choose whether RBAC will be enabled and whether local Windows Administrators keep the bypass."),
                1 =>
                    (
                        "Step 2 of 3 — Role membership",
                        "Assign AD/local Windows users or groups to BitKeyBridge roles."),
                _ =>
                    (
                        "Step 3 of 3 — Validate & apply",
                        "Preview the resulting access for the current identity, validate every principal, and save only when you choose Finish.")
            };

        if (_step == 2)
            UpdateReview();

        _back.Enabled =
            _step > 0;
        _next.Text =
            _step == 2
                ? "Finish"
                : "Next";
    }

    private void MoveBack()
    {
        if (_step <= 0)
            return;

        ShowStep(
            _step - 1);
    }

    private void MoveNext()
    {
        if (_step < 2)
        {
            ShowStep(
                _step + 1);
            return;
        }

        Apply();
    }

    private void UpdateReview()
    {
        var candidate =
            BuildCandidateConfig();

        var auth =
            new AuthorizationService(
                candidate);
        var read =
            auth.Check(
                BitKeyBridgePermission.RecoveryRead);
        var rotate =
            auth.Check(
                BitKeyBridgePermission.Rotate);
        var admin =
            auth.Check(
                BitKeyBridgePermission.Administrator);

        _review.Text =
            "RBAC enabled: " +
            candidate.RbacEnabled +
            Environment.NewLine +
            "Local Administrators bypass: " +
            candidate.RbacAllowLocalAdministrators +
            Environment.NewLine +
            Environment.NewLine +
            "Recovery Readers:" +
            Environment.NewLine +
            FormatList(
                candidate.RbacRecoveryReaders) +
            Environment.NewLine +
            Environment.NewLine +
            "Rotation Operators:" +
            Environment.NewLine +
            FormatList(
                candidate.RbacRotationOperators) +
            Environment.NewLine +
            Environment.NewLine +
            "BitKeyBridge Administrators:" +
            Environment.NewLine +
            FormatList(
                candidate.RbacAdministrators) +
            Environment.NewLine +
            Environment.NewLine +
            "Current identity: " +
            AuthorizationService.CurrentIdentityName() +
            Environment.NewLine +
            "  Recovery Read: " +
            FormatDecision(
                read) +
            Environment.NewLine +
            "  Intune rotation: " +
            FormatDecision(
                rotate) +
            Environment.NewLine +
            "  Administration UI: " +
            FormatDecision(
                admin);

        var dangerous =
            candidate.RbacEnabled &&
            !candidate.RbacAllowLocalAdministrators &&
            !admin.Allowed;

        UiStyle.SetStatus(
            _status,
            dangerous
                ? "Warning: the current identity would lose BitKeyBridge Administration access after restart. Add an administrator group/user before finishing."
                : candidate.RbacEnabled
                    ? "RBAC will be enabled only after Finish. Current-identity preview is shown above."
                    : "RBAC will remain disabled. Role lists may still be prepared now for later enablement.",
            dangerous
                ? UiStatusKind.Error
                : UiStatusKind.Neutral);
    }

    private AppConfig BuildCandidateConfig()
    {
        var candidate =
            new AppConfig
            {
                RbacEnabled =
                    _enableRbac.Checked,
                RbacAllowLocalAdministrators =
                    _allowLocalAdmins.Checked,
                RbacRecoveryReaders =
                    ParsePrincipals(
                        _readers.Text),
                RbacRotationOperators =
                    ParsePrincipals(
                        _rotators.Text),
                RbacAdministrators =
                    ParsePrincipals(
                        _administrators.Text)
            };

        var current =
            AuthorizationService.CurrentIdentityName();

        AddCurrentIfSelected(
            candidate.RbacRecoveryReaders,
            _addCurrentReader.Checked,
            current);
        AddCurrentIfSelected(
            candidate.RbacRotationOperators,
            _addCurrentRotator.Checked,
            current);
        AddCurrentIfSelected(
            candidate.RbacAdministrators,
            _addCurrentAdmin.Checked,
            current);

        return candidate;
    }

    private void Apply()
    {
        _diagnostics.Clear();

        try
        {
            var candidate =
                BuildCandidateConfig();

            if (candidate.RbacEnabled &&
                !candidate.RbacAllowLocalAdministrators &&
                candidate.RbacRecoveryReaders.Count == 0 &&
                candidate.RbacRotationOperators.Count == 0 &&
                candidate.RbacAdministrators.Count == 0)
            {
                throw new InvalidOperationException(
                    "RBAC would deny all privileged actions. Configure at least one role member or keep the local Administrators bypass enabled.");
            }

            var validation =
                new AuthorizationService(
                    candidate)
                    .ValidateConfiguredPrincipals();

            if (validation.Count > 0)
            {
                throw new InvalidOperationException(
                    "The following Windows principals could not be resolved:" +
                    Environment.NewLine +
                    string.Join(
                        Environment.NewLine,
                        validation));
            }

            var admin =
                new AuthorizationService(
                    candidate)
                    .Check(
                        BitKeyBridgePermission.Administrator);

            if (candidate.RbacEnabled &&
                !candidate.RbacAllowLocalAdministrators &&
                !admin.Allowed)
            {
                throw new InvalidOperationException(
                    "The current identity would lose BitKeyBridge Administration access. Add the current identity or one of its groups to BitKeyBridge Administrators, or keep the local Administrators bypass enabled.");
            }

            RbacEnabled =
                candidate.RbacEnabled;
            AllowLocalAdministrators =
                candidate.RbacAllowLocalAdministrators;
            RecoveryReaders =
                candidate.RbacRecoveryReaders;
            RotationOperators =
                candidate.RbacRotationOperators;
            Administrators =
                candidate.RbacAdministrators;

            DialogResult =
                DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _status,
                "RBAC configuration was not changed. Resolve the validation issue below.",
                UiStatusKind.Error);

            _diagnostics.ShowError(
                "RBAC validation failed.",
                "RbacSetupWizard",
                ex,
                ("CurrentIdentity", AuthorizationService.CurrentIdentityName()),
                ("EnableRbac", _enableRbac.Checked.ToString()),
                ("AllowLocalAdministrators", _allowLocalAdmins.Checked.ToString()));
        }
    }

    private static void AddCurrentIfSelected(
        ICollection<string> list,
        bool selected,
        string current)
    {
        if (!selected ||
            string.IsNullOrWhiteSpace(
                current))
        {
            return;
        }

        if (!list.Contains(
                current,
                StringComparer.OrdinalIgnoreCase))
        {
            list.Add(
                current);
        }
    }

    private static List<string> ParsePrincipals(
        string value) =>
        value
            .Split(
                ['\r', '\n', ';', ','],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string FormatList(
        IReadOnlyCollection<string> values) =>
        values.Count == 0
            ? "  (none)"
            : string.Join(
                Environment.NewLine,
                values.Select(
                    value =>
                        "  • " +
                        value));

    private static string FormatDecision(
        AuthorizationDecision decision) =>
        (decision.Allowed
            ? "Allowed"
            : "Denied") +
        " — " +
        decision.Reason;
}
