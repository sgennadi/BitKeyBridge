namespace BitKeyBridge;

/// <summary>
/// Compact read-only recovery checklist. No retrieval, elevation, network
/// calls, clipboard access, signed policy bypass or audit writes are possible.
/// </summary>
internal sealed class RecoveryWorkflowDialog : DpiAwareForm
{
    internal RecoveryWorkflowRoute SelectedRoute { get; private set; }

    internal RecoveryWorkflowDialog(
        RecoveryWorkflowReadinessService.Snapshot report,
        string? languageCode = null)
    {
        var locale = UiLanguageService.Normalize(languageCode);
        string Tr(string key) => UiLanguageService.Translate(locale, key);
        var rtl = UiLanguageService.IsRightToLeft(locale);
        Text = Tr("RecoveryDialogTitle");
        RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = rtl;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 625);
        MinimumSize = new Size(600, 430);
        Font = UiStyle.BodyFont;

        var root = new TableLayoutPanel
        {
            Name = "RecoveryWorkflowRoot",
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var heading = new Label
        {
            Name = "RecoveryWorkflowHeading",
            Text = Tr("RecoveryDialogHeading"),
            AccessibleName = "Read-only recovery preparation",
            Font = UiStyle.CreateSectionTitleFont(),
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 9)
        };
        UiStyle.ConfigureWrappingLabel(heading);
        root.Controls.Add(heading, 0, 0);

        var summary = new Label
        {
            Name = "RecoveryWorkflowSummary",
            Text = Tr("RecoveryDeviceLabel") + " " + report.Computer +
                   " | " + Tr("RecoverySourceLabel") + " " + report.Source +
                   Environment.NewLine + report.Notice,
            AccessibleName = "Selected device and recovery security limitations",
            RightToLeft = RightToLeft.No,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10)
        };
        UiStyle.ConfigureWrappingLabel(summary);
        root.Controls.Add(summary, 0, 1);

        var scroll = new Panel
        {
            Name = "RecoveryWorkflowScrollableSteps",
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        var content = new TableLayoutPanel
        {
            Name = "RecoveryWorkflowSteps",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = report.Steps.Count,
            Padding = new Padding(0, 0, 6, 0)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < report.Steps.Count; i++)
        {
            var item = report.Steps[i];
            var localizedStepName = Tr("RecoveryStep" + (i + 1));
            var localizedStatus = item.Status switch
            {
                "OK" => Tr("RecoveryStatusOk"),
                "Action required" => Tr("RecoveryStatusAction"),
                "Unverified" => Tr("RecoveryStatusUnverified"),
                "Not tested" => Tr("RecoveryStatusNotTested"),
                "Info" => Tr("RecoveryStatusInfo"),
                "Required at retrieval" => Tr("RecoveryStatusRequired"),
                "Optional" => Tr("RecoveryStatusOptional"),
                "Pending protected ACK" => Tr("RecoveryStatusPendingAck"),
                "Policy-dependent" => Tr("RecoveryStatusPolicy"),
                "Explicit action only" => Tr("RecoveryStatusExplicit"),
                _ => item.Status
            };
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var group = new TableLayoutPanel
            {
                Name = "RecoveryWorkflowStep" + i,
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 9)
            };
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            group.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = localizedStepName + " - " + localizedStatus,
                AccessibleName = localizedStepName + ": " + localizedStatus,
                Font = UiStyle.CreateEmphasisFont(10F),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = Padding.Empty
            };
            UiStyle.ConfigureWrappingLabel(title);

            var note = new Label
            {
                Text = item.Explanation,
                AccessibleName = item.Explanation,
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 3, 0, 0)
            };
            UiStyle.ConfigureWrappingLabel(note);
            group.Controls.Add(title, 0, 0);
            group.Controls.Add(note, 0, 1);
            content.Controls.Add(group, 0, i);
        }
        scroll.Controls.Add(content);
        root.Controls.Add(scroll, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        var close = UiStyle.CreateActionButton(Tr("RecoveryClose"), DialogResult.Cancel);
        var goStart = UiStyle.CreateActionButton(Tr("RecoveryOpenStart"));
        var health = UiStyle.CreateActionButton(Tr("RecoveryAccessHealth"));
        goStart.Click += (_, _) => Finish(RecoveryWorkflowRoute.Start);
        health.Click += (_, _) => Finish(RecoveryWorkflowRoute.AccessHealth);
        actions.Controls.Add(close);
        actions.Controls.Add(goStart);
        actions.Controls.Add(health);

        // These commands only navigate to existing metadata/search pages.
        // They do not reveal, copy or rotate any secret. The owner Form
        // revalidates the exact Start row and caller rights after closing.
        if (report.IsCloudSelection)
        {
            AddRouteButton(Tr("RecoveryOpenDevices"), RecoveryWorkflowRoute.Devices,
                report.CanNavigateToRecoveryScreen);
        }
        else
        {
            AddRouteButton(Tr("RecoveryOpenBitLocker"), RecoveryWorkflowRoute.BitLocker,
                report.CanNavigateToRecoveryScreen);
            AddRouteButton(Tr("RecoveryOpenLaps"), RecoveryWorkflowRoute.Laps,
                report.CanNavigateToRecoveryScreen);
        }
        root.Controls.Add(actions, 0, 3);

        AcceptButton = goStart;
        CancelButton = close;

        void AddRouteButton(string title, RecoveryWorkflowRoute route, bool enabled)
        {
            var button = UiStyle.CreateActionButton(title);
            button.Name = "RecoveryRoute" + route;
            button.AccessibleName = title + " search (no secret retrieval)";
            button.Enabled = enabled;
            button.Click += (_, _) => Finish(route);
            actions.Controls.Add(button);
        }

        void Finish(RecoveryWorkflowRoute route)
        {
            SelectedRoute = route;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
