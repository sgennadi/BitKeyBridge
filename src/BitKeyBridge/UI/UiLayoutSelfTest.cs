namespace BitKeyBridge;

public static class UiLayoutSelfTest
{
    private sealed class RecreatedHandleLayoutForm : DpiAwareForm
    {
        internal void RecreateNativeHandleForTest() => RecreateHandle();
    }

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
            "Scale175",
            new Size(
                1200,
                780),
            1.75F),
        new(
            "RdpCompact200",
            new Size(
                1024,
                768),
            2.00F),
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

        // Runtime check: a native Explorer icon is insufficient if WinForms
        // modal windows still use the default framework title-bar icon.
        using (var brandIconForm = new DpiAwareForm())
        {
            if (!brandIconForm.HasBrandIconForUiSelfTest)
                failures.Add("BitKeyBridge branded icon is not assigned to WinForms dialogs.");
        }

        VerifyNipkgBrandBanner(failures);
        VerifyModernAccentAndDynamicTheme(failures);

        VerifyStatusGlyphScaling(
            failures);

        VerifyUnchangedStatusDoesNotRepaint(failures);
        VerifyVisibleLabelReflowCoalescing(failures);

        VerifyFixedHeaderWidthCaching(failures);

        VerifyWrappingStatusMeasurement(
            failures);

        VerifyStatusGlyphAlignmentInset(failures);

        VerifyHighContrastStatusPalette(
            failures);

        VerifyListViewColumnScaling(
            failures);

        VerifyLapsHelpdeskStates(
            failures);

        VerifyHebrewIdentityDirection(
            failures);

        VerifyHelpdeskChecklistLocalization(
            failures);

        VerifyIntuneSetupWizardNavigation(failures);
        VerifyDeviceCodeCancellation(failures);
        VerifyDevicesCloudShortcut(failures);
        VerifyStartupSettingsWithoutAdministrativePages(failures);

        VerifyDiagnosticCopyVisibility(failures);

        // A DPI report must distinguish the initially measured MainForm from
        // any auxiliary form, and include only aggregate phase timings.
        var performanceReport = UiLayoutTelemetry.Report();
        foreach (var expected in new[]
        {
            "Initial DPI - main window",
            "Initial DPI - other windows",
            "DPI phase - theme installation",
            "DPI phase - control defaults",
            "DPI phase - work-area adjustment",
            "DPI phase - WinForms PerformLayout",
            "DPI phase - label widths",
            "DPI phase - theme refresh",
            "Language EN/RU -> Hebrew",
            "Language Hebrew -> EN/RU",
            "Language EN <-> RU",
            "p50=", "p95=", "recent="
        })
            if (!performanceReport.Contains(expected, StringComparison.Ordinal))
                failures.Add("Missing DPI timing phase: " + expected);

        VerifyStartupWizardTextWrapping(failures);
        VerifyModalDialogLocalization(failures);
        VerifyDevice360CardIsolation(failures);

        VerifyStartSourcePolicies(failures);

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

    private static void VerifyVisibleLabelReflowCoalescing(List<string> failures)
    {
        try
        {
            using var dialog = new RecreatedHandleLayoutForm
            {
                Size = new Size(620, 360),
                StartPosition = FormStartPosition.Manual
            };
            using var host = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            var first = new Label
            {
                AutoSize = true,
                Visible = false,
                MaximumSize = new Size(900, 0),
                Text = "First fixed-width label on a hidden page"
            };
            var second = new Label
            {
                AutoSize = true,
                Visible = false,
                MaximumSize = new Size(900, 0),
                Text = "Second fixed-width label on a hidden page"
            };
            host.Controls.Add(first);
            host.Controls.Add(second);
            dialog.Controls.Add(host);
            dialog.Show();
            Application.DoEvents();

            var baseline = dialog.VisibleLabelWidthBatchCountForUiSelfTest;
            first.Visible = true;
            second.Visible = true;
            if (dialog.PendingVisibleLabelWidthsForUiSelfTest != 2)
                failures.Add("Visible label widths were not coalesced before UI dispatch.");
            Application.DoEvents();

            if (dialog.VisibleLabelWidthBatchCountForUiSelfTest != baseline + 1 ||
                dialog.PendingVisibleLabelWidthsForUiSelfTest != 0)
                failures.Add("Two visible labels scheduled more than one layout callback.");
            if (first.MaximumSize.Width <= 0 || second.MaximumSize.Width <= 0 ||
                first.MaximumSize.Width > host.ClientSize.Width ||
                second.MaximumSize.Width > host.ClientSize.Width)
                failures.Add("Batched visible labels were not constrained to their viewport.");

            // A no-op pump must not perform an additional parent reflow.
            Application.DoEvents();
            if (dialog.VisibleLabelWidthBatchCountForUiSelfTest != baseline + 1)
                failures.Add("Idle UI dispatch repeated a visible-label layout batch.");

            first.Visible = false;
            second.Visible = false;
            first.Visible = true;
            second.Visible = true;
            Application.DoEvents();
            if (dialog.VisibleLabelWidthBatchCountForUiSelfTest != baseline + 2)
                failures.Add("Second tab activation did not coalesce visible labels.");
            if (!UiLayoutTelemetry.Report().Contains(
                    "Visible-label width batches: batches=", StringComparison.Ordinal))
                failures.Add("Non-secret visible-label layout telemetry is missing.");

            // Simulate the native HWND recreation caused by RTL/DPI/RDP.
            // A previously posted callback may be discarded by WinForms;
            // new visible-label updates must not remain permanently blocked.
            var beforeRecreation = dialog.VisibleLabelWidthBatchCountForUiSelfTest;
            first.Visible = false;
            second.Visible = false;
            first.Visible = true;
            second.Visible = true;
            dialog.RecreateNativeHandleForTest();
            Application.DoEvents();
            first.Visible = false;
            second.Visible = false;
            first.Visible = true;
            second.Visible = true;
            Application.DoEvents();
            if (dialog.PendingVisibleLabelWidthsForUiSelfTest != 0 ||
                dialog.VisibleLabelWidthBatchCountForUiSelfTest <= beforeRecreation)
                failures.Add("HWND recreation stranded a coalesced label-width batch.");
        }
        catch (Exception ex)
        {
            failures.Add("Visible-label coalescing regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyFixedHeaderWidthCaching(List<string> failures)
    {
        // Real TextRenderer measurement. The same fixed caption/font/DPI
        // must reuse a result; changes to DPI/font/header style cannot.
        try
        {
            using var regular = new Font(UiStyle.BodyFont, FontStyle.Regular);
            using var bold = new Font(UiStyle.BodyFont, FontStyle.Bold);
            var cache = new UiHeaderWidthCache(capacity: 4);
            var caption = UiStaticCaptionCatalog.TranslateOrOriginal("he", "Back");
            var first = cache.Measure(
                "Back", caption, regular, 96,
                UiHeaderWidthCache.HeaderKind.DataGrid);
            var expected = TextRenderer.MeasureText(
                caption, regular, new Size(4096, 1024),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix).Width +
                UiStyle.ScaleLogicalPixels(26, 96);
            var second = cache.Measure(
                "Back", caption, regular, 96,
                UiHeaderWidthCache.HeaderKind.DataGrid);
            if (first != expected || second != expected ||
                cache.CacheHits != 1 || cache.CacheMisses != 1 ||
                cache.EntryCount != 1)
                failures.Add("Fixed header width cache did not reuse an identical measurement.");

            cache.Measure("Back", caption, regular, 144,
                UiHeaderWidthCache.HeaderKind.DataGrid);
            cache.Measure("Back", caption, regular, 96,
                UiHeaderWidthCache.HeaderKind.ListView);
            cache.Measure("Back", caption, bold, 96,
                UiHeaderWidthCache.HeaderKind.DataGrid);
            if (cache.CacheMisses != 4 || cache.EntryCount != 4)
                failures.Add("Fixed header width cache mixed font, DPI or rendering styles.");

            // Device identifiers, account values and recovery keys are not
            // permitted as a key, even if caller passes a text measurement.
            var unknownRejected = false;
            try
            {
                cache.Measure("device-identity-not-a-caption", "SENSITIVE",
                    regular, 96, UiHeaderWidthCache.HeaderKind.DataGrid);
            }
            catch (ArgumentException)
            {
                unknownRejected = true;
            }
            if (!unknownRejected)
                failures.Add("Fixed header width cache accepted arbitrary live metadata.");

            // Supplying a valid catalogue source alone is not enough:
            // reject arbitrary caller data masquerading as its translation.
            var spoofedRejected = false;
            try
            {
                cache.Measure("Back", "SENSITIVE", regular, 96,
                    UiHeaderWidthCache.HeaderKind.DataGrid);
            }
            catch (ArgumentException)
            {
                spoofedRejected = true;
            }
            if (!spoofedRejected || cache.CacheMisses != 4)
                failures.Add("Fixed header width cache accepted an untrusted caption value.");

            cache.Measure("Back", caption, regular, 192,
                UiHeaderWidthCache.HeaderKind.DataGrid);
            if (cache.EntryCount != cache.Capacity || cache.CacheMisses != 5)
                failures.Add("Fixed header cache exceeded its bounded capacity.");
            cache.Measure("Back", caption, regular, 96,
                UiHeaderWidthCache.HeaderKind.DataGrid);
            if (cache.CacheMisses != 6)
                failures.Add("Fixed header cache did not evict the oldest measurement.");
            if (!UiLayoutTelemetry.Report().Contains(
                    "Fixed-header width cache: hits=", StringComparison.Ordinal))
                failures.Add("Non-secret header cache diagnostics were not reported.");
        }
        catch (Exception ex)
        {
            failures.Add("Fixed header width cache regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyStartupWizardTextWrapping(List<string> failures)
    {
        // The chooser is purely local: no AD/Graph authentication, no
        // credentials and no recovery values. Its prose must wrap at
        // 100-200% DPI/RDP without extending outside the actual dialog.
        try
        {
            using var wizard = new StartupConnectionWizardDialog(
                StartConnectionPolicy.Both);
            wizard.Show();
            try
            {
                var root = wizard.Controls.Find(
                    "StartupConnectionWizardLayout", true).FirstOrDefault()
                    as TableLayoutPanel;
                var caption = wizard.Controls.Find(
                    "StartupConnectionIntro", true).FirstOrDefault()
                    as Label;
                var explanation = wizard.Controls.Find(
                    "StartupConnectionExplanation", true).FirstOrDefault()
                    as Label;
                if (root is null || caption is null || explanation is null)
                {
                    failures.Add("Connection chooser wrapping controls are absent.");
                    return;
                }

                foreach (var width in new[] { 620, 750, 950 })
                {
                    wizard.ClientSize = new Size(width, 525);
                    Application.DoEvents();
                    root.PerformLayout();
                    var available = root.ClientSize.Width -
                        root.Padding.Horizontal + 2;
                    if (available < 120 ||
                        caption.MaximumSize.Width <= 0 ||
                        caption.MaximumSize.Width > available ||
                        explanation.MaximumSize.Width <= 0 ||
                        explanation.MaximumSize.Width > available ||
                        caption.AutoEllipsis || explanation.AutoEllipsis)
                    {
                        failures.Add(
                            "Connection chooser prose is clipped or exceeds its live DPI width.");
                        break;
                    }
                }
            }
            finally
            {
                wizard.Close();
            }
        }
        catch (Exception ex)
        {
            failures.Add("Connection chooser responsive prose: " +
                ex.GetType().Name + ": " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyDevice360CardIsolation(List<string> failures)
    {
        // Native WinForms test with ONLY synthetic metadata. No login, LDAP,
        // Graph, recovery-key lookup, LAPS secret, audit write or clipboard.
        const string syntheticRecoveryId = "UI-DO-NOT-DISPLAY-RECOVERY-ID";
        var cloudRecord = new UnifiedDeviceInfo
        {
            ComputerName = "UI-TEST-INTUNE-001",
            IdentityKey = "INTUNE:MANAGED:ui-example-001",
            IdentityStatus = "Intune object",
            FoundInEntra = true,
            FoundInIntune = true,
            EntraDeviceId = "0f29ed68-8f58-478e-bb0f-89bc64aa1a2b",
            ManagedDeviceId = "ui-example-001",
            OperatingSystem = "Windows",
            OsVersion = "11",
            ComplianceState = "compliant",
            IsEncrypted = true,
            RecoveryIds = [syntheticRecoveryId]
        };
        var cloud = Device360Snapshot.FromUnified(cloudRecord);
        if (!cloud.IdentityVerified || cloud.BitLockerMetadataCount != 1 ||
            cloud.InAd != null || cloud.InEntra != true ||
            cloud.InIntune != true)
            failures.Add("Device 360 source-qualified cloud metadata projection is incorrect.");

        var operational = Device360OperationalStatus.Capture(cloud,
            adConnected: false, cloudSessionPresent: true,
            rbacEnabled: true, ticketRequired: true,
            protectedAuditRequired: true, auditBrokerConfigured: true,
            searchPartial: true);
        if (operational.Ad != Device360SourceEvidence.NotConnectedUnverified ||
            operational.Entra != Device360SourceEvidence.FoundInSearch ||
            operational.Intune != Device360SourceEvidence.FoundInSearch ||
            operational.EffectiveRecoveryRightsChecked || !operational.SearchPartial)
            failures.Add("Device 360 inferred recovery permissions from search metadata.");

        // The structured per-source report can fail even when another
        // source supplied the matching Entra device ID. Fail closed:
        // never turn that linked identifier into a green source tile.
        var cloudFailure = Device360OperationalStatus.Capture(cloud,
            adConnected: false, cloudSessionPresent: true,
            rbacEnabled: false, ticketRequired: false,
            protectedAuditRequired: true, auditBrokerConfigured: true,
            entraLookupFailed: true);
        if (cloudFailure.Entra != Device360SourceEvidence.LookupFailed ||
            cloudFailure.Intune != Device360SourceEvidence.FoundInSearch ||
            !cloudFailure.SearchPartial ||
            cloudFailure.EffectiveRecoveryRightsChecked)
            failures.Add("Device 360 failed to preserve structured Entra source errors.");

        var unverifiedSession = Device360OperationalStatus.Capture(
            new Device360Snapshot(), adConnected: true,
            cloudSessionPresent: true, rbacEnabled: true,
            ticketRequired: true, protectedAuditRequired: true,
            auditBrokerConfigured: true);
        if (unverifiedSession.Ad != Device360SourceEvidence.SessionPresentUnverified ||
            unverifiedSession.Intune != Device360SourceEvidence.SessionPresentUnverified)
            failures.Add("Device 360 confused active sessions with verified device records.");

        var unresolved = Device360Snapshot.FromUnified(new UnifiedDeviceInfo
        {
            ComputerName = "UI-TEST-UNVERIFIED",
            IdentityKey = "ENTRA:KEY:" + syntheticRecoveryId,
            RecoveryIds = [syntheticRecoveryId]
        });
        if (unresolved.IdentityVerified ||
            unresolved.QualifiedIdentity.Contains(syntheticRecoveryId,
                StringComparison.Ordinal))
            failures.Add("Device 360 accepted a recovery ID as a device identity.");

        var ad = Device360Snapshot.FromHome(new HomeSearchResult
        {
            ComputerName = "UI-TEST-AD-001",
            AdComputerDistinguishedName =
                "CN=UI-TEST-AD-001,OU=Test,DC=example,DC=com",
            BitLockerKeyCount = 1,
            LapsCandidateFound = true,
            LapsStatus = "Detected"
        });
        if (!ad.IdentityVerified || ad.InAd != true ||
            !ad.LapsMetadataDetected || ad.BitLockerMetadataCount != 1)
            failures.Add("Device 360 AD/LAPS metadata projection is incorrect.");

        try
        {
            foreach (var (language, snapshot) in new[]
            {
                ("en", ad),
                ("ru", ad),
                ("he", cloud)
            })
            {
                // Exercise source-specific failures and usable-but-unverified
                // sessions rather than only the shortest disconnected labels.
                var tileStatus = Device360OperationalStatus.Capture(snapshot,
                    adConnected: true, cloudSessionPresent: true,
                    rbacEnabled: true, ticketRequired: true,
                    protectedAuditRequired: true, auditBrokerConfigured: true,
                    intuneLookupFailed: language == "ru",
                    entraLookupFailed: language == "he");
                using var dialog = new Device360Dialog(snapshot, language,
                    operational: tileStatus);
                dialog.Show();
                dialog.ClientSize = new Size(980, 700);
                Application.DoEvents();
                dialog.PerformLayout();

                var sections = FindByName(dialog, "Device360CardSections");
                var recovery = FindByName(dialog, "Device360CardRecoverySection");
                var adSource = FindByName(dialog, "Device360SourceAd");
                var entraSource = FindByName(dialog, "Device360SourceEntra");
                var intuneSource = FindByName(dialog, "Device360SourceIntune");
                var close = FindByName(dialog, "Device360CardClose");
                if (dialog.RightToLeftLayout != (language == "he") ||
                    sections is not TableLayoutPanel ||
                    recovery is not UiRecoveryCard ||
                    adSource is not Label ||
                    entraSource is not Label ||
                    intuneSource is not Label ||
                    close is not Button)
                    failures.Add("Device 360 RTL/control regression (" + language +
                        "): FormRTL=" + dialog.RightToLeft +
                        "; Mirrored=" + dialog.RightToLeftLayout +
                        "; Sections=" + (sections is TableLayoutPanel) +
                        "; Recovery=" + (recovery is UiRecoveryCard) +
                        "; AD=" + (adSource is Label) +
                        "; Entra=" + (entraSource is Label) +
                        "; Intune=" + (intuneSource is Label) +
                        "; Close=" + (close is Button) + ".");

                // This regression checks the actual native client viewport,
                // not only GetPreferredSize or successful UI event dispatch.
                // The previous footer was cut off in all three CI PNGs.
                if (FindByName(dialog, "Device360CardActions") is not TableLayoutPanel footer)
                {
                    failures.Add("Device 360 footer grid is missing: " + language + ".");
                }
                else
                {
                    var buttons = footer.Controls.OfType<Button>().ToArray();
                    if (buttons.Length < 2 || buttons.Length > 4)
                        failures.Add("Device 360 footer button count is invalid: " + language + ".");
                    // An odd number of routes must not leave the last row
                    // half-empty. Preserve two equal columns for four actions.
                    var lastFooterButton = buttons
                        .OrderBy(button => footer.GetRow(button))
                        .ThenBy(button => footer.GetColumn(button))
                        .LastOrDefault(button => footer.GetRow(button) ==
                            (buttons.Length - 1) / 2);
                    if (buttons.Length % 2 != 0 &&
                        (lastFooterButton is null ||
                         footer.GetColumn(lastFooterButton) != 0 ||
                         footer.GetColumnSpan(lastFooterButton) != 2))
                        failures.Add("Device 360 last action did not span the row: " +
                            language + ".");
                    if (buttons.Length % 2 == 0 &&
                        buttons.Any(button => footer.GetColumnSpan(button) != 1))
                        failures.Add("Device 360 even footer actions overlap columns: " +
                            language + ".");
                    var expectedCloseColumn = language == "he" ? 1 : 0;
                    if (footer.RightToLeft != RightToLeft.No ||
                        FindByName(dialog, "Device360CardClose") is not Button closeAction ||
                        footer.GetColumn(closeAction) != expectedCloseColumn)
                        failures.Add("Device 360 footer was mirrored twice: " + language + ".");
                    foreach (var button in buttons)
                    {
                        var visibleBounds = dialog.RectangleToClient(
                            button.RectangleToScreen(button.ClientRectangle));
                        var area = dialog.ClientRectangle;
                        // Bottom-border clearance is required: a previous
                        // version passed ClientRectangle geometry yet painted
                        // buttons touching the PNG's last pixel.
                        var bottomSafetyInset = UiStyle.ScaleLogicalPixels(12,
                            dialog.DeviceDpi);
                        if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0 ||
                            visibleBounds.Left < area.Left ||
                            visibleBounds.Top < area.Top ||
                            visibleBounds.Right > area.Right ||
                            visibleBounds.Bottom > area.Bottom - bottomSafetyInset)
                        {
                            failures.Add("Device 360 action clipped (" + language +
                                ", " + button.Name + "): bounds=" +
                                visibleBounds + ", client=" + area + ".");
                        }
                    }
                }

                // The 980x700 screenshot alone did not catch truncated
                // localized source statuses or missing footer borders at the
                // window's documented minimum size. Resize the *real shown*
                // WinForms form; do not assert only preferred dimensions.
                foreach (var compactSize in new[]
                {
                    new Size(720, 540),
                    new Size(610, 440)
                })
                {
                    dialog.ClientSize = compactSize;
                    Application.DoEvents();
                    dialog.PerformLayout();
                    Application.DoEvents();

                    foreach (var tile in new[] { adSource, entraSource, intuneSource })
                    {
                        if (tile is not Label tileLabel)
                            continue;
                        var measured = TextRenderer.MeasureText(
                            tileLabel.Text, tileLabel.Font,
                            new Size(Math.Max(1, tileLabel.ClientSize.Width), 10000),
                            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                        if (measured.Height > tileLabel.ClientSize.Height + 2)
                            failures.Add("Device 360 localized source text clipped (" +
                                language + ", " + compactSize.Width + "px, " +
                                tileLabel.Name + "): measured=" + measured.Height +
                                ", available=" + tileLabel.ClientSize.Height + ".");
                    }

                    if (FindByName(dialog, "Device360CardActions") is TableLayoutPanel compactFooter)
                    {
                        var safeBottom = dialog.ClientRectangle.Bottom -
                            UiStyle.ScaleLogicalPixels(12, dialog.DeviceDpi);
                        foreach (var button in compactFooter.Controls.OfType<Button>())
                        {
                            var bounds = dialog.RectangleToClient(
                                button.RectangleToScreen(button.ClientRectangle));
                            if (bounds.Width <= 0 || bounds.Height <= 0 ||
                                bounds.Left < 0 || bounds.Top < 0 ||
                                bounds.Right > dialog.ClientRectangle.Right ||
                                bounds.Bottom > safeBottom)
                                failures.Add("Device 360 compact footer clipped (" +
                                    language + ", " + compactSize.Width + "px, " +
                                    button.Name + "): " + bounds + ".");
                        }
                    }
                    else
                    {
                        failures.Add("Device 360 compact footer missing (" +
                            language + ", " + compactSize.Width + "px).");
                    }

                    if (compactSize.Width == 610 &&
                        string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                            "true", StringComparison.OrdinalIgnoreCase) &&
                        Environment.GetEnvironmentVariable(
                            "BITKEYBRIDGE_SYNTHETIC_UI_SNAPSHOTS") == "1")
                    {
                        using var compactBitmap = new Bitmap(dialog.ClientSize.Width,
                            dialog.ClientSize.Height);
                        dialog.DrawToBitmap(compactBitmap,
                            new Rectangle(Point.Empty, compactBitmap.Size));
                        compactBitmap.Save(Path.Combine(AppContext.BaseDirectory,
                            "ui-device-360-" + language + "-compact.png"),
                            System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                // Preserve the established full-size top/policy screenshots.
                dialog.ClientSize = new Size(980, 700);
                Application.DoEvents();
                dialog.PerformLayout();

                if (language == "he" &&
                    FindByName(dialog, "Device360SourceStates") is TableLayoutPanel sourceTable &&
                    ((sourceTable.GetColumn(
                        FindByName(dialog, "Device360SourceAdCard")!) != 2) ||
                     (sourceTable.GetColumn(
                        FindByName(dialog, "Device360SourceIntuneCard")!) != 0)))
                    failures.Add("Device 360 source tiles are not mirrored in Hebrew.");

                if ((language == "he") !=
                    (FindByName(dialog, "Device360RouteDevices") is Button) ||
                    (language != "he") !=
                    (FindByName(dialog, "Device360RouteBitLocker") is Button))
                    failures.Add("Device 360 source-qualified navigation is incorrect: " +
                        language + ".");

                if (FindByName(dialog, "Device360CardAccessSection") is not UiRecoveryCard ||
                    FindByName(dialog, "Device360AuthorizationResult") is not TextBox rights ||
                    rights.Text != UiLanguageService.Translate(language,
                        "Device360AccessUnchecked") ||
                    FindByName(dialog, "Device360RouteAccessHealth") is not Button)
                    failures.Add("Device 360 access-policy/navigation regression: " +
                        language + ".");

                var texts = new List<string>();
                void Collect(Control control)
                {
                    texts.Add(control.Text ?? string.Empty);
                    foreach (Control child in control.Controls)
                        Collect(child);
                }
                Collect(dialog);
                if (texts.Any(value => value.Contains(syntheticRecoveryId,
                        StringComparison.Ordinal)) ||
                    texts.Any(value => value.Contains("Recovery IDs:",
                        StringComparison.Ordinal)))
                    failures.Add("Device 360 leaked a recovery identifier: " + language + ".");

                if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                        "true", StringComparison.OrdinalIgnoreCase) &&
                    Environment.GetEnvironmentVariable(
                        "BITKEYBRIDGE_SYNTHETIC_UI_SNAPSHOTS") == "1")
                {
                    using var bitmap = new Bitmap(dialog.ClientSize.Width,
                        dialog.ClientSize.Height);
                    dialog.DrawToBitmap(bitmap,
                        new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(AppContext.BaseDirectory,
                        "ui-device-360-" + language + ".png"),
                        System.Drawing.Imaging.ImageFormat.Png);

                    // Also render the new policy/source-completeness section,
                    // which is deliberately below the identity and recovery
                    // information in the scrollable device overview.
                    if (FindByName(dialog, "Device360CardScroll") is Panel scroller)
                    {
                        scroller.AutoScrollPosition = new Point(0,
                            Math.Max(0, scroller.DisplayRectangle.Height -
                                scroller.ClientSize.Height));
                        Application.DoEvents();
                        using var policyBitmap = new Bitmap(dialog.ClientSize.Width,
                            dialog.ClientSize.Height);
                        dialog.DrawToBitmap(policyBitmap,
                            new Rectangle(Point.Empty, policyBitmap.Size));
                        policyBitmap.Save(Path.Combine(AppContext.BaseDirectory,
                            "ui-device-360-" + language + "-policy.png"),
                            System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                dialog.Close();
            }

            using var blocked = new Device360Dialog(
                Device360Snapshot.FromHome(new HomeSearchResult
                {
                    ComputerName = "UI-TEST-COLLISION",
                    IdentityAmbiguous = true,
                    AdComputerDistinguishedName =
                        "CN=UI-TEST-COLLISION,DC=example,DC=com"
                }), "en");
            blocked.CreateControl();
            if (FindByName(blocked, "Device360RouteBitLocker") is not null ||
                FindByName(blocked, "Device360RouteLaps") is not null ||
                FindByName(blocked, "Device360RouteDevices") is not null)
                failures.Add("Ambiguous Device 360 identity has a recovery shortcut.");
        }
        catch (Exception ex)
        {
            failures.Add("Device 360 metadata UI regression: " +
                ex.GetType().Name + ": " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyModalDialogLocalization(List<string> failures)
    {
        static bool HasText(Control root, string text)
        {
            var queue = new Stack<Control>();
            queue.Push(root);
            while (queue.Count != 0)
            {
                var current = queue.Pop();
                if (current.Text == text)
                    return true;
                foreach (Control child in current.Controls)
                    queue.Push(child);
            }
            return false;
        }

        try
        {
            using var prompt = new AdCredentialPromptDialog(@"DOMAIN\helpdesk");
            using var startup = new StartupConnectionWizardDialog(
                StartConnectionPolicy.ActiveDirectory);

            // Test real TextBox and ToolStrip properties, not only labels.
            // Previously translated placeholders did not return to English
            // after switching RU/HE. Caller-owned dynamic hints and text
            // must not be overwritten by the static-caption localizer.
            using var hints = new DpiAwareForm { Text = "Choose a connection" };
            using var menu = new ContextMenuStrip();
            var localizedAction = new ToolStripMenuItem("Back")
            {
                ToolTipText = "Copy diagnostics",
                Tag = "fixed-command-token"
            };
            menu.Items.Add(localizedAction);
            var tenant = new TextBox
            {
                Name = "LanguageRoundtripTenant",
                PlaceholderText = "Tenant GUID or verified domain",
                Text = "contoso.onmicrosoft.com",
                ContextMenuStrip = menu
            };
            var dynamicHint = new TextBox
            {
                Name = "LanguageRoundtripDynamic",
                PlaceholderText = "custom hint: 12345",
                Text = "live value: 67890"
            };
            // Exercise native ListView/DataGridView header captions in addition
            // to ToolStripItem.Text: callers may later replace any with live
            // device/identity metadata that must remain untouched.
            var localizedList = new ListView { Name = "LanguageRoundtripList" };
            UiStyle.ConfigureListViewColumns(localizedList, ("Back", 120));
            var localizedGrid = new DataGridView { Name = "LanguageRoundtripGrid" };
            localizedGrid.Columns.AddRange(UiStyle.CreateSortableTextColumn(
                "LanguageRoundtripHeader", "Back"));
            hints.Controls.Add(tenant);
            hints.Controls.Add(dynamicHint);
            hints.Controls.Add(localizedList);
            hints.Controls.Add(localizedGrid);

            foreach (var (language, domainTitle, startupTitle, connectText) in
                new[]
                {
                    ("ru", "Подключение к Active Directory", "Выберите подключение", "Подключиться"),
                    ("he", "התחברות ל-Active Directory", "בחירת חיבור", "התחברות"),
                    ("en", "Connect to Active Directory", "Choose a connection", "Connect"),
                    ("ru", "Подключение к Active Directory", "Выберите подключение", "Подключиться")
                })
            {
                UiDialogLocalization.Apply(prompt, language);
                UiDialogLocalization.Apply(startup, language);
                UiDialogLocalization.Apply(hints, language);
                if (tenant.PlaceholderText !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Tenant GUID or verified domain") ||
                    localizedAction.ToolTipText !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Copy diagnostics") ||
                    localizedAction.Text !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Back") ||
                    localizedList.Columns[0].Text !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(language, "Back") ||
                    localizedGrid.Columns[0].HeaderText !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(language, "Back") ||
                    (string?)localizedAction.Tag != "fixed-command-token" ||
                    tenant.Text != "contoso.onmicrosoft.com" ||
                    dynamicHint.Text != "live value: 67890" ||
                    dynamicHint.PlaceholderText != "custom hint: 12345")
                    failures.Add("RU/HE/EN modal hint roundtrip or live data regression: " +
                        language);
                if (prompt.Text != domainTitle || !HasText(startup, startupTitle) ||
                    !HasText(prompt, connectText))
                    failures.Add("Modal dialog translation missing or stale: " + language);
                // Explanatory labels created from concatenated source
                // literals must be translated, not just dialog buttons.
                if (!HasText(prompt, UiStaticCaptionCatalog.TranslateOrOriginal(
                        language,
                        "Enter the account for this connection. The password stays in this process. " +
                        "A domain or DC can be entered on a standalone computer. Empty DC uses automatic discovery.")))
                    failures.Add("AD credential guidance was not localized: " + language);
                if (!HasText(startup, UiStaticCaptionCatalog.TranslateOrOriginal(
                        language,
                        "Select a source for this session. Recovery passwords remain hidden " +
                        "until an authorized technician explicitly requests them.")))
                    failures.Add("Startup source guidance was not localized: " + language);
                if (prompt.Username != @"DOMAIN\helpdesk")
                    failures.Add("Dialog localization modified supplied AD account data.");
            }

            // Replacing a known hint with runtime information must not
            // silently restore old translations. A NEW known English hint
            // must become the new baseline instead of reverting to the old.
            tenant.PlaceholderText = "operator supplied hint";
            localizedAction.ToolTipText = "runtime tooltip";
            localizedAction.Text = "Current device: 001";
            localizedList.Columns[0].Text = "device-001";
            localizedGrid.Columns[0].HeaderText = "account@example.invalid";
            UiDialogLocalization.Apply(hints, "he");
            if (tenant.PlaceholderText != "operator supplied hint" ||
                localizedAction.ToolTipText != "runtime tooltip" ||
                localizedAction.Text != "Current device: 001" ||
                localizedList.Columns[0].Text != "device-001" ||
                localizedGrid.Columns[0].HeaderText != "account@example.invalid")
                failures.Add("Modal localizer rewrote externally updated hints.");

            tenant.PlaceholderText = "Domain DNS name (optional)";
            localizedAction.ToolTipText = "Copy diagnostics";
            localizedAction.Text = "Copy diagnostics";
            localizedList.Columns[0].Text = "Copy diagnostics";
            localizedGrid.Columns[0].HeaderText = "Copy diagnostics";
            UiDialogLocalization.Apply(hints, "ru");
            if (tenant.PlaceholderText !=
                    UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Domain DNS name (optional)") ||
                localizedAction.ToolTipText !=
                    UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Copy diagnostics") ||
                localizedAction.Text !=
                    UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Copy diagnostics") ||
                localizedList.Columns[0].Text !=
                    UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Copy diagnostics") ||
                localizedGrid.Columns[0].HeaderText !=
                    UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Copy diagnostics"))
                failures.Add("Modal localizer did not adopt new known English hints.");
        }
        catch (Exception ex)
        {
            failures.Add("Modal EN/RU/HE translation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyUnchangedStatusDoesNotRepaint(
        List<string> failures)
    {
        using var status = new UiStatusLabel { Text = "Ready" };
        UiStyle.ConfigureStatusLabel(status);
        UiStyle.SetStatus(status, "Ready", UiStatusKind.Success);
        status.CreateControl();

        var original = status.Image;
        var invalidations = 0;
        status.Invalidated += (_, _) => invalidations++;

        // UI polling can repeat identical status updates many times.
        // There must be no Image replacement or forced repaint.
        UiStyle.SetStatus(status, "Ready", UiStatusKind.Success);
        UiStyle.RefreshStatusLabel(status);
        if (!ReferenceEquals(original, status.Image) ||
            invalidations != 0)
        {
            failures.Add(
                "Unchanged status text/kind forced a repaint or image replacement.");
        }

        UiStyle.SetStatus(status, "Disconnected", UiStatusKind.Warning);
        if (status.Text != "Disconnected" ||
            status.StatusKind != UiStatusKind.Warning)
        {
            failures.Add(
                "Status optimization suppressed an actual state change.");
        }
    }

    private static void VerifyNipkgBrandBanner(List<string> failures)
    {
        // Visual fixture uses static localized copy only: no domain, device,
        // account, clipboard or recovery material is ever rendered or logged.
        try
        {
            using var host = new Panel { Size = new Size(940, 170) };
            using var banner = new UiNipkgBrandBanner();
            host.Controls.Add(banner);
            host.CreateControl();
            banner.CreateControl();
            host.PerformLayout();
            banner.PerformLayout();

            if (banner.Heading.Text != "BitKeyBridge" ||
                banner.Tagline.Text != UiNipkgBrandBanner.IntroCaption ||
                banner.Tagline.MaximumSize.Width <= 0 ||
                banner.Tagline.MaximumSize.Width >
                    banner.ClientSize.Width - banner.Padding.Horizontal + 2)
                failures.Add("NI-style brand header is missing text or its DPI wrapping width.");

            banner.SetCaptionDirection(true);
            if (banner.Tagline.TextAlign != ContentAlignment.MiddleRight ||
                banner.Tagline.Anchor != AnchorStyles.Right)
                failures.Add("NI-style brand header lost Hebrew text alignment.");
            banner.SetCaptionDirection(false);
            if (banner.Tagline.TextAlign != ContentAlignment.MiddleLeft ||
                banner.Tagline.Anchor != AnchorStyles.Left)
                failures.Add("NI-style brand header failed to restore LTR.");

            if (!SystemInformation.HighContrast &&
                banner.Width > 200 && banner.Height > 20)
            {
                using var image = new Bitmap(banner.Width, banner.Height);
                banner.DrawToBitmap(image,
                    new Rectangle(Point.Empty, banner.Size));
                // The gradient must contain visibly different navy/blue
                // regions, not a solid color or a missing paint callback.
                var navyPixel = image.GetPixel(8, image.Height / 2);
                var right = image.GetPixel(image.Width - 8, image.Height / 2);
                if (navyPixel.ToArgb() == right.ToArgb() ||
                    navyPixel.ToArgb() == UiStyle.ModernCanvas.ToArgb())
                    failures.Add("NI-style navy-to-blue banner did not paint.");
            }
        }
        catch (Exception ex)
        {
            failures.Add("NI-style brand banner: " + ex.GetType().Name + ": " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyModernAccentAndDynamicTheme(List<string> failures)
    {
        if (SystemInformation.HighContrast)
            return; // Native high contrast colors must always win.

        try
        {
            using var host = new Panel();
            var search = UiStyle.CreateActionButton("Search");
            search.Name = "HomeUnifiedSearchButton";
            var reveal = UiStyle.CreateActionButton("Reveal Key");
            reveal.Name = "HomeRevealBitLockerButton";
            host.Controls.Add(search);
            host.Controls.Add(reveal);
            // Generic bordered panels still use native Region hit-testing.
            // Test that their deferred Region is rebuilt after HWND creation;
            // buttons instead use antialiased owner-painted rounded chrome.
            var borderedPanel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(120, 40)
            };
            host.Controls.Add(borderedPanel);
            var input = new TextBox { Size = new Size(160, 30) };
            var plainPanel = new Panel { Size = new Size(160, 46) };
            host.Controls.Add(input);
            host.Controls.Add(plainPanel);

            var themeBefore = UiLayoutTelemetry.ThemeCountsForUiSelfTest();
            var perTypeBefore = UiLayoutTelemetry.ThemeInstallCallsForUiSelfTest();
            UiModernTheme.Apply(host);
            var themeAfter = UiLayoutTelemetry.ThemeCountsForUiSelfTest();
            var perTypeAfter = UiLayoutTelemetry.ThemeInstallCallsForUiSelfTest();
            if (themeAfter.Installed - themeBefore.Installed !=
                    perTypeAfter - perTypeBefore ||
                !UiLayoutTelemetry.ThemeInstallReport().Contains(
                    "Theme install - Buttons: calls=", StringComparison.Ordinal))
                failures.Add("NI theme per-control profiling missed installed controls.");
            if (themeAfter.Installed <= themeBefore.Installed ||
                themeAfter.Deferred <= themeBefore.Deferred)
                failures.Add("Theme failed to defer rounded regions before native handles exist.");
            if (!UiModernTheme.IsPrimaryAction(search) ||
                UiModernTheme.IsPrimaryAction(reveal) ||
                search.BackColor != UiStyle.ModernPositiveAccent ||
                !UiModernTheme.IsPositiveAction(search) ||
                UiModernTheme.IsPositiveAction(reveal) ||
                search.ForeColor != Color.White ||
                reveal.BackColor != UiStyle.ModernSurface)
            {
                failures.Add("Modern accent incorrectly styled a recovery-secret action " +
                    "or failed to distinguish the main Search action.");
            }

            // An idempotent second application must preserve already-themed
            // controls, while ControlAdded styles dynamic buttons.
            UiModernTheme.Apply(host);
            // Native handle creation must install the deferred region, and
            // a second idempotent Apply must not repeat full-tree setup.
            var installedBefore = UiLayoutTelemetry.ThemeCountsForUiSelfTest();
            _ = borderedPanel.Handle;
            var appliedAfter = UiLayoutTelemetry.ThemeCountsForUiSelfTest();
            if (appliedAfter.Applied <= installedBefore.Applied ||
                borderedPanel.Region is null)
                failures.Add("Deferred rounded region was not applied on HandleCreated.");

            _ = search.Handle;
            if (search.Region is not null || reveal.Region is not null ||
                search.FlatAppearance.BorderSize != 0)
                failures.Add("Rounded buttons still use clipped native borders.");

            // DPI/font refresh walks the entire tree, unlike idempotent Apply.
            // It must not opt owner-painted buttons or native inputs back into
            // Region clipping, while registered bordered panels keep rounding.
            _ = host.Handle;
            _ = reveal.Handle;
            _ = input.Handle;
            _ = plainPanel.Handle;
            for (var pass = 0; pass < 2; pass++)
            {
                search.Width += 10;
                input.Width += 10;
                borderedPanel.Width += 10;
                UiModernTheme.Refresh(host);
                if (host.Region is not null || search.Region is not null ||
                    reveal.Region is not null || input.Region is not null ||
                    plainPanel.Region is not null)
                    failures.Add("Theme refresh reintroduced native clipping on unregistered controls.");
                if (borderedPanel.Region is null)
                    failures.Add("Theme refresh lost registered panel rounding.");
            }

            // Draw the real FlatStyle.Button to a bitmap: both outer corners
            // must show the parent surface, not the square native fill.
            search.Size = new Size(160, 46);
            using (var image = new Bitmap(search.Width, search.Height))
            {
                search.DrawToBitmap(image,
                    new Rectangle(Point.Empty, search.Size));
                var middle = image.GetPixel(image.Width / 2, image.Height / 2);
                var corner = image.GetPixel(0, 0);
                if (middle.ToArgb() == corner.ToArgb())
                    failures.Add("Antialiased button corner mask was not painted.");
            }

            foreach (var scale in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f })
            {
                var bounds = UiRecoveryGraphics.InsideStrokeBounds(
                    new Size(160, 46), scale);
                if (bounds.Left < scale / 2f ||
                    bounds.Top < scale / 2f ||
                    bounds.Right > 160f - scale / 2f ||
                    bounds.Bottom > 46f - scale / 2f)
                    failures.Add("Rounded border stroke exceeds client bounds.");

                // Render each physical scale into an isolated bitmap. Curved
                // edges must have antialiased intermediate pixels, with no
                // extra stroke drawn outside the target's client rectangle.
                using var pixels = new Bitmap(160, 46);
                using var g = Graphics.FromImage(pixels);
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.PixelOffsetMode =
                    System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                using var curve = UiRecoveryGraphics.RoundRect(
                    bounds, Math.Min(14f * scale, 20f));
                using var brush = new SolidBrush(Color.Black);
                g.FillPath(brush, curve);
                var blended = false;
                for (var y = 0; y < Math.Min(24, pixels.Height); y++)
                {
                    for (var x = 0; x < Math.Min(24, pixels.Width); x++)
                    {
                        var value = pixels.GetPixel(x, y).R;
                        if (value is > 0 and < 255)
                        {
                            blended = true;
                            break;
                        }
                    }
                    if (blended)
                        break;
                }
                if (!blended)
                    failures.Add("Rounded geometry has no antialiased edge pixels.");
            }

            UiModernTheme.Apply(host);
            if (UiLayoutTelemetry.ThemeCountsForUiSelfTest().Installed !=
                appliedAfter.Installed)
                failures.Add("Repeated theme Apply reinstalled themed controls.");
            var retry = UiStyle.CreateActionButton("Retry check");
            retry.Name = "UpdateCenterRetryCheckButton";
            host.Controls.Add(retry);
            if (retry.BackColor != UiStyle.ModernAccent ||
                search.BackColor != UiStyle.ModernPositiveAccent)
                failures.Add("Theme failed to style dynamically added primary controls.");
        }
        catch (Exception ex)
        {
            failures.Add("Modern theme actions: " + ex.GetType().Name + ": " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
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
                "Helpdesk deployment checklist",
                () =>
                {
                    var main = new MainForm(NewConfig(), layoutSelfTest: true);
                    try
                    {
                        var wizard = main.CreateHelpdeskDeploymentWizard();
                        wizard.Disposed += (_, _) => main.Dispose();
                        return wizard;
                    }
                    catch
                    {
                        main.Dispose();
                        throw;
                    }
                }),
            (
                "Environment prerequisites",
                () => new EnvironmentDiagnosticDialog(
                    NewConfig(), cloudConfigured: false, computer: "PC-TEST", runOnOpen: false)),
            (
                "Device diagnostics center",
                () => new AdvancedDiagnosticCenterDialog(
                    NewConfig(),
                    () => Task.FromResult<string?>(null),
                    _ => { },
                    () => { },
                    "PC-TEST")),
            (
                "BitLocker diagnostic commands",
                () => new BitLockerDiagnosticCommandsDialog(
                    BitLockerDiagnosticRunbook.Build(
                        new BitLockerDiagnosticContext { SearchQuery = "PC-TEST" }))),
            (
                "AD credential prompt",
                () =>
                    new AdCredentialPromptDialog(
                        @"DOMAIN\helpdesk")),
            (
                "AD access troubleshooting",
                () =>
                    new AdAccessTroubleshootingDialog(
                        "OU=Workstations,DC=example,DC=com",
                        @"DOMAIN\BitKeyBridge-Recovery-Readers",
                        "BitLocker / LAPS self-test")),
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
                "RBAC setup wizard",
                () =>
                    new RbacSetupWizardDialog(
                        NewConfig())),
            (
                "Intune setup wizard",
                () =>
                    new IntuneSetupWizardDialog(
                        new CloudAuthConfig
                        {
                            TenantId =
                                "contoso.onmicrosoft.com",
                            ClientId =
                                Guid.Empty.ToString(),
                            EnableLapsPermissions =
                                true
                        },
                        _ =>
                            Task.CompletedTask)),
            (
                "Startup connection wizard",
                () => new StartupConnectionWizardDialog(
                    StartConnectionPolicy.ActiveDirectory)),
            (
                "Non-admin Microsoft 365 connection wizard",
                () => new CloudConnectionWizardDialog(
                    new CloudAuthConfig
                    {
                        TenantId = "contoso.onmicrosoft.com",
                        ClientId = Guid.Empty.ToString()
                    })),
            (
                "Recovery workflow preflight",
                () => new RecoveryWorkflowDialog(
                    RecoveryWorkflowReadinessService.Build(
                        new AppConfig
                        {
                            RbacEnabled = true,
                            RequireRecoveryAccessReference = true,
                            RequireProtectedAuditForRecovery = true,
                            AuditBrokerEnabled = true,
                            RbacRecoveryReaders = ["S-1-5-32-544"]
                        },
                        new HomeSearchResult
                        {
                            ComputerName = "PC-12345",
                            AdComputerDistinguishedName = "CN=PC-12345,OU=Clients,DC=example,DC=test",
                            BitLockerKeyCount = 2,
                            LatestRecoveryId = Guid.Empty.ToString(),
                            LapsCandidateFound = true,
                            LapsStatus = "Detected"
                        },
                        adConnected: true,
                        cloudConnected: false,
                        new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" }))),
            (
                "Hebrew recovery workflow preflight",
                () => new RecoveryWorkflowDialog(
                    RecoveryWorkflowReadinessService.Build(
                        new AppConfig(), new HomeSearchResult
                        {
                            ComputerName = "PC-HEBREW", IsCloudRecord = true,
                            SourceIdentity = "ENTRA:DEVICE:some-device-guid",
                            BitLockerKeyCount = 1
                        },
                        adConnected: false, cloudConnected: true,
                        new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" }),
                    "he")),
            (
                "Russian recovery workflow preflight",
                () => new RecoveryWorkflowDialog(
                    RecoveryWorkflowReadinessService.Build(
                        new AppConfig(), new HomeSearchResult
                        {
                            ComputerName = "PC-RUS", BitLockerKeyCount = 1,
                            AdComputerDistinguishedName = "CN=PC-RUS,OU=Clients,DC=example,DC=test"
                        },
                        adConnected: true, cloudConnected: false,
                        new AuthorizationDecision { Allowed = true, Permission = "RecoveryRead" }),
                    "ru")),
            (
                "Recovery access",
                () =>
                    new RecoveryAccessDialog(
                        "PC-12345",
                        Guid.Empty.ToString(),
                        requireReference: true,
                        referencePattern: "^INC-[0-9]+$",
                        referenceExample: "INC-12345")),
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
            // TableLayoutPanel calculates Dock=Fill bounds per cell.
            // Overwriting them with its entire DisplayRectangle makes
            // pinned footers incorrectly fill the whole tab in headless
            // layout tests, producing false clipping errors.
            if (child.Dock ==
                    DockStyle.Fill &&
                child.Parent is not null &&
                child.Parent is not TableLayoutPanel)
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

    private static void VerifyStartupSettingsWithoutAdministrativePages(List<string> failures)
    {
        foreach (var admin in new[] { false, true })
        {
            try
            {
                // Exercise the real startup value loaders, which ordinary
                // layout tests skipped. Values above NumericUpDown's default
                // max=100 reproduce the second restricted-startup failure.
                using var main = new MainForm(
                    new AppConfig
                    {
                        AutoConnectOnStart = false,
                        HealthEndpointEnabled = false,
                        ServiceIdentityMode = "LocalSystem",
                        RemoteApiPort = 8443,
                        SecretDisplaySeconds = 300,
                        SensitiveClipboardSeconds = 120
                    },
                    layoutSelfTest: true,
                    administrationAllowedForLayoutSelfTest: admin);
                if (!main.PreviewStartupSettingsForUiSelfTest(admin))
                    failures.Add("Startup settings/page visibility mismatch; admin=" + admin);
            }
            catch (Exception ex)
            {
                failures.Add("Startup settings; admin=" + admin + ": " +
                    DiagnosticRedaction.Sanitize(ex.GetType().Name + ": " + ex.Message));
            }
        }
    }

    private static void VerifyDevicesCloudShortcut(List<string> failures)
    {
        try
        {
            using var main = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true);
            if (!main.PreviewDevicesCloudShortcutForUiTest())
                failures.Add("Devices missing-Cloud action failed to open Settings > Cloud.");
        }
        catch (Exception ex)
        {
            failures.Add("Devices Cloud navigation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyDeviceCodeCancellation(List<string> failures)
    {
        try
        {
            // No Graph traffic or browser launch; do not Show the modal
            // device-code window in this UI regression.
            using var prompt = new DeviceCodeDialog(new DeviceCodeInfo
            {
                UserCode = "TEST-CODE",
                VerificationUri = "https://microsoft.com/devicelogin",
                Message = "Test fixture only",
                ExpiresIn = 900
            });
            if (FindByName(prompt, "DeviceCodeActions") is not TableLayoutPanel actions ||
                actions.RowCount != 1 || actions.ColumnCount != 2 ||
                FindByName(prompt, "DeviceCodeCancel") is not Button cancel ||
                FindByName(prompt, "DeviceCodeContinue") is not Button proceed ||
                cancel.DialogResult != DialogResult.Cancel ||
                proceed.DialogResult != DialogResult.OK ||
                !ReferenceEquals(prompt.CancelButton, cancel) ||
                !ReferenceEquals(prompt.AcceptButton, proceed))
                failures.Add("Device Code dialog must expose distinct cancel and continue actions.");

            using var main = new MainForm(
                new AppConfig { AutoConnectOnStart = false,
                    HealthEndpointEnabled = false },
                layoutSelfTest: true);
            if (!main.PreviewCloudCancellationForUiSelfTest())
                failures.Add("Cloud Cancel must immediately stop marquee and signal cancellation.");
        }
        catch (Exception ex)
        {
            failures.Add("Device Code cancellation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    /// <summary>
    /// Recreate the screenshot regression: DpiAwareForm forced horizontal
    /// FlowLayoutPanel navigation to wrap, leaving only Cancel visible.
    /// These tests display REAL WinForms handles; they never call Graph or
    /// invoke the Configure & Verify operation.
    /// </summary>
    private static void VerifyIntuneSetupWizardNavigation(List<string> failures)
    {
        var config = new CloudAuthConfig
        {
            TenantId = "organizations",
            ClientId = "11111111-1111-4111-8111-111111111111"
        };
        foreach (var language in new[] { "en", "ru", "he" })
        {
            foreach (var fontScale in new[] { 1F, 2F })
            {
                try
                {
                    using var wizard = new IntuneSetupWizardDialog(
                        config, _ => Task.CompletedTask, language)
                    {
                        StartPosition = FormStartPosition.Manual,
                        ClientSize = new Size(920, 720)
                    };
                    using var font = new Font(UiStyle.BodyFont.FontFamily,
                        UiStyle.BodyFont.Size * fontScale,
                        FontStyle.Regular, GraphicsUnit.Point);
                    wizard.Font = font;
                    wizard.Show();
                    UiDialogLocalization.Apply(wizard, language);

                    if (FindByName(wizard, "IntuneSetupWizardNavigation")
                            is not TableLayoutPanel navigation ||
                        FindByName(wizard, "IntuneSetupWizardFooter")
                            is not TableLayoutPanel footer ||
                        FindByName(wizard, "IntuneSetupWizardBack") is not Button back ||
                        FindByName(wizard, "IntuneSetupWizardNext") is not Button next ||
                        FindByName(wizard, "IntuneSetupWizardCancel") is not Button cancel)
                    {
                        failures.Add("Intune wizard navigation controls are missing: " +
                            language + " / " + fontScale);
                        continue;
                    }

                    var backCell = navigation.GetPositionFromControl(back);
                    var nextCell = navigation.GetPositionFromControl(next);
                    var cancelCell = navigation.GetPositionFromControl(cancel);
                    if (navigation.ColumnCount != 3 ||
                        navigation.RowCount != 1 ||
                        navigation.RightToLeft != RightToLeft.No ||
                        backCell.Column != 0 || backCell.Row != 0 ||
                        nextCell.Column != 1 || nextCell.Row != 0 ||
                        cancelCell.Column != 2 || cancelCell.Row != 0 ||
                        !ReferenceEquals(wizard.AcceptButton, next) ||
                        !ReferenceEquals(wizard.CancelButton, cancel))
                        failures.Add("Intune wizard navigation must remain a single row with keyboard actions: " +
                            language + " / " + fontScale);

                    foreach (var step in new[] { 0, 1, 2 })
                    {
                        wizard.PreviewStepForUiSelfTest(step);
                        UiDialogLocalization.Apply(wizard, language);
                        wizard.PerformLayout();
                        Application.DoEvents();

                        var englishStep = step switch
                        {
                            0 => "Step 1 of 3 — Prerequisites",
                            1 => "Step 2 of 3 — Capabilities",
                            _ => "Step 3 of 3 — Configure & verify"
                        };
                        var expectedStep = UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, englishStep);
                        if (FindByName(wizard, "IntuneWizardStepCaption")
                                is not Label stepHeading ||
                            stepHeading.Text != expectedStep)
                            failures.Add("Intune wizard step title is not localized: " +
                                language + " / step " + step);

                        var viewport = wizard.RectangleToScreen(wizard.ClientRectangle);
                        var footerBounds = footer.RectangleToScreen(footer.ClientRectangle);
                        var navBounds = navigation.RectangleToScreen(navigation.ClientRectangle);
                        if (!viewport.Contains(footerBounds) ||
                            !footerBounds.Contains(navBounds))
                        {
                            failures.Add("Intune wizard footer is clipped: " +
                                language + " / " + fontScale + " / step " + step);
                            continue;
                        }

                        foreach (var button in new[] { back, next, cancel })
                        {
                            var buttonBounds = button.RectangleToScreen(
                                button.ClientRectangle);
                            if (!button.Visible || button.IsDisposed ||
                                buttonBounds.Width < UiStyle.MinimumButtonWidth ||
                                buttonBounds.Height < UiStyle.MinimumButtonHeight ||
                                !navBounds.Contains(buttonBounds) ||
                                !footerBounds.Contains(buttonBounds) ||
                                !viewport.Contains(buttonBounds))
                            {
                                failures.Add("Intune wizard button clipped or missing: " +
                                    button.Name + " / " + language + " / " +
                                    fontScale + " / step " + step);
                            }
                        }

                        var expectedAction = step == 2
                            ? UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Configure & Verify")
                            : UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Next");
                        if (next.Text != expectedAction)
                            failures.Add("Intune wizard Next caption is not localized: " +
                                language + " / step " + step);
                    }

                    wizard.PreviewStepForUiSelfTest(2);
                    UiDialogLocalization.Apply(wizard, language);
                    var expectedReview = UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Setup actions:");
                    if (FindByName(wizard, "IntuneWizardReview")
                            is not RichTextBox review ||
                        !review.Text.Contains(expectedReview,
                            StringComparison.Ordinal) ||
                        !review.Text.Contains("BitlockerKey.Read.All",
                            StringComparison.Ordinal))
                        failures.Add("Intune wizard review translation lost static captions or Graph scope IDs: " +
                            language + " / " + fontScale);

                    if (!wizard.PreviewTenantValidationForUiSelfTest())
                        failures.Add("Intune wizard allowed a missing, generic or malformed tenant: " +
                            language + " / " + fontScale);
                    if (!wizard.PreviewCancellationForUiSelfTest())
                        failures.Add("Intune wizard Cancel failed to stop marquee or dismisses early: " +
                            language + " / " + fontScale);
                    if (wizard.SetupResult is not null)
                        failures.Add("Navigation-only wizard test unexpectedly ran setup.");
                }
                catch (Exception ex)
                {
                    failures.Add("Intune wizard navigation regression (" +
                        language + " / " + fontScale + "): " +
                        DiagnosticRedaction.Sanitize(ex.Message));
                }
            }
        }
    }

    // Synthetic/metadata-only Start fixture. A control can be Visible=true
    // while its entire native rectangle lies below the scroll viewport.
    // Intersect every parent clipping rectangle, including the recovery card
    // and the outer Start page, rather than trusting the Visible property.
    private static void VerifyStartRecoveryButtonsOnScreen(
        MainForm form, List<string> failures)
    {
        if (FindByName(form, "HomeLayoutRoot") is not Control viewport)
        {
            failures.Add("Start viewport is missing for the 1080p recovery action check.");
            return;
        }

        foreach (var name in new[]
        {
            "HomeRevealBitLockerButton", "HomeCopyBitLockerButton",
            "HomeRevealLapsButton", "HomeCopyLapsButton"
        })
        {
            if (FindByName(form, name) is not Button button ||
                button.IsDisposed || !button.Visible ||
                button.ClientSize.Width < 20 || button.ClientSize.Height < 20)
            {
                failures.Add("1080p Start recovery action is absent: " + name);
                continue;
            }

            // Also catch a fully visible but oversized action: an
            // AutoSize+Dock.Fill feedback loop previously inflated the
            // nominal 46px recovery button to 100px at 96 DPI.
            var maxHeight = (int)Math.Ceiling(72d *
                Math.Max(UiStyle.BaselineDpi, button.DeviceDpi) /
                UiStyle.BaselineDpi);
            if (button.ClientSize.Height > maxHeight)
                failures.Add("1080p Start recovery action inflated vertically: " +
                    name + "; height=" + button.ClientSize.Height +
                    "; limit=" + maxHeight + ".");

            var visible = button.RectangleToScreen(button.ClientRectangle);
            var foundViewport = false;
            for (Control? ancestor = button.Parent; ancestor is not null;
                 ancestor = ancestor.Parent)
            {
                visible = Rectangle.Intersect(
                    visible, ancestor.RectangleToScreen(ancestor.ClientRectangle));
                if (!ReferenceEquals(ancestor, viewport))
                    continue;
                foundViewport = true;
                break;
            }

            var viewportBounds = viewport.RectangleToScreen(
                viewport.ClientRectangle);
            var bottomClearance = UiStyle.ScaleLogicalPixels(8,
                button.DeviceDpi);
            if (!foundViewport ||
                visible.Width < button.ClientSize.Width - 4 ||
                visible.Height < button.ClientSize.Height - 4 ||
                button.RectangleToScreen(button.ClientRectangle).Bottom >
                    viewportBounds.Bottom - bottomClearance)
            {
                failures.Add("1080p Start recovery action clipped or touching " +
                    "the bottom viewport border: " + name +
                    "; visible=" + visible.Width + "x" + visible.Height +
                    "; expected=" + button.ClientSize.Width + "x" +
                    button.ClientSize.Height + "; bottomClearance=" +
                    bottomClearance + ". " +
                    form.StartViewportGeometryReport());
            }
        }
    }

    // The generic language regression constructs hidden controls and cannot
    // prove that their translated captions fit the native Start viewport.
    // Run the real 1600x900 selected-device fixture in each UI language;
    // no AD/Graph calls, personal history, clipboard or secrets are used.
    private static void VerifyNativeLocalizedSelectedStart(
        MainForm form, DataGridView results, List<string> failures)
    {
        var selected = results.CurrentRow?.Tag;
        if (selected is null)
        {
            failures.Add("Localized native Start lost its synthetic selection.");
            return;
        }

        try
        {
            foreach (var language in new[] { "ru", "he", "ru", "en" })
            {
                form.PreviewShellLanguageForLayoutTest(language);
                Application.DoEvents();
                form.ReflowMainViewport();
                form.PerformLayout();
                Application.DoEvents();

                var tabs = FindByName(form, "MainWorkspaceTabs") as TabControl;
                if (tabs is null || tabs.SelectedIndex != 0 ||
                    tabs.TabPages[0].Text !=
                        UiLanguageService.Translate(language, "Start") ||
                    tabs.RightToLeftLayout != (language == "he"))
                {
                    failures.Add("Native Start language/RTL navigation mismatch: " +
                        language + ".");
                }

                // Test object identity, not device data or recovery material.
                if (!ReferenceEquals(results.CurrentRow?.Tag, selected) ||
                    results.SelectedRows.Count != 1)
                {
                    failures.Add("Native Start language change lost selection: " +
                        language + ".");
                }

                // The catalog auditor examines only registered fixed captions,
                // including menus and column headers, never live identity data.
                var captions = form.PreviewFixedCaptionAuditForUiSelfTest();
                if (captions.Stale != 0)
                {
                    failures.Add("Native Start has stale fixed translations: " +
                        language + " (count=" + captions.Stale + ").");
                }

                if (language is "ru" or "he")
                {
                    if (FindByName(form, "SecretLifetimeStatus") is not
                            UiStatusLabel lifetime ||
                        lifetime.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "No recovery secret is currently retained."))
                        failures.Add("Native Start lifetime notice was not localized: " +
                            language + ".");

                    // The disconnected Choose-source notice is a known
                    // fixed caption, never a live authentication result.
                    if (FindByName(form, "HomeConnectionSource") is ComboBox source &&
                        source.SelectedIndex == 0 &&
                        (FindByName(form, "HomeCloudConnectionStatus") is not
                            UiStatusLabel chooser ||
                         chooser.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                             language,
                             "Use Connect / Change to choose a source. No credentials are requested until Connect.")))
                        failures.Add("Native Start source instruction was not localized: " +
                            language + ".");

                    var lapsReveal = FindByName(form,
                        "HomeRevealLapsButton") as Button;
                    var lapsCopy = FindByName(form,
                        "HomeCopyLapsButton") as Button;
                    if (lapsReveal?.Text !=
                            UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Reveal Password") ||
                        lapsCopy?.Text !=
                            UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Copy Password"))
                        failures.Add("Start LAPS action captions remain untranslated: " +
                            language + ".");

                    var paneHandle = FindByName(form,
                        "HomeLapsPaneHostDragHandle") as Label;
                    if (paneHandle is null || !paneHandle.Text.Contains(
                            UiLanguageService.Translate(
                                language, "HomePaneDragInstruction"),
                            StringComparison.Ordinal))
                        failures.Add("Start drag-handle instruction remains untranslated: " +
                            language + ".");

                    VerifyStartRecoveryButtonsOnScreen(form, failures);
                    VerifySelectedHomeStatusBounds(form, "NativeLocalizedStart1080p",
                        new Scenario(language + "-Selected1600x900",
                            new Size(1600, 900), 1F), failures);
                    SaveSyntheticStartSnapshotForCi(
                        form, "selected-" + language, failures);
                }
            }

            // Synthetic-only negative test: a runtime message must survive
            // a language round-trip unchanged. Never sample actual data.
            if (FindByName(form, "HomeUnifiedSearchStatus") is UiStatusLabel live)
            {
                var before = live.Text;
                const string synthetic = "UI-TEST-DYNAMIC-STATUS";
                try
                {
                    live.Text = synthetic;
                    foreach (var language in new[] { "he", "ru", "en" })
                    {
                        form.PreviewShellLanguageForLayoutTest(language);
                        if (live.Text != synthetic)
                            failures.Add("Runtime Start status changed on language switch: " +
                                language + ".");
                    }
                }
                finally
                {
                    live.Text = before;
                }
            }
        }
        finally
        {
            // Never leave the test shell in another language; the ordinary
            // navigation and HWND recreation tests expect English here.
            form.PreviewShellLanguageForLayoutTest("en");
            Application.DoEvents();
            form.ReflowMainViewport();
            form.PerformLayout();
        }
    }

    // Explicitly opted-in CI screenshots are generated only from the
    // disconnected layout-self-test fixture, which contains one synthetic
    // UI-TEST-PC row but never retrieves recovery keys, passwords or tokens.
    // Normal desktop --ui-self-test invocations never write screenshots.
    private static void SaveSyntheticStartSnapshotForCi(
        MainForm form, string phase, List<string> failures)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable(
                "BITKEYBRIDGE_SYNTHETIC_UI_SNAPSHOTS"), "1",
                StringComparison.Ordinal))
            return;

        var compact = phase.StartsWith("compact-", StringComparison.Ordinal);
        if (FindByName(form, "HomeLayoutRoot") is not Control viewport ||
            viewport.ClientSize.Width < (compact ? 1200 : 1450) ||
            viewport.ClientSize.Height < (compact ? 500 : 700))
        {
            failures.Add("Synthetic Start snapshot viewport is not ready: " + phase);
            return;
        }

        try
        {
            using var bitmap = new Bitmap(
                viewport.ClientSize.Width, viewport.ClientSize.Height);
            viewport.DrawToBitmap(bitmap,
                new Rectangle(Point.Empty, bitmap.Size));
            var path = Path.Combine(AppContext.BaseDirectory,
                "ui-start-1080p-" + phase + ".png");
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);

            // CI's native DrawToBitmap exposed opaque black outside the
            // rounded card path. Test the top-left corner (outside the 20px
            // radius) of both synthetic cards. A normal light-theme parent
            // must paint an opaque surface here, never black GDI remnants.
            if (!SystemInformation.HighContrast)
            {
                foreach (var cardName in new[]
                {
                    "HomeBitLockerCard", "HomeLapsCard"
                })
                {
                    if (FindByName(form, cardName) is not UiRecoveryCard card)
                    {
                        failures.Add("Synthetic recovery card missing: " + cardName);
                        continue;
                    }
                    var origin = viewport.PointToClient(
                        card.PointToScreen(Point.Empty));
                    var point = new Point(origin.X + 2, origin.Y + 2);
                    if (point.X < 0 || point.X >= bitmap.Width ||
                        point.Y < 0 || point.Y >= bitmap.Height)
                    {
                        failures.Add("Synthetic recovery corner is outside viewport: " +
                            cardName);
                        continue;
                    }
                    var pixel = bitmap.GetPixel(point.X, point.Y);
                    if (pixel.R < 40 && pixel.G < 40 && pixel.B < 40)
                        failures.Add("Synthetic recovery card has a black rounded corner: " +
                            cardName + "; RGB=" + pixel.R + "," + pixel.G +
                            "," + pixel.B + ".");
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add("Synthetic Start snapshot failed: " +
                ex.GetType().Name);
        }
    }

    // Opt-in screenshot of the ENTIRE synthetic shell, not only HomeLayoutRoot:
    // this proves the footer warning is visible and there is no global banner
    // above the tab bar. Never inspect or capture a connected workstation.
    private static void SaveSyntheticAuditFooterSnapshotForCi(
        MainForm form, List<string> failures)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable(
                "BITKEYBRIDGE_SYNTHETIC_UI_SNAPSHOTS"), "1",
                StringComparison.Ordinal))
            return;

        if (FindByName(form, "MainLayoutRoot") is not Control shell ||
            shell.ClientSize.Width < 1450 || shell.ClientSize.Height < 650)
        {
            failures.Add("Portable audit shell snapshot was not ready.");
            return;
        }

        try
        {
            using var bitmap = new Bitmap(
                shell.ClientSize.Width, shell.ClientSize.Height);
            shell.DrawToBitmap(bitmap,
                new Rectangle(Point.Empty, bitmap.Size));
            var path = Path.Combine(AppContext.BaseDirectory,
                "ui-start-1080p-portable-audit-footer.png");
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex)
        {
            failures.Add("Portable audit shell snapshot failed: " +
                ex.GetType().Name);
        }
    }

    // Regression for the reported real 1600x720 screenshot with five recent
    // records and a selected computer. The old 1600x900-only native fixture
    // passed even while the visible short window clipped the recovery panes.
    // This test never loads live AD, Intune, prior history or recovery secrets.
    private static void VerifyCompactNativeSelectedStart(
        MainForm form, DataGridView results, List<string> failures)
    {
        var originalSize = form.ClientSize;
        var selected = results.CurrentRow?.Tag;
        if (selected is null)
        {
            failures.Add("Compact native Start is missing synthetic selection.");
            return;
        }

        // Reproduce a generic diagnostic notice, distinct from the portable
        // audit fallback (which now lives in the persistent footer). Genuine
        // errors still need a compact inline detail panel with Copy/Dismiss.
        var diagnostic = FindByName(form, "ApplicationDiagnostics") as
            UiDiagnosticPanel;
        var originalDiagnosticActive = diagnostic?.HasActiveDiagnostics ?? false;
        if (diagnostic is null)
            failures.Add("Compact Start is missing global diagnostics panel.");
        else if (!originalDiagnosticActive)
            diagnostic.ShowMessage("Synthetic diagnostic notice",
                "UI-only diagnostics: no device identities or secrets.");

        try
        {
            foreach (var size in new[]
            {
                new Size(1600, 720),
                new Size(1366, 768)
            })
            {
                form.ClientSize = size;
                foreach (var language in new[] { "en", "ru", "he" })
                {
                    form.PreviewShellLanguageForLayoutTest(language);
                    Application.DoEvents();
                    form.ReflowMainViewport();
                    form.PerformLayout();
                    Application.DoEvents();

                    var geometry = form.CaptureStartViewportGeometry();
                    var report = form.StartViewportGeometryReport();
                    var scale = Math.Max(form.DeviceDpi /
                        (double)UiStyle.BaselineDpi,
                        form.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));
                    if (diagnostic is { HasActiveDiagnostics: true } &&
                        (diagnostic.Height > 72 * scale ||
                         !diagnostic.AreActionsWithinVisibleBounds()))
                        failures.Add("Compact Start audit notice reserves too much " +
                            "height or clips Copy/Dismiss: " +
                            size.Width + "x" + size.Height + "/" + language +
                            "; diagnosticHeight=" + diagnostic.Height +
                            "; " + diagnostic.ActionBoundsReport());
                    if (geometry is null ||
                        geometry.Value.CardColumns != 3 ||
                        geometry.Value.CardRows != 1 ||
                        geometry.Value.CardsVisibleHeight < 125 * scale)
                        failures.Add("Compact Start hid recovery panes: " +
                            size.Width + "x" + size.Height + "/" + language +
                            ". " + report);

                    // A successful/neutral search notice duplicates the
                    // selected-device summary and may consume the last 40px
                    // needed for complete recovery buttons. Warnings and
                    // failures must instantly take their row back.
                    if (FindByName(form, "HomeUnifiedSearchStatus") is
                            UiStatusLabel searchStatus)
                    {
                        var previousText = searchStatus.Text;
                        var previousKind = searchStatus.StatusKind;
                        if (searchStatus.Visible)
                            failures.Add("Compact selected Start retained redundant " +
                                "search-status height: " + language + "/" +
                                size.Width + "x" + size.Height);
                        UiStyle.SetStatus(searchStatus, "Synthetic warning: test-only",
                            UiStatusKind.Warning);
                        form.ReflowMainViewport();
                        Application.DoEvents();
                        if (!searchStatus.Visible)
                            failures.Add("Compact Start hid a real search warning: " +
                                language + "/" + size.Width + "x" + size.Height);
                        UiStyle.SetStatus(searchStatus, previousText, previousKind);
                        form.ReflowMainViewport();
                        Application.DoEvents();
                    }

                    // The Start viewport is smaller than ClientSize because
                    // the native menu/tabs/status strips also take vertical
                    // pixels. Evaluate the density for that *actual* viewport
                    // and allow the two compact tiers their distinct caps.
                    var logicalHeight = (geometry?.ViewportHeight ?? 0) / scale;
                    var density = logicalHeight < 700 ? 2 :
                        logicalHeight < 800 ? 1 : 0;
                    var maxRecent = density == 2 ? 74 :
                        density == 1 ? 96 : UiStyle.HomeRecentQuickListHeight + 2;
                    var maxCard = density == 2 ? 215 :
                        density == 1 ? 255 : UiStyle.HomeThreePaneMinimumHeight + 5;

                    if (FindByName(form, "HomeNipkgBrandBanner") is not
                            UiNipkgBrandBanner banner ||
                        banner.Tagline.Visible == (density != 0) ||
                        (density != 0 &&
                         banner.AccessibleDescription !=
                             UiNipkgBrandBanner.IntroCaption))
                        failures.Add("Start height density did not adapt its banner: " +
                            size.Width + "x" + size.Height + "/" + language);

                    if (FindByName(form, "HomeRecentQuickPanel") is not
                            TableLayoutPanel recent ||
                        recent.RowStyles.Count < 2 ||
                        recent.RowStyles[1].Height > maxRecent * scale)
                        failures.Add("Start recent-computer list retained tall row: " +
                            size.Width + "x" + size.Height + "/" + language);

                    if (FindByName(form, "HomeServiceCards") is not
                            TableLayoutPanel cards ||
                        cards.MinimumSize.Height > maxCard * scale)
                        failures.Add("Start card minimum height not adapted: " +
                            size.Width + "x" + size.Height + "/" + language);

                    if (!ReferenceEquals(results.CurrentRow?.Tag, selected) ||
                        results.SelectedRows.Count != 1)
                        failures.Add("Compact Start resized away the selected computer: " +
                            size.Width + "x" + size.Height + "/" + language);

                    // Preserve exact native HWND/client geometry while changing
                    // font zoom. The earlier whole-form Font setter silently
                    // rescaled the 1600x720 fixture to 945x507.
                    var nativeFormFontPoints = form.Font.SizeInPoints;
                    var nativeGridFontPoints = results.Font.SizeInPoints;
                    // Explicit density/zoom regression uses the same live WinForms
                    // tree, selected metadata and three-language fixture.
                    foreach (var densityMode in new[]
                             { "Auto", "Compact", "Comfortable" })
                    foreach (var zoom in new[] { 90, 100, 110, 125 })
                    {
                        form.PreviewSetUiDisplayPreferencesForUiSelfTest(
                            densityMode, zoom);
                        form.ReflowMainViewport();
                        Application.DoEvents();
                        var display = form.PreviewUiDisplayStateForUiSelfTest();
                        if (form.ClientSize != size)
                            failures.Add("UI zoom changed native window size: " +
                                size.Width + "x" + size.Height +
                                ", actual=" + form.ClientSize + "/" +
                                language + "/" + densityMode + "/" + zoom);
                        if (Math.Abs(form.Font.SizeInPoints -
                                     nativeFormFontPoints) > 0.01f)
                            failures.Add("UI zoom changed native Form DPI font.");
                        if (display.Density != densityMode ||
                            display.Scale != zoom ||
                            display.StartTier !=
                                UiDisplayPreferencePolicy.ResolveStartTier(
                                    densityMode, form.CaptureStartViewportGeometry()
                                        is { } snapshot
                                        ? snapshot.ViewportHeight /
                                          UiDisplayPreferencePolicy.EffectiveScale(
                                              form.DeviceDpi, form.Font.Size,
                                              UiStyle.BodyFont.Size, zoom)
                                        : 0))
                            failures.Add("Start visual preference mismatch: " +
                                size.Width + "x" + size.Height + "/" +
                                language + "/" + densityMode + "/" + zoom);
                        if (!ReferenceEquals(results.CurrentRow?.Tag, selected))
                            failures.Add("UI zoom changed selected computer.");

                        // Save the real native 1366px / 125%-font intermediate
                        // two-column layout for visual review in Windows CI.
                        // Only synthetic names and masked metadata are present.
                        if (size.Width == 1366 && densityMode == "Auto" &&
                            zoom == 125)
                            SaveSyntheticStartSnapshotForCi(form,
                                "compact-selected-1366-zoom125-" + language,
                                failures);

                        // Test the adaptive card BREAKPOINT using the actual
                        // native pixel width after font zoom, not a model of
                        // the window or only the default 100% setting.
                        if (FindByName(form, "HomeServiceCards") is
                                TableLayoutPanel adaptiveCards &&
                            adaptiveCards.Parent is Control cardArea)
                        {
                            var width = Math.Max(0,
                                cardArea.ClientSize.Width -
                                cardArea.Padding.Horizontal);
                            var effectiveScale =
                                UiDisplayPreferencePolicy.EffectiveScale(
                                    form.DeviceDpi, form.Font.Size,
                                    UiStyle.BodyFont.Size, zoom);
                            var expectedColumns = width >= (int)Math.Ceiling(
                                UiStyle.HomeThreePaneMinimumWidth * effectiveScale)
                                ? 3 : width >= (int)Math.Ceiling(
                                    UiStyle.HomeRecoveryCardsPairMinimumWidth *
                                    effectiveScale) ? 2 : 1;
                            var expectedRows = expectedColumns == 3 ? 1 :
                                expectedColumns == 2 ? 2 : 3;
                            if (adaptiveCards.ColumnCount != expectedColumns ||
                                adaptiveCards.RowCount != expectedRows)
                                failures.Add("Start DPI/zoom adaptive grid mismatch: " +
                                    size.Width + "x" + size.Height + "/" +
                                    language + "/" + densityMode + "/" + zoom +
                                    "; columns=" + adaptiveCards.ColumnCount +
                                    "/" + expectedColumns + "; rows=" +
                                    adaptiveCards.RowCount + "/" + expectedRows);

                            if (expectedColumns == 2)
                            {
                                var lastPane = adaptiveCards.Controls
                                    .Cast<Control>()
                                    .FirstOrDefault(pane =>
                                        adaptiveCards.GetRow(pane) == 1);
                                if (lastPane is null ||
                                    adaptiveCards.GetColumn(lastPane) != 0 ||
                                    adaptiveCards.GetColumnSpan(lastPane) != 2)
                                    failures.Add("Start DPI/zoom third pane has lost " +
                                        "its two-column span: " + size.Width +
                                        "x" + size.Height + "/" + language +
                                        "/" + densityMode + "/" + zoom);
                            }

                            if (expectedRows > 1 &&
                                FindByName(form, "HomeLayoutRoot") is
                                    TableLayoutPanel startScroller)
                            {
                                var heights = startScroller.GetRowHeights();
                                var required = heights.Take(5).Sum() +
                                    adaptiveCards.MinimumSize.Height +
                                    startScroller.Padding.Vertical;
                                if (required > startScroller.ClientSize.Height &&
                                    startScroller.AutoScrollMinSize.Height < required)
                                    failures.Add("Start DPI/zoom lost scroll " +
                                        "reachability for stacked cards: " +
                                        size.Width + "x" + size.Height + "/" +
                                        language + "/" + densityMode + "/" + zoom +
                                        "; scrollExtent=" +
                                        startScroller.AutoScrollMinSize.Height +
                                        "; required=" + required);
                            }
                        }
                        else
                        {
                            failures.Add("Start adaptive card viewport is missing " +
                                "after DPI/font zoom.");
                        }
                    }
                    form.PreviewSetUiDisplayPreferencesForUiSelfTest("Auto", 100);
                    form.ReflowMainViewport();
                    Application.DoEvents();
                    if (form.ClientSize != size ||
                        Math.Abs(results.Font.SizeInPoints -
                            nativeGridFontPoints) > 0.01f)
                        failures.Add("Start zoom roundtrip failed to restore native " +
                            "window bounds/grid font at " +
                            size.Width + "x" + size.Height + "/" + language);

                    // The four sensitive actions must remain reachable. Their
                    // existence alone is not enough; check native clipping.
                    VerifyStartRecoveryButtonsOnScreen(form, failures);
                    if (size.Width == 1600)
                        SaveSyntheticStartSnapshotForCi(
                            form, "compact-selected-" + language, failures);
                }
            }
        }
        finally
        {
            if (!originalDiagnosticActive)
                diagnostic?.Clear();
            form.PreviewShellLanguageForLayoutTest("en");
            form.ClientSize = originalSize;
            Application.DoEvents();
            form.ReflowMainViewport();
            form.PerformLayout();
            Application.DoEvents();
        }
    }

    // Verify the real fallback warning's geometry without calling AuditService
    // or requesting a recovery secret. The notice should live in the footer,
    // not reserve another AutoSize row above Start. Clicking reveals details
    // on demand; dismissing the details must never dismiss the warning.
    private static void VerifyPortableAuditFooterNative(
        MainForm form, List<string> failures)
    {
        if (FindByName(form, "ApplicationDiagnostics") is not
                UiDiagnosticPanel diagnostics ||
            FindByName(form, "MainLayoutRoot") is not
                TableLayoutPanel shell ||
            FindByName(form, "GlobalStatusBar") is not
                StatusStrip status ||
            status.Items.Find("GlobalPortableAuditStatus", false)
                .FirstOrDefault() is not ToolStripStatusLabel footer)
        {
            failures.Add("Portable audit footer UI fixture is incomplete.");
            return;
        }

        var originalSize = form.ClientSize;
        var hadDiagnostics = diagnostics.HasActiveDiagnostics;
        if (hadDiagnostics)
        {
            failures.Add("Portable audit footer fixture started with an active " +
                "global diagnostic banner.");
            return;
        }

        try
        {
            form.PreviewPortableAuditFallbackForUiSelfTest(true);
            foreach (var size in new[]
                     { new Size(1600, 720), new Size(1366, 768) })
            {
                form.ClientSize = size;
                foreach (var language in new[] { "en", "ru", "he" })
                {
                    form.PreviewShellLanguageForLayoutTest(language);
                    form.ReflowMainViewport();
                    form.PerformLayout();
                    Application.DoEvents();

                    var expected = language switch
                    {
                        "ru" => "Аудит: локальный",
                        "he" => "ביקורת: מקומית",
                        _ => "Audit: portable"
                    };
                    if (!footer.Available ||
                        footer.Placement != ToolStripItemPlacement.Main ||
                        footer.Bounds.Width <= 0 ||
                        footer.Bounds.Right > status.ClientSize.Width + 2 ||
                        footer.Text != expected ||
                        footer.AccessibleDescription?.Contains(
                            "not tamper-proof", StringComparison.Ordinal) != true)
                        failures.Add("Portable audit footer is hidden, clipped or " +
                            "untranslated: " + size.Width + "x" + size.Height +
                            "/" + language + "; bounds=" + footer.Bounds +
                            "; placement=" + footer.Placement + ".");

                    if (diagnostics.HasActiveDiagnostics ||
                        diagnostics.Visible ||
                        shell.GetRowHeights()[1] > 4)
                        failures.Add("Portable audit warning reserved the old " +
                            "global diagnostic banner height: " + size.Width +
                            "x" + size.Height + "/" + language +
                            "; rowHeight=" + shell.GetRowHeights()[1] + ".");

                    VerifyStartRecoveryButtonsOnScreen(form, failures);
                    if (size.Width == 1600 && language == "en")
                    {
                        SaveSyntheticAuditFooterSnapshotForCi(form, failures);
                        footer.PerformClick();
                        form.PerformLayout();
                        Application.DoEvents();
                        if (!diagnostics.HasActiveDiagnostics ||
                            !diagnostics.AreActionsWithinVisibleBounds())
                            failures.Add("Portable audit footer cannot open the " +
                                "full accessible warning details.");
                        diagnostics.Clear();
                        form.ReflowMainViewport();
                        Application.DoEvents();
                        if (!footer.Available ||
                            diagnostics.HasActiveDiagnostics)
                            failures.Add("Dismissing portable audit details " +
                                "also dismissed the persistent footer warning.");
                    }
                }
            }
            form.PreviewPortableAuditFallbackForUiSelfTest(false);
            form.ReflowMainViewport();
            Application.DoEvents();
            if (footer.Available)
                failures.Add("Portable audit footer remained after clearing " +
                    "the UI-only fallback fixture.");
        }
        finally
        {
            form.PreviewPortableAuditFallbackForUiSelfTest(false);
            if (!hadDiagnostics)
                diagnostics.Clear();
            form.PreviewShellLanguageForLayoutTest("en");
            form.ClientSize = originalSize;
            form.ReflowMainViewport();
            form.PerformLayout();
            Application.DoEvents();
        }
    }

    // No disk history or directory session: the UI test uses synthetic rows
    // to verify 0 -> 1 -> 15 -> 0 recent devices and stacked/wide transitions.
    private static void VerifyAdaptiveRecentHistory(
        MainForm form, List<string> failures)
    {
        if (FindByName(form, "HomeRecentComputers") is not DataGridView grid ||
            FindByName(form, "HomeRecentQuickPanel") is not TableLayoutPanel recent ||
            FindByName(form, "HomeConnectionsAndRecent") is not TableLayoutPanel split)
        {
            failures.Add("Adaptive Recent history fixture is missing from Start.");
            return;
        }

        var originalSize = form.ClientSize;
        try
        {
            grid.Rows.Clear();
            form.ReflowMainViewport();
            Application.DoEvents();
            var noHistory = form.CaptureStartViewportGeometry();
            if (recent.Visible || split.ColumnCount != 1 ||
                split.RowStyles.Count != 2 ||
                split.RowStyles[1].SizeType != SizeType.Absolute ||
                split.RowStyles[1].Height != 0F)
                failures.Add("Empty Recent history still reserves a connection column or row.");

            grid.Rows.Add("UI-TEST-RECENT-01", "Search", DateTime.UtcNow);
            form.ReflowMainViewport();
            Application.DoEvents();
            var shortHeight = recent.RowStyles[1].Height;
            var scale = Math.Max(form.DeviceDpi / (double)UiStyle.BaselineDpi,
                form.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));
            var maxHeight = UiStyle.HomeRecentQuickListHeight * scale;
            if (!recent.Visible || split.ColumnCount != 1 ||
                shortHeight < 45 * scale || shortHeight >= maxHeight - 4)
                failures.Add("One Recent computer did not produce a compact, stacked list.");

            for (var index = 2; index <= 15; index++)
                grid.Rows.Add("UI-TEST-RECENT-" + index.ToString("00"),
                    "Search", DateTime.UtcNow);
            form.ReflowMainViewport();
            Application.DoEvents();
            var longHeight = recent.RowStyles[1].Height;
            if (longHeight <= shortHeight ||
                longHeight > maxHeight + 2)
                failures.Add("Recent history did not expand and cap its grid height.");

            form.ClientSize = new Size(1600, 900);
            form.ReflowMainViewport();
            Application.DoEvents();
            if (split.ColumnCount != 2 || !recent.Visible)
                failures.Add("Populated Recent history must occupy the right wide-screen column.");

            grid.Rows.Clear();
            form.ClientSize = originalSize;
            form.ReflowMainViewport();
            Application.DoEvents();
            var cleared = form.CaptureStartViewportGeometry();
            if (split.ColumnCount != 1 || recent.Visible ||
                (noHistory is { } before && cleared is { } after &&
                 after.ConnectionHeight > before.ConnectionHeight + 12))
                failures.Add("Cleared Recent history retained a visible panel, extra height or empty column.");
        }
        finally
        {
            grid.Rows.Clear();
            form.ClientSize = originalSize;
            form.ReflowMainViewport();
            Application.DoEvents();
        }
    }

    /// <summary>
    /// Regression for a visually missing copy button: the old check only
    /// looked up button names, while Hebrew WinForms AutoSize/RTL could
    /// clip both action controls outside their visible parent rectangle.
    /// </summary>
    private static void VerifyDiagnosticCopyVisibility(List<string> failures)
    {
        try
        {
            using var form = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true)
            {
                StartPosition = FormStartPosition.Manual,
                ClientSize = new Size(1024, 768)
            };
            // A hidden Form reports Control.Visible=false for everything
            // and its AutoSize rows may not lay out. Test actual WinForms
            // handles and visibility, not just Controls.Find/Text.
            form.Show();
            Application.DoEvents();
            // Shown queues a 55-ms deferred viewport repair. The baseline
            // must include the same completed native layout as expand/collapse.
            form.ReflowMainViewport();
            form.PerformLayout();
            Application.DoEvents();

            // Exercise native geometry, not only the existence of controls.
            // This report is pixels only: no device identifiers or secrets.
            var initialStart = form.CaptureStartViewportGeometry();
            // On a stacked 1024px Start, every connection section must
            // actually receive the wide single-column cell. A 200px
            // provisional chooser width wraps a one-line status into a
            // phantom 120px row and hides the recovery cards.
            var sourceChooser = FindByName(
                form, "HomeConnectionSourceChooser") as TableLayoutPanel;
            var splitLayout = FindByName(
                form, "HomeConnectionsAndRecent") as TableLayoutPanel;
            if (initialStart is { } initialGeometry &&
                initialGeometry.ViewportWidth > 800 &&
                splitLayout is { ColumnCount: 1 } &&
                (sourceChooser is null || sourceChooser.Width < 500))
                failures.Add("Start connection chooser retained provisional 200px width after viewport reflow. " +
                    form.StartViewportGeometryReport());

            if (initialStart is null ||
                !form.StartViewportGeometryReport().Contains(
                    "Start viewport (pixels only): viewport=", StringComparison.Ordinal))
            {
                failures.Add("Visible Start did not publish non-secret pixel geometry.");
            }
            else
            {
                form.PreviewSetConnectionOptionsExpandedForUiTest(true);
                form.ReflowMainViewport();
                Application.DoEvents();
                var expanded = form.CaptureStartViewportGeometry();
                form.PreviewSetConnectionOptionsExpandedForUiTest(false);
                form.ReflowMainViewport();
                Application.DoEvents();
                var collapsed = form.CaptureStartViewportGeometry();
                if (expanded is null || collapsed is null ||
                    // Expanded controls must not leave the original
                    // Connections height behind when collapsed again.
                    collapsed.Value.ConnectionHeight >
                        initialStart.Value.ConnectionHeight + 12)
                {
                    failures.Add("Collapsing Start connection options retained expanded panel height. " +
                        "Before=" + initialStart.Value.ConnectionHeight +
                        "; Expanded=" + (expanded?.ConnectionHeight ?? -1) +
                        "; Collapsed=" + (collapsed?.ConnectionHeight ?? -1) + ". " +
                        form.StartViewportGeometryReport());
                }
            }

            VerifyAdaptiveRecentHistory(form, failures);

            // Reproduce the reported 1920x1080 / 100% desktop with an
            // approximately 1600x900 application client area. WinForms HWNDs
            // can measure a larger client than the CI runner's virtual display;
            // check real control geometry before any scheduled work-area clamp.
            // No AD/Graph session, computer identifier or secret is involved.
            var originalClientSize = form.ClientSize;
            // The real 1080p screenshot included Recent computers. Populate
            // metadata-only synthetic rows for both visual QA screenshots,
            // without reading the user's persisted recent-device history.
            var snapshotRecent = FindByName(
                form, "HomeRecentComputers") as DataGridView;
            try
            {
                if (snapshotRecent is not null)
                {
                    snapshotRecent.Rows.Clear();
                    for (var index = 1; index <= 5; index++)
                        snapshotRecent.Rows.Add(
                            "UI-TEST-RECENT-" + index.ToString("00"),
                            "Search", DateTime.UtcNow);
                }
                form.ClientSize = new Size(1600, 900);
                form.ReflowMainViewport();
                form.PerformLayout();
                var wideStart = form.CaptureStartViewportGeometry();
                SaveSyntheticStartSnapshotForCi(form, "empty", failures);
                if (wideStart is null ||
                    wideStart.Value.ViewportWidth < 1450 ||
                    wideStart.Value.ViewportHeight < 700 ||
                    wideStart.Value.CardColumns != 3 ||
                    wideStart.Value.CardRows != 1 ||
                    wideStart.Value.CardsVisibleHeight <
                        Math.Min(150, wideStart.Value.CardsHeight / 2))
                {
                    failures.Add("Standard 1080p Start viewport hides BitLocker/LAPS/Computers cards beneath unused status space.");
                }

                // The customer screenshot had a selected computer and two
                // extra status rows. Measure that real selection geometry too,
                // not only the default unselected workspace.
                if (FindByName(form, "HomeUnifiedSearchResults") is DataGridView results)
                {
                    var row = results.Rows.Add(
                        "UI-TEST-PC", "AD", "Available", DateTime.UtcNow,
                        "Detected", DateTime.UtcNow);
                    results.Rows[row].Tag = new HomeSearchResult
                    {
                        ComputerName = "UI-TEST-PC",
                        ComputerId = Guid.Empty.ToString(),
                        BitLockerKeyCount = 1,
                        BitLockerStatus = "Available",
                        LapsCandidateFound = true,
                        LapsStatus = "Detected"
                    };
                    results.ClearSelection();
                    results.Rows[row].Selected = true;
                    results.CurrentCell = results.Rows[row].Cells[0];
                    form.ReflowMainViewport();
                    form.PerformLayout();
                    // Run the caption bounds assertion on the exact native
                    // 1600x900 fixture that produces the screenshot. The
                    // generic compact/DPI matrix alone missed this clipping.
                    VerifySelectedHomeStatusBounds(
                        form, "NativeStart1080p",
                        new Scenario("Selected1600x900", new Size(1600, 900), 1F),
                        failures);
                    var selectedWideStart = form.CaptureStartViewportGeometry();
                    // Assert the actual buttons are wholly on screen; merely
                    // seeing the top of a card is not sufficient for helpdesk.
                    VerifyStartRecoveryButtonsOnScreen(form, failures);
                    SaveSyntheticStartSnapshotForCi(form, "selected", failures);
                    if (selectedWideStart is null ||
                        selectedWideStart.Value.SelectionHeight <= 0 ||
                        selectedWideStart.Value.SelectionHeight > 150 ||
                        selectedWideStart.Value.CardsVisibleHeight <
                            Math.Min(120, selectedWideStart.Value.CardsHeight / 2))
                        failures.Add("Standard 1080p selected computer pushes recovery cards below the visible viewport. " +
                            form.StartViewportGeometryReport());
                    // Exercise the ACTUAL shown selected-device Start window
                    // through Russian and Hebrew before restoring English.
                    // Keep the selected metadata object unchanged and capture
                    // separate synthetic PNGs for each language.
                    VerifyNativeLocalizedSelectedStart(form, results, failures);
                    VerifyCompactNativeSelectedStart(form, results, failures);
                    VerifyPortableAuditFooterNative(form, failures);
                    results.Rows.Clear();
                    form.ReflowMainViewport();
                }
            }
            finally
            {
                snapshotRecent?.Rows.Clear();
                form.ClientSize = originalClientSize;
                form.ReflowMainViewport();
                form.PerformLayout();
            }

            // Three requested viewport repairs during one UI-message turn
            // must schedule just one native callback. A second burst must
            // still execute rather than getting stranded behind a stale flag.
            var firstNavigation = form.PreviewNavigationReflowStateForUiSelfTest();
            form.PreviewQueueNavigationReflowForUiSelfTest();
            form.PreviewQueueNavigationReflowForUiSelfTest();
            form.PreviewQueueNavigationReflowForUiSelfTest();
            var queuedNavigation = form.PreviewNavigationReflowStateForUiSelfTest();
            if (!queuedNavigation.Pending ||
                queuedNavigation.Posted != firstNavigation.Posted + 1 ||
                queuedNavigation.Coalesced != firstNavigation.Coalesced + 2)
                failures.Add("Rapid tab navigation posted redundant WinForms callbacks.");
            Application.DoEvents();
            var drainedNavigation = form.PreviewNavigationReflowStateForUiSelfTest();
            if (drainedNavigation.Pending ||
                drainedNavigation.Dispatched != firstNavigation.Dispatched + 1)
                failures.Add("Navigation coalescing lost the latest viewport repair.");
            form.PreviewQueueNavigationReflowForUiSelfTest();
            Application.DoEvents();
            var secondNavigation = form.PreviewNavigationReflowStateForUiSelfTest();
            if (secondNavigation.Pending ||
                secondNavigation.Posted != queuedNavigation.Posted + 1 ||
                secondNavigation.Dispatched != drainedNavigation.Dispatched + 1)
                failures.Add("Navigation reflow did not schedule after an earlier batch.");

            // Exercise real WinForms SelectedIndexChanged events, rather
            // than just manually queueing three identical root-page repairs.
            // A nested-tab event with no stopwatch must not erase timing
            // of the final main-tab change. Retain the same TabPage objects,
            // selected layout and main native HWND across both bursts.
            if (FindByName(form, "MainWorkspaceTabs") is not TabControl mainTabs ||
                mainTabs.TabPages.Count < 3)
            {
                failures.Add("Main navigation tabs are missing from the UI fixture.");
            }
            else
            {
                var initialTabHandle = mainTabs.Handle;
                var originalPages = mainTabs.TabPages.Cast<TabPage>().ToArray();
                var beforeRealNavigation =
                    form.PreviewNavigationReflowStateForUiSelfTest();
                mainTabs.SelectedIndex = 1;
                mainTabs.SelectedIndex = 2;
                mainTabs.SelectedIndex = 0;
                form.PreviewQueueNavigationReflowForUiSelfTest();
                var queuedRealNavigation =
                    form.PreviewNavigationReflowStateForUiSelfTest();
                if (!queuedRealNavigation.Pending ||
                    queuedRealNavigation.Posted != beforeRealNavigation.Posted + 1 ||
                    queuedRealNavigation.Coalesced < beforeRealNavigation.Coalesced + 3 ||
                    queuedRealNavigation.RequestedIndex != 0 ||
                    !queuedRealNavigation.HasPendingTiming)
                    failures.Add("Main/nested tab burst lost final selection or timing.");

                Application.DoEvents();
                var afterRealNavigation =
                    form.PreviewNavigationReflowStateForUiSelfTest();
                if (afterRealNavigation.Pending ||
                    afterRealNavigation.Dispatched !=
                        beforeRealNavigation.Dispatched + 1 ||
                    afterRealNavigation.LastDispatchedIndex != 0 ||
                    mainTabs.SelectedIndex != 0 ||
                    mainTabs.Handle != initialTabHandle ||
                    !originalPages.SequenceEqual(
                        mainTabs.TabPages.Cast<TabPage>()))
                    failures.Add("Rapid real-tab navigation lost page state or recreated tabs.");

                mainTabs.SelectedIndex = 2;
                Application.DoEvents();
                var finalNavigation =
                    form.PreviewNavigationReflowStateForUiSelfTest();
                if (finalNavigation.Pending ||
                    finalNavigation.LastDispatchedIndex != 2 ||
                    finalNavigation.Dispatched != afterRealNavigation.Dispatched + 1)
                    failures.Add("Second real-tab navigation did not display latest selection.");
                mainTabs.SelectedIndex = 0;
                Application.DoEvents();

                // Recreate the native parent HWND while navigation repair is
                // pending. Any callback for the old HWND must be ignored, and
                // a new callback must reflow the current selected page.
                var beforeRecreation = form.PreviewNavigationReflowStateForUiSelfTest();
                form.PreviewQueueNavigationReflowForUiSelfTest();
                form.PreviewRecreateNativeHandleForUiSelfTest();
                Application.DoEvents();
                var afterRecreation = form.PreviewNavigationReflowStateForUiSelfTest();
                if (afterRecreation.Pending ||
                    afterRecreation.LastDispatchedIndex != mainTabs.SelectedIndex ||
                    afterRecreation.Dispatched <= beforeRecreation.Dispatched)
                    failures.Add("HWND recreation stranded the latest navigation reflow.");
            }

            // Measure a REAL deferred selected-page pass, not only the
            // synchronous caption translation. Two changes in one message
            // burst must keep the newest timestamp and end with no pending
            // language stopwatch. Tests do not change saved user preferences.
            var beforeLanguage = UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
            form.PreviewShellLanguageForLayoutTest("he");
            if (!form.PreviewLanguageViewportPendingForUiSelfTest())
                failures.Add("Deferred language viewport timer did not start.");
            form.PreviewShellLanguageForLayoutTest("ru");
            if (!form.PreviewLanguageViewportPendingForUiSelfTest())
                failures.Add("Rapid language change lost latest viewport timing.");
            Application.DoEvents();
            var afterLanguage = UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
            if (form.PreviewLanguageViewportPendingForUiSelfTest() ||
                afterLanguage.Ready <= beforeLanguage.Ready ||
                afterLanguage.Superseded <= beforeLanguage.Superseded)
                failures.Add("Rapid language changes stranded the deferred viewport timer.");

            form.PreviewShellLanguageForLayoutTest("he");
            var beforeRecreatedLanguage =
                UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
            form.PreviewRecreateNativeHandleForUiSelfTest();
            Application.DoEvents();
            var afterRecreatedLanguage =
                UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
            if (form.PreviewLanguageViewportPendingForUiSelfTest() ||
                afterRecreatedLanguage.Ready <= beforeRecreatedLanguage.Ready)
                failures.Add("HWND recreation stranded the latest language viewport measurement.");

            form.PreviewShellLanguageForLayoutTest("en");
            Application.DoEvents();
            if (form.PreviewLanguageViewportPendingForUiSelfTest() ||
                !UiLayoutTelemetry.Report().Contains(
                    "Language to selected viewport ready: runs=", StringComparison.Ordinal) ||
                !UiLayoutTelemetry.Report().Contains(
                    "Language viewport measurements superseded:", StringComparison.Ordinal))
                failures.Add("Deferred language p50/p95 diagnostics were not completed.");

            // A native layout can already be running when the language
            // callback arrives. Dispatch alone must not record a completed
            // geometry pass or clear the pending language stopwatch.
            var beforeBusyLanguage =
                UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
            form.PreviewHoldMainViewportReflowForUiSelfTest(true);
            try
            {
                form.PreviewShellLanguageForLayoutTest("ru");
                Application.DoEvents();
                var whileBusyLanguage =
                    UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
                if (!form.PreviewLanguageViewportPendingForUiSelfTest() ||
                    whileBusyLanguage.Ready != beforeBusyLanguage.Ready)
                    failures.Add("Busy native layout falsely completed language viewport timing.");
            }
            finally
            {
                form.PreviewHoldMainViewportReflowForUiSelfTest(false);
            }
            // A successful follow-up WM_SIZE/idle viewport pass completes
            // the exact same stopwatch without inventing a new language
            // change or recording the skipped attempt as a success.
            form.ReflowMainViewport();
            var afterBusyLanguage =
                UiLayoutTelemetry.LanguageViewportCountsForUiSelfTest();
            if (form.PreviewLanguageViewportPendingForUiSelfTest() ||
                afterBusyLanguage.Ready != beforeBusyLanguage.Ready + 1)
                failures.Add("Busy native layout retry stranded language viewport timing.");

            // The busy-layout fixture deliberately finishes in Russian.
            // Check the actual Russian status-link caption before returning
            // to the original English baseline for subsequent assertions.
            if (FindByName(form, "GlobalStatusBar") is not StatusStrip russianStatus ||
                russianStatus.Items.Find("GlobalCopyDiagnostics", false)
                    .FirstOrDefault() is not ToolStripStatusLabel russianCopy ||
                russianCopy.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                    "ru", "Copy diagnostics"))
                failures.Add("Busy-language viewport repair lost the Russian status copy link.");

            form.PreviewShellLanguageForLayoutTest("en");
            Application.DoEvents();
            if (form.PreviewLanguageViewportPendingForUiSelfTest())
                failures.Add("English baseline restoration left viewport timing pending.");

            if (FindByName(form, "GlobalStatusBar") is not StatusStrip initialStatus ||
                initialStatus.RightToLeft != RightToLeft.No ||
                initialStatus.Items.Find("GlobalCopyDiagnostics", false)
                    .FirstOrDefault() is not ToolStripStatusLabel initialCopy ||
                initialCopy.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                    "en", "Copy diagnostics"))
                failures.Add("Initial LTR status-bar copy link is absent.");

            for (var i = 0; i < 2; i++)
            {
                using var largeFont = i == 0
                    ? new Font(UiStyle.BodyFont.FontFamily,
                        UiStyle.BodyFont.Size, FontStyle.Regular,
                        GraphicsUnit.Point)
                    : new Font(UiStyle.BodyFont.FontFamily,
                        UiStyle.BodyFont.Size * 2F, FontStyle.Regular,
                        GraphicsUnit.Point);
                form.Font = largeFont;
                form.PreviewShowUiPerformanceReportForLayoutTest();

                foreach (var language in new[] { "he", "ru", "en", "he" })
                {
                    form.PreviewShellLanguageForLayoutTest(language);
                    form.PerformLayout();
                    Application.DoEvents();

                    var diagnosticPanel = FindByName(
                        form, "ApplicationDiagnostics") as UiDiagnosticPanel;
                    if (diagnosticPanel is null ||
                        !diagnosticPanel.AreActionsWithinVisibleBounds())
                        failures.Add("Copy Diagnostics is clipped or not visible: " +
                            language + ", font scale " + (i == 0 ? "100" : "200") +
                            "%. " + (diagnosticPanel?.ActionBoundsReport() ?? "No panel"));

                    if (FindByName(form, "GlobalStatusBar") is not StatusStrip status ||
                        status.Items.Find("GlobalCopyDiagnostics", false)
                            .FirstOrDefault() is not ToolStripStatusLabel copyLink ||
                        !copyLink.Available ||
                        copyLink.Placement != ToolStripItemPlacement.Main ||
                        copyLink.Bounds.Width <= 0 ||
                        copyLink.Bounds.Right > status.ClientSize.Width + 2 ||
                        copyLink.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Copy diagnostics"))
                        failures.Add("Persistent status bar copy link is clipped: " +
                            language + ", font scale " + (i == 0 ? "100" : "200") + "%.");
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add("Diagnostic copy visibility regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyHelpdeskChecklistLocalization(List<string> failures)
    {
        var englishSteps = new[]
        {
            "Install the Windows service on a test workstation (administrator)",
            "Configure a scoped RecoveryReaders group; disable admin bypass",
            "Verify AD and Entra / Intune delegation independently",
            "Provision protected audit and restart the service (administrator)",
            "Sign in as an authorized non-admin and run Access Health",
            "Verify unauthorized access is denied and secrets are never logged",
            "Validate protected journal, RDP, 100-200% DPI and rollback"
        };

        try
        {
            using var main = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true);

            foreach (var language in new[] { "en", "ru", "he" })
            {
                main.PreviewShellLanguageForLayoutTest(language);
                using var wizard = main.CreateHelpdeskDeploymentWizard();
                UiDialogLocalization.Apply(wizard, language);

                var list = FindByName(wizard, "HelpdeskDeploymentSteps")
                    as CheckedListBox;
                var progress = FindByName(wizard, "HelpdeskDeploymentProgress")
                    as Label;
                if (list is null || progress is null ||
                    list.Items.Count != englishSteps.Length)
                {
                    failures.Add("Helpdesk localized checklist is missing items: " +
                        language);
                    continue;
                }

                for (var i = 0; i < englishSteps.Length; i++)
                {
                    var expected = UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, englishSteps[i]);
                    if (expected == englishSteps[i] && language != "en")
                        failures.Add("Helpdesk checklist caption missing from catalog: " +
                            i + " (" + language + ")");
                    if (!string.Equals(list.Items[i] as string, expected,
                            StringComparison.Ordinal))
                        failures.Add("Helpdesk checklist left untranslated: " +
                            i + " (" + language + ")");
                }

                var template = UiLanguageService.Translate(
                    language, "HelpdeskDeploymentProgressTemplate");
                var expectedZero = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    template, 0, englishSteps.Length);
                if (progress.Text != expectedZero)
                    failures.Add("Helpdesk initial progress translation incorrect: " +
                        language);

                // This checklist is informational, not an authorization or
                // service operation. Its checked states must remain tied to
                // positions, independent of translated display text.
                list.SetItemChecked(0, true);
                var expectedOne = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    template, 1, englishSteps.Length);
                if (!list.GetItemChecked(0) || progress.Text != expectedOne)
                    failures.Add("Helpdesk progress counter lost state: " +
                        language);

                list.SetItemChecked(0, false);
                if (list.GetItemChecked(0) || progress.Text != expectedZero)
                    failures.Add("Helpdesk checklist did not clear its count: " +
                        language);
            }
        }
        catch (Exception ex)
        {
            failures.Add("Helpdesk EN/RU/HE checklist regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyHebrewIdentityDirection(List<string> failures)
    {
        try
        {
            using var form = new MainForm(
                new AppConfig
                {
                    AutoConnectOnStart = false,
                    HealthEndpointEnabled = false
                },
                layoutSelfTest: true);

            // ToolStripItem.Tag can carry command state; language switching
            // must not overwrite it with the original caption.
            var copyItemBeforeTranslation = form.MainMenuStrip?.Items
                .Find("CopyUiPerformanceReportMenuItem", true)
                .OfType<ToolStripMenuItem>().FirstOrDefault();
            var protectedMenuTag = new object();
            if (copyItemBeforeTranslation is not null)
                copyItemBeforeTranslation.Tag = protectedMenuTag;

            // Exercise recursion: a static caption inside an unknown nested
            // menu must be translated in all three languages.
            var topTools = form.MainMenuStrip?.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.DropDownItems
                    .OfType<ToolStripMenuItem>().Any(child =>
                        child.Name == "CopyUiPerformanceReportMenuItem"));
            using var nestedMenu = new ToolStripMenuItem("Nested menu fixture");
            using var nestedItem = new ToolStripMenuItem("Windows Event Log");
            nestedMenu.DropDownItems.Add(nestedItem);
            // Real ToolStripItems are not Controls. After a fixed-caption
            // translation, operators may replace the item with runtime
            // device text. Editable menu fields must never be translated.
            var dynamicMenuTag = new object();
            using var mutableMenuItem = new ToolStripMenuItem("Back")
            {
                Tag = dynamicMenuTag
            };
            using var menuInput = new ToolStripTextBox { Text = "Back" };
            using var menuChoice = new ToolStripComboBox { Text = "Back" };
            nestedMenu.DropDownItems.Add(mutableMenuItem);
            nestedMenu.DropDownItems.Add(menuInput);
            nestedMenu.DropDownItems.Add(menuChoice);
            topTools?.DropDownItems.Add(nestedMenu);

            form.PreviewShellLanguageForLayoutTest("he");
            if (mutableMenuItem.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                    "he", "Back"))
                failures.Add("Nested fixed menu fixture did not translate to Hebrew.");
            mutableMenuItem.Text = "live device: test-001";
            form.PreviewShowUiPerformanceReportForLayoutTest();

            var banner = FindByName(form, "ApplicationDiagnostics") as UiDiagnosticPanel;
            if (banner is null || !banner.HasActiveDiagnostics ||
                FindByName(banner, "CopyDiagnosticsButton") is not Button ||
                FindByName(banner, "DismissDiagnosticsButton") is not Button)
                failures.Add("Diagnostics banner lost the copy/dismiss controls.");
            var copyUiMenu = form.MainMenuStrip?.Items
                .Find("CopyUiPerformanceReportMenuItem", true)
                .OfType<ToolStripMenuItem>().FirstOrDefault();
            if (copyUiMenu is null ||
                copyUiMenu.ShortcutKeys != (Keys.Control | Keys.Shift | Keys.P) ||
                !copyUiMenu.ShowShortcutKeys)
                failures.Add("Global UI performance copy shortcut is missing or not registered.");

            var tabs = FindByName(form, "MainWorkspaceTabs") as TabControl;
            if (tabs is null || !tabs.RightToLeftLayout ||
                tabs.RightToLeft != RightToLeft.Yes ||
                tabs.DrawMode != TabDrawMode.Normal ||
                tabs.TabPages.Count < 6 ||
                tabs.TabPages[0].Text != UiLanguageService.Translate("he", "Start") ||
                tabs.TabPages[3].Name != "DevicesWorkspace")
                failures.Add("Hebrew mirrored navigation does not retain source-qualified tab identities.");

            if (FindByName(form, "UpdateCenterCompact") is not CheckBox updateDensity ||
                updateDensity.Text != UiLanguageService.Translate("he", "UpdateCenterCompact"))
                failures.Add("Hebrew Update Center switch did not localize.");

            // All administrative controls remain in the form, but verbose
            // optional descriptions start collapsed. They can be expanded
            // from one consistent Tools menu action in every language.
            var helpItem = form.MainMenuStrip?.Items
                .Find("UiShowSectionExplanations", true)
                .OfType<ToolStripMenuItem>().FirstOrDefault();
            if (helpItem is null || helpItem.Checked ||
                helpItem.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                    "he", "Show explanations"))
                failures.Add("Compact administrative explanation menu was not available in Hebrew.");
            if (helpItem is not null)
            {
                helpItem.Checked = true;
                if (!helpItem.Checked)
                    failures.Add("Could not expand optional admin section descriptions.");
                helpItem.Checked = false;
            }
            var optionalCount = 0;
            var explanationQueue = new Stack<Control>();
            explanationQueue.Push(form);
            while (explanationQueue.Count > 0)
            {
                var current = explanationQueue.Pop();
                if (current is Label label &&
                    string.Equals(label.Tag as string,
                        "BKB.OptionalSectionExplanation", StringComparison.Ordinal))
                    optionalCount++;
                foreach (Control child in current.Controls)
                    explanationQueue.Push(child);
            }
            if (optionalCount < 8)
                failures.Add("Administrative explanations were removed instead of safely collapsed.");
            // WinForms originally mirrored the Form itself, recursively
            // resetting native child windows and sometimes blanking Hebrew
            // captions after a language switch. Only the tab strip may mirror.
            if (form.RightToLeftLayout || form.RightToLeft == RightToLeft.Yes)
                failures.Add("Hebrew language selection must not mirror the MainForm HWND.");
            if (form.MainMenuStrip?.Items.Find("UiLanguageMenu", false).FirstOrDefault() is
                not ToolStripMenuItem languageItem ||
                languageItem.Text != UiLanguageService.Translate("he", "Language") ||
                !languageItem.AutoSize || languageItem.Padding.Horizontal < 12)
                failures.Add("Hebrew Language menu was clipped or lost its text layout.");
            if (tabs is not null && tabs.TabPages.Cast<TabPage>().Any(
                    page => page.RightToLeft != RightToLeft.No))
                failures.Add("Main workspace pages must keep child/secret controls LTR.");

            // Runtime-created and duplicate named actions must be translated.
            // They are deliberately added after the first Hebrew pass to catch
            // the old one-time control tree snapshot regression.
            var dynamicCaptionsPanel = new FlowLayoutPanel
            {
                Name = "LateLocalizedActionsFixture",
                AutoSize = true
            };
            tabs?.TabPages[0].Controls.Add(dynamicCaptionsPanel);
            using var lateAction1 = UiStyle.CreateActionButton("Untranslated late button");
            lateAction1.Name = "HomeRecoverySteps";
            using var lateAction2 = UiStyle.CreateActionButton("Another untranslated button");
            lateAction2.Name = "HomeRecoverySteps";
            dynamicCaptionsPanel.Controls.Add(lateAction1);
            dynamicCaptionsPanel.Controls.Add(lateAction2);
            if (lateAction1.Text != UiLanguageService.Translate("he", "MainRecoverySteps") ||
                lateAction2.Text != UiLanguageService.Translate("he", "MainRecoverySteps") ||
                lateAction1.RightToLeft != RightToLeft.Yes ||
                lateAction2.RightToLeft != RightToLeft.Yes)
                failures.Add("Late/duplicate WinForms controls were not translated in active Hebrew UI.");
            // Legacy UI refreshes may transiently clear Text. Only fixed,
            // registered captions are repaired; dynamic status is unaffected.
            lateAction2.Text = string.Empty;
            if (lateAction2.Text != UiLanguageService.Translate("he", "MainRecoverySteps"))
                failures.Add("Blank fixed Hebrew caption was not restored.");


            var translatedControls = new (string Name, string Key)[]
            {
                ("HomeUseWindowsIdentity", "MainWindowsAccount"),
                ("HomeUseOtherAccount", "MainOtherAdAccount"),
                ("HomeConnectAdButton", "MainConnectAd"),
                ("HomeConnectionToolsButton", "MainConnectionTools"),
                ("HomeRecoverySteps", "MainRecoverySteps"),
                ("HomeBitLockerDiagnosticLink", "MainViewCommands"),
                ("HomeLoadLapsHistoryButton", "MainLoadHistory"),
                ("HelpdeskOpenDeviceWorkspaceButton", "MainOpenDeviceSearch"),
                ("RunEnvironmentCheckButton", "MainEnvironmentCheck"),
                ("RunUiPerformanceDiagnosticsButton", "MainUiPerformance")
            };
            var originalConnectionSelection =
                (FindByName(form, "HomeConnectionSource") as ComboBox)?.SelectedIndex;
            var connectionChoiceEnglish = new[]
            {
                "Choose a source",
                "Active Directory",
                "Microsoft Entra / Intune (Microsoft 365)",
                "Both: Active Directory + Microsoft Entra"
            };
            foreach (var language in new[] { "he", "en", "ru", "he" })
            {
                form.PreviewShellLanguageForLayoutTest(language);
                if (FindByName(form, "HomeNipkgBrandBanner")
                        is not UiNipkgBrandBanner brand ||
                    brand.Heading.Text != "BitKeyBridge" ||
                    brand.Tagline.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, UiNipkgBrandBanner.IntroCaption) ||
                    brand.Tagline.TextAlign != (language == "he"
                        ? ContentAlignment.MiddleRight
                        : ContentAlignment.MiddleLeft) ||
                    brand.Tagline.Anchor != (language == "he"
                        ? AnchorStyles.Right : AnchorStyles.Left))
                    failures.Add("NI-style Start banner lost EN/RU/HE caption or direction: " +
                        language);
                var expectedEventLog = UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Windows Event Log");
                var expectedLatestRelease = UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Latest GitHub release");
                var latestReleaseItem = form.MainMenuStrip?.Items
                    .Find("LatestGithubReleaseMenuItem", true)
                    .FirstOrDefault();
                if (nestedItem.Text != expectedEventLog ||
                    latestReleaseItem?.Text != expectedLatestRelease)
                    failures.Add("Nested or top Tools menu caption not translated: " +
                        language);
                if (copyItemBeforeTranslation is not null &&
                    !ReferenceEquals(copyItemBeforeTranslation.Tag, protectedMenuTag))
                    failures.Add("Language switch overwrote ToolStrip command Tag: " +
                        language);
                if (mutableMenuItem.Text != "live device: test-001" ||
                    !ReferenceEquals(mutableMenuItem.Tag, dynamicMenuTag) ||
                    menuInput.Text != "Back" || menuChoice.Text != "Back")
                    failures.Add("Language switch rewrote runtime menu caption or editable value: " +
                        language);
                // The performance report must not disappear when a
                // high-DPI WinForms TabControl changes RTL orientation.
                if (banner is null || !banner.HasActiveDiagnostics ||
                    FindByName(banner, "CopyDiagnosticsButton") is not Button copyButton ||
                    copyButton.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Copy diagnostics"))
                    failures.Add("Diagnostics copy action disappeared or lost translation: " +
                        language);
                if (copyUiMenu is null ||
                    copyUiMenu.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Copy UI diagnostics"))
                    failures.Add("Always-visible Tools diagnostic copy action lost its caption: " +
                        language);
                // This picker is an enum-by-index, not a text-matched choice:
                // EN/RU/HE switches may never select a different identity
                // provider or trigger an asynchronous new connection.
                if (FindByName(form, "HomeConnectionSource") is not ComboBox source ||
                    source.Items.Count != connectionChoiceEnglish.Length ||
                    source.SelectedIndex != originalConnectionSelection ||
                    connectionChoiceEnglish.Where((english, index) =>
                        source.Items[index] as string !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(language, english)).Any())
                    failures.Add("Start connection source picker translation changed the selected source: " +
                        language);
                if (FindByName(form, "HomeConnectionOptionsToggle") is not Button optionsToggle)
                    failures.Add("Connection options toggle not found.");
                else
                {
                    if (optionsToggle.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "More connection options..."))
                        failures.Add("Collapsed connection options were not translated: " + language);
                    form.PreviewSetConnectionOptionsExpandedForUiTest(true);
                    if (optionsToggle.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Hide connection options"))
                        failures.Add("Expanded connection options were not translated: " + language);
                    form.PreviewSetConnectionOptionsExpandedForUiTest(false);
                    if (optionsToggle.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "More connection options..."))
                        failures.Add("Collapsing connection options lost translation: " + language);
                }
                if (helpItem is not null &&
                    helpItem.Text != UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, "Show explanations"))
                    failures.Add("Section explanations menu was not translated: " + language);
                if (tabs is not null &&
                    (tabs.DrawMode != TabDrawMode.Normal ||
                     tabs.RightToLeftLayout != (language == "he") ||
                     tabs.RightToLeft != (language == "he"
                         ? RightToLeft.Yes : RightToLeft.No)))
                    failures.Add("Native main tabs were recreated or lost correct RTL/LTR orientation.");
                foreach (var lateAction in new[] { lateAction1, lateAction2 })
                {
                    if (lateAction.Text != UiLanguageService.Translate(language, "MainRecoverySteps") ||
                        lateAction.RightToLeft != (language == "he" ? RightToLeft.Yes : RightToLeft.No))
                        failures.Add("Dynamic duplicate caption remained blank, stale or LTR-invalid: " +
                            language);
                }
                foreach (var (name, key) in translatedControls)
                {
                    var control = FindByName(form, name);
                    if (control is null ||
                        string.IsNullOrWhiteSpace(control.Text) ||
                        control.Text != UiLanguageService.Translate(language, key))
                        failures.Add("Language switch left an empty or stale caption: " +
                            name + " (" + language + ")");
                }
                // Verify the actual unnamed title/description/button controls
                // from the reported Export & Automation screenshot. This was
                // previously all English except the top-level Settings tab.
                foreach (var english in new[]
                {
                    "Export & Automation", "Recovery Export Storage",
                    "Export Scopes", "Output root:", "Subdirectory:",
                    "Save Export Settings", "Secure Output...",
                    "Remove Selected", "Check All", "Run Export", "Dry Run",
                    "Override publish guards (row drop / scope change)"
                })
                {
                    var expected = UiStaticCaptionCatalog.TranslateOrOriginal(
                        language, english);
                    var found = false;
                    var pending = new Stack<Control>();
                    pending.Push(form);
                    while (pending.Count > 0)
                    {
                        var current = pending.Pop();
                        if (string.Equals(current.Text, expected,
                                StringComparison.Ordinal))
                            found = true;
                        foreach (Control child in current.Controls)
                            pending.Push(child);
                    }
                    if (!found)
                        failures.Add("Export/admin caption was not translated: " +
                            english + " (" + language + ")");
                }
                // Headers are separate native ListView items, not Controls.
                var expectedScope = UiStaticCaptionCatalog.TranslateOrOriginal(
                    language, "Scope");
                var headersFound = false;
                var listPending = new Stack<Control>();
                listPending.Push(form);
                while (listPending.Count > 0)
                {
                    var current = listPending.Pop();
                    if (current is ListView list && list.Columns
                        .Cast<ColumnHeader>().Any(h => h.Text == expectedScope))
                        headersFound = true;
                    foreach (Control child in current.Controls)
                        listPending.Push(child);
                }
                if (!headersFound)
                    failures.Add("Export scope ListView headers were not localized: " +
                        language);

                // The other core grids must also localize. Assert
                // names/selection and data remain untouched by UI language.
                foreach (var (gridName, columnName, english) in new[]
                {
                    ("HomeRecentComputers", "LastAction", "Last action"),
                    ("HomeRecentComputers", "LastUsed", "Last used"),
                    ("HomeUnifiedSearchResults", "BitLockerDate", "BitLocker key date"),
                    ("HomeUnifiedSearchResults", "LapsDate", "LAPS metadata date"),
                    ("BitLockerSearchResults", "KeyDate", "Key date"),
                    ("BitLockerSearchResults", "Latest", "Latest"),
                    ("LapsSearchResults", "ComputerId", "Device / AD object ID"),
                    ("LapsKeyResults", "RecordType", "Current / history"),
                    ("LapsKeyResults", "Account", "Account"),
                    ("LapsKeyResults", "Age", "Age"),
                    ("LapsKeyResults", "Expires", "Expires")
                })
                {
                    if (FindByName(form, gridName) is not DataGridView grid ||
                        grid.Columns[columnName] is not DataGridViewColumn column ||
                        column.HeaderText !=
                            UiStaticCaptionCatalog.TranslateOrOriginal(language, english))
                        failures.Add("Core data grid header untranslated: " +
                            gridName + "/" + columnName + " (" + language + ")");
                }

                // DataGridViewColumn is not part of the Control tree.
                // LAPS Coverage must preserve column identity and data while
                // localizing headers without clipping at high DPI.
                if (FindByName(form, "LapsCoverageDevices") is DataGridView lapsGrid)
                {
                    foreach (var (columnName, english) in new[]
                    {
                        ("Guid", "AD object GUID"),
                        ("OU", "OU / distinguished name"),
                        ("Backup", "Visible LAPS metadata"),
                        ("Expiry", "Expiration (UTC)"),
                        ("State", "Expiry state"),
                        ("LastLogon", "Last AD logon (UTC)")
                    })
                    {
                        var expected = UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, english);
                        if (lapsGrid.Columns[columnName] is not DataGridViewColumn column ||
                            string.IsNullOrWhiteSpace(column.HeaderText) ||
                            column.HeaderText != expected)
                            failures.Add("LAPS grid header not localized: " +
                                english + " (" + language + ")");
                    }
                }
                if (FindByName(form, "LapsCoverageChanges") is DataGridView changeGrid)
                    foreach (var english in new[] { "Change type", "Previous", "Current" })
                        if (!changeGrid.Columns.Cast<DataGridViewColumn>().Any(
                            column => column.HeaderText ==
                                UiStaticCaptionCatalog.TranslateOrOriginal(language, english)))
                            failures.Add("LAPS comparison grid header not localized: " +
                                english + " (" + language + ")");

                if (tabs is not null &&
                    tabs.TabPages.Cast<TabPage>().Any(
                        page => string.IsNullOrWhiteSpace(page.Text)))
                    failures.Add("Language switch left an empty main workspace tab.");

                // Audit every REGISTERED static source, rather than only the
                // hand-picked controls above. Output is numeric only; never
                // retain or report user-entered text or recovery material.
                var captionAudit = form.PreviewFixedCaptionAuditForUiSelfTest();
                if (captionAudit.Controls < 10 ||
                    captionAudit.Menus < 1 || captionAudit.Headers < 5)
                    failures.Add("Static caption audit did not cover controls, menus and headers.");
                if (captionAudit.Stale != 0)
                    failures.Add("Static caption audit found stale EN/RU/HE values: " +
                        language + " (count=" + captionAudit.Stale + ").");
            }

            // Confirm this audit can DETECT an actual stale English caption
            // after Hebrew localization, then verify the ordinary language
            // pass repairs it. The fixture is a fixed registered action.
            lateAction1.Text = UiLanguageService.Translate("en", "MainRecoverySteps");
            var intentionallyStale = form.PreviewFixedCaptionAuditForUiSelfTest();
            if (intentionallyStale.Stale < 1)
                failures.Add("Static caption audit failed to detect a stale English label.");
            form.PreviewShellLanguageForLayoutTest("he");
            if (form.PreviewFixedCaptionAuditForUiSelfTest().Stale != 0)
                failures.Add("Static caption audit did not recover after Hebrew repair.");

            // Adopting a NEW known English caption must still work after
            // runtime text was preserved. It must not touch menu bindings.
            mutableMenuItem.Text = "Copy diagnostics";
            foreach (var language in new[] { "ru", "en", "he" })
            {
                form.PreviewShellLanguageForLayoutTest(language);
                if (mutableMenuItem.Text !=
                        UiStaticCaptionCatalog.TranslateOrOriginal(
                            language, "Copy diagnostics") ||
                    !ReferenceEquals(mutableMenuItem.Tag, dynamicMenuTag) ||
                    menuInput.Text != "Back" || menuChoice.Text != "Back")
                    failures.Add("Runtime menu source replacement failed EN/RU/HE: " +
                        language);
            }


            // MainForm's native grid/list headers can also be overwritten by
            // live device or account details after an initial static caption
            // was translated. Such data must never be cached or localized.
            DataGridViewColumn? dynamicGridHeader = null;
            if (FindByName(form, "LapsCoverageChanges") is DataGridView sourceGrid)
                dynamicGridHeader = sourceGrid.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(column => column.HeaderText ==
                        UiStaticCaptionCatalog.TranslateOrOriginal(
                            "he", "Change type"));

            ColumnHeader? dynamicListHeader = null;
            var headerSearch = new Stack<Control>();
            headerSearch.Push(form);
            while (headerSearch.Count > 0)
            {
                var current = headerSearch.Pop();
                if (current is ListView list && dynamicListHeader is null)
                    dynamicListHeader = list.Columns.Cast<ColumnHeader>()
                        .FirstOrDefault(column => column.Text ==
                            UiStaticCaptionCatalog.TranslateOrOriginal("he", "Scope"));
                foreach (Control child in current.Controls)
                    headerSearch.Push(child);
            }
            if (dynamicGridHeader is null || dynamicListHeader is null)
                failures.Add("Native main-window dynamic column fixture was not found.");
            else
            {
                dynamicGridHeader.HeaderText = "runtime account: user@example.invalid";
                dynamicListHeader.Text = "runtime device: PC-001";
                foreach (var language in new[] { "ru", "en", "he" })
                {
                    form.PreviewShellLanguageForLayoutTest(language);
                    if (dynamicGridHeader.HeaderText !=
                            "runtime account: user@example.invalid" ||
                        dynamicListHeader.Text != "runtime device: PC-001")
                        failures.Add("Main grid/list localization overwrote live header data: " +
                            language);
                }

                dynamicGridHeader.HeaderText = "Change type";
                dynamicListHeader.Text = "Scope";
                foreach (var language in new[] { "ru", "en", "he" })
                {
                    form.PreviewShellLanguageForLayoutTest(language);
                    if (dynamicGridHeader.HeaderText !=
                            UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Change type") ||
                        dynamicListHeader.Text !=
                            UiStaticCaptionCatalog.TranslateOrOriginal(
                                language, "Scope"))
                        failures.Add("Main grid/list failed to adopt replacement English captions: " +
                            language);
                }
            }

            if (FindByName(form, "LapsCoverageChanges") is DataGridView lateGrid)
            {
                // Use centralized column factory (HiDPI/WinForms policy).
                using var lateColumn = UiStyle.CreateSortableTextColumn(
                    "LateLocalizedHeaderFixture", "Expiration (UTC)");
                lateGrid.Columns.AddRange([lateColumn]);
                if (lateColumn.HeaderText != UiStaticCaptionCatalog.TranslateOrOriginal(
                        "he", "Expiration (UTC)"))
                    failures.Add("Late-added DataGridView column was not localized in Hebrew.");
                form.PreviewShellLanguageForLayoutTest("ru");
                if (lateColumn.HeaderText != UiStaticCaptionCatalog.TranslateOrOriginal(
                        "ru", "Expiration (UTC)"))
                    failures.Add("Late-added DataGridView column did not switch to Russian.");
                lateGrid.Columns.Remove(lateColumn);
                form.PreviewShellLanguageForLayoutTest("he");
            }

            // Disposed buttons must never remain in the active caption cache.
            // Replacing a control on a live page must pick up Hebrew instantly.
            dynamicCaptionsPanel.Controls.Remove(lateAction1);
            lateAction1.Dispose();
            using var replacementAction = UiStyle.CreateActionButton("Replacement action");
            replacementAction.Name = "HomeRecoverySteps";
            dynamicCaptionsPanel.Controls.Add(replacementAction);
            if (replacementAction.Text != UiLanguageService.Translate("he", "MainRecoverySteps"))
                failures.Add("Recreated action lost active Hebrew localization.");
            form.PreviewShellLanguageForLayoutTest("ru");
            if (replacementAction.Text != UiLanguageService.Translate("ru", "MainRecoverySteps"))
                failures.Add("Recreated action did not follow subsequent language switches.");
            form.PreviewShellLanguageForLayoutTest("he");

            if (FindByName(form, "UpdateCenterCompact") is not CheckBox updateDensityFinal ||
                updateDensityFinal.Text != UiLanguageService.Translate("he", "UpdateCenterCompact"))
                failures.Add("Hebrew Update Center switch lost its caption after repeated switching.");

            form.PreviewShellLanguageForLayoutTest("ru");
            if (FindByName(form, "UpdateCenterCompact") is not CheckBox russianDensity ||
                russianDensity.Text != UiLanguageService.Translate("ru", "UpdateCenterCompact"))
                failures.Add("Russian Update Center switch did not localize.");
            form.PreviewShellLanguageForLayoutTest("he");

            foreach (var name in new[]
            {
                "HomeUnifiedQuery", "BitLockerSearchQuery",
                "LapsLiveSearchQuery", "UnifiedDeviceSearchQuery",
                "BitLockerRecoverySecretField", "LapsPasswordValue",
                "EntraBitLockerSecretField"
            })
            {
                if (FindByName(form, name) is not TextBox literal ||
                    literal.RightToLeft != RightToLeft.No)
                    failures.Add("Hebrew shell changed identity or secret field direction: " + name);
            }
        }
        catch (Exception ex)
        {
            failures.Add("Hebrew main shell orientation regression: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static void VerifyLapsHelpdeskStates(
        List<string> failures)
    {
        var candidate = new LapsSearchResult
        {
            ComputerName = "IT-EUGENEB11"
        };

        if (HelpdeskStatus.Laps(null) != "Not verified" ||
            HelpdeskStatus.Laps(candidate) != "Not verified" ||
            new HomeSearchResult().LapsStatus != "Not verified")
        {
            failures.Add("Start LAPS missing metadata must be Not verified.");
        }

        var selected = new HomeSearchResult
        {
            ComputerName = candidate.ComputerName,
            LapsCandidateFound = false,
            LapsStatus = "Not verified"
        };
        if (!HelpdeskStatus.CanAttemptLapsRead(true, selected) ||
            HelpdeskStatus.CanAttemptLapsRead(false, selected) ||
            HelpdeskStatus.CanAttemptLapsRead(true, null))
        {
            failures.Add("Start LAPS action must permit explicit reads of selected AD computers without metadata.");
        }

        if (HelpdeskStatus.ResolveHomeDefaultAction(null) !=
                HomeDefaultAction.None ||
            HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
                HomeDefaultAction.ChooseRecoveryType ||
            HelpdeskStatus.CanAttemptBitLockerSearch(false, selected) ||
            !HelpdeskStatus.CanAttemptBitLockerSearch(true, selected))
        {
            failures.Add("Unverified metadata must require an explicit operator choice.");
        }

        selected.LapsCandidateFound = true;
        selected.LapsStatus = "Detected";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.Laps)
        {
            failures.Add("LAPS-only detected metadata must open LAPS.");
        }

        selected.LapsStatus = "Expired";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.Laps)
        {
            failures.Add("Expired LAPS metadata must still open LAPS.");
        }

        selected.LapsStatus = "Not verified";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.ChooseRecoveryType)
        {
            failures.Add("Unverified LAPS metadata must not route automatically.");
        }

        selected.BitLockerKeyCount = 1;
        selected.LapsCandidateFound = true;
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.BitLocker)
        {
            failures.Add("When BitLocker and LAPS coexist, the default action must be BitLocker.");
        }

        var cloud = new HomeSearchResult
        {
            ComputerName = "PC-CLOUD",
            IsCloudRecord = true,
            SourceIdentity = "INTUNE:MANAGED:managed-01",
            BitLockerKeyCount = 1
        };
        if (HelpdeskStatus.ResolveHomeDefaultAction(cloud) !=
            HomeDefaultAction.Devices)
        {
            failures.Add("Cloud metadata must route to Devices and not AD Recovery.");
        }

        selected.BitLockerKeyCount = 0;
        selected.BitLockerLookupFailed = true;
        selected.LapsCandidateFound = true;
        selected.LapsStatus = "Detected";
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
            HomeDefaultAction.ChooseRecoveryType)
        {
            failures.Add("A failed BitLocker lookup must not auto-route LAPS as if there were no key.");
        }

        selected.BitLockerLookupFailed = false;
        selected.IdentityAmbiguous = true;
        selected.BitLockerKeyCount = 1;
        if (HelpdeskStatus.ResolveHomeDefaultAction(selected) !=
                HomeDefaultAction.ChooseRecoveryType ||
            HelpdeskStatus.CanAttemptLapsRead(true, selected) ||
            HelpdeskStatus.CanAttemptBitLockerSearch(true, selected))
        {
            failures.Add("Ambiguous AD object identity must block both quick recovery actions.");
        }

        selected.IdentityAmbiguous = false;
        if (!HelpdeskStatus.HasIdentityCollision(
                [
                    new RecoverySearchResult
                    {
                        ComputerName = "PC-DUP",
                        ComputerDistinguishedName = "CN=PC-DUP,OU=Old,DC=example,DC=test"
                    }
                ],
                [
                    new LapsSearchResult
                    {
                        ComputerName = "PC-DUP",
                        ComputerId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        ComputerDistinguishedName = "CN=PC-DUP,OU=New,DC=example,DC=test"
                    }
                ],
                "PC-DUP") ||
            HelpdeskStatus.HasIdentityCollision(
                [],
                [
                    new LapsSearchResult
                    {
                        ComputerName = "PC-SINGLE",
                        ComputerId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
                    }
                ],
                "PC-SINGLE"))
        {
            failures.Add("Stable AD identifiers are not being used to detect duplicate computer names.");
        }

        if (!HelpdeskStatus.ShouldRetryBitLockerInDomain(
                0, "OU=Win11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il") ||
            HelpdeskStatus.ShouldRetryBitLockerInDomain(
                1, "OU=Win11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il") ||
            HelpdeskStatus.ShouldRetryBitLockerInDomain(
                0, "DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il"))
        {
            failures.Add("BitLocker exact-computer domain retry must occur only after a scoped metadata miss.");
        }

        selected.BitLockerOutsideSelectedScope = true;
        if (!HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, "OU=Windows11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il") ||
            HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, "DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il"))
        {
            failures.Add("Recovery navigation must reuse full domain scope when the key was found outside the selected OU.");
        }

        selected.BitLockerOutsideSelectedScope = false;
        selected.BitLockerKeyCount = 0;
        if (!HelpdeskStatus.ShouldSelectDomainForRecovery(
                selected, "OU=Windows11,DC=yosh,DC=ac,DC=il", "DC=yosh,DC=ac,DC=il"))
        {
            failures.Add("Unverified BitLocker navigation must retry the domain scope.");
        }

        var onlyBitLocker = new HomeSearchResult
        {
            BitLockerKeyCount = 1,
            LapsCandidateFound = true,
            LapsStatus = "Not verified"
        };
        var onlyLaps = new HomeSearchResult
        {
            LapsCandidateFound = true,
            LapsStatus = "Detected"
        };
        var both = new HomeSearchResult
        {
            BitLockerKeyCount = 1,
            LapsCandidateFound = true,
            LapsStatus = "Expired"
        };
        if (HelpdeskStatus.VisibleHomeServices(null) != (false, false) ||
            HelpdeskStatus.VisibleHomeServices(cloud) != (false, false) ||
            HelpdeskStatus.VisibleHomeServices(onlyBitLocker) != (true, false) ||
            HelpdeskStatus.VisibleHomeServices(onlyLaps) != (false, true) ||
            HelpdeskStatus.VisibleHomeServices(both) != (true, true) ||
            HelpdeskStatus.VisibleHomeServices(new HomeSearchResult()) != (true, true))
        {
            failures.Add(
                "Start cards must show only verified services while leaving completely unknown results diagnosable.");
        }

        candidate.ExpiresAtUtc = DateTime.UtcNow.AddDays(10);
        if (HelpdeskStatus.Laps(candidate) != "Detected")
            failures.Add("Start LAPS future expiration must be Detected.");

        candidate.ExpiresAtUtc = DateTime.UtcNow.AddDays(-10);
        if (HelpdeskStatus.Laps(candidate) != "Expired")
            failures.Add("Start LAPS past expiration must be Expired.");

        using var row = new LapsPasswordEntry
        {
            Attribute = "msLAPS-EncryptedPassword",
            Status = LapsPasswordStatus.AccessDenied
        };
        if (HelpdeskStatus.LapsEntry(row) != "Decrypt unauthorized")
            failures.Add("Encrypted LAPS AccessDenied must be Decrypt unauthorized.");

        row.Status = LapsPasswordStatus.DecryptionFailed;
        if (HelpdeskStatus.LapsEntry(row) != "Decrypt failed")
            failures.Add("Other encrypted LAPS decryption failures must not be called Unauthorized.");

        row.Status = LapsPasswordStatus.NotReturned;
        if (HelpdeskStatus.LapsEntry(row) != "Not returned")
            failures.Add("Unreturned LAPS record must not imply no backup.");
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

        if (form is MainForm main)
        {
            if (form.AutoScroll)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: MainForm must use page scrolling, not form scrolling.");
            }

            VerifyMainViewportFill(main, formName, scenario, failures);
            VerifyNestedWorkspaceViewport(main, formName, scenario, failures);
            VerifyMainFormTabs(form, formName, scenario, failures);
            var healthResults = FindByName(main, "AccessHealthResults") as ListView;
            if (healthResults is not null && !healthResults.ShowItemToolTips)
                failures.Add($"{formName}/{scenario.Name}: Access Health long diagnostics must have full-text hover access.");
            var toolsMenu = main.MainMenuStrip?.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(x => x.Text == "Tools");
            if (toolsMenu is null ||
                toolsMenu.DropDownItems.Find("RecoveryStepsMenu", false).Length != 1 ||
                toolsMenu.DropDownItems.Find("AccessHealthMenu", false).Length != 1)
                failures.Add($"{formName}/{scenario.Name}: global Recovery Steps and Access Health shortcuts must be discoverable from Tools.");
            if (FindByName(main, "HomeRecoverySteps") is not Button contextSteps ||
                contextSteps.Enabled)
                failures.Add($"{formName}/{scenario.Name}: Start Recovery Steps button should exist but remain disabled without a selected device.");
            VerifySelectedHomeRecoveryLayout(main, formName, scenario, failures);
            VerifyHomeStatusGlyphColumn(main, formName, scenario, failures);
        }
        else
        {
            if (!form.AutoScroll)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: dialog scrolling is disabled.");
            }
        }
        if (form is RecoveryWorkflowDialog)
        {
            var expectedCloud = formName.StartsWith("Hebrew", StringComparison.Ordinal);
            var expectedHebrew = expectedCloud;
            if (form.RightToLeftLayout != expectedHebrew ||
                (expectedCloud
                    ? FindByName(form, "RecoveryRouteDevices") is not Button dev || !dev.Enabled
                    : FindByName(form, "RecoveryRouteBitLocker") is not Button bit || !bit.Enabled ||
                      FindByName(form, "RecoveryRouteLaps") is not Button laps || !laps.Enabled))
                failures.Add($"{formName}/{scenario.Name}: translated recovery guide lacks correct RTL or source-specific metadata navigation.");
        }
        if (form is not MainForm)
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

    private static void VerifyMainViewportFill(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var shell = FindByName(main, "MainLayoutRoot");
        var tabs = FindByName(main, "MainWorkspaceTabs");
        var start = FindByName(main, "HomeLayoutRoot");
        if (shell is null || tabs is null || start is null)
        {
            failures.Add($"{formName}/{scenario.Name}: docked workspace layout is missing.");
            return;
        }

        // Tests the real resizing logic, not NormalizeDockFillSizes: this
        // catches the formerly tiny, top-left Start viewport after maximize.
        main.ClientSize = new Size(
            Math.Max(1500, scenario.ClientSize.Width + 240),
            Math.Max(900, scenario.ClientSize.Height + 140));
        main.PerformLayout();
        main.ReflowMainViewport();
        main.PerformLayout();

        if (shell.Dock != DockStyle.Fill ||
            tabs.Dock != DockStyle.Fill ||
            start.Dock != DockStyle.Fill ||
            shell.Width < main.ClientSize.Width * 0.90 ||
            tabs.Width < shell.ClientSize.Width * 0.90)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: maximized window leaves a small top-left workspace instead of filling the client area.");
        }
    }

    /// <summary>
    /// Recreate the real screenshot failure: leave a selected Dock=Fill child
    /// at a provisional narrow size even though its outer tab is full width.
    /// Verify that one production reflow (not test-only normalization) repairs
    /// Start, BitLocker, LAPS, Helpdesk, Devices, Diagnostics, Settings, and
    /// every selected nested page at compact and maximized RDP viewport sizes.
    /// </summary>
    private static void VerifyNestedWorkspaceViewport(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        if (FindByName(main, "MainWorkspaceTabs") is not TabControl tabs)
            return;

        var originalSize = main.ClientSize;
        var originalTab = tabs.SelectedTab;
        try
        {
            var testSizes = new[]
            {
                scenario.ClientSize,
                new Size(
                    Math.Max(1500, scenario.ClientSize.Width + 240),
                    Math.Max(900, scenario.ClientSize.Height + 140))
            };

            foreach (var size in testSizes)
            {
                main.ClientSize = size;
                main.PerformLayout();
                NormalizeDockFillSizes(main);

                foreach (TabPage page in tabs.TabPages)
                {
                    tabs.SelectedTab = page;
                    Application.DoEvents();
                    NormalizeDockFillSizes(main);
                    main.ReflowMainViewport();
                    VerifySelectedPage(page, page.Text, size.Width);
                }
            }
        }
        finally
        {
            if (originalTab is not null)
                tabs.SelectedTab = originalTab;
            main.ClientSize = originalSize;
            main.ReflowMainViewport();
        }

        void VerifySelectedPage(TabPage page, string route, int windowWidth)
        {
            page.CreateControl();
            var content = page.Controls.Cast<Control>()
                .FirstOrDefault(child => child.Dock == DockStyle.Fill);

            if (content is null)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}/{route}: selected page has no full-size workspace.");
                return;
            }

            var available = page.ClientSize;
            if (available.Width < 240 || available.Height < 150)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}/{route}: tab content viewport is too small " +
                    $"({available.Width}x{available.Height} in {windowWidth}px window).");
                return;
            }

            // This deliberately poisons a currently selected, docked child.
            // A test-side NormalizeDockFillSizes after this point would hide
            // the defect, so the real MainForm routine must repair it.
            content.Size = new Size(
                Math.Min(128, available.Width / 4),
                Math.Min(86, available.Height / 4));
            main.ReflowMainViewport();

            if (content.Width < available.Width * 0.80 ||
                content.Height < available.Height * 0.80)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}/{route}: selected nested content remains " +
                    $"at {content.Width}x{content.Height} inside " +
                    $"{available.Width}x{available.Height} tab after ReflowMainViewport.");
            }

            if (content is FlowLayoutPanel
                { FlowDirection: FlowDirection.TopDown } flow)
            {
                flow.PerformLayout();
                var rightLimit = flow.ClientSize.Width - flow.Padding.Right + 3;
                var minimumSectionWidth = Math.Max(
                    0, flow.ClientSize.Width - flow.Padding.Horizontal -
                    SystemInformation.VerticalScrollBarWidth - 4);

                foreach (Control child in flow.Controls)
                {
                    if (child is not TableLayoutPanel { AutoSize: true } &&
                        child is not FlowLayoutPanel { AutoSize: true })
                        continue;

                    if (minimumSectionWidth > 100 &&
                        child.Width < minimumSectionWidth * 0.78)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}/{route}: AutoSize section " +
                            $"'{ControlName(child)}' remained narrow " +
                            $"({child.Width} of {minimumSectionWidth}px).");
                    }
                    if (child.Right > rightLimit)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}/{route}: AutoSize section " +
                            $"'{ControlName(child)}' is horizontally clipped " +
                            $"(Right={child.Right}, Limit={rightLimit}).");
                    }
                }

                if (page.Name == "DiagnosticToolsTab")
                {
                    if (FindByName(page, "DiagnosticToolsActions")
                        is not FlowLayoutPanel actions ||
                        actions.Controls.OfType<Button>().Count() != 6 ||
                        FindByName(page, "RunUiPerformanceDiagnosticsButton")
                            is not Button)
                    {
                        failures.Add(
                            $"{formName}/{scenario.Name}: Diagnostics > Tools actions are missing.");
                    }
                    else
                    {
                        foreach (var button in actions.Controls.OfType<Button>())
                        {
                            if (button.Right > actions.ClientSize.Width + 3 ||
                                button.Bottom > actions.ClientSize.Height + 3)
                            {
                                failures.Add(
                                    $"{formName}/{scenario.Name}: Diagnostics > Tools " +
                                    $"button '{button.Text}' is clipped at {windowWidth}px.");
                            }
                        }
                    }
                }
            }

            if (content is not TabControl nested)
                return;

            var originalNestedTab = nested.SelectedTab;
            try
            {
                foreach (TabPage child in nested.TabPages)
                {
                    nested.SelectedTab = child;
                    Application.DoEvents();
                    NormalizeDockFillSizes(main);
                    main.ReflowMainViewport();
                    VerifySelectedPage(child, route + " > " + child.Text, windowWidth);
                }
            }
            finally
            {
                if (originalNestedTab is not null)
                    nested.SelectedTab = originalNestedTab;
            }
        }
    }

    private static void VerifySelectedHomeRecoveryLayout(
        MainForm main, string formName, Scenario scenario,
        List<string> failures)
    {
        var root = FindByName(main, "HomeLayoutRoot") as TableLayoutPanel;
        var area = FindByName(main, "HomeSearchResultsArea") as TableLayoutPanel;
        var cards = FindByName(main, "HomeServiceCards") as TableLayoutPanel;
        var grid = FindByName(main, "HomeUnifiedSearchResults") as DataGridView;
        var bitLocker = FindByName(main, "HomeBitLockerPaneHost") as TableLayoutPanel;
        var laps = FindByName(main, "HomeLapsPaneHost") as TableLayoutPanel;
        var computers = FindByName(main, "HomeComputersPaneHost") as TableLayoutPanel;
        var recent = FindByName(main, "HomeRecentComputers") as DataGridView;
        var quickRecent = FindByName(main, "HomeRecentQuickPanel") as TableLayoutPanel;

        if (root is null || area is null || cards is null || grid is null ||
            bitLocker is null || laps is null || computers is null ||
            recent is null || quickRecent is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: one of the three draggable Start panes or top-right Recent computers is missing.");
            return;
        }

        // Regression for the real 1920x1080 / 100%-DPI screenshot:
        // status and connection rows must not hold stale expanded heights.
        // Checking layout policy here is reliable for virtual DPI scenarios;
        // native screen geometry is also verified below on wide viewports.
        var connectionGroup = FindByName(main, "HomeConnectionsGroup") as GroupBox;
        var adConnection = FindByName(main, "HomeAdConnectionLayout") as TableLayoutPanel;
        var searchControls = FindByName(main, "HomeSearchControls") as TableLayoutPanel;
        var selectionPanel = FindByName(main, "HomeSelectionPanel") as TableLayoutPanel;
        var lifetimeActions = FindByName(main, "HomeSecretLifetimeActions") as FlowLayoutPanel;
        if (connectionGroup is null || adConnection is null ||
            searchControls is null || selectionPanel is null ||
            lifetimeActions is null ||
            new Control[] { connectionGroup, adConnection, searchControls,
                selectionPanel, lifetimeActions }.Any(control =>
                !control.AutoSize || control is GroupBox group &&
                    group.AutoSizeMode != AutoSizeMode.GrowAndShrink ||
                control is Panel panel &&
                    panel.AutoSizeMode != AutoSizeMode.GrowAndShrink))
        {
            failures.Add($"{formName}/{scenario.Name}: Start status sections must shrink after hiding expanded controls.");
        }

        var connectionLayout =
            FindByName(main, "HomeConnectionsAndRecent") as TableLayoutPanel;
        if (connectionLayout is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: upper Connections and Recent layout must be responsive.");
        }
        else
        {
            var scale = Math.Max(
                main.DeviceDpi / (double)UiStyle.BaselineDpi,
                main.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));
            var actualWidth = connectionLayout.Parent?.ClientSize.Width ?? 0;
            actualWidth -= connectionLayout.Parent?.Padding.Horizontal ?? 0;
            var hasRecentEntries = recent.Rows.Count > 0;
            var pair = hasRecentEntries &&
                actualWidth >= (int)Math.Ceiling(
                    UiStyle.HomeConnectionRecentPairMinimumWidth * scale);
            if (connectionLayout.ColumnCount != (pair ? 2 : 1) ||
                connectionLayout.RowCount != (pair ? 1 : 2))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Recent computers must be absent from empty space, appear beside Connections on wide screens, and stack on narrow/high-DPI screens.");
            }
            if (!hasRecentEntries &&
                (quickRecent.Visible ||
                 connectionLayout.RowStyles.Count < 2 ||
                 connectionLayout.RowStyles[1].SizeType != SizeType.Absolute ||
                 connectionLayout.RowStyles[1].Height != 0F))
                failures.Add(
                    $"{formName}/{scenario.Name}: an empty Recent history must consume zero Start rows.");
        }

        if (root.RowCount != 6 ||
            root.RowStyles[5].SizeType != SizeType.Percent ||
            !ReferenceEquals(cards.Parent, area) ||
            cards.Controls.Count != 3 ||
            !ReferenceEquals(recent.Parent, quickRecent))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Start must reserve its workspace for three equal panes, with Recent computers in Connections' top-right area.");
        }

        // Simulate metadata-only computer selection. Do not connect to AD,
        // call Graph or retrieve any recovery password in the layout test.
        try
        {
            var rowIndex = grid.Rows.Add(
                "UI-TEST-PC", "AD", "Available", DateTime.UtcNow,
                "Detected", DateTime.UtcNow);
            var computer = new HomeSearchResult
            {
                ComputerName = "UI-TEST-PC",
                ComputerId = Guid.Empty.ToString(),
                BitLockerKeyCount = 1,
                BitLockerStatus = "Available",
                LapsCandidateFound = true,
                LapsStatus = "Detected"
            };
            grid.Rows[rowIndex].Tag = computer;
            grid.ClearSelection();
            grid.Rows[rowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[rowIndex].Cells[0];
            main.ReflowMainViewport();

            var width = area.Parent?.ClientSize.Width ?? 0;
            width -= area.Parent?.Padding.Horizontal ?? 0;
            var effectiveScale = Math.Max(
                main.DeviceDpi / (double)UiStyle.BaselineDpi,
                main.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size));
            bool expectedThree = width >= (int)Math.Ceiling(
                UiStyle.HomeThreePaneMinimumWidth * effectiveScale);
            bool expectedTwo = !expectedThree && width >= (int)Math.Ceiling(
                UiStyle.HomeRecoveryCardsPairMinimumWidth * effectiveScale);

            // This failure previously survived structural three-column tests:
            // all three cards existed but started below the visible viewport.
            // At wide 100%-scale sizes, verify that the card row is actually
            // on screen after a metadata-only selection.
            if (expectedThree && effectiveScale <= 1.05 &&
                root.ClientSize.Height >= 700 && cards.Height > 0)
            {
                var cardTop = cards.PointToScreen(Point.Empty).Y;
                var viewportTop = root.PointToScreen(Point.Empty).Y;
                var cardBottomLimit = viewportTop + root.ClientSize.Height;
                if (cardTop >= cardBottomLimit - 60)
                    failures.Add($"{formName}/{scenario.Name}: Start recovery cards are pushed below the visible viewport.");
            }

            var hosts = new[] { bitLocker, laps, computers };
            var cells = hosts.Select(cards.GetPositionFromControl).ToArray();
            if (expectedThree)
            {
                bool equallySized =
                    cards.ColumnCount == 3 && cards.RowCount == 1 &&
                    cells.All(position => position.Row == 0 &&
                        position.Column is >= 0 and <= 2) &&
                    cells.Select(position => position.Column).Distinct().Count() == 3 &&
                    cards.ColumnStyles.Count == 3 &&
                    cards.ColumnStyles.Cast<ColumnStyle>().All(style =>
                        style.SizeType == SizeType.Percent &&
                        Math.Abs(style.Width - 100F / 3F) < 0.1F);
                if (!equallySized)
                    failures.Add(
                        $"{formName}/{scenario.Name}: BitLocker, LAPS and Computers must each occupy one equal third of the wide Start workspace.");

                // Uniform margins and Dock=Fill also guarantee equal rendered
                // width and height, not just equal percentage column styles.
                if (hosts.All(host => host.Width > 0 && host.Height > 0) &&
                    (hosts.Max(host => host.Width) - hosts.Min(host => host.Width) > 4 ||
                     hosts.Max(host => host.Height) - hosts.Min(host => host.Height) > 4))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: the three draggable cards must have equal actual dimensions.");
                }

                // Any panel can exchange places with either other panel.
                // This action must preserve the selected computer and cards.
                var originalBitLocker = cards.GetPositionFromControl(bitLocker).Column;
                var originalComputers = cards.GetPositionFromControl(computers).Column;
                main.ExchangeHomePanesForUiSelfTest("BitLocker", "Computers");
                main.ReflowMainViewport();
                if (cards.GetPositionFromControl(bitLocker).Column != originalComputers ||
                    cards.GetPositionFromControl(computers).Column != originalBitLocker ||
                    grid.SelectedRows.Count != 1 ||
                    !ReferenceEquals(grid.SelectedRows[0].Tag, computer))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: dragging BitLocker onto Computers must swap their positions without losing the computer selection.");
                }
                main.ExchangeHomePanesForUiSelfTest("BitLocker", "Computers");
                main.ReflowMainViewport();

                // Verify the third pane can be moved independently.
                var originalLaps = cards.GetPositionFromControl(laps).Column;
                originalComputers = cards.GetPositionFromControl(computers).Column;
                main.ExchangeHomePanesForUiSelfTest("Laps", "Computers");
                main.ReflowMainViewport();
                if (cards.GetPositionFromControl(laps).Column != originalComputers ||
                    cards.GetPositionFromControl(computers).Column != originalLaps)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: LAPS must be independently draggable onto Computers.");
                }
                main.ExchangeHomePanesForUiSelfTest("Laps", "Computers");
                main.ReflowMainViewport();
            }
            else if (expectedTwo)
            {
                var bottomHost = hosts.FirstOrDefault(
                    host => cards.GetPositionFromControl(host).Row == 1);
                if (cards.ColumnCount != 2 || cards.RowCount != 2 ||
                    cards.RowStyles.Count != 2 ||
                    cards.RowStyles.Cast<RowStyle>().Any(
                        row => row.SizeType != SizeType.Absolute) ||
                    cells.Count(position => position.Row == 0) != 2 ||
                    cells.Count(position => position.Row == 1) != 1 ||
                    cells.Where(position => position.Row == 0)
                        .Select(position => position.Column).Distinct().Count() != 2 ||
                    bottomHost is null ||
                    cards.GetColumn(bottomHost) != 0 ||
                    cards.GetColumnSpan(bottomHost) != 2)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: intermediate-width Start must retain two side-by-side panes and a full-width third pane.");
                }
            }
            else
            {
                if (cards.ColumnCount != 1 || cards.RowCount != 3 ||
                    cells.Any(position => position.Column != 0) ||
                    cells.Select(position => position.Row).Distinct().Count() != 3 ||
                    cards.RowStyles.Count != 3 ||
                    cards.RowStyles.Cast<RowStyle>().Any(row => row.SizeType != SizeType.Absolute))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: compact HiDPI Start must stack all three equally high panes with scrolling.");
                }
            }

            // Check reset while a real grid row is selected. Reordering
            // its surrounding hosts must preserve the exact result object.
            main.PreviewResetVisualLayoutForUiSelfTest();
            main.ReflowMainViewport();
            if (grid.SelectedRows.Count != 1 ||
                !ReferenceEquals(grid.SelectedRows[0].Tag, computer))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: visual reset lost the exact selected device metadata.");
            }
            // Validate actual WinForms grid coordinates, not only the
            // persisted pane-order string. It must work both in 3 columns
            // and in the vertically stacked compact/RDP layout.
            var resetCells = new[]
            {
                cards.GetPositionFromControl(bitLocker),
                cards.GetPositionFromControl(laps),
                cards.GetPositionFromControl(computers)
            };
            var resetLayoutCorrect = expectedThree
                ? resetCells.Select(x => x.Column).SequenceEqual([0, 1, 2]) &&
                  resetCells.All(x => x.Row == 0)
                : expectedTwo
                    ? resetCells.Select(x => x.Column).SequenceEqual([0, 1, 0]) &&
                      resetCells.Select(x => x.Row).SequenceEqual([0, 0, 1]) &&
                      cards.GetColumnSpan(computers) == 2
                    : resetCells.Select(x => x.Row).SequenceEqual([0, 1, 2]) &&
                      resetCells.All(x => x.Column == 0);
            if (!resetLayoutCorrect)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: visual reset left overlapping or incorrectly ordered WinForms panes.");
            }

            if (FindByName(cards, "HomeRevealBitLockerButton") is Button { Enabled: true } ||
                FindByName(cards, "HomeRevealLapsButton") is Button { Enabled: true } ||
                FindByName(cards, "HomeLoadLapsHistoryButton") is Button { Enabled: true })
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: metadata-only selection cannot reveal LAPS/BitLocker secrets or load history without authorization.");
            }
        }
        finally
        {
            grid.Rows.Clear();
            main.ReflowMainViewport();
        }
    }

    private static void VerifySelectedHomeStatusBounds(
        MainForm main, string formName, Scenario scenario,
        List<string> failures)
    {
        // This specifically tests the metadata-only selected-device view.
        // The previous test checked only the label's HEIGHT after wrapping;
        // a narrow AutoSize flow could still hide "retained." on the same
        // one-line caption while all other layout assertions passed.
        if (FindByName(main, "SecretLifetimeStatus") is not UiStatusLabel status ||
            status.Parent is not FlowLayoutPanel flow ||
            flow.Parent is not Control parent ||
            !status.Visible || parent.ClientSize.Width <= 0)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Selected Start lifetime status is not visible.");
            return;
        }

        var rowWidth = Math.Max(flow.ClientSize.Width,
            parent.ClientSize.Width - parent.Padding.Horizontal -
            flow.Margin.Horizontal);
        var available = Math.Max(1, rowWidth - flow.Padding.Horizontal -
            status.Margin.Horizontal);

        // Never calculate the expected text bounds with the status label's
        // own GetPreferredSize(): an already-clamped MaximumSize can make a
        // truncated caption appear geometrically correct to that method.
        // Measure the full caption independently with the same TextRenderer
        // flags that UiStatusLabel uses when painting.
        const TextFormatFlags flags = TextFormatFlags.Left |
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        var glyphGap = status.Image is null ? 0 :
            (int)Math.Ceiling(UiStyle.ControlGap *
                status.DeviceDpi / (double)UiStyle.BaselineDpi);
        var chromeWidth = status.Padding.Horizontal +
            (status.BorderStyle == BorderStyle.None ? 0 : 2) +
            status.GlyphColumnInset +
            (status.Image?.Width ?? 0) + glyphGap;
        var chromeHeight = status.Padding.Vertical +
            (status.BorderStyle == BorderStyle.None ? 0 : 2);
        var text = status.Text ?? string.Empty;
        var textFullWidth = TextRenderer.MeasureText(
            text, status.Font, new Size(8192, 16384), flags).Width;
        var requiredOneLine = chromeWidth + textFullWidth;
        var wrappedHeight = TextRenderer.MeasureText(
            text, status.Font, new Size(
                Math.Max(1, status.ClientSize.Width - chromeWidth), 16384),
            flags).Height;
        var requiredHeight = chromeHeight +
            Math.Max(status.Image?.Height ?? 0, wrappedHeight);
        var shouldFitOneLine = requiredOneLine <= available;

        // At the exact painted width, GDI's constrained MeasureText can
        // report a height up to two logical pixels above the visible glyph
        // line (default overhang padding). The latest native PNG confirms
        // the complete one-line caption. Allow only that DPI-scaled rounding
        // difference; an actual hidden wrapped line still adds ~font height
        // and must fail the assertion.
        var heightRoundingTolerance = Math.Max(2,
            (int)Math.Ceiling(2d * status.DeviceDpi /
                UiStyle.BaselineDpi));
        if (status.ClientSize.Width <= 0 ||
            status.ClientSize.Height + heightRoundingTolerance < requiredHeight ||
            (shouldFitOneLine &&
             status.ClientSize.Width + 2 < requiredOneLine))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Selected Start lifetime caption is clipped " +
                $"(label={status.ClientSize.Width}x{status.ClientSize.Height}, " +
                $"fullWidth={requiredOneLine}, neededHeight={requiredHeight}, " +
                $"availableWidth={available}, maxWidth={status.MaximumSize.Width}).");
        }
    }

    private static void VerifyStartSourcePolicies(List<string> failures)
    {
        // Incomplete Microsoft 365 configuration must be rejected locally.
        // No sign-in callback, network request, disk write or Graph exception
        // is needed to validate these non-secret prerequisites.
        const string clientGuid = "11111111-1111-4111-8111-111111111111";
        // Privileged app creation requires a specific tenant, unlike
        // ordinary delegated Device Code where "organizations" can be used.
        var invalidAdminTenants = new string?[]
        {
            null, "", " ", "organizations", "common", "consumers",
            "00000000-0000-0000-0000-000000000000",
            "not-a-tenant", "https://contoso.onmicrosoft.com",
            "contoso.onmicrosoft.com/path", "contoso..com",
            "-bad.contoso.com", "contoso.com:443"
        };
        foreach (var tenant in invalidAdminTenants)
            if (EntraSetupService.ValidateTenantForAdministrativeSetup(
                    tenant) is null)
                failures.Add("Administrative Graph preflight accepted an ambiguous tenant.");

        foreach (var tenant in new[]
        {
            "contoso.onmicrosoft.com", "Contoso.COM",
            "11111111-1111-4111-8111-111111111111"
        })
            if (EntraSetupService.ValidateTenantForAdministrativeSetup(
                    tenant) is not null)
                failures.Add("Administrative Graph preflight rejected a valid tenant.");

        if (MainForm.CloudConnectionPrerequisiteError(null, clientGuid,
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError(" ", clientGuid,
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", "",
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", "not-a-guid",
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", Guid.Empty.ToString(),
                null, certificateMode: false) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", clientGuid,
                null, certificateMode: true) is null ||
            MainForm.CloudConnectionPrerequisiteError("organizations", clientGuid,
                null, certificateMode: false) is not null ||
            MainForm.CloudConnectionPrerequisiteError("contoso.onmicrosoft.com",
                clientGuid, "ABCD1234", certificateMode: true) is not null)
        {
            failures.Add("Cloud setup preflight must reject incomplete tenant/client/certificate before Graph sign-in.");
        }

        // A Helpdesk GUI must never auto-elevate, and disabling the CLI UAC
        // prompt must not allow privileged maintenance without admin rights.
        if (UpdateTrustPolicy.CanEnableUnattended(false, "Example Publisher") ||
            UpdateTrustPolicy.CanEnableUnattended(true, " ") ||
            !UpdateTrustPolicy.CanEnableUnattended(true, "Example Publisher"))
        {
            failures.Add("Unattended updater requires both trusted signature policy and publisher.");
        }

        if (StartupPrivilegePolicy.NeedsElevation(
                false, false, false, false, false) ||
            !StartupPrivilegePolicy.NeedsElevation(
                true, false, false, false, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, true, false, false, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, false, true, false, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, false, false, true, false) ||
            StartupPrivilegePolicy.NeedsElevation(
                true, false, false, false, true))
        {
            failures.Add("Least-privilege startup policy regression: GUI must not request UAC.");
        }

        if (OperatingSystem.IsWindows())
        {
            var metadataOnlyPolicy = new AppConfig
            {
                RbacEnabled = true,
                RbacAllowLocalAdministrators = false
            };
            var metadataAuthorization = new AuthorizationService(metadataOnlyPolicy);
            if (!metadataAuthorization.Check(BitKeyBridgePermission.SearchDevices).Allowed ||
                metadataAuthorization.Check(BitKeyBridgePermission.RecoveryRead).Allowed)
            {
                failures.Add("Metadata-only search must not grant secret RecoveryRead access.");
            }
        }

        // Simulate an unwritable audit destination; the audit service must
        // report failure, never claim that an access event was recorded.
        var invalidDirectory = System.IO.Path.GetTempFileName();
        try
        {
            var audit = new AuditService(
                System.IO.Path.Combine(invalidDirectory, "audit.jsonl"));
            if (audit.Write("AuditStorageRegression") is not null)
                failures.Add("Mandatory audit preflight must detect unwritable destinations.");
        }
        finally
        {
            try { File.Delete(invalidDirectory); }
            catch (IOException) { /* Only temporary test cleanup. */ }
            catch (UnauthorizedAccessException) { /* Only temporary test cleanup. */ }
        }
        if (StartConnectionPolicy.Normalize("unknown") != StartConnectionPolicy.Choose ||
            StartConnectionPolicy.NeedsActiveDirectory(StartConnectionPolicy.MicrosoftCloud) ||
            StartConnectionPolicy.NeedsMicrosoftCloud(StartConnectionPolicy.ActiveDirectory) ||
            !StartConnectionPolicy.NeedsActiveDirectory(StartConnectionPolicy.Both) ||
            !StartConnectionPolicy.NeedsMicrosoftCloud(StartConnectionPolicy.Both))
        {
            failures.Add("Start connection source must isolate AD from cloud-only searches.");
        }

        // Source persistence is per-user and opt-in. Existing saved
        // preferences from earlier builds must still show the chooser.
        failures.AddRange(StartConnectionPolicy.RunOfflineTests());

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.ActiveDirectory))
        {
            if (wizard.Controls.Find("StartupRememberLastConnection", true)
                is not [CheckBox { Checked: false, Enabled: true }])
            {
                failures.Add("Remembered startup connection must be unchecked by default.");
            }
        }

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.Both, rememberLastConnection: true))
        {
            if (!wizard.RememberLastConnectionOnStart ||
                wizard.Controls.Find("StartupRememberLastConnection", true)
                    is not [CheckBox { Checked: true, Enabled: true }])
            {
                failures.Add("Remember-last startup selection was not restored in the wizard.");
            }
        }

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.Choose, rememberLastConnection: true))
        {
            if (wizard.RememberLastConnectionOnStart ||
                wizard.Controls.Find("StartupRememberLastConnection", true)
                    is not [CheckBox { Checked: false, Enabled: false }])
            {
                failures.Add("Offline Start must disable remembered automatic connection.");
            }
            if (wizard.Controls.Find("StartupConnectionConfirm", true)
                    is not [Button { Text: "Open Start" }] ||
                wizard.Controls.Find("StartupConnectionSkip", true)
                    is not [Button { Text: "Cancel" }])
            {
                failures.Add("Offline Start must be a separate explicit action from Cancel.");
            }
        }

        // Cloud setup is allowed without a pre-existing tenant/app registration:
        // it opens the existing administrator-only setup wizard. No token,
        // Graph consent or credential is ever created by changing radio modes.
        using (var cloud = new CloudConnectionWizardDialog(new CloudAuthConfig()))
        {
            var approved = cloud.Controls.Find("CloudWizardExistingApp", true)
                .OfType<RadioButton>().FirstOrDefault();
            var admin = cloud.Controls.Find("CloudWizardAdministratorSetup", true)
                .OfType<RadioButton>().FirstOrDefault();
            var tenant = cloud.Controls.Find("CloudWizardTenant", true)
                .OfType<TextBox>().FirstOrDefault();
            var client = cloud.Controls.Find("CloudWizardClient", true)
                .OfType<TextBox>().FirstOrDefault();
            if (approved is null || admin is null || tenant is null || client is null ||
                cloud.Controls.Find("CloudWizardPermissionsToggle", true)
                    is not [Button] ||
                cloud.Controls.Find("CloudWizardPermissions", true)
                    is not [Label])
            {
                failures.Add("Cloud wizard must retain all sources and expandable delegated-permissions guidance.");
            }
            else
            {
                admin.Checked = true;
                if (tenant.Enabled || client.Enabled || !cloud.RequestAdministrativeSetup)
                    failures.Add("Admin setup must not require existing tenant or client IDs.");
                approved.Checked = true;
                if (!tenant.Enabled || !client.Enabled || cloud.RequestAdministrativeSetup)
                    failures.Add("Existing-app sign-in must expose its tenant and client ID controls.");
            }
        }

        using (var wizard = new StartupConnectionWizardDialog(
            StartConnectionPolicy.ActiveDirectory,
            rememberLastConnection: true, automaticConnectionsAllowed: false))
        {
            if (wizard.RememberLastConnectionOnStart ||
                wizard.Controls.Find("StartupRememberLastConnection", true)
                    is not [CheckBox { Checked: false, Enabled: false }])
            {
                failures.Add("Machine configuration must disable automatic reconnection.");
            }
        }

        // Detect wrong passwords using error codes (not only English text).
        // Network outages and unrelated LDAP errors must not trigger
        // an automatic credential prompt.
        if (!MainForm.IsAdLogonFailure(
                new System.ComponentModel.Win32Exception(1326)) ||
            !MainForm.IsAdLogonFailure(
                new Exception("The user name or password is incorrect.")) ||
            MainForm.IsAdLogonFailure(
                new Exception("The domain controller is unreachable.")))
        {
            failures.Add(
                "AD sign-in retry must recognize logon failure without retrying on connection outages.");
        }

        if (!new StartConnectionPreferences().RecoveryCardsOnLeft)
        {
            failures.Add(
                "First run must default to BitLocker/LAPS recovery on the left.");
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

        if (!form.KeyPreview)
            failures.Add($"{formName}/{scenario.Name}: Ctrl+1..7 global keyboard tab navigation requires KeyPreview.");

        if (!form.KeyPreview)
            failures.Add(
                $"{formName}/{scenario.Name}: keyboard shortcuts require MainForm.KeyPreview.");

        var preferredOrder = new[]
        {
            "Start", "BitLocker", "LAPS", "Devices", "Helpdesk", "Diagnostics"
        };
        foreach (var requiredBackgroundUpdaterControl in new[]
                 {
                     "InstallBackgroundUpdateTaskButton",
                     "BackgroundUpdateTaskStatusButton",
                     "RemoveBackgroundUpdateTaskButton",
                     "BackgroundUpdateHour"
                 })
        {
            if (FindByName(form, requiredBackgroundUpdaterControl) is null)
                failures.Add(
                    $"{formName}/{scenario.Name}: scheduled updater is missing " +
                    requiredBackgroundUpdaterControl);
        }
        // Every responsive size/DPI scenario must expose the Update Center
        // without creating a task, clearing a rollback hold or installing.
        foreach (var name in new[]
                 {
                     "UpdateCenterV2", "UpdateCenterSummary",
                     "UpdateCenterTaskDetails", "UpdateCenterRefreshButton",
                     "UpdateCenterRetryCheckButton", "UpdateCenterPauseButton",
                     "UpdateCenterEnableButton", "UpdateCenterCompact"
                 })
        {
            if (FindByName(form, name) is null)
                failures.Add(
                    $"{formName}/{scenario.Name}: Update Center control missing: " + name);
        }
        if (tabs.TabPages.Count > 3 &&
            tabs.TabPages[3].Name != "DevicesWorkspace")
            failures.Add(
                $"{formName}/{scenario.Name}: invariant Devices workspace ID missing; localization must not break navigation.");
        if (tabs.TabPages.Count > 5 &&
            tabs.TabPages[5].Name != "DiagnosticsWorkspace")
            failures.Add(
                $"{formName}/{scenario.Name}: invariant Diagnostics workspace ID missing.");
        if (tabs.TabPages.Count > 6 &&
            tabs.TabPages[6].Name != "SettingsWorkspace")
            failures.Add(
                $"{formName}/{scenario.Name}: invariant admin Settings workspace ID missing.");

        var actualOrder = tabs.TabPages.Cast<TabPage>()
            .Select(page => page.Text).ToArray();
        if (actualOrder.Length < preferredOrder.Length ||
            !actualOrder.Take(preferredOrder.Length)
                .SequenceEqual(preferredOrder, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: unexpected main tab order: " +
                string.Join(" / ", actualOrder) +
                "; required Start / BitLocker / LAPS / Devices / Helpdesk / Diagnostics.");
        }
        if (actualOrder.Length >= 5)
        {
            foreach (var (tabIndex, queryName) in new[]
            {
                (0, "HomeUnifiedQuery"),
                (1, "BitLockerSearchQuery"),
                (2, "LapsLiveSearchQuery"),
                (3, "UnifiedDeviceSearchQuery")
            })
            {
                if (FindByName(tabs.TabPages[tabIndex], queryName) is not TextBox)
                    failures.Add(
                        $"{formName}/{scenario.Name}: Ctrl+F search input {queryName} is missing.");
            }
        }

        // The Helpdesk landing tab reuses Start rather than maintaining
        // a separate query, duplicate BitLocker/LAPS buttons or AD/Graph work.
        if (tabs.TabPages.Count > 4)
        {
            var helpdeskPage = tabs.TabPages[4];
            if (FindByName(helpdeskPage, "HelpdeskOpenDeviceWorkspaceButton") is not Button ||
                FindByName(helpdeskPage, "HelpdeskSelectedDeviceSummary") is not UiStatusLabel ||
                FindByName(helpdeskPage, "HelpdeskRecoveryStepsButton") is not Button ||
                FindByName(helpdeskPage, "HelpdeskDashboardStatus") is not UiStatusLabel ||
                FindByName(helpdeskPage, "HelpdeskQuickSearch") is not null ||
                helpdeskPage.Controls.OfType<FlowLayoutPanel>()
                    .SelectMany(panel => panel.Controls.OfType<TextBox>()).Any())
            {
                failures.Add($"{formName}/{scenario.Name}: Helpdesk must reuse Start search and display selected-device metadata.");
            }
        }

        if (tabs.TabPages.Count == 0 ||
            !string.Equals(
                tabs.TabPages[0].Text,
                "Start",
                StringComparison.OrdinalIgnoreCase) ||
            !ReferenceEquals(tabs.SelectedTab, tabs.TabPages[0]))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Start must be the first and selected workspace at launch.");
        }
        else
        {
            var startPage =
                tabs.TabPages[0];

            var requiredStartControls =
                new[]
                {
                    "HomeConnectionSourceChooser",
                    "HomeConnectionSource",
                    "HomeConnectionPrimaryActions",
                    "HomeConnectionOptionsToggle",
                    "HomeConnectionExtraActions",
                    "HomeConnectionDirectActions",
                    "HomeConnectionDirectHint",
                    "HomeReconnectWizardButton",
                    "HomeBitLockerPaneHost",
                    "HomeLapsPaneHost",
                    "HomeComputersPaneHost",
                    "HomeRecentQuickPanel",
                    "HomeConnectCloudButton",
                    "HomeConfigureCloudButton",
                    "HomeCloudConnectionStatus",
                    "HomeConnectionStatus",
                    "HomeAdAccountStatus",
                    "HomeConnectAdButton",
                    "HomeDisconnectAdButton",
                    "HomeConnectionToolsButton",
                    "HomeServiceCards",
                    "HomeBitLockerDiagnosticLink",
                    "HomeBitLockerCard",
                    "HomeLapsCard",
                    "HomeLapsHistoryArea",
                    "HomeLapsHistoryGrid",
                    "HomeLapsHistoryStatus",
                    "HomeLoadLapsHistoryButton",
                    "HomeLapsSelectedRecord",
                    "HomeRevealBitLockerButton",
                    "HomeCopyBitLockerButton",
                    "HomeRevealLapsButton",
                    "HomeCopyLapsButton",
                    "HomeSelectionSummary",
                    "SecretLifetimeStatus",
                    "StartSetupStatus",
                    "HomeUnifiedQuery",
                    "HomeUnifiedSearchResults",
                    "HomeRecentComputers",
                    "HomeUseWindowsIdentity",
                    "HomeUseOtherAccount",

                    "AdvancedConnectionGroup"
                };

            if (FindByName(startPage, "HomeServiceCards") is TableLayoutPanel cards)
            {
                if (FindByName(cards, "HomeBitLockerCard") is not UiRecoveryCard ||
                    FindByName(cards, "HomeLapsCard") is not UiRecoveryCard ||
                    FindByName(cards, "HomeComputersCard") is not UiRecoveryCard ||
                    cards.Controls.Count != 3)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start requires three equal draggable cards: BitLocker, LAPS and Computers.");
                }

                if (FindByName(startPage, "HomeRevealLapsButton") is Button { Enabled: true } ||
                    FindByName(startPage, "HomeCopyLapsButton") is Button { Enabled: true } ||
                    FindByName(startPage, "HomeLoadLapsHistoryButton") is Button { Enabled: true })
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start cannot reveal or copy LAPS without a selected computer and explicit action.");
                }
            }

            if (FindByName(startPage, "HomeConnectionToolsButton") is Button toolsButton)
            {
                var actions = toolsButton.ContextMenuStrip?.Items
                    .OfType<ToolStripMenuItem>()
                    .Select(item => item.Name)
                    .ToHashSet(StringComparer.Ordinal) ?? [];
                if (!actions.Contains("HomeEnvironmentCheckMenuItem") ||
                    !actions.Contains("HomeDeviceDiagnosticsMenuItem") ||
                    !actions.Contains("HomeAdvancedConnectionMenuItem"))
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: the consolidated Connection tools menu must keep Environment Check, Device Diagnostics and Advanced settings available.");
                }
            }

            if (FindByName(startPage, "HomeLapsHistoryGrid") is DataGridView historyGrid)
            {
                if (!historyGrid.ReadOnly ||
                    historyGrid.MultiSelect ||
                    historyGrid.ScrollBars != ScrollBars.Both ||
                    historyGrid.Columns.Count != 4 ||
                    historyGrid.Rows.Count != 0)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: LAPS history must be empty, non-secret, read-only and horizontally scrollable at startup.");
                }

                if (historyGrid.Columns["Type"] is null ||
                    historyGrid.Columns["Account"] is null ||
                    historyGrid.Columns["Updated"] is null ||
                    historyGrid.Columns["Status"] is null)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: LAPS history requires Type, Account, Updated and Status columns.");
                }
            }

            if (FindByName(startPage, "HomeBitLockerDiagnosticLink") is LinkLabel help &&
                (!help.Text.Contains("diagnostic", StringComparison.OrdinalIgnoreCase) ||
                 help.Enabled))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: inline BitLocker diagnostic commands link must start disabled until selection.");
            }

            if (FindByName(startPage, "HomeDeviceDetailsButton") is not null ||
                FindByName(startPage, "HomeRotateBitLockerButton") is not null ||
                FindByName(startPage, "HomeBitLockerDiagnosticButton") is not null)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start must not contain navigation-only device or BitLocker diagnostic buttons.");
            }

            foreach (var controlName in
                     requiredStartControls)
            {
                if (FindByName(
                        startPage,
                        controlName) is null)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start workspace is missing '{controlName}'.");
                }
            }

            if (FindByName(startPage, "HomeConnectionStatus") is UiStatusLabel connectionStatus &&
                FindByName(startPage, "HomeAdAccountStatus") is UiStatusLabel accountStatus &&
                (connectionStatus.Parent is not TableLayoutPanel connectionLayout ||
                 !ReferenceEquals(accountStatus.Parent, connectionLayout) ||
                 connectionLayout.GetRow(accountStatus) != connectionLayout.GetRow(connectionStatus) + 1 ||
                 accountStatus.Margin.Top < UiStyle.SectionGap))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start must have a full DPI-scaled section gap between Connected and Connected as.");
            }

            if (tabs.SelectedIndex != 0)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Start workspace is not selected by default.");
            }

            if (FindByName(
                    startPage,
                    "AdCredentialStatus") is UiStatusLabel credentialStatus &&
                credentialStatus.MaximumSize.Width < 300)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: hidden Advanced layout collapsed the AD credential status width to {credentialStatus.MaximumSize.Width}px.");
            }

            if (FindByName(startPage, "HomeSearchResultsArea")
                    is TableLayoutPanel resultsArea &&
                FindByName(startPage, "HomeServiceCards")
                    is TableLayoutPanel threeCards &&
                FindByName(startPage, "HomeUnifiedSearchResults")
                    is DataGridView resultsGrid &&
                FindByName(startPage, "HomeSelectionPanel")
                    is TableLayoutPanel selectionPanel &&
                FindByName(startPage, "HomeComputersCard")
                    is UiRecoveryCard computersCard &&
                FindByName(startPage, "HomeBitLockerPaneHost")
                    is TableLayoutPanel bitLockerHost &&
                FindByName(startPage, "HomeLapsPaneHost")
                    is TableLayoutPanel lapsHost &&
                FindByName(startPage, "HomeComputersPaneHost")
                    is TableLayoutPanel computersHost)
            {
                if (!ReferenceEquals(threeCards.Parent, resultsArea) ||
                    !ReferenceEquals(selectionPanel.Parent, resultsArea) ||
                    !ReferenceEquals(resultsGrid.Parent, computersCard) ||
                    !ReferenceEquals(bitLockerHost.Parent, threeCards) ||
                    !ReferenceEquals(lapsHost.Parent, threeCards) ||
                    !ReferenceEquals(computersHost.Parent, threeCards) ||
                    threeCards.Controls.Count != 3)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: Start must show three equal draggable BitLocker, LAPS and Computers panes, even before selection.");
                }

                if (selectionPanel.Visible)
                {
                    failures.Add(
                        $"{formName}/{scenario.Name}: secret/selection status must stay hidden until a computer is selected.");
                }
            }
            else
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: three-pane Start recovery/results container is missing.");
            }

        }

        // Administrator-only LAPS Coverage remains discoverable with the
        // same central UiStyle across compact, 150% and 200% scaling.
        if (FindByName(form, "LapsCoverageScanButton") is not Button ||
            FindByName(form, "LapsCoverageDevices") is not DataGridView ||
            FindByName(form, "LapsCoverageChanges") is not DataGridView ||
            FindByName(form, "LapsCoverageCompareSnapshotsButton") is not Button ||
            FindByName(form, "LapsCoverageRequestRotation") is not Button lapsRotate ||
            FindByName(form, "LapsRotationOptIn") is not CheckBox lapsOptIn)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS Coverage and optional rotation controls are missing.");
        }
        else if (lapsRotate.Enabled || lapsOptIn.Checked)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS expiry writes must remain disabled by default.");
        }

        if (FindByName(
                form,
                "AutomaticUpdateInstall") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Updates workspace is missing the automatic-install control.");
        }

        if (FindByName(
                form,
                "UpdateNowButton") is null ||
            FindByName(
                form,
                "RequireTrustedUpdateSignature") is null ||
            FindByName(
                form,
                "TrustedUpdatePublisher") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Updates workspace is missing retry or Authenticode policy controls.");
        }

        // Compact Helpdesk settings still expose the actual policy state
        // and always retain discoverable controls for profiles and audit.
        if (FindByName(form, "HelpdeskProfilesToggle") is not Button ||
            FindByName(form, "HelpdeskProfilesGrid") is not TableLayoutPanel ||
            FindByName(form, "HelpdeskRecoveryPolicyToggle") is not Button ||
            FindByName(form, "HelpdeskRecoveryPolicyGrid") is not TableLayoutPanel ||
            FindByName(form, "HelpdeskRecoveryPolicyOverview")
                is not UiStatusLabel overview ||
            !overview.Text.Contains("Saved recovery policy:",
                StringComparison.Ordinal) ||
            !overview.Text.Contains("does not grant secret access",
                StringComparison.Ordinal))
        {
            failures.Add(
                $"{formName}/{scenario.Name}: compact Settings must display saved audit policy and retain both expandable sections.");
        }

        // Verify the collapsed policy can be opened reliably even when its
        // nested Settings TabPage started life at 200x100 (or was hidden
        // during a DPI/RDP switch). Avoid Control.Visible as a state flag:
        // WinForms combines it with visibility of all ancestors.
        if (form is MainForm settingsMain &&
            FindByName(form, "HelpdeskRecoveryPolicyToggle") is Button policyToggle &&
            FindByName(form, "HelpdeskRecoveryPolicyGrid") is TableLayoutPanel policyGrid &&
            tabs.TabPages.Cast<TabPage>().FirstOrDefault(
                page => page.Name == "SettingsWorkspace") is TabPage settingsTab &&
            settingsTab.Controls.OfType<TabControl>().FirstOrDefault() is TabControl settingsSections &&
            settingsSections.TabPages.Cast<TabPage>().FirstOrDefault(
                page => page.Name == "HelpdeskBasicsTab") is TabPage helpdeskSettings)
        {
            var previous = tabs.SelectedTab;
            var previousSection = settingsSections.SelectedTab;
            try
            {
                tabs.SelectedTab = settingsTab;
                settingsSections.SelectedTab = helpdeskSettings;
                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(true);
                settingsMain.ReflowMainViewport();
                if (policyToggle.Text != "Hide recovery access policy")
                    failures.Add($"{formName}/{scenario.Name}: expanding recovery policy did not update its controls.");

                // Calling Setup & Permissions twice is idempotent rather
                // than accidentally hiding the policy on its second visit.
                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(true);
                if (policyToggle.Text != "Hide recovery access policy")
                    failures.Add($"{formName}/{scenario.Name}: repeated Settings navigation collapsed an open policy.");

                if (policyGrid.Parent is FlowLayoutPanel viewport &&
                    viewport.ClientSize.Width > 600 &&
                    (policyGrid.Width < viewport.ClientSize.Width / 2 ||
                     policyGrid.Right > viewport.ClientSize.Width +
                         SystemInformation.VerticalScrollBarWidth))
                {
                    failures.Add($"{formName}/{scenario.Name}: expanded policy was squeezed or clipped by its viewport.");
                }

                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(false);
                if (policyToggle.Text != "Show recovery access policy...")
                    failures.Add($"{formName}/{scenario.Name}: Settings policy could not be collapsed.");
            }
            finally
            {
                settingsMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(false);
                settingsSections.SelectedTab = previousSection;
                tabs.SelectedTab = previous;
            }
        }

        if (FindByName(form, "ResetVisualLayoutButton") is not Button)
            failures.Add($"{formName}/{scenario.Name}: visual-only layout reset action is missing.");

        if (form is MainForm visualMain)
        {
            // Disturb only visual preferences, then verify that a reset
            // restores defaults without editing source authorization or
            // saved recovery/audit policies.
            var securityBefore =
                visualMain.PreviewNonVisualSettingsForUiSelfTest();
            visualMain.ExchangeHomePanesForUiSelfTest("BitLocker", "Computers");
            var compact = FindByName(form, "UpdateCenterCompact") as CheckBox;
            if (compact is not null)
                compact.Checked = false;
            visualMain.PreviewSetConnectionOptionsExpandedForUiTest(true);
            visualMain.PreviewSetHelpdeskProfilesExpandedForUiTest(true);
            visualMain.PreviewSetHelpdeskRecoveryPolicyExpandedForUiTest(true);
            visualMain.PreviewResetVisualLayoutForUiSelfTest();
            if (visualMain.PreviewHomePaneOrderForUiSelfTest() !=
                "BitLocker,Laps,Computers")
                failures.Add($"{formName}/{scenario.Name}: reset did not restore the default three-pane order.");
            if (compact is not { Checked: true })
                failures.Add($"{formName}/{scenario.Name}: reset did not restore compact Updates.");
            if (FindByName(form, "HomeConnectionOptionsToggle") is not Button
                { Text: "More connection options..." } ||
                FindByName(form, "HelpdeskProfilesToggle") is not Button
                { Text: "Show environment profiles..." } ||
                FindByName(form, "HelpdeskRecoveryPolicyToggle") is not Button
                { Text: "Show recovery access policy..." })
            {
                failures.Add($"{formName}/{scenario.Name}: visual reset did not collapse optional Connection and Settings sections.");
            }
            if (securityBefore !=
                visualMain.PreviewNonVisualSettingsForUiSelfTest())
                failures.Add($"{formName}/{scenario.Name}: visual reset changed authentication source or recovery policy.");
            // No second swap: that would silently undo the visual reset.
        }

        if (FindByName(
                form,
                "HelpdeskBasicsTab") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Administration is missing the Helpdesk basics workspace.");
        }

        if (FindByName(form, "EnterpriseSettingsTab") is null ||
            FindByName(form, "EnterpriseSettingsSections") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: advanced enterprise features must remain accessible from Settings.");
        }

        if (!tabs.TabPages.Cast<TabPage>().Any(page => page.Text == "Diagnostics") ||
            FindByName(form, "AccessHealthTab") is null ||
            FindByName(form, "DiagnosticToolsTab") is null ||
            FindByName(form, "RunEnvironmentCheckButton") is null ||
            FindByName(form, "RunAdvancedDeviceDiagnosticsButton") is null ||
            FindByName(form, "RunBitLockerCommandsButton") is null ||
            FindByName(form, "RunLapsCommandsButton") is null ||
            FindByName(form, "RunUiPerformanceDiagnosticsButton") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Diagnostics must provide Access Health and the existing recovery runbooks.");
        }

        if (FindByName(
                form,
                "UpdateHistory") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Updates workspace is missing update history.");
        }

        if (FindByName(
                form,
                "GlobalStatusBar") is not StatusStrip globalStatusBar)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: global connection status bar was not found.");
        }
        else if (globalStatusBar.Items["GlobalAdAccountStatus"] is not ToolStripStatusLabel)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: global Active Directory account indicator was not found.");
        }

        if (FindByName(
                form,
                "RecoveryBackToStart") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Recovery Back to Start button was not found.");
        }

        if (FindByName(
                form,
                "LapsBackToStart") is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS Back to Start button was not found.");
        }

        var lapsWarningPanel =
            FindByName(form, "LapsZeroRecordWarningPanel") as TableLayoutPanel;
        var lapsWarning =
            FindByName(form, "LapsZeroRecordWarning") as UiStatusLabel;
        var lapsCommands =
            FindByName(form, "LapsDiagnosticCommandsButton") as Button;
        var lapsKeyGrid =
            FindByName(form, "LapsKeyResults") as DataGridView;

        if (lapsWarningPanel is null ||
            lapsWarning is null ||
            lapsCommands is null ||
            lapsKeyGrid is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: LAPS warning, key grid or diagnostic commands button is missing.");
        }
        else
        {
            if (lapsWarningPanel.Visible)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: LAPS warning is visible before a failed read.");
            }

            if (lapsWarning.StatusKind != UiStatusKind.Warning)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: the LAPS warning is not using the shared warning palette.");
            }

            if (lapsWarningPanel.Parent is not TableLayoutPanel workspace ||
                workspace.GetRow(lapsWarningPanel) >= workspace.GetRow(lapsKeyGrid))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: LAPS warning must be positioned above the key results grid.");
            }
        }

        var bitLockerWarningPanel =
            FindByName(form, "BitLockerZeroRecordWarningPanel") as TableLayoutPanel;
        var bitLockerWarning =
            FindByName(form, "BitLockerZeroRecordWarning") as UiStatusLabel;
        var bitLockerCommands =
            FindByName(form, "BitLockerDiagnosticCommandsButton") as Button;
        var bitLockerResults =
            FindByName(form, "BitLockerSearchResults") as DataGridView;

        if (bitLockerWarningPanel is null ||
            bitLockerWarning is null ||
            bitLockerCommands is null ||
            bitLockerResults is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: BitLocker warning/results/diagnostic controls missing.");
        }
        else
        {
            if (bitLockerWarningPanel.Visible)
                failures.Add(
                    $"{formName}/{scenario.Name}: BitLocker warning must start hidden.");

            if (bitLockerWarning.StatusKind != UiStatusKind.Warning)
                failures.Add(
                    $"{formName}/{scenario.Name}: BitLocker warning bypasses shared warning palette.");

            if (bitLockerWarningPanel.Parent is not TableLayoutPanel workspace ||
                workspace.GetRow(bitLockerWarningPanel) >= workspace.GetRow(bitLockerResults))
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: BitLocker warning must be above the results grid.");
            }
        }

        VerifyTabControlPages(
            tabs,
            formName,
            scenario,
            failures,
            form,
            checkRecoveryConnection: true);

        VerifyComputerQueryNavigation(
            (MainForm)form,
            tabs,
            formName,
            scenario,
            failures);
    }

    private static void VerifyComputerQueryNavigation(
        MainForm main,
        TabControl tabs,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var start = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "StartWorkspace");
        var recovery = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "RecoveryWorkspace");
        var laps = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "LapsWorkspace");
        var helpdesk = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Name == "HelpdeskDashboardTab");

        var homeQuery = start is null
            ? null : FindByName(start, "HomeUnifiedQuery") as TextBox;
        var recoveryQuery = recovery is null
            ? null : FindByName(recovery, "BitLockerSearchQuery") as TextBox;
        var lapsQuery = laps is null
            ? null : FindByName(laps, "LapsLiveSearchQuery") as TextBox;

        if (start is null || recovery is null || laps is null || helpdesk is null ||
            homeQuery is null || recoveryQuery is null || lapsQuery is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: computer query navigation controls are missing.");
            return;
        }

        var initialTab = tabs.SelectedTab;
        try
        {
            tabs.SelectedTab = start;
            homeQuery.Text = "PC-NAV-001";
            tabs.SelectedTab = recovery;
            if (recoveryQuery.Text != "PC-NAV-001")
                failures.Add($"{formName}/{scenario.Name}: Start to BitLocker computer was lost.");

            tabs.SelectedTab = laps;
            if (lapsQuery.Text != "PC-NAV-001")
                failures.Add($"{formName}/{scenario.Name}: BitLocker to LAPS computer was lost.");

            lapsQuery.Text = "PC-NAV-002";
            tabs.SelectedTab = recovery;
            if (recoveryQuery.Text != "PC-NAV-002")
                failures.Add($"{formName}/{scenario.Name}: LAPS to BitLocker computer was lost.");

            tabs.SelectedTab = start;
            if (homeQuery.Text != "PC-NAV-002")
                failures.Add($"{formName}/{scenario.Name}: LAPS to Start computer was lost.");

            homeQuery.Clear();
            tabs.SelectedTab = laps;
            if (lapsQuery.Text.Length != 0)
                failures.Add($"{formName}/{scenario.Name}: clearing computer must clear stale tab queries.");

            tabs.SelectedTab = start;
            homeQuery.Text = "PC";
            var results =
                FindByName(start, "HomeUnifiedSearchResults") as DataGridView;
            if (results is not null)
            {
                var index = results.Rows.Add(
                    "PC-SELECTED", "AD", "Available", DBNull.Value, "Detected", DBNull.Value);
                results.Rows[index].Tag = new HomeSearchResult
                {
                    ComputerName = "PC-SELECTED",
                    BitLockerKeyCount = 1,
                    LapsCandidateFound = true,
                    LapsStatus = "Detected"
                };
                results.ClearSelection();
                results.Rows[index].Selected = true;
                results.CurrentCell = results.Rows[index].Cells[0];
                tabs.SelectedTab = helpdesk;
                var selectedSummary = FindByName(
                    helpdesk, "HelpdeskSelectedDeviceSummary") as UiStatusLabel;
                if (selectedSummary is null ||
                    !selectedSummary.Text.Contains("PC-SELECTED",
                        StringComparison.Ordinal))
                {
                    failures.Add($"{formName}/{scenario.Name}: Helpdesk must show the exact selected Start metadata without a second search.");
                }

                main.PreviewHelpdeskDeviceWorkspaceForUiSelfTest();
                if (!ReferenceEquals(tabs.SelectedTab, start) ||
                    results.SelectedRows.Count != 1 ||
                    results.SelectedRows[0].Tag is not HomeSearchResult selected ||
                    selected.ComputerName != "PC-SELECTED")
                {
                    failures.Add($"{formName}/{scenario.Name}: returning from Helpdesk lost the source-qualified Start selection.");
                }

                // The same selected row can become ambiguous when the
                // metadata result includes another AD object with this name.
                // Helpdesk must never imply it can authorize recovery by name.
                if (results.Rows[index].Tag is HomeSearchResult tested)
                {
                    tested.IdentityAmbiguous = true;
                    tabs.SelectedTab = helpdesk;
                    if (selectedSummary is null ||
                        selectedSummary.StatusKind != UiStatusKind.Warning ||
                        !selectedSummary.Text.Contains("ambiguous",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"{formName}/{scenario.Name}: ambiguous AD device must be clearly unverified in Helpdesk.");
                    }

                    // Likewise, a cloud result lacking its source-qualified
                    // Entra identity must not be displayed as verified.
                    tested.IdentityAmbiguous = false;
                    tested.IsCloudRecord = true;
                    tested.SourceIdentity = string.Empty;
                    tabs.SelectedTab = start;
                    tabs.SelectedTab = helpdesk;
                    if (selectedSummary is null ||
                        selectedSummary.StatusKind != UiStatusKind.Warning ||
                        !selectedSummary.Text.Contains("unverified",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"{formName}/{scenario.Name}: cloud device without an exact identity must remain unverified in Helpdesk.");
                    }

                    // Restore normal AD metadata before the existing
                    // BitLocker/LAPS query navigation regression.
                    tested.IsCloudRecord = false;
                    tabs.SelectedTab = start;
                }

                tabs.SelectedTab = recovery;
                if (recoveryQuery.Text != "PC-SELECTED")
                    failures.Add($"{formName}/{scenario.Name}: selected Start computer was not retained.");

                // A *new* query must invalidate the selected old device
                // immediately, even before the debounce or a network call.
                tabs.SelectedTab = start;
                homeQuery.Text = "PC-NEW-DEVICE";
                if (results.Rows.Count != 0 ||
                    (selectedSummary is not null &&
                     selectedSummary.Text.Contains("PC-SELECTED",
                         StringComparison.Ordinal)))
                {
                    failures.Add($"{formName}/{scenario.Name}: editing Start search retained stale device metadata or Helpdesk selection.");
                }
            }
        }
        finally
        {
            // The synthetic result was added only to check navigation.
            // Clear its selection before the next assertion checks that
            // Recovery Steps starts disabled without a selected computer.
            // Otherwise the test itself leaves a false-positive enabled
            // action behind, even though production navigation is correct.
            var testGrid = FindByName(start, "HomeUnifiedSearchResults") as DataGridView;
            testGrid?.Rows.Clear();
            homeQuery.Clear();
            tabs.SelectedTab = initialTab;
        }
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
                        "Start",
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
                    string.Equals(page.Text, "LAPS", StringComparison.OrdinalIgnoreCase))
                {
                    VerifyLapsCompactFooter(
                        page,
                        pagePath,
                        scenario,
                        failures);
                }

                if (checkRecoveryConnection &&
                    string.Equals(
                        page.Text,
                        "BitLocker",
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

    private static void VerifyLapsCompactFooter(
        TabPage page,
        string path,
        Scenario scenario,
        List<string> failures)
    {
        var layout =
            FindByName(page, "LapsWorkspaceLayout") as TableLayoutPanel;
        var viewport =
            FindByName(page, "LapsScrollViewport") as Panel;
        var scroll =
            FindByName(page, "LapsScrollableContent") as TableLayoutPanel;
        var footer =
            FindByName(page, "LapsPasswordPanel") as TableLayoutPanel;
        var password =
            FindByName(page, "LapsPasswordValue") as TextBox;
        var reveal =
            FindByName(page, "LapsRevealPasswordButton") as Button;
        var copy =
            FindByName(page, "LapsCopyPasswordButton") as Button;
        var summary =
            FindByName(page, "LapsSelectedRecordSummary") as UiStatusLabel;
        var status =
            FindByName(page, "LapsOperationStatus") as UiStatusLabel;
        var grid =
            FindByName(page, "LapsKeyResults") as DataGridView;
        var progress =
            FindByName(page, "LapsProgress") as ProgressBar;
        var zeroWarning =
            FindByName(page, "LapsZeroRecordWarning") as UiStatusLabel;
        var zeroWarningPanel =
            FindByName(page, "LapsZeroRecordWarningPanel") as TableLayoutPanel;
        var searchGrid =
            FindByName(page, "LapsSearchResults") as DataGridView;
        var actions =
            FindByName(page, "LapsReadActions") as FlowLayoutPanel;
        var detailsPanel =
            FindByName(page, "LapsDetailsPanel") as TableLayoutPanel;
        var details =
            FindByName(page, "LapsRecordDetails") as RichTextBox;

        if (layout is null || viewport is null || scroll is null || footer is null ||
            password is null || reveal is null || copy is null ||
            summary is null || status is null || grid is null ||
            progress is null || zeroWarning is null || zeroWarningPanel is null ||
            searchGrid is null || actions is null || detailsPanel is null ||
            details is null)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS compact password footer or record grid is missing.");
            return;
        }

        if (!ReferenceEquals(viewport.Parent, layout) ||
            !ReferenceEquals(scroll.Parent, viewport) ||
            !ReferenceEquals(footer.Parent, layout) ||
            layout.GetRow(viewport) != 0 ||
            layout.GetRow(footer) != 1 ||
            layout.RowStyles[0].SizeType != SizeType.Percent ||
            layout.RowStyles[1].SizeType != SizeType.AutoSize ||
            !viewport.AutoScroll ||
            scroll.AutoScroll ||
            !scroll.AutoSize ||
            scroll.AutoSizeMode != AutoSizeMode.GrowAndShrink ||
            scroll.Dock != DockStyle.Top)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS content must use an outer scroll viewport, an AutoSize content table and a separately pinned password footer.");
        }

        // Regression for the actual LAPS decryption screenshot: a percentage
        // row inside an AutoScroll TableLayoutPanel allowed selected-record
        // details to overprint the warning and action row. Every child now
        // consumes its own measured AutoSize row, while both grids scroll
        // internally within an explicit, non-collapsing height.
        if (scroll.RowStyles.Count != 8 ||
            scroll.RowStyles.Cast<RowStyle>().Any(
                row => row.SizeType != SizeType.AutoSize) ||
            !ReferenceEquals(actions.Parent, scroll) ||
            !ReferenceEquals(zeroWarningPanel.Parent, scroll) ||
            !ReferenceEquals(grid.Parent, scroll) ||
            !ReferenceEquals(detailsPanel.Parent, scroll) ||
            scroll.GetRow(actions) != 3 ||
            scroll.GetRow(zeroWarningPanel) != 4 ||
            scroll.GetRow(grid) != 5 ||
            scroll.GetRow(detailsPanel) != 6 ||
            grid.Dock != DockStyle.Top ||
            grid.Height < grid.MinimumSize.Height ||
            searchGrid.Dock != DockStyle.Top ||
            searchGrid.Height < searchGrid.MinimumSize.Height ||
            details.Height < details.MinimumSize.Height ||
            !actions.WrapContents)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS warning, grids, actions and details must occupy non-overlapping AutoSize rows, with wrapping actions and bounded grid heights.");
        }

        if (!ReferenceEquals(password.Parent, footer) ||
            !ReferenceEquals(status.Parent, footer) ||
            !ReferenceEquals(summary.Parent, footer) ||
            !ReferenceEquals(reveal.Parent, copy.Parent) ||
            !ReferenceEquals(reveal.Parent?.Parent, footer) ||
            footer.GetRow(password) != 3 ||
            footer.GetRow(status) != 1 ||
            footer.GetRow(summary) != 2)
        {
            failures.Add(
                $"{path}/{scenario.Name}: selected password, record summary and buttons must be inside the fixed footer.");
        }

        if (!ReferenceEquals(progress.Parent, footer) ||
            footer.GetRow(progress) != 0 ||
            footer.GetColumnSpan(progress) != 2 ||
            progress.Dock != DockStyle.Fill ||
            progress.Style != ProgressBarStyle.Marquee)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS progress must be a full-width bar in the pinned footer, never a clipped fragment inside the scrollable results grid.");
        }

        if (!status.AutoSize ||
            !summary.AutoSize ||
            !zeroWarning.AutoSize ||
            status.AutoEllipsis ||
            summary.AutoEllipsis ||
            zeroWarning.AutoEllipsis ||
            status.MaximumSize.Width <= 0 ||
            summary.MaximumSize.Width <= 0 ||
            zeroWarning.MaximumSize.Width <= 0)
        {
            failures.Add(
                $"{path}/{scenario.Name}: LAPS operation, record summary and zero-record warnings must use width-constrained multiline status labels.");
        }

        if (!password.UseSystemPasswordChar ||
            !password.ReadOnly ||
            password.AccessibleName != "Selected LAPS password")
        {
            failures.Add(
                $"{path}/{scenario.Name}: password field must start masked and accessible.");
        }

        // WinForms Visible includes ancestor visibility. A headless test
        // form is not shown, so even correctly laid out children can have
        // Visible=false. Test the actual geometry and parent structure.
        if (footer.Height <= 0 ||
            footer.Bottom > layout.ClientSize.Height + 3 ||
            viewport.Bottom > footer.Top + 3)
        {
            failures.Add(
                $"{path}/{scenario.Name}: pinned LAPS footer is outside the visible tab viewport " +
                $"(Footer: Y:{footer.Top}, Height:{footer.Height}, Bottom:{footer.Bottom}; " +
                $"Layout: Height={layout.ClientSize.Height}, Scroll: Height={scroll.Height}).");
        }

        foreach (var name in new[] { "Account", "Expires", "Status" })
        {
            if (grid.Columns[name] is not DataGridViewColumn column ||
                column.MinimumWidth < 130)
            {
                failures.Add(
                    $"{path}/{scenario.Name}: LAPS column '{name}' has insufficient readable width.");
            }
        }

        if (grid.ScrollBars != ScrollBars.Both)
        {
            failures.Add(
                $"{path}/{scenario.Name}: narrow LAPS result grids must allow horizontal scrolling.");
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

    private static void VerifyStatusGlyphAlignmentInset(
        List<string> failures)
    {
        using var bordered = new UiStatusLabel();
        UiStyle.ConfigureStatusLabel(bordered);
        UiStyle.SetStatus(bordered, "Connected", UiStatusKind.Success);
        var baseWidth = bordered.GetPreferredSize(Size.Empty).Width;
        var baseX = bordered.GlyphLeftInset;
        bordered.GlyphColumnInset = UiStyle.ControlGap * 2;
        var alignedWidth = bordered.GetPreferredSize(Size.Empty).Width;

        using var inline = new UiStatusLabel();
        UiStyle.ConfigureInlineStatusLabel(inline);
        UiStyle.SetStatus(inline, "Connected as: helpdesk", UiStatusKind.Success);

        if (bordered.GlyphLeftInset - baseX != UiStyle.ControlGap * 2 ||
            alignedWidth < baseWidth + UiStyle.ControlGap * 2 - 1 ||
            inline.GlyphBaseLeftInset >= bordered.GlyphBaseLeftInset)
        {
            failures.Add(
                "Status glyph column offset must change both paint position and measured width, including inline Connected as labels.");
        }
    }

    private static void VerifyHomeStatusGlyphColumn(
        MainForm main,
        string formName,
        Scenario scenario,
        List<string> failures)
    {
        var page = FindByName(main, "HomeLayoutRoot");
        if (page is null)
            return;

        var names = new[]
        {
            "HomeConnectionStatus", "HomeAdAccountStatus",
            "HomeCloudConnectionStatus", "StartSetupStatus",
            "HomeUnifiedSearchStatus", "HomeSelectionSummary",
            "SecretLifetimeStatus"
        };
        var visible = names
            .Select(name => FindByName(main, name) as UiStatusLabel)
            .Where(label => label is { Visible: true, Width: > 0, Image: not null })
            .Select(label => new
            {
                Label = label!,
                Painted = MainForm.HomeGlyphPaintedX(page, label!)
            })
            .Where(item => item.Painted.HasValue)
            .ToArray();

        var tolerance = (int)Math.Ceiling(
            UiStyle.HomeStatusGlyphGroupingDistance *
            Math.Max(main.DeviceDpi / (double)UiStyle.BaselineDpi,
                main.Font.Size / Math.Max(1F, UiStyle.BodyFont.Size)));

        foreach (var item in visible)
        {
            var label = item.Label;
            if (label.Parent is not FlowLayoutPanel || !label.AutoSize ||
                label.Dock != DockStyle.None)
                continue;

            // Check the real bounds after glyph alignment, not merely the
            // preferred width: the old flow row kept a narrower stored size.
            var required = label.GetPreferredSize(new Size(label.Width, 0));
            if (label.Height < required.Height)
                failures.Add($"{formName}/{scenario.Name}: Start inline status clips its caption after glyph alignment (height={label.Height}, required={required.Height}).");
        }

        var sorted = visible.OrderBy(item =>
            item.Painted!.Value - item.Label.GlyphColumnInset).ToArray();
        for (var first = 0; first < sorted.Length;)
        {
            var start = sorted[first].Painted!.Value -
                sorted[first].Label.GlyphColumnInset;
            var end = first + 1;
            while (end < sorted.Length &&
                   sorted[end].Painted!.Value -
                   sorted[end].Label.GlyphColumnInset - start <= tolerance)
                end++;
            for (var i = first + 1; i < end; i++)
            {
                if (Math.Abs(sorted[i].Painted!.Value -
                             sorted[first].Painted!.Value) > 1)
                    failures.Add($"{formName}/{scenario.Name}: Start status icons are not in a vertical column.");
            }
            first = end;
        }
    }

    private static void VerifyWrappingStatusMeasurement(
        List<string> failures)
    {
        using var label = new UiStatusLabel();
        UiStyle.ConfigureWrappingStatusLabel(label);
        label.Text = string.Concat(
            Enumerable.Repeat(
                "The current LAPS expiration may be overdue; check the password history and authorization. ",
                7));

        label.MaximumSize = new Size(280, 0);
        var wrapped = label.GetPreferredSize(Size.Empty);

        label.MaximumSize = new Size(2200, 0);
        var wide = label.GetPreferredSize(Size.Empty);

        if (wrapped.Width > 280 ||
            wrapped.Height <= wide.Height)
        {
            failures.Add(
                "UiStatusLabel must honor MaximumSize.Width when the parent requests an unconstrained preferred size, so long LAPS warnings wrap and grow vertically.");
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
        var openStart =
            FindByName(
                root,
                "RecoveryOpenStartButton");

        if (source is null ||
            openStart is null)
        {
            failures.Add(
                $"{formName}/{scenario.Name}: Recovery source / Start connection shortcut was not found.");
            return;
        }

        var forbidden =
            new[]
            {
                "ConnectAdButton",
                "ConnectAdCancelButton",
                "DisconnectAdButton",
                "AdvancedConnectionGroup"
            };

        foreach (var controlName in
                 forbidden)
        {
            if (FindByName(
                    root,
                    controlName) is not null)
            {
                failures.Add(
                    $"{formName}/{scenario.Name}: Recovery must not own '{controlName}'; AD connection is managed on Start.");
            }
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
