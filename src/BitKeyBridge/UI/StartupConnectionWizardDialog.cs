namespace BitKeyBridge;

/// <summary>
/// Explicit connection choice on first launch and whenever the operator has
/// not opted into a remembered source. Choosing one cannot retrieve secrets,
/// grant Graph consent, persist passwords or elevate a user.
/// </summary>
public sealed class StartupConnectionWizardDialog : DpiAwareForm
{
    private readonly RadioButton _ad = new();
    private readonly RadioButton _cloud = new();
    private readonly RadioButton _both = new();
    private readonly RadioButton _later = new();
    private readonly CheckBox _otherAdAccount = new();
    private readonly CheckBox _rememberConnection = new();
    private readonly bool _automaticConnectionsAllowed;
    private readonly Label _explanation = new();
    private readonly Button _connect = UiStyle.CreateActionButton("Connect");
    private readonly Button _skip = UiStyle.CreateActionButton("Cancel", DialogResult.Cancel);

    public string SelectedSource =>
        _ad.Checked ? StartConnectionPolicy.ActiveDirectory :
        _cloud.Checked ? StartConnectionPolicy.MicrosoftCloud :
        _both.Checked ? StartConnectionPolicy.Both :
        StartConnectionPolicy.Choose;

    public bool UseOtherAdAccount => _otherAdAccount.Checked &&
        StartConnectionPolicy.NeedsActiveDirectory(SelectedSource);

    public bool RememberLastConnectionOnStart => _rememberConnection.Checked &&
        SelectedSource != StartConnectionPolicy.Choose;

    public StartupConnectionWizardDialog(string? previouslySelectedSource,
        bool preferOtherAdAccount = false, bool rememberLastConnection = false,
        bool automaticConnectionsAllowed = true)
    {
        _automaticConnectionsAllowed = automaticConnectionsAllowed;
        Name = "StartupConnectionWizardDialog";
        Text = "BitKeyBridge - Connect";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(750, 520);
        MinimumSize = new Size(610, 460);

        var root = new TableLayoutPanel
        {
            Name = "StartupConnectionWizardLayout",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(UiStyle.PagePadding)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 6; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "Choose a connection",
            Font = UiStyle.CreatePageTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        root.Controls.Add(title, 0, 0);

        var caption = new Label
        {
            Name = "StartupConnectionIntro",
            Text = "Select a source for this session. Recovery passwords remain hidden " +
                   "until an authorized technician explicitly requests them.",
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
            (_ad, "StartupConnectAD", "Active Directory"),
            (_cloud, "StartupConnectCloud", "Microsoft 365 (Entra / Intune)"),
            (_both, "StartupConnectBoth", "AD + Microsoft 365"),
            (_later, "StartupConnectLater", "Start without a connection")
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
        _otherAdAccount.Text = "Use another AD account (ask for credentials)";
        _otherAdAccount.AutoSize = true;
        _otherAdAccount.Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.SectionGap);
        root.Controls.Add(_otherAdAccount, 0, 3);

        _rememberConnection.Name = "StartupRememberLastConnection";
        _rememberConnection.AccessibleName =
            "Remember selected connection and reconnect automatically on the next launch";
        _rememberConnection.Text = "Use this source next time (no passwords stored)";
        _rememberConnection.AutoSize = true;
        _rememberConnection.Margin = new Padding(0, 0, 0, UiStyle.SectionGap);
        root.Controls.Add(_rememberConnection, 0, 4);
        _rememberConnection.CheckedChanged += (_, _) => UpdateOptions();

        _explanation.Name = "StartupConnectionExplanation";
        _explanation.AutoSize = true;
        _explanation.MaximumSize = new Size(710, 0);
        _explanation.Margin = new Padding(0, 0, 0, UiStyle.SectionGap);
        UiStyle.ConfigureWrappingLabel(_explanation);
        root.Controls.Add(_explanation, 0, 5);

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
        root.Controls.Add(footer, 0, 6);

        Controls.Add(root);

        // The wizard is a resizable native dialog. Fixed 710/720-pixel text
        // maxima were wider than its actual content area on some 125%-200%
        // DPI and RDP viewports, clipping explanation lines. Constrain only
        // the two static prose labels to the live TableLayout client width
        // and let AutoSize grow rows vertically; never resize the whole form.
        var lastTextWidth = 0;
        void FitProseToDialog()
        {
            if (root.IsDisposed || root.Disposing || root.ClientSize.Width <= 0)
                return;
            var scrollbar = root.VerticalScroll.Visible
                ? SystemInformation.VerticalScrollBarWidth : 0;
            var width = Math.Max(120, root.ClientSize.Width -
                root.Padding.Horizontal - scrollbar -
                Math.Max(caption.Margin.Horizontal, _explanation.Margin.Horizontal) - 2);
            if (width == lastTextWidth)
                return;
            lastTextWidth = width;
            caption.MaximumSize = new Size(width, 0);
            _explanation.MaximumSize = new Size(width, 0);
        }
        root.ClientSizeChanged += (_, _) => FitProseToDialog();
        Shown += (_, _) => FitProseToDialog();
        FitProseToDialog();

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
        if (preferOtherAdAccount &&
            StartConnectionPolicy.NeedsActiveDirectory(SelectedSource))
            _otherAdAccount.Checked = true;
        _rememberConnection.Checked = automaticConnectionsAllowed &&
            rememberLastConnection && SelectedSource != StartConnectionPolicy.Choose;
        UpdateOptions();
    }

    private void UpdateOptions()
    {
        var ad = StartConnectionPolicy.NeedsActiveDirectory(SelectedSource);
        _otherAdAccount.Enabled = ad;
        if (!ad)
            _otherAdAccount.Checked = false;

        _rememberConnection.Enabled = _automaticConnectionsAllowed &&
            SelectedSource != StartConnectionPolicy.Choose;
        if (!_rememberConnection.Enabled)
            _rememberConnection.Checked = false;

        _connect.Text = SelectedSource == StartConnectionPolicy.Choose
            ? "Open Start" : "Connect";

        var sourceExplanation = SelectedSource switch
        {
            StartConnectionPolicy.ActiveDirectory =>
                "Uses the current Windows account by default. Recovery access still requires delegated AD permissions.",
            StartConnectionPolicy.MicrosoftCloud =>
                "Uses delegated work-account sign-in and MFA. An approved Entra application is required.",
            StartConnectionPolicy.Both =>
                "Connects to AD, then Microsoft 365. Each source checks its own permissions.",
            _ => "Continue offline. Choose and connect a source from Start when ready."
        };

        _explanation.Text = sourceExplanation +
            (!_automaticConnectionsAllowed
                ? " Automatic reconnection is disabled by administrator policy."
                : RememberLastConnectionOnStart
                ? " Next launch will reuse the selected source, never an AD password or MFA token."
                : " Enable 'Use this source next time' to skip this choice on the next launch.");
    }
}
