using System.Security.Cryptography;
using System.Text;

namespace BitKeyBridge;

public sealed partial class MainForm
{
    private readonly ComboBox _lapsSource = new();
    private readonly TextBox _lapsQuery = new();
    private readonly CheckBox _lapsHistory = new();
    private readonly ComboBox _lapsView = new();
    private readonly Button _lapsAccessCheck = new();
    private readonly Button _lapsRead = new();
    private readonly Button _lapsCancel = new();
    private readonly Button _lapsReveal = new();
    private readonly Button _lapsCopy = new();
    private readonly Button _lapsCopyAccount = new();
    private readonly Button _lapsSearchButton = new();
    private readonly DataGridView _lapsSearchRows = new();
    private readonly DataGridView _lapsRows = new();
    private readonly TextBox _lapsPassword = new();
    private readonly RichTextBox _lapsDetails = new();
    private readonly Button _lapsDetailsToggle = new();
    private readonly UiStatusLabel _lapsStatus = new();
    private readonly TableLayoutPanel _lapsZeroWarningPanel = new();
    private readonly UiStatusLabel _lapsZeroWarning = new();
    private readonly Button _lapsDiagnosticCommands = new();
    private readonly ProgressBar _lapsProgress = new();
    private readonly UiDiagnosticPanel _lapsDiagnostics = new();
    private LapsReadResult? _lapsResult;
    private CancellationTokenSource? _lapsReadCancellation;
    private CancellationTokenSource? _lapsSearchCancellation;
    private System.Windows.Forms.Timer? _lapsClearTimer;
    private System.Windows.Forms.Timer? _lapsSearchDebounceTimer;
    private int _lapsGeneration;
    private int _lapsSearchGeneration;
    private bool _suppressLapsSearchQueue;
    private bool _suppressLapsCandidateSelection;
    private bool _lapsReading;
    private string _lapsResourceId = string.Empty;
    private string _lapsAuditSource = string.Empty;

    private TabPage BuildLapsWorkspaceTab()
    {
        var tab =
            new TabPage("LAPS");

        var root =
            new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(
                    UiStyle.PagePadding),
                ColumnCount = 1,
                RowCount = 11
            };
        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        for (var index = 0;
             index < 11;
             index++)
        {
            root.RowStyles.Add(
                new RowStyle(
                    index == 5
                        ? SizeType.Percent
                        : SizeType.AutoSize,
                    index == 5
                        ? 100F
                        : 0F));
        }

        tab.Controls.Add(
            root);

        var titlePanel =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    2,
                RowCount =
                    1,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            };

        titlePanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        titlePanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        titlePanel.Controls.Add(
            new Label
            {
                Text =
                    "LAPS passwords and history",
                AutoSize =
                    true,
                Font =
                    UiStyle.CreatePageTitleFont(),
                Anchor =
                    AnchorStyles.Left
            },
            0,
            0);

        _lapsBackToStart.Name =
            "LapsBackToStart";
        _lapsBackToStart.Text =
            "Back to Start";
        _lapsBackToStart.Visible =
            false;
        UiStyle.ConfigureActionButton(
            _lapsBackToStart);
        titlePanel.Controls.Add(
            _lapsBackToStart,
            1,
            0);

        _lapsBackToStart.Click +=
            (_, _) =>
            {
                _lapsBackToStart.Visible =
                    false;

                if (_homeWorkspaceTab is not null)
                {
                    _mainTabs.SelectedTab =
                        _homeWorkspaceTab;
                    _homeQuery.Focus();
                }
            };

        root.Controls.Add(
            titlePanel,
            0,
            0);

        var search =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    3,
                RowCount =
                    2,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };
        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        search.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        search.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        search.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        search.Controls.Add(
            new Label
            {
                Text =
                    "Source:",
                AutoSize =
                    true,
                Anchor =
                    AnchorStyles.Left
            },
            0,
            0);

        _lapsSource.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _lapsSource.Items.AddRange([
            "Active Directory (Legacy / Windows LAPS / DSRM)",
            "Microsoft Entra ID"
        ]);
        _lapsSource.Dock =
            DockStyle.Fill;
        _lapsSource.SelectedIndex =
            0;
        search.Controls.Add(
            _lapsSource,
            1,
            0);
        search.SetColumnSpan(
            _lapsSource,
            2);

        search.Controls.Add(
            new Label
            {
                Text =
                    "Computer / device ID:",
                AutoSize =
                    true,
                Anchor =
                    AnchorStyles.Left
            },
            0,
            1);

        _lapsQuery.Name =
            "LapsLiveSearchQuery";
        _lapsQuery.Dock =
            DockStyle.Fill;
        _lapsQuery.PlaceholderText =
            "Type part of a computer name or device ID";
        search.Controls.Add(
            _lapsQuery,
            1,
            1);

        _lapsSearchButton.Name =
            "LapsSearchButton";
        _lapsSearchButton.Text =
            "Search";
        UiStyle.ConfigureActionButton(
            _lapsSearchButton);
        search.Controls.Add(
            _lapsSearchButton,
            2,
            1);

        root.Controls.Add(
            search,
            0,
            1);

        _lapsSearchRows.Name =
            "LapsSearchResults";
        _lapsSearchRows.AccessibleName =
            "LAPS device search results";
        _lapsSearchRows.Dock =
            DockStyle.Top;
        _lapsSearchRows.MinimumSize =
            new Size(
                0,
                120);
        UiStyle.ConfigureDataGridView(
            _lapsSearchRows);
        _lapsSearchRows.Columns.AddRange([
            UiStyle.CreateSortableTextColumn(
                "Computer",
                "Computer",
                145),
            UiStyle.CreateSortableTextColumn(
                "ComputerId",
                "Device / AD object ID",
                210),
            UiStyle.CreateSortableTextColumn(
                "KeyDate",
                "Key date",
                120),
            UiStyle.CreateSortableTextColumn(
                "Latest",
                "Latest",
                60),
            UiStyle.CreateSortableTextColumn(
                "Source",
                "Source",
                90)
        ]);
        _lapsSearchRows.Columns["KeyDate"]!
            .DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        root.Controls.Add(
            _lapsSearchRows,
            0,
            2);

        var actions =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                WrapContents =
                    true,
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            };

        _lapsHistory.Text =
            "Include password history";
        _lapsHistory.AutoSize =
            true;
        _lapsHistory.Checked =
            true;
        actions.Controls.Add(
            _lapsHistory);

        actions.Controls.Add(
            new Label
            {
                Text =
                    "View:",
                AutoSize =
                    true,
                Margin =
                    new Padding(
                        UiStyle.ControlGap,
                        8,
                        4,
                        0)
            });

        _lapsView.DropDownStyle =
            ComboBoxStyle.DropDownList;
        _lapsView.Items.AddRange([
            "All",
            "Current",
            "History"
        ]);
        _lapsView.SelectedIndex =
            0;
        _lapsView.MinimumSize =
            new Size(
                105,
                0);
        actions.Controls.Add(
            _lapsView);

        _lapsRead.Text =
            "Read LAPS";
        UiStyle.ConfigureActionButton(
            _lapsRead);
        actions.Controls.Add(
            _lapsRead);

        _lapsCancel.Text =
            "Cancel";
        UiStyle.ConfigureActionButton(
            _lapsCancel);
        _lapsCancel.Enabled =
            false;
        actions.Controls.Add(
            _lapsCancel);

        var connect =
            UiStyle.CreateActionButton(
                "Connect to AD");
        var accessHelp =
            UiStyle.CreateActionButton(
                "AD access help...");
        var clear =
            UiStyle.CreateActionButton(
                "Clear passwords");

        actions.Controls.Add(
            connect);
        actions.Controls.Add(
            accessHelp);
        actions.Controls.Add(
            clear);

        root.Controls.Add(
            actions,
            0,
            3);

        // Keep the empty-result explanation above the password grid, rather
        // than burying it below the scrolling records/details workspace.
        _lapsZeroWarningPanel.Name =
            "LapsZeroRecordWarningPanel";
        _lapsZeroWarningPanel.Dock =
            DockStyle.Top;
        _lapsZeroWarningPanel.AutoSize =
            true;
        _lapsZeroWarningPanel.AutoSizeMode =
            AutoSizeMode.GrowAndShrink;
        _lapsZeroWarningPanel.ColumnCount =
            1;
        _lapsZeroWarningPanel.RowCount =
            2;
        _lapsZeroWarningPanel.Margin =
            new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap);
        _lapsZeroWarningPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));
        _lapsZeroWarningPanel.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));
        _lapsZeroWarningPanel.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        _lapsZeroWarning.Name =
            "LapsZeroRecordWarning";
        _lapsZeroWarning.AccessibleName =
            "LAPS password read warning";
        _lapsZeroWarning.Dock =
            DockStyle.Fill;
        UiStyle.ConfigureStatusLabel(
            _lapsZeroWarning);
        UiStyle.SetStatus(
            _lapsZeroWarning,
            "No readable LAPS password records were returned.",
            UiStatusKind.Warning);
        _lapsZeroWarningPanel.Controls.Add(
            _lapsZeroWarning, 0, 0);

        _lapsDiagnosticCommands.Name =
            "LapsDiagnosticCommandsButton";
        _lapsDiagnosticCommands.Text =
            "Diagnostic steps and commands...";
        UiStyle.ConfigureActionButton(
            _lapsDiagnosticCommands);
        _lapsDiagnosticCommands.Anchor =
            AnchorStyles.Left;
        _lapsDiagnosticCommands.Margin =
            new Padding(0, UiStyle.ControlGap, 0, 0);
        _lapsDiagnosticCommands.Click +=
            (_, _) => ShowLapsDiagnosticCommands();
        _lapsZeroWarningPanel.Controls.Add(
            _lapsDiagnosticCommands, 0, 1);
        _lapsZeroWarningPanel.Visible =
            false;
        root.Controls.Add(
            _lapsZeroWarningPanel, 0, 4);

        _lapsRows.Name =
            "LapsKeyResults";
        _lapsRows.AccessibleName =
            "LAPS key results";
        _lapsRows.Dock =
            DockStyle.Fill;
        _lapsRows.MinimumSize =
            new Size(
                0,
                180);
        UiStyle.ConfigureDataGridView(
            _lapsRows);
        _lapsRows.Columns.AddRange([
            UiStyle.CreateSortableTextColumn(
                "Source",
                "Source / version",
                145),
            UiStyle.CreateSortableTextColumn(
                "RecordType",
                "Current / history",
                95),
            UiStyle.CreateSortableTextColumn(
                "Account",
                "Account",
                130),
            UiStyle.CreateSortableTextColumn(
                "KeyDate",
                "Key date",
                125),
            UiStyle.CreateSortableTextColumn(
                "Latest",
                "Latest",
                65),
            UiStyle.CreateSortableTextColumn(
                "Age",
                "Age",
                70),
            UiStyle.CreateSortableTextColumn(
                "Expires",
                "Expires",
                125),
            UiStyle.CreateSortableTextColumn(
                "Status",
                "Status",
                100)
        ]);
        _lapsRows.Columns["KeyDate"]!
            .DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        _lapsRows.Columns["Expires"]!
            .DefaultCellStyle.Format =
            "yyyy-MM-dd HH:mm:ss";
        root.Controls.Add(
            _lapsRows,
            0,
            5);

        var detailsPanel =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    1,
                RowCount =
                    2,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };
        detailsPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        detailsPanel.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        detailsPanel.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        _lapsDetailsToggle.Name =
            "LapsDetailsToggle";
        _lapsDetailsToggle.Text =
            "Details...";
        UiStyle.ConfigureActionButton(
            _lapsDetailsToggle);
        _lapsDetailsToggle.Anchor =
            AnchorStyles.Left;
        detailsPanel.Controls.Add(
            _lapsDetailsToggle,
            0,
            0);

        _lapsDetails.Dock =
            DockStyle.Top;
        _lapsDetails.ReadOnly =
            true;
        _lapsDetails.MinimumSize =
            new Size(
                0,
                90);
        _lapsDetails.Font =
            UiStyle.CreateMonospaceFont(
                9F);
        _lapsDetails.Visible =
            false;
        detailsPanel.Controls.Add(
            _lapsDetails,
            0,
            1);

        root.Controls.Add(
            detailsPanel,
            0,
            6);

        var secret =
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
                        UiStyle.ControlGap,
                        0,
                        UiStyle.ControlGap)
            };
        secret.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));
        secret.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        secret.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));
        secret.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        secret.Controls.Add(
            new Label
            {
                Text =
                    "Password:",
                AutoSize =
                    true,
                Anchor =
                    AnchorStyles.Left
            },
            0,
            0);

        _lapsPassword.Dock =
            DockStyle.Fill;
        _lapsPassword.ReadOnly =
            true;
        _lapsPassword.UseSystemPasswordChar =
            true;
        _lapsPassword.Font =
            UiStyle.CreateMonospaceFont(
                10F);
        secret.Controls.Add(
            _lapsPassword,
            1,
            0);

        var secretActions =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                WrapContents =
                    true
            };

        _lapsReveal.Text =
            "Reveal password";
        _lapsCopy.Text =
            "Copy password";
        _lapsCopyAccount.Text =
            "Copy account";
        UiStyle.ConfigureActionButton(
            _lapsReveal);
        UiStyle.ConfigureActionButton(
            _lapsCopy);
        UiStyle.ConfigureActionButton(
            _lapsCopyAccount);
        _lapsReveal.Enabled =
            false;
        _lapsCopy.Enabled =
            false;
        _lapsCopyAccount.Enabled =
            false;
        secretActions.Controls.Add(
            _lapsReveal);
        secretActions.Controls.Add(
            _lapsCopy);
        secretActions.Controls.Add(
            _lapsCopyAccount);
        secret.Controls.Add(
            secretActions,
            1,
            1);
        root.Controls.Add(
            secret,
            0,
            7);

        _lapsStatus.Dock =
            DockStyle.Top;
        UiStyle.SetStatus(
            _lapsStatus,
            "Type at least two characters for live metadata search. Select a device and press Enter or double-click to read LAPS. Search never reads passwords.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _lapsStatus,
            0,
            8);

        _lapsProgress.Name =
            "LapsProgress";
        _lapsProgress.AccessibleName =
            "LAPS operation progress";
        _lapsProgress.Dock =
            DockStyle.Top;
        _lapsProgress.Style =
            ProgressBarStyle.Marquee;
        _lapsProgress.MarqueeAnimationSpeed =
            25;
        _lapsProgress.Visible =
            false;
        _lapsProgress.Margin =
            new Padding(
                0,
                UiStyle.ControlGap,
                0,
                0);
        root.Controls.Add(
            _lapsProgress,
            0,
            9);

        root.Controls.Add(
            _lapsDiagnostics,
            0,
            10);

        _lapsDetailsToggle.Click +=
            (_, _) =>
            {
                _lapsDetails.Visible =
                    !_lapsDetails.Visible;
                _lapsDetailsToggle.Text =
                    _lapsDetails.Visible
                        ? "Hide details"
                        : "Details...";
            };

        _lapsRead.Click +=
            async (_, _) =>
                await ReadLapsAsync();
        _lapsCancel.Click +=
            (_, _) =>
            {
                CancelLapsSearch();
                CancelLapsRead();
            };
        _lapsView.SelectedIndexChanged +=
            (_, _) =>
                RenderLapsRows();

        _lapsSearchButton.Click +=
            async (_, _) =>
                await SearchLapsCandidatesAsync(
                    allowInteractiveAuth: true,
                    readExactMatch: false);

        _lapsQuery.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode !=
                    Keys.Enter)
                {
                    return;
                }

                e.SuppressKeyPress =
                    true;
                _lapsSearchDebounceTimer?.Stop();

                if (GetSelectedLapsSearchResult() is not null)
                {
                    SelectLapsSearchCandidate();
                    await ReadLapsAsync();
                    return;
                }

                await SearchLapsCandidatesAsync(
                    allowInteractiveAuth: true,
                    readExactMatch: true);
            };

        _lapsQuery.TextChanged +=
            (_, _) =>
            {
                ClearLapsResult();

                if (!_suppressLapsSearchQueue)
                {
                    QueueLapsLiveSearch();
                }
            };

        _lapsSearchDebounceTimer =
            new System.Windows.Forms.Timer
            {
                Interval =
                    SearchText.DebounceMilliseconds
            };
        _lapsSearchDebounceTimer.Tick +=
            async (_, _) =>
            {
                _lapsSearchDebounceTimer.Stop();

                if (_lapsQuery.Text.Trim().Length >=
                    SearchText.MinimumLiveSearchCharacters)
                {
                    await SearchLapsCandidatesAsync(
                        allowInteractiveAuth: false,
                        readExactMatch: false);
                }
            };

        _lapsSearchRows.SelectionChanged +=
            (_, _) =>
                SelectLapsSearchCandidate();

        _lapsSearchRows.CellDoubleClick +=
            async (_, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    SelectLapsSearchCandidate();
                    await ReadLapsAsync();
                }
            };

        _lapsSearchRows.KeyDown +=
            async (_, e) =>
            {
                if (e.KeyCode !=
                        Keys.Enter ||
                    GetSelectedLapsSearchResult() is null)
                {
                    return;
                }

                e.SuppressKeyPress =
                    true;
                SelectLapsSearchCandidate();
                await ReadLapsAsync();
            };

        _adUsername.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _adServer.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _adDomain.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _adExplicitCredentials.CheckedChanged +=
            (_, _) =>
                ClearLapsResult();
        _adCredentialStorage.SelectedIndexChanged +=
            (_, _) =>
                ClearLapsResult();
        _cloudTenant.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _cloudClient.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _cloudUsername.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _cloudThumbprint.TextChanged +=
            (_, _) =>
                ClearLapsResult();
        _cloudAuthMode.SelectedIndexChanged +=
            (_, _) =>
                ClearLapsResult();

        _lapsSource.SelectedIndexChanged +=
            (_, _) =>
            {
                CancelLapsSearch();
                ClearLapsResult();
                _lapsSearchRows.Rows.Clear();

                var cloud =
                    _lapsSource.SelectedIndex ==
                    1;
                connect.Enabled =
                    !cloud;
                _lapsQuery.PlaceholderText =
                    cloud
                        ? "Part of Entra device name or device ID"
                        : "Part of computer name; full AD object GUID also works";
                QueueLapsLiveSearch();
            };

        connect.Click +=
            async (_, _) =>
                await TestDirectoryConnectionAsync(
                    promptForOu: false,
                    forceManualCredentials: true);

        accessHelp.Click +=
            (_, _) =>
                ShowAdAccessTroubleshooting(
                    "LAPS password / history access in Active Directory");

        clear.Click +=
            (_, _) =>
                ClearLapsResult();

        _lapsRows.SelectionChanged +=
            (_, _) =>
                SelectLapsEntry();
        _lapsReveal.Click +=
            (_, _) =>
                RevealLapsPassword(
                    copy: false);
        _lapsCopy.Click +=
            (_, _) =>
                RevealLapsPassword(
                    copy: true);
        _lapsCopyAccount.Click +=
            (_, _) =>
                CopySelectedLapsAccount();

        FormClosed +=
            (_, _) =>
            {
                _lapsSearchDebounceTimer?.Stop();
                CancelLapsSearch();
                ClearLapsResult();
            };

        return tab;
    }

    private void QueueLapsLiveSearch()
    {
        _lapsSearchDebounceTimer?.Stop();

        if (_lapsQuery.Text.Trim().Length <
            SearchText.MinimumLiveSearchCharacters)
        {
            return;
        }

        _lapsSearchDebounceTimer?.Start();
    }

    private void CancelLapsSearch()
    {
        _lapsSearchDebounceTimer?.Stop();
        _lapsSearchCancellation?.Cancel();
    }

    private LapsSearchResult? GetSelectedLapsSearchResult()
    {
        return _lapsSearchRows.SelectedRows.Count == 1
            ? _lapsSearchRows.SelectedRows[0].Tag as
                LapsSearchResult
            : null;
    }

    private void SelectLapsSearchCandidate()
    {
        if (_suppressLapsCandidateSelection)
            return;

        var selected =
            GetSelectedLapsSearchResult();

        if (selected is null)
            return;

        _suppressLapsSearchQueue =
            true;

        try
        {
            _lapsQuery.Text =
                selected.LookupValue;
            _lapsQuery.SelectionStart =
                _lapsQuery.TextLength;
        }
        finally
        {
            _suppressLapsSearchQueue =
                false;
        }

        ClearLapsResult();

        UiStyle.SetStatus(
            _lapsStatus,
            $"Selected {selected.ComputerName}. Press Enter or double-click to read LAPS.",
            UiStatusKind.Neutral);
    }

    private async Task SearchLapsCandidatesAsync(
        bool allowInteractiveAuth,
        bool readExactMatch = false)
    {
        _lapsSearchDebounceTimer?.Stop();

        var query =
            _lapsQuery.Text.Trim();

        if (query.Length == 0)
            return;

        var cloud =
            _lapsSource.SelectedIndex == 1;

        if (cloud &&
            !allowInteractiveAuth &&
            (_cloudToken is null ||
             !_cloudTokenHasLaps ||
             _cloudToken.ExpiresAt <=
                DateTime.Now.AddMinutes(1)))
        {
            return;
        }

        if (!cloud &&
            allowInteractiveAuth)
        {
            if (!EnsureSessionAdCredentialForConnection())
                return;

            SaveDirectorySettings(
                showConfirmation: false,
                allowInMemoryFallback: true);
        }

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                "SearchLapsMetadata",
                computerName: query,
                source:
                    cloud
                        ? "LAPS-Entra"
                        : "LAPS-AD"))
        {
            return;
        }

        _lapsSearchCancellation?.Cancel();

        using var cancellation =
            new CancellationTokenSource();
        var generation =
            ++_lapsSearchGeneration;
        _lapsSearchCancellation =
            cancellation;

        _lapsSearchButton.Enabled =
            false;
        _lapsCancel.Enabled =
            true;
        _lapsProgress.Visible =
            true;

        try
        {
            UiStyle.SetStatus(
                _lapsStatus,
                cloud
                    ? "Searching Entra LAPS devices..."
                    : "Searching Active Directory computers...",
                UiStatusKind.Busy);

            List<LapsSearchResult> rows;

            if (cloud)
            {
                if (allowInteractiveAuth &&
                    !await EnsureCloudTokenAsync(
                        forLaps: true,
                        cancellation.Token))
                {
                    return;
                }

                using var graph =
                    new CloudGraphService();

                rows =
                    await graph.SearchLapsDevicesAsync(
                        _cloudToken!.AccessToken,
                        query,
                        100,
                        cancellation.Token);
            }
            else
            {
                var snapshot =
                    new AppConfig
                    {
                        AdConnectionMode =
                            _config.AdConnectionMode,
                        AdDomain =
                            _config.AdDomain,
                        AdServer =
                            _config.AdServer,
                        AdPort =
                            _config.AdPort,
                        AdUseLdaps =
                            _config.AdUseLdaps
                    };
                var credential =
                    AdSessionCredentials
                        .CreateNetworkCredential(
                            _config);
                var service =
                    new LapsDirectoryService(
                        snapshot,
                        credential);

                rows =
                    await Task.Run(
                        () =>
                            service.SearchMetadata(
                                query,
                                100,
                                cancellation.Token),
                        cancellation.Token);
            }

            cancellation.Token
                .ThrowIfCancellationRequested();

            if (generation !=
                _lapsSearchGeneration)
            {
                return;
            }

            _suppressLapsCandidateSelection =
                true;

            try
            {
                _lapsSearchRows.Rows.Clear();

                foreach (var row in rows)
                {
                    var rowIndex =
                        _lapsSearchRows.Rows.Add(
                            row.ComputerName,
                            row.ComputerId,
                            row.KeyDateUtc,
                            row.IsLatest is null
                                ? "-"
                                : row.IsLatest.Value
                                    ? "Yes"
                                    : "No",
                            row.Source);

                    _lapsSearchRows.Rows[rowIndex].Tag =
                        row;
                }

                _lapsSearchRows.ClearSelection();
                _lapsSearchRows.CurrentCell =
                    null;
            }
            finally
            {
                _suppressLapsCandidateSelection =
                    false;
            }

            var normalizedQuery =
                SearchText.NormalizeIdentifierFragment(
                    query);

            var exactMatches =
                rows
                    .Select(
                        (row, index) =>
                            new
                            {
                                Row = row,
                                Index = index
                            })
                    .Where(
                        item =>
                            item.Row.ComputerName.Equals(
                                query,
                                StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(
                                 normalizedQuery) &&
                             SearchText.NormalizeIdentifierFragment(
                                 item.Row.ComputerId)
                                 .Equals(
                                     normalizedQuery,
                                     StringComparison.Ordinal)))
                    .ToList();

            var selectedIndex =
                rows.Count == 1
                    ? 0
                    : exactMatches.Count == 1
                        ? exactMatches[0].Index
                        : -1;

            if (selectedIndex >= 0)
            {
                _lapsSearchRows.ClearSelection();
                _lapsSearchRows.Rows[selectedIndex].Selected =
                    true;
                _lapsSearchRows.CurrentCell =
                    _lapsSearchRows.Rows[selectedIndex].Cells[0];
                SelectLapsSearchCandidate();

                if (readExactMatch)
                {
                    await ReadLapsAsync();
                }
            }
            else
            {
                UiStyle.SetStatus(
                    _lapsStatus,
                    rows.Count == 0
                        ? "No matching LAPS device was found."
                        : $"Found {rows.Count} matching device(s). Select one and press Enter or double-click to read LAPS.",
                    rows.Count == 0
                        ? UiStatusKind.Warning
                        : UiStatusKind.Success);
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed &&
                generation ==
                    _lapsSearchGeneration)
            {
                UiStyle.SetStatus(
                    _lapsStatus,
                    "LAPS search canceled.",
                    UiStatusKind.Warning);
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed ||
                generation !=
                    _lapsSearchGeneration)
            {
                return;
            }

            UiStyle.SetStatus(
                _lapsStatus,
                allowInteractiveAuth
                    ? "LAPS device search failed. Review diagnostics below."
                    : "Live LAPS search is unavailable; press Enter or Search to retry interactively.",
                UiStatusKind.Error);

            if (allowInteractiveAuth)
            {
                _lapsDiagnostics.ShowError(
                    "LAPS device search failed.",
                    "SearchLapsMetadata",
                    ex,
                    ("Source",
                        cloud
                            ? "Entra"
                            : "Active Directory"),
                    ("Query", query));
            }
        }
        finally
        {
            if (ReferenceEquals(
                    _lapsSearchCancellation,
                    cancellation))
            {
                _lapsSearchCancellation =
                    null;
            }

            if (!IsDisposed &&
                generation ==
                    _lapsSearchGeneration)
            {
                _lapsSearchButton.Enabled =
                    true;
                _lapsCancel.Enabled =
                    _lapsReading;
                _lapsProgress.Visible =
                    _lapsReading;
            }
        }
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
        _lapsAccessCheck.Enabled = false;
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
                if (!await EnsureCloudTokenAsync(forLaps: true, cancellation.Token))
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
                var readTask =
                    Task.Run(
                        () => service.Read(
                            query,
                            history,
                            cancellation.Token));

                var cancelSignal =
                    Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellation.Token);

                var completed =
                    await Task.WhenAny(
                        readTask,
                        cancelSignal);

                if (completed != readTask)
                {
                    _ = readTask.ContinueWith(
                        static task =>
                        {
                            _ = task.Exception;
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted |
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    throw new OperationCanceledException(
                        cancellation.Token);
                }

                pending =
                    await readTask;
            }
            if (IsDisposed || cancellation.IsCancellationRequested || generation != _lapsGeneration) return;
            _lapsResult = pending;
            pending = null;
            _lapsResourceId = resourceId;
            _lapsAuditSource = source;
            RenderLapsRows();
            var available = _lapsResult.Entries.Count(x => x.HasPassword);
            WriteRecoveryAudit("ReadLapsPasswords", context,
                result: available == 0 ? "NoReadablePasswords" : available == _lapsResult.Entries.Count ? "Success" : "Partial",
                computerName: _lapsResult.ComputerName,
                recoveryId: resourceId, source: source,
                details: $"Records={_lapsResult.Entries.Count}; Available={available}; IncludeHistory={history}; DC={_lapsResult.DirectoryServer}");

            if (available > 0)
            {
                RecordRecentComputer(
                    _lapsResult.ComputerName,
                    _lapsResult.ComputerId,
                    "LAPS read");
            }
            if (_lapsRows.SelectedRows.Count > 0)
            {
                SelectLapsEntry();
            }
            else
            {
                _lapsDetails.Text =
                    BuildLapsSummaryText(
                        _lapsResult);
            }

            if (available == 0)
            {
                _lapsZeroWarningPanel.Visible = true;
                UiStyle.SetStatus(
                    _lapsZeroWarning,
                    _lapsResult.Entries.Count == 0
                        ? LapsReadDiagnostics.EmptyStatus(_lapsResult, cloud)
                        : "LAPS records were returned, but no passwords are readable. " +
                          "Check record status, AD read/decrypt authorization and the backup policy. " +
                          "Open Diagnostic steps and commands.",
                    UiStatusKind.Warning);

                if (_lapsResult.Entries.Count == 0)
                {
                    var diagnostics =
                        LapsReadDiagnostics.EmptyDetails(
                            _lapsResult,
                            cloud);

                    _lapsDetails.Text =
                        diagnostics;
                    _lapsDetails.Visible =
                        true;
                    _lapsDetailsToggle.Text =
                        "Hide details";

                    _lapsDiagnostics.ShowMessage(
                        "No LAPS password attributes were returned.",
                        diagnostics);

                    UiStyle.SetStatus(
                        _lapsStatus,
                        LapsReadDiagnostics.EmptyStatus(
                            _lapsResult,
                            cloud),
                        UiStatusKind.Warning);
                }
                else
                {
                    // Encrypted LAPS records can be visible without being
                    // decryptable by this identity. Keep their rows and
                    // per-record error details visible.
                    UiStyle.SetStatus(
                        _lapsStatus,
                        $"{_lapsResult.Entries.Count} LAPS record(s) returned, " +
                        "but no passwords could be read. Select a row to " +
                        "inspect its status; encrypted LAPS may require " +
                        "separate decryption authorization.",
                        UiStatusKind.Warning);
                }

                // No passwords are present in memory. Preserve diagnostics
                // rather than starting the secret-expiration timer that
                // would erase the only useful explanation in two minutes.
                return;
            }

            var expiredCurrent =
                _lapsResult.Entries.Any(
                    row =>
                        !row.IsHistory &&
                        row.HasPassword &&
                        row.Status ==
                            LapsPasswordStatus.Available &&
                        IsLapsExpired(
                            row));

            var historyMissing =
                history &&
                _lapsResult.Entries.Count > 0 &&
                !_lapsResult.Entries.Any(
                    row =>
                        row.IsHistory);

            var readMessages =
                new List<string>
                {
                    $"{_lapsResult.Entries.Count} record(s), {available} readable password(s)."
                };

            if (historyMissing)
            {
                readMessages.Add(
                    "History unavailable (disabled, not retained, or not readable).");
            }

            if (expiredCurrent)
            {
                readMessages.Add(
                    "The current LAPS expiration time is in the past; password rotation may be overdue.");
            }

            readMessages.Add(
                $"Passwords clear after {_config.SecretDisplaySeconds} seconds.");

            UiStyle.SetStatus(
                _lapsStatus,
                string.Join(
                    " ",
                    readMessages),
                expiredCurrent ||
                available == 0 ||
                available !=
                    _lapsResult.Entries.Count
                    ? UiStatusKind.Warning
                    : UiStatusKind.Success);
            _lapsClearTimer =
                new System.Windows.Forms.Timer
                {
                    Interval =
                        Math.Max(
                            15,
                            _config.SecretDisplaySeconds) *
                        1000
                };
            _lapsClearTimer.Tick +=
                (_, _) =>
                    ClearLapsResult();
            _lapsClearTimer.Start();

            StartSecretLifetimeCountdown(
                "LAPS password",
                _config.SecretDisplaySeconds);
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
                _lapsAccessCheck.Enabled = true;
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

    private LapsPasswordEntry? GetSelectedLapsEntry()
    {
        return _lapsRows.SelectedRows.Count == 1
            ? _lapsRows.SelectedRows[0].Tag as
                LapsPasswordEntry
            : null;
    }

    private void SelectLapsEntry()
    {
        _lapsPassword.Clear();
        _lapsPassword.UseSystemPasswordChar = true;
        _lapsReveal.Text = "Reveal password";
        var row =
            GetSelectedLapsEntry();
        var readable =
            row is
            {
                HasPassword: true,
                Status: LapsPasswordStatus.Available
            };
        _lapsReveal.Enabled =
            _lapsCopy.Enabled =
                readable;
        _lapsCopyAccount.Enabled =
            row is not null &&
            !string.IsNullOrWhiteSpace(
                row.AccountName);

        if (row is null ||
            _lapsResult is null)
        {
            return;
        }

        var accessHint =
            row.Status is
                LapsPasswordStatus.AccessDenied or
                LapsPasswordStatus.DecryptionFailed
                ? Environment.NewLine +
                  "Access help: use AD access help... to generate scoped BitLocker/LAPS delegation guidance. " +
                  "For encrypted Windows LAPS, read ACL and DPAPI-NG decrypt authorization are separate."
                : string.Empty;

        var details =
            new List<string>
            {
                $"Computer: {_lapsResult.ComputerName} | Directory: {_lapsResult.DirectoryServer}"
            };

        var identity =
            new List<string>
            {
                $"Attribute: {row.Attribute}"
            };

        if (!string.IsNullOrWhiteSpace(
                row.AccountSid))
        {
            identity.Add(
                $"Account SID: {row.AccountSid}");
        }

        if (!string.IsNullOrWhiteSpace(
                _lapsResult.PasswordVersion))
        {
            identity.Add(
                $"Password version: {_lapsResult.PasswordVersion}");
        }

        details.Add(
            string.Join(
                " | ",
                identity));

        details.Add(
            $"Type: {(row.IsHistory ? "History" : "Current")} | " +
            $"Age: {FormatLapsAge(row.UpdatedAtUtc)} | " +
            $"Expires: {FormatLapsExpiry(row)}");

        var statusText =
            row.Status.ToString();

        if (IsLapsExpired(
                row))
        {
            statusText +=
                " / Expired";
        }

        if (!string.IsNullOrWhiteSpace(
                row.StatusDetail) &&
            !(row.Status ==
                  LapsPasswordStatus.Available &&
              row.StatusDetail.Equals(
                  "Password read successfully.",
                  StringComparison.OrdinalIgnoreCase)))
        {
            statusText +=
                ". " +
                row.StatusDetail;
        }

        details.Add(
            "Status: " +
            statusText);

        if (IsLapsExpired(
                row))
        {
            details.Add(
                "Warning: current LAPS expiration is in the past; password rotation may be overdue.");
        }

        if (!string.IsNullOrWhiteSpace(
                _lapsResult.Note))
        {
            details.Add(
                _lapsResult.Note);
        }

        if (!string.IsNullOrWhiteSpace(
                accessHint))
        {
            details.Add(
                accessHint.Trim());
        }

        _lapsDetails.Text =
            string.Join(
                Environment.NewLine,
                details);

        var showTechnicalDetails =
            row.Status !=
                LapsPasswordStatus.Available;

        _lapsDetails.Visible =
            showTechnicalDetails;
        _lapsDetailsToggle.Text =
            showTechnicalDetails
                ? "Hide details"
                : "Details...";
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
        if (_lapsResult is null ||
            GetSelectedLapsEntry() is not
                LapsPasswordEntry
                {
                    HasPassword: true,
                    Status:
                        LapsPasswordStatus.Available
                } row)
        {
            return;
        }
        var action = copy ? "CopyLapsPassword" : "RevealLapsPassword";
        var context = AuthorizeLapsAccess(action, _lapsResult.ComputerName, _lapsResourceId, _lapsAuditSource);
        if (context is null) return;
        WriteRecoveryAudit(action, context, computerName: _lapsResult.ComputerName, recoveryId: _lapsResourceId,
            source: _lapsAuditSource, details: $"Attribute={row.Attribute}; Account={row.AccountName}; History={row.IsHistory}; Updated={row.UpdatedAtUtc:O}");
        if (copy)
        {
            try
            {
                CopyKeyWithAutoClear(
                    row.CopyPassword());
            }
            catch (Exception ex)
            {
                UiStyle.SetStatus(
                    _lapsStatus,
                    "The password could not be copied to the clipboard.",
                    UiStatusKind.Warning);
                WindowsEventLogService.TryWrite(
                    "LAPS password clipboard copy failed: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4579,
                    "Clipboard");
            }
        }
        else
        {
            _lapsPassword.Text = row.CopyPassword();
            _lapsPassword.UseSystemPasswordChar = false;
            _lapsReveal.Text = "Hide password";
        }
    }

    private void RenderLapsRows()
    {
        if (_lapsRows.IsDisposed)
            return;

        _lapsRows.Rows.Clear();

        if (_lapsResult is null)
            return;

        var mode =
            _lapsView.SelectedIndex;

        foreach (var row in
                 _lapsResult.OrderedEntries)
        {
            if (mode == 1 &&
                row.IsHistory)
            {
                continue;
            }

            if (mode == 2 &&
                !row.IsHistory)
            {
                continue;
            }

            var rowIndex =
                _lapsRows.Rows.Add(
                    row.Source,
                    row.IsHistory
                        ? "History"
                        : "Current",
                    row.AccountName,
                    row.UpdatedAtUtc,
                    row.IsHistory
                        ? "No"
                        : "Yes",
                    FormatLapsAge(
                        row.UpdatedAtUtc),
                    row.ExpiresAtUtc,
                    HelpdeskStatus.LapsEntry(
                        row));

            _lapsRows.Rows[rowIndex].Tag =
                row;
        }

        _lapsRows.ClearSelection();

        if (_lapsRows.Rows.Count == 1)
        {
            _lapsRows.Rows[0].Selected =
                true;
            _lapsRows.CurrentCell =
                _lapsRows.Rows[0].Cells[0];
            SelectLapsEntry();
        }
    }

    private static bool IsLapsExpired(
        LapsPasswordEntry row)
    {
        return !row.IsHistory &&
               row.ExpiresAtUtc.HasValue &&
               row.ExpiresAtUtc.Value
                   .ToUniversalTime() <
               DateTime.UtcNow;
    }

    private static string FormatLapsExpiry(
        LapsPasswordEntry row)
    {
        if (!row.ExpiresAtUtc.HasValue)
            return "Not stored";

        var text =
            row.ExpiresAtUtc.Value
                .ToUniversalTime()
                .ToString(
                    "yyyy-MM-dd HH:mm:ss 'UTC'");

        return IsLapsExpired(
            row)
            ? text + " (expired)"
            : text;
    }

    private static string BuildLapsSummaryText(
        LapsReadResult result)
    {
        var parts =
            new List<string>
            {
                $"Computer: {result.ComputerName}",
                $"Directory: {result.DirectoryServer}"
            };

        if (!string.IsNullOrWhiteSpace(
                result.ComputerId))
        {
            parts.Add(
                $"Device / AD object ID: {result.ComputerId}");
        }

        if (!string.IsNullOrWhiteSpace(
                result.PasswordVersion))
        {
            parts.Add(
                $"Password version: {result.PasswordVersion}");
        }

        var text =
            string.Join(
                " | ",
                parts);

        if (!string.IsNullOrWhiteSpace(
                result.Note))
        {
            text +=
                Environment.NewLine +
                result.Note;
        }

        return text;
    }

    private static string FormatLapsAge(
        DateTime? updatedAtUtc)
    {
        if (!updatedAtUtc.HasValue)
            return "Unknown";

        var age =
            DateTime.UtcNow -
            updatedAtUtc.Value.ToUniversalTime();

        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        if (age.TotalDays >= 1)
            return $"{age.TotalDays:0.#} d";

        if (age.TotalHours >= 1)
            return $"{age.TotalHours:0.#} h";

        return $"{Math.Max(0, age.TotalMinutes):0} min";
    }

    private void CopySelectedLapsAccount()
    {
        if (GetSelectedLapsEntry() is not
                LapsPasswordEntry row ||
            string.IsNullOrWhiteSpace(
                row.AccountName))
        {
            return;
        }

        try
        {
            Clipboard.SetText(
                row.AccountName);
            UiStyle.SetStatus(
                _lapsStatus,
                "Account name copied.",
                UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            UiStyle.SetStatus(
                _lapsStatus,
                "The account name could not be copied.",
                UiStatusKind.Warning);
            WindowsEventLogService.TryWrite(
                "LAPS account clipboard copy failed: " +
                ex.Message,
                EventLogSeverity.Warning,
                4580,
                "Clipboard");
        }
    }

    private async Task CheckLapsAccessAsync()
    {
        if (_lapsReading)
            return;

        var query =
            _lapsQuery.Text.Trim();

        if (query.Length == 0)
        {
            UiStyle.SetStatus(
                _lapsStatus,
                "Enter the exact computer name or device ID.",
                UiStatusKind.Warning);
            return;
        }

        var cloud =
            _lapsSource.SelectedIndex == 1;

        if (!cloud)
        {
            if (!EnsureSessionAdCredentialForConnection())
                return;

            SaveDirectorySettings(
                showConfirmation: false,
                allowInMemoryFallback: true);
        }

        var source =
            cloud
                ? "LAPS-Entra"
                : "LAPS-AD";

        if (!AuthorizeAction(
                BitKeyBridgePermission.RecoveryRead,
                "CheckLapsAccess",
                computerName: query,
                source: source))
        {
            return;
        }

        ClearLapsResult();
        var generation =
            _lapsGeneration;

        using var cancellation =
            new CancellationTokenSource();
        _lapsReadCancellation =
            cancellation;
        _lapsReading =
            true;
        _lapsAccessCheck.Enabled =
            false;
        _lapsRead.Enabled =
            false;
        _lapsCancel.Enabled =
            true;
        _lapsProgress.Visible =
            true;
        _lapsDiagnostics.Clear();
        _lapsSource.Enabled =
            false;
        _lapsQuery.Enabled =
            false;
        _lapsHistory.Enabled =
            false;
        _lapsView.Enabled =
            false;

        try
        {
            UiStyle.SetStatus(
                _lapsStatus,
                "Checking LAPS access without requesting a password...",
                UiStatusKind.Busy);

            LapsAccessCheckResult result;

            if (cloud)
            {
                if (!await EnsureCloudTokenAsync(
                        forLaps: true,
                        cancellation.Token))
                {
                    return;
                }

                using var graph =
                    new CloudGraphService();

                result =
                    await graph.CheckLapsAccessAsync(
                        _cloudToken!.AccessToken,
                        query,
                        cancellation.Token);
            }
            else
            {
                var snapshot =
                    new AppConfig
                    {
                        AdConnectionMode =
                            _config.AdConnectionMode,
                        AdDomain =
                            _config.AdDomain,
                        AdServer =
                            _config.AdServer,
                        AdPort =
                            _config.AdPort,
                        AdUseLdaps =
                            _config.AdUseLdaps
                    };
                var credential =
                    AdSessionCredentials
                        .CreateNetworkCredential(
                            _config);
                var service =
                    new LapsDirectoryService(
                        snapshot,
                        credential);
                var worker =
                    Task.Run(
                        () =>
                            service.CheckAccess(
                                query,
                                cancellation.Token));

                var cancelSignal =
                    Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellation.Token);

                if (await Task.WhenAny(
                        worker,
                        cancelSignal) !=
                    worker)
                {
                    _ = worker.ContinueWith(
                        static task =>
                        {
                            _ = task.Exception;
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted |
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    throw new OperationCanceledException(
                        cancellation.Token);
                }

                result =
                    await worker;
            }

            if (IsDisposed ||
                cancellation.IsCancellationRequested ||
                generation != _lapsGeneration)
            {
                return;
            }

            var builder =
                new StringBuilder();
            builder.AppendLine(
                $"Computer: {result.ComputerName}");
            builder.AppendLine(
                $"ID: {result.ComputerId}");
            builder.AppendLine(
                $"Source: {result.Source}");
            builder.AppendLine(
                $"Directory: {result.DirectoryServer}");
            builder.AppendLine();

            foreach (var check in
                     result.Checks)
            {
                builder.AppendLine(
                    $"{check.Name}: {check.State}");
                builder.AppendLine(
                    $"  {check.Detail}");
            }

            if (!cloud)
            {
                builder.AppendLine();
                builder.AppendLine(
                    "If password read/decrypt/history access is missing, open AD access help... for scoped delegation examples. " +
                    "Windows LAPS read ACL, decrypt authorization and history retention are separate controls.");
            }

            _lapsDetails.Text =
                builder.ToString();

            var failed =
                result.Checks.Count(
                    x =>
                        x.State ==
                        LapsAccessState.Failed);

            _audit.Write(
                "CheckLapsAccess",
                failed == 0
                    ? "Success"
                    : "Partial",
                computerName:
                    result.ComputerName,
                source:
                    source,
                details:
                    $"Checks={result.Checks.Count}; Failed={failed}; PasswordRequested=False");

            UiStyle.SetStatus(
                _lapsStatus,
                failed == 0
                    ? "LAPS access diagnostics completed without requesting a password."
                    : "LAPS access diagnostics completed with one or more failures. See details.",
                failed == 0
                    ? UiStatusKind.Success
                    : UiStatusKind.Warning);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed &&
                generation == _lapsGeneration)
            {
                UiStyle.SetStatus(
                    _lapsStatus,
                    "LAPS access check canceled.",
                    UiStatusKind.Warning);
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed ||
                generation != _lapsGeneration)
            {
                return;
            }

            _audit.Write(
                "CheckLapsAccess",
                "Failed",
                computerName:
                    query,
                source:
                    source,
                details:
                    ex.Message);

            UiStyle.SetStatus(
                _lapsStatus,
                "LAPS access check failed. Review diagnostics below.",
                UiStatusKind.Error);

            _lapsDiagnostics.ShowError(
                "LAPS access check failed.",
                "CheckLapsAccess",
                ex,
                ("Source", source),
                ("Lookup", query),
                ("Server", _config.AdServer),
                ("Domain", _config.AdDomain));
        }
        finally
        {
            if (ReferenceEquals(
                    _lapsReadCancellation,
                    cancellation))
            {
                _lapsReadCancellation =
                    null;
            }

            _lapsReading =
                false;

            if (!IsDisposed)
            {
                _lapsAccessCheck.Enabled =
                    true;
                _lapsRead.Enabled =
                    true;
                _lapsCancel.Enabled =
                    false;
                _lapsProgress.Visible =
                    false;
                _lapsSource.Enabled =
                    true;
                _lapsQuery.Enabled =
                    true;
                _lapsHistory.Enabled =
                    true;
                _lapsView.Enabled =
                    true;
            }
        }
    }

    private void ShowLapsDiagnosticCommands()
    {
        if (_lapsResult is null)
            return;

        var cloud = _lapsSource.SelectedIndex == 1;
        using var dialog = new LapsDiagnosticCommandsDialog(
            LapsDiagnosticRunbook.Build(_lapsResult, cloud));
        dialog.ShowDialog(this);
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
        _lapsRows.Rows.Clear();
        _lapsZeroWarningPanel.Visible = false;
        _lapsDetails.Clear();
        _lapsDetails.Visible =
            false;
        _lapsDetailsToggle.Text =
            "Details...";
        _lapsDiagnostics.Clear();
        _lapsReveal.Text = "Reveal password";
        _lapsReveal.Enabled = _lapsCopy.Enabled = false;
        _lapsCopyAccount.Enabled = false;
        _lapsResult?.Dispose();
        _lapsResult = null;
        _lapsResourceId = _lapsAuditSource = string.Empty;
        if (!IsDisposed)
            UiStyle.SetStatus(
                _lapsStatus,
                "Passwords cleared. Search/select a computer; Enter or double-click reads LAPS.",
                UiStatusKind.Neutral);
    }
}
