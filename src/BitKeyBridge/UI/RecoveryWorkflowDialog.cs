namespace BitKeyBridge;

/// <summary>
/// Compact read-only recovery checklist. No retrieval, elevation, network
/// calls, clipboard access, signed policy bypass or audit writes are possible.
/// </summary>
internal sealed class RecoveryWorkflowDialog : DpiAwareForm
{
    internal RecoveryWorkflowDialog(RecoveryWorkflowReadinessService.Snapshot report)
    {
        Text = "Recovery Steps - Read-only Preflight";
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
            Text = "Recovery steps - no passwords or recovery keys requested",
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
            Text = "Device: " + report.Computer + " | Source: " + report.Source +
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
                Text = item.Name + " - " + item.Status,
                AccessibleName = item.Name + ": " + item.Status,
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
        var close = UiStyle.CreateActionButton("Close", DialogResult.Cancel);
        var goStart = UiStyle.CreateActionButton("Open Start", DialogResult.Yes);
        var health = UiStyle.CreateActionButton("Access Health", DialogResult.No);
        actions.Controls.Add(close);
        actions.Controls.Add(goStart);
        actions.Controls.Add(health);
        root.Controls.Add(actions, 0, 3);

        AcceptButton = goStart;
        CancelButton = close;
    }
}
