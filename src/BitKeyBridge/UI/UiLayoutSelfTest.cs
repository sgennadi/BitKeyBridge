namespace BitKeyBridge;

public static class UiLayoutSelfTest
{
    private sealed record Scenario(
        string Name,
        Size ClientSize,
        float FontScale);

    private static readonly Scenario[] Scenarios =
    [
        new(
            "Compact",
            new Size(
                900,
                620),
            1.0F),
        new(
            "LargeText",
            new Size(
                1050,
                720),
            1.50F),
        new(
            "Scale200Stress",
            new Size(
                1280,
                800),
            2.00F)
    ];

    public static int Run()
    {
        var reportPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "ui-self-test.log");

        try
        {
            if (File.Exists(
                    reportPath))
            {
                File.Delete(
                    reportPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "UI self-test report cleanup failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        var failures =
            new List<string>();

        VerifyStatusGlyphScaling(
            failures);

        VerifyHighContrastStatusPalette(
            failures);

        VerifyListViewColumnScaling(
            failures);

        var factories =
            CreateFormFactories();

        foreach (var scenario in
                 Scenarios)
        {
            foreach (var factory in
                     factories)
            {
                DpiAwareForm? form =
                    null;
                Font? scaledFont =
                    null;

                try
                {
                    form =
                        factory.Factory();

                    form.MinimumSize =
                        Size.Empty;
                    form.MaximumSize =
                        Size.Empty;
                    form.ClientSize =
                        scenario.ClientSize;

                    if (Math.Abs(
                            scenario.FontScale -
                            1.0F) >
                        0.01F)
                    {
                        scaledFont =
                            new Font(
                                UiStyle.BodyFont.FontFamily,
                                UiStyle.BodyFont.Size *
                                scenario.FontScale,
                                FontStyle.Regular,
                                GraphicsUnit.Point);

                        form.Font =
                            scaledFont;
                    }

                    form.CreateControl();
                    NormalizeDockFillSizes(
                        form);
                    form.PrepareResponsiveLayoutForTesting();
                    form.PerformLayout();
                    NormalizeDockFillSizes(
                        form);
                    form.PrepareResponsiveLayoutForTesting();

                    VerifyForm(
                        form,
                        factory.Name,
                        scenario,
                        failures);
                }
                catch (Exception ex)
                {
                    failures.Add(
                        $"{factory.Name}/{scenario.Name}: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    form?.Dispose();
                    scaledFont?.Dispose();
                }
            }
        }

        var reportLines =
            failures.Count == 0
                ? new[]
                {
                    "UI-SELF-TEST OK"
                }
                : failures
                    .Select(
                        failure =>
                            "UI-SELF-TEST FAILED: " +
                            failure)
                    .ToArray();

        try
        {
            File.WriteAllLines(
                reportPath,
                reportLines);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "UI self-test report write failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        foreach (var line in
                 reportLines)
        {
            if (failures.Count == 0)
                Console.WriteLine(line);
            else
                Console.Error.WriteLine(line);
        }

        return failures.Count == 0
            ? 0
            : 1;
    }

    private static IReadOnlyList<(
        string Name,
        Func<DpiAwareForm> Factory)>
        CreateFormFactories()
    {
        AppConfig NewConfig() =>
            new()
            {
                AutoConnectOnStart = false,
                RecoverySearchSource =
                    "LiveAD",
                HealthEndpointEnabled =
                    false,
                OutputRoot =
                    Path.GetTempPath(),
                OutputSubdirectory =
                    "BitKeyBridge-UiSelfTest"
            };

        return
        [
            (
                "MainForm",
                () =>
                    new MainForm(
                        NewConfig(),
                        layoutSelfTest: true)),
            (
                "AD credential prompt",
                () =>
                    new AdCredentialPromptDialog(
                        @"DOMAIN\helpdesk")),
            (
                "Input",
                () =>
                    new InputDialog(
                        "Input",
                        "A deliberately long prompt used to verify that explanatory text wraps instead of being clipped at compact or large-text settings.")),
            (
                "OU browser",
                () =>
                    new OuBrowserForm()),
            (
                "RBAC",
                () =>
                    new RbacSettingsDialog(
                        NewConfig())),
            (
                "Recovery access",
                () =>
                    new RecoveryAccessDialog(
                        "PC-12345",
                        Guid.Empty.ToString(),
                        requireReference: true,
                        allowRotationReminder: true,
                        defaultRotationReminder: true)),
            (
                "Incident verification",
                () =>
                    new RecoveryIncidentVerificationDialog()),
            (
                "Remote API tokens",
                () =>
                    new RemoteApiScopedTokenDialog(
                        NewConfig())),
            (
                "Secret display",
                () =>
                    new SecretDisplayDialog(
                        "Secret",
                        "A long description used only for layout validation.",
                        "SELF-TEST-SECRET",
                        "A long footer used only for layout validation.")),
            (
                "Secure output",
                () =>
                    new SecureOutputDialog(
                        Path.GetTempPath())),
            (
                "Storage maintenance",
                () =>
                    new StorageMaintenanceDialog(
                        NewConfig())),
            (
                "Privileged access",
                () =>
                    new PrivilegedAccessSettingsDialog(
                        NewConfig()))
        ];
    }

    private static void NormalizeDockFillSizes(
        Control root)
    {
        if (root is TabControl tabs)
        {
            var pageTarget =
                tabs.DisplayRectangle.Size;

            if (pageTarget.Width > 0 &&
                pageTarget.Height > 0)
            {
                foreach (TabPage page in
                         tabs.TabPages)
                {
                    if (page.Size !=
                        pageTarget)
                    {
                        page.Size =
                            pageTarget;
                    }
                }
            }
        }

        foreach (Control child in
                 root.Controls)
        {
            if (child.Dock ==
                    DockStyle.Fill &&
                child.Parent is not null)
            {
                var target =
                    child.Parent.DisplayRectangle.Size;

                if (target.Width > 0 &&
                    target.Height > 0 &&
                    child.Size != target)
                {
                    child.Size =
                        target;
                }
            }

            NormalizeDockFillSizes(
                child);
        }
    }

    private static void VerifyForm(
        DpiAwareForm form,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (form.AutoScaleMode !=
            AutoScaleMode.Dpi)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: AutoScaleMode is {form.AutoScaleMode}, expected Dpi.");
        }

        if (!form.AutoScroll)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: form scrolling is disabled.");
        }

        if (form is MainForm)
        {
            VerifyMainFormTabs(
                form,
                formName,
                scenario,
                failures);
        }
        else
        {
            VerifyDialogKeyboardBehavior(
                form,
                formName,
                scenario,
                failures);

            VerifyControlTree(
                form,
                formName,
                scenario,
                failures);
        }
    }

    private static void VerifyDialogKeyboardBehavior(
        DpiAwareForm form,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (form is StorageMaintenanceDialog)
            return;

        if (form.CancelButton is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: modal dialog has no standard Escape/Cancel action.");
        }

        if (form is not RemoteApiScopedTokenDialog &&
            form.AcceptButton is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: modal dialog has no standard Enter/Accept action.");
        }
    }

    private static void VerifyMainFormTabs(
        DpiAwareForm form,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var tabs =
            FindFirst<TabControl>(
                form);

        if (tabs is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: main TabControl was not found.");
            return;
        }

        VerifyTabControlPages(
            tabs,
            formName,
            scenario,
            failures,
            form,
            checkRecoveryConnection: true);
    }

    private static void VerifyTabControlPages(
        TabControl tabs,
        string path,
        Scenario scenario,
        List<string> failures,
        DpiAwareForm form,
        bool checkRecoveryConnection = false)
    {
        var originalIndex =
            tabs.SelectedIndex;

        try
        {
            for (var index = 0;
                 index < tabs.TabPages.Count;
                 index++)
            {
                tabs.SelectedIndex =
                    index;

                var page =
                    tabs.TabPages[index];
                var pagePath =
                    $"{path}/{page.Text}";

                page.CreateControl();

                if (checkRecoveryConnection &&
                    string.Equals(
                        page.Text,
                        "Recovery",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var advanced =
                        FindByName(
                            page,
                            "AdvancedConnectionGroup");

                    if (advanced is not null)
                    {
                        advanced.Visible =
                            true;
                    }
                }

                // Tab selection and Dock=Fill layout are message-loop driven in
                // WinForms. A headless WinExe can retain the framework's default
                // 200x100 child size, so normalize Dock=Fill geometry before and
                // after pumping pending layout work.
                NormalizeDockFillSizes(
                    form);
                Application.DoEvents();
                form.PerformLayout();
                tabs.PerformLayout();
                page.PerformLayout();
                NormalizeDockFillSizes(
                    form);
                form.PrepareResponsiveLayoutForTesting();
                Application.DoEvents();
                form.PerformLayout();
                tabs.PerformLayout();
                page.PerformLayout();
                NormalizeDockFillSizes(
                    form);

                VerifyControlTree(
                    page,
                    pagePath,
                    scenario,
                    failures);

                if (checkRecoveryConnection &&
                    string.Equals(
                        page.Text,
                        "Recovery",
                        StringComparison.OrdinalIgnoreCase))
                {
                    VerifyMainConnectionBar(
                        page,
                        pagePath,
                        scenario,
                        failures);
                }
            }
        }
        finally
        {
            if (originalIndex >= 0 &&
                originalIndex <
                tabs.TabPages.Count)
            {
                tabs.SelectedIndex =
                    originalIndex;
            }
        }
    }

    private static void VerifyControlTree(
        Control root,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        foreach (Control control in
                 root.Controls)
        {
            if (!control.Visible)
                continue;

            if (control is UiStatusLabel statusLabel)
            {
                VerifyStatusSurface(
                    statusLabel,
                    formName,
                    scenario,
                    failures);
            }

            if (control is ListView listView &&
                listView.Columns.Count > 0)
            {
                if (!UiStyle.HasConfiguredListViewColumns(
                        listView))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: ListView '{ControlName(listView)}' bypasses shared DPI-aware column configuration.");
                }

                foreach (ColumnHeader column in
                         listView.Columns)
                {
                    var minimumHeaderWidth =
                        UiStyle.GetMinimumListViewHeaderWidth(
                            listView,
                            column.Text);

                    if (column.Width <
                        minimumHeaderWidth)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}: ListView header '{column.Text}' may clip at the active text/DPI scale " +
                            $"(Width={column.Width}, Minimum={minimumHeaderWidth}).");
                    }
                }
            }

            if (control is FlowLayoutPanel flow &&
                flow.FlowDirection is
                    FlowDirection.LeftToRight or
                    FlowDirection.RightToLeft &&
                !flow.WrapContents)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: horizontal FlowLayoutPanel '{ControlName(flow)}' cannot wrap.");
            }

            if (control is Button button &&
                !string.IsNullOrWhiteSpace(
                    button.Text))
            {
                var preferred =
                    button.GetPreferredSize(
                        Size.Empty);

                if (!button.AutoSize)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: button '{button.Text}' is not using shared AutoSize behavior.");
                }

                if (button.MinimumSize.Height <
                    UiStyle.MinimumButtonHeight)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: button '{button.Text}' minimum height is {button.MinimumSize.Height}, expected at least {UiStyle.MinimumButtonHeight}.");
                }

                if (!button.AutoSize &&
                    button.Dock !=
                    DockStyle.Fill &&
                    (button.ClientSize.Width <
                         preferred.Width ||
                     button.ClientSize.Height <
                         preferred.Height))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: button '{button.Text}' may clip its caption.");
                }
            }

            if (control is Label label &&
                label.AutoSize &&
                label.MaximumSize.Width > 0 &&
                label.Parent is not null &&
                label.Parent.ClientSize.Width > 0)
            {
                var parentRight =
                    label.Parent.ClientSize.Width -
                    label.Parent.Padding.Right;

                if (label.Right >
                    parentRight +
                    2)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: wrapping label '{ShortText(label.Text)}' exceeds the visible parent width " +
                        $"(X={label.Left}, Width={label.Width}, Right={label.Right}, ParentWidth={label.Parent.ClientSize.Width}; " +
                        $"Ancestors={DescribeAncestors(label)}).");
                }
            }

            if (control is TabControl nestedTabs)
            {
                var form =
                    nestedTabs.FindForm() as
                    DpiAwareForm;

                if (form is not null)
                {
                    VerifyTabControlPages(
                        nestedTabs,
                        formName,
                        scenario,
                        failures,
                        form);
                }

                continue;
            }

            VerifyControlTree(
                control,
                formName,
                scenario,
                failures);
        }
    }

    private static void VerifyStatusSurface(
        UiStatusLabel status,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (status.Image is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' has no DPI-safe glyph.");
        }

        if (status.TabStop)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' must not participate in keyboard tab navigation.");
        }

        if (string.IsNullOrWhiteSpace(
                status.AccessibleName) ||
            string.IsNullOrWhiteSpace(
                status.AccessibleDescription))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' is missing accessibility metadata.");
        }

        if (!string.IsNullOrWhiteSpace(
                status.Text) &&
            status.AccessibleDescription is not null &&
            !status.AccessibleDescription.Contains(
                status.Text,
                StringComparison.Ordinal))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: status '{ControlName(status)}' accessibility description is stale.");
        }
    }

    private static void VerifyHighContrastStatusPalette(
        List<string> failures)
    {
        foreach (var kind in
                 Enum.GetValues<UiStatusKind>())
        {
            var highContrast =
                UiStyle.ResolveStatusColors(
                    kind,
                    highContrast: true);

            if (highContrast.BackColor !=
                    SystemColors.Window ||
                highContrast.ForeColor !=
                    SystemColors.WindowText)
            {
                failures.Add(
                    $"High Contrast status palette for {kind} does not use Windows system colors.");
            }
        }
    }

    private static void VerifyListViewColumnScaling(
        List<string> failures)
    {
        const int logical =
            150;

        var scale100 =
            UiStyle.ScaleLogicalPixels(
                logical,
                UiStyle.BaselineDpi);
        var scale150 =
            UiStyle.ScaleLogicalPixels(
                logical,
                144);
        var scale200 =
            UiStyle.ScaleLogicalPixels(
                logical,
                192);
        var roundTrip =
            UiStyle.ToLogicalPixels(
                scale200,
                192);

        if (scale100 != 150 ||
            scale150 != 225 ||
            scale200 != 300 ||
            roundTrip != logical)
        {
            failures.Add(
                $"ListView column scaling failed: logical={logical}; 100%={scale100}; 150%={scale150}; 200%={scale200}; round-trip={roundTrip}.");
        }
    }

    private static void VerifyStatusGlyphScaling(
        List<string> failures)
    {
        var normal =
            UiStatusGlyphs.Get(
                UiStatusKind.Success,
                UiStyle.BaselineDpi,
                Color.DarkGreen);
        var scale200 =
            UiStatusGlyphs.Get(
                UiStatusKind.Success,
                UiStyle.BaselineDpi * 2,
                Color.DarkGreen);

        if (normal.Width <= 0 ||
            normal.Height <= 0 ||
            scale200.Width <
                normal.Width * 2 - 1 ||
            scale200.Height <
                normal.Height * 2 - 1)
        {
            failures.Add(
                $"Status glyph scaling is not DPI-safe: 96-DPI={normal.Width}x{normal.Height}; 192-DPI={scale200.Width}x{scale200.Height}.");
        }
    }

    private static void VerifyMainConnectionBar(
        Control root,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var source =
            FindByName(
                root,
                "RecoverySourceSelector");
        var connect =
            FindByName(
                root,
                "ConnectAdButton");
        var cancel =
            FindByName(
                root,
                "ConnectAdCancelButton");
        var status =
            FindByName(
                root,
                "AdConnectionStatus");
        var advanced =
            FindByName(
                root,
                "AdvancedConnectionButton");

        if (source is null ||
            connect is null ||
            cancel is null ||
            status is null ||
            advanced is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: recovery connection controls were not found by name.");
            return;
        }

        if (source.Parent is not
                TableLayoutPanel table ||
            !ReferenceEquals(
                connect.Parent,
                table) ||
            !ReferenceEquals(
                cancel.Parent,
                table) ||
            !ReferenceEquals(
                status.Parent,
                table) ||
            !ReferenceEquals(
                advanced.Parent,
                table))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Source, Connect, Cancel and connection status are not in one responsive table.");
            return;
        }

        if (table.GetColumnSpan(
                status) !=
            table.ColumnCount)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: connection status does not span the full connection bar.");
        }

        if (source.Width <= 0 ||
            connect.Width <= 0 ||
            Math.Abs(
                source.Width -
                connect.Width) >
            24)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Live AD selector and Connect button are not visually balanced.");
        }

        if (source.TabIndex != 0 ||
            connect.TabIndex != 1 ||
            cancel.TabIndex != 2 ||
            advanced.TabIndex != 3)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: connection keyboard order must be Source -> Connect -> Cancel -> Advanced.");
        }

        if (status.TabStop)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: connection status must not participate in keyboard tab navigation.");
        }

    }

    private static T? FindFirst<T>(
        Control root)
        where T : Control
    {
        if (root is T typed)
            return typed;

        foreach (Control child in
                 root.Controls)
        {
            var found =
                FindFirst<T>(
                    child);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static Control? FindByName(
        Control root,
        string name)
    {
        if (string.Equals(
                root.Name,
                name,
                StringComparison.Ordinal))
        {
            return root;
        }

        foreach (Control child in
                 root.Controls)
        {
            var found =
                FindByName(
                    child,
                    name);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static string DescribeAncestors(
        Control control)
    {
        var parts =
            new List<string>();
        var current =
            control.Parent;

        while (current is not null &&
               parts.Count < 8)
        {
            parts.Add(
                $"{current.GetType().Name}[{current.ClientSize.Width}x{current.ClientSize.Height}]");

            current =
                current.Parent;
        }

        return string.Join(
            " > ",
            parts);
    }

    private static string ControlName(
        Control control) =>
        string.IsNullOrWhiteSpace(
            control.Name)
            ? control.GetType().Name
            : control.Name;

    private static string ShortText(
        string value)
    {
        var text =
            (value ?? string.Empty)
                .Replace(
                    Environment.NewLine,
                    " ",
                    StringComparison.Ordinal)
                .Trim();

        return text.Length <= 48
            ? text
            : text[..48] + "...";
    }
}
