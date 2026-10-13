namespace BitKeyBridge;

/// <summary>
/// Explicit connection choice at every interactive launch. Selecting a source
/// alone cannot retrieve passwords, grant Graph consent, or elevate a user.
/// </summary>
public sealed class StartupConnectionWizardDialog : DpiAwareForm
{
    private readonly RadioButton _ad = new();
    private readonly RadioButton _cloud = new();
    private readonly RadioButton _both = new();
    private readonly RadioButton _later = new();
    private readonly CheckBox _otherAdAccount = new();
    private readonly Label _explanation = new();
    private readonly Button _connect = UiStyle.CreateActionButton("Connect");
    private readonly Button _skip = UiStyle.CreateActionButton("Not now", DialogResult.Cancel);

    public string SelectedSource =>
        _ad.Checked ? StartConnectionPolicy.ActiveDirectory :
        _cloud.Checked ? StartConnectionPolicy.MicrosoftCloud :
        _both.Checked ? StartConnectionPolicy.Both :
        StartConnectionPolicy.Choose;

    public bool UseOtherAdAccount => _otherAdAccount.Checked &&
        StartConnectionPolicy.NeedsActiveDirectory(SelectedSource);

    public StartupConnectionWizardDialog(string? previouslySelectedSource)
    {
        Name = "StartupConnectionWizardDialog";
        Text = "BitKeyBridge - Connect";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(610, 460);

        var root = new TableLayoutPanel
        {
            Name = "StartupConnectionWizardLayout",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(UiStyle.PagePadding)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 5; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "Connect to BitKeyBridge",
            Font = UiStyle.CreatePageTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        root.Controls.Add(title, 0, 0);

        var caption = new Label
        {
            Text = "Choose where this session will read device recovery information. " +
                   "BitLocker and LAPS passwords stay hidden until you explicitly request them.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        UiStyle.ConfigureWrappingLabel(caption);
        root.Controls.Add(caption, 0, 1);

        var options = new FlowLayoutPanel
        {
            Name = "StartupConnectionSources",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty
        };
        foreach (var (control, name, captionText) in new[]
        {
            (_ad, "StartupConnectAD", "Active Directory (on-premises)"),
            (_cloud, "StartupConnectCloud", "Microsoft Entra / Intune (Microsoft 365)"),
            (_both, "StartupConnectBoth", "Both: Active Directory + Microsoft 365"),
            (_later, "StartupConnectLater", "Choose later")
        })
        {
            control.Name = name;
            control.Text = captionText;
            control.AutoSize = true;
            control.Margin = new Padding(0, 0, 0, UiStyle.ControlGap);
            options.Controls.Add(control);
            control.CheckedChanged += (_, _) => UpdateOptions();
        }
        root.Controls.Add(options, 0, 2);

        _otherAdAccount.Name = "StartupOtherAdAccount";
        _otherAdAccount.Text = "Use another AD account (request credentials on Connect)";
        _otherAdAccount.AutoSize = true;
        _otherAdAccount.Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.SectionGap);
        root.Controls.Add(_otherAdAccount, 0, 3);

        _explanation.Name = "StartupConnectionExplanation";
        _explanation.AutoSize = true;
        _explanation.MaximumSize = new Size(710, 0);
        _explanation.Margin = new Padding(0, 0, 0, UiStyle.SectionGap);
        UiStyle.ConfigureWrappingLabel(_explanation);
        root.Controls.Add(_explanation, 0, 4);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Margin = Padding.Empty
        };
        _connect.Name = "StartupConnectionConfirm";
        _skip.Name = "StartupConnectionSkip";
        footer.Controls.Add(_connect);
        footer.Controls.Add(_skip);
        root.Controls.Add(footer, 0, 5);

        Controls.Add(root);
        _connect.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        AcceptButton = _connect;
        CancelButton = _skip;

        switch (StartConnectionPolicy.Normalize(previouslySelectedSource))
        {
            case StartConnectionPolicy.ActiveDirectory: _ad.Checked = true; break;
            case StartConnectionPolicy.MicrosoftCloud: _cloud.Checked = true; break;
            case StartConnectionPolicy.Both: _both.Checked = true; break;
            default: _later.Checked = true; break;
        }
        UpdateOptions();
    }

    private void UpdateOptions()
    {
        var ad = StartConnectionPolicy.NeedsActiveDirectory(SelectedSource);
        _otherAdAccount.Enabled = ad;
        if (!ad)
            _otherAdAccount.Checked = false;

        _connect.Text = SelectedSource == StartConnectionPolicy.Choose
            ? "Continue to Start" : "Connect";

        _explanation.Text = SelectedSource switch
        {
            StartConnectionPolicy.ActiveDirectory =>
                "First try the current Windows account. No Domain Admin membership is needed " +
                "for sign-in; access to recovery secrets depends on delegated AD permissions.",
            StartConnectionPolicy.MicrosoftCloud =>
                "Sign in with your work account using Device Code and MFA. If no approved " +
                "Microsoft Entra application is configured, the cloud setup wizard opens.",
            StartConnectionPolicy.Both =>
                "Connect to AD first, then sign in to Microsoft 365. Each source uses its own " +
                "permissions; cloud application setup may require administrator approval.",
            _ => "No credentials are requested. You can select a connection source on Start later."
        };
    }
}
