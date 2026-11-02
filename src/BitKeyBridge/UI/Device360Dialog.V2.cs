using System.Text;

namespace BitKeyBridge;

internal sealed partial class Device360Dialog
{
    private Device360HardwareInventory? _device360RemoteHardware;
    private CancellationTokenSource? _device360HardwareCancellation;

    /// <summary>
    /// Reuse the original, fully localized source, recovery and policy
    /// controls. They are moved to tab pages BEFORE the first Show() call.
    /// No search, Microsoft 365 prompt, reveal or remote WMI is triggered.
    /// </summary>
    private void InitializeDevice360V2(
        Panel viewport, TableLayoutPanel overviewSections,
        UiRecoveryCard identity, UiRecoveryCard recovery,
        UiRecoveryCard posture, UiRecoveryCard access,
        Device360Snapshot snapshot, Device360OperationalStatus operational,
        string language)
    {
        string Tr(string key) => UiLanguageService.Translate(language, key);
        var rtl = UiLanguageService.IsRightToLeft(language);
        var observations = Device360HealthService.Evaluate(snapshot, operational);

        var tabs = new TabControl
        {
            Name = "Device360CardTabs",
            Dock = DockStyle.Fill,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
            RightToLeftLayout = rtl,
            // Two native tab rows are preferable to hiding the last
            // RU/HE tab behind horizontal arrows at 610px and high DPI.
            Multiline = true
        };
        viewport.Controls.Remove(overviewSections);
        viewport.AutoScroll = false;
        viewport.Controls.Add(tabs);

        // All source tiles stay visible on the overview. Moving the existing
        // cards (not rebuilding their text) preserves EN/RU/HE, access checks
        // and exact metadata fields from both Start and Devices.
        overviewSections.Controls.Remove(identity);
        overviewSections.Controls.Remove(recovery);
        overviewSections.Controls.Remove(posture);
        overviewSections.Controls.Remove(access);
        var overview = NewPage("Device360TabOverview", Tr("Device360V2Overview"));
        overview.Container.Controls.Add(overviewSections);
        tabs.TabPages.Add(overview.Tab);

        var identityPage = NewPage("Device360TabIdentity",
            Tr("Device360V2Identity"));
        identityPage.Container.Controls.Add(Stack(identity,
            IdentitySourceMap(), IdentityEvidence()));

        var securityPage = NewPage("Device360TabSecurity",
            Tr("Device360V2Security"));
        securityPage.Container.Controls.Add(Stack(recovery, access));

        var hardwarePage = NewPage("Device360TabHardware",
            Tr("Device360V2Hardware"));
        var hardwareDetails = HardwareFields();
        hardwarePage.Container.Controls.Add(Stack(posture, hardwareDetails));
        // Keep page order consistent with the helpdesk workflow in all
        // languages. In Hebrew the native tab strip mirrors visually, but
        // these semantic pages retain the same logical order.
        tabs.TabPages.Add(hardwarePage.Tab);
        tabs.TabPages.Add(securityPage.Tab);
        tabs.TabPages.Add(identityPage.Tab);

        // Audit is a separate, strictly read-only POLICY overview. The
        // actual protected journal ACK is evaluated by the existing recovery
        // handlers; opening a card cannot assert an untested audit result.
        var auditPage = NewPage("Device360TabAudit",
            Tr("Device360V2Audit"));
        auditPage.Container.Controls.Add(Stack(AuditEvidence()));
        tabs.TabPages.Add(auditPage.Tab);

        var diagnosticsPage = NewPage("Device360TabDiagnostics",
            Tr("Device360V2Diagnostics"));
        var diagnostics = new TextBox
        {
            Name = "Device360V2SmartDiagnostics",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
            BackColor = UiStyle.ModernSurface,
            ForeColor = UiStyle.ModernForeground,
            Font = UiStyle.BodyFont,
            Text = Device360HealthUiFormatter.Summary(observations, language) +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine,
                    observations.Select(item =>
                        Device360HealthUiFormatter.Format(item, language)))
        };
        diagnosticsPage.Container.Controls.Add(diagnostics);
        tabs.TabPages.Add(diagnosticsPage.Tab);

        // Compact non-secret health summary visible immediately, without a
        // false green all-clear for missing sources or incomplete recovery.
        var summary = new UiRecoveryCard
        {
            Name = "Device360V2HealthOverview",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(UiStyle.RecoveryCardPadding),
            Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap)
        };
        var reportButtons = new FlowLayoutPanel
        {
            Name = "Device360V2ReportActions",
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = rtl ? FlowDirection.RightToLeft :
                FlowDirection.LeftToRight,
            Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
        };
        var copy = UiStyle.CreateActionButton(Tr("Device360V2CopyReport"));
        copy.Name = "Device360V2CopyReport";
        var save = UiStyle.CreateActionButton(Tr("Device360V2SaveReport"));
        save.Name = "Device360V2SaveReport";
        reportButtons.Controls.Add(copy);
        reportButtons.Controls.Add(save);
        var reportStatus = new Label
        {
            Name = "Device360V2ReportStatus",
            Text = string.Empty,
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = UiStyle.ModernMuted,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
        };
        var healthLabel = new Label
        {
            Name = "Device360V2HealthSummary",
            Text = Tr("Device360V2HealthTitle") + Environment.NewLine +
                Device360HealthUiFormatter.Summary(observations, language),
            Font = UiStyle.CreateEmphasisFont(11),
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = observations.Any(x =>
                    x.State == Device360CheckState.Attention)
                ? UiStyle.ModernMuted : UiStyle.ModernForeground,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        UiStyle.ConfigureWrappingLabel(healthLabel);
        // Dock order: last added docks first.
        summary.Controls.Add(reportStatus);
        summary.Controls.Add(reportButtons);
        summary.Controls.Add(healthLabel);
        overviewSections.Controls.Add(summary, 0, 2);

        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(Device360ReportService.Build(
                    snapshot, operational, _device360RemoteHardware));
                reportStatus.Text = Tr("Device360V2ReportCopied");
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException
                or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                reportStatus.Text = "Clipboard unavailable (" +
                    ex.GetType().Name + ").";
            }
        };

        save.Click += (_, _) =>
        {
            var safeComputer = new string((snapshot.Computer ?? "Device")
                .Where(ch => char.IsAsciiLetterOrDigit(ch) ||
                    ch is '-' or '_').Take(48).ToArray());
            if (string.IsNullOrWhiteSpace(safeComputer))
                safeComputer = "Device";
            using var dialog = new SaveFileDialog
            {
                Title = Tr("Device360V2SaveReport"),
                Filter = "Text report (*.txt)|*.txt",
                DefaultExt = "txt",
                AddExtension = true,
                FileName = "BitKeyBridge-Device360-" + safeComputer + ".txt",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                File.WriteAllText(dialog.FileName,
                    Device360ReportService.Build(snapshot, operational,
                        _device360RemoteHardware), new UTF8Encoding(false));
                reportStatus.Text = Tr("Device360V2ReportSaved");
            }
            catch (Exception ex) when (ex is IOException or
                                       UnauthorizedAccessException or
                                       System.Security.SecurityException)
            {
                reportStatus.Text = "Report save failed (" +
                    ex.GetType().Name + ").";
            }
        };

        // Only the operator's explicit click starts a network WMI operation.
        // No domain privileges are granted and the result remains in memory
        // for this single dialog. The existing source qualification is
        // checked again immediately before the query.
        var probe = UiStyle.CreateActionButton(Tr("Device360V2HardwareCheck"));
        probe.Name = "Device360V2HardwareProbe";
        probe.Dock = DockStyle.Top;
        probe.Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.ControlGap);
        probe.Enabled = Device360HardwareService.CanQuery(snapshot);
        probe.AccessibleDescription = Tr("Device360V2HardwareHint");
        var hardwareStatus = new Label
        {
            Name = "Device360V2HardwareStatus",
            Text = probe.Enabled
                ? Tr("Device360V2NotQueried")
                : Tr("Device360V2NoRemote"),
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = UiStyle.ModernMuted,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
        };
        UiStyle.ConfigureWrappingLabel(hardwareStatus);
        hardwareDetails.Controls.Add(hardwareStatus);
        hardwareDetails.Controls.Add(probe);

        probe.Click += async (_, _) =>
        {
            if (_device360HardwareCancellation is not null ||
                !Device360HardwareService.CanQuery(snapshot))
                return;
            using var cancellation = new CancellationTokenSource();
            _device360HardwareCancellation = cancellation;
            probe.Enabled = false;
            hardwareStatus.Text = Tr("Device360V2Querying");
            try
            {
                var result = await Device360HardwareService.QueryAsync(
                    snapshot, cancellation.Token);
                if (IsDisposed || Disposing || cancellation.IsCancellationRequested)
                    return;
                _device360RemoteHardware = result.Inventory;
                hardwareStatus.Text = result.Explanation;
                if (result.Inventory is { } inventory)
                {
                    SetHardware("Device360V2RemoteManufacturer",
                        inventory.Manufacturer);
                    SetHardware("Device360V2RemoteModel", inventory.Model);
                    if (FindIn(posture, "Device360Hardware") is TextBox original &&
                        (original.Text == Tr("Device360NotVerified") ||
                         string.IsNullOrWhiteSpace(snapshot.Hardware)))
                    {
                        var text = (inventory.Manufacturer + " " +
                            inventory.Model).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            original.Text = "WMI: " + text;
                    }
                    SetHardware("Device360V2BiosVersion", inventory.BiosVersion);
                    SetHardware("Device360V2BiosManufacturer",
                        inventory.BiosManufacturer);
                    SetHardware("Device360V2BiosReleaseDate",
                        inventory.BiosReleaseDate);
                    SetHardware("Device360V2TpmVersion",
                        inventory.TpmSpecification);
                    SetHardware("Device360V2TpmEnabled", inventory.TpmEnabled);
                    SetHardware("Device360V2TpmActivated", inventory.TpmActivated);
                    SetHardware("Device360V2TpmOwned", inventory.TpmOwned);
                    SetHardware("Device360V2TpmDetail", inventory.TpmDiagnostic);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (!IsDisposed && !Disposing)
                    hardwareStatus.Text = Tr("Device360V2HardwareCancelled");
            }
            finally
            {
                if (ReferenceEquals(_device360HardwareCancellation, cancellation))
                    _device360HardwareCancellation = null;
                if (!IsDisposed && !Disposing)
                    probe.Enabled = Device360HardwareService.CanQuery(snapshot);
            }
        };

        FormClosing += (_, _) => _device360HardwareCancellation?.Cancel();

        void SetHardware(string name, string? text)
        {
            if (FindIn(hardwareDetails, name) is TextBox field)
                field.Text = string.IsNullOrWhiteSpace(text)
                    ? Tr("Device360NotVerified") : text;
        }

        (TabPage Tab, Panel Container) NewPage(string name, string caption)
        {
            var page = new TabPage(caption)
            {
                Name = name,
                BackColor = UiStyle.ModernCanvas,
                Padding = new Padding(UiStyle.ControlGap)
            };
            var canvas = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiStyle.ModernCanvas
            };
            page.Controls.Add(canvas);
            return (page, canvas);
        }

        TableLayoutPanel Stack(params Control[] cards)
        {
            var column = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = cards.Length,
                Padding = new Padding(0, 0,
                    SystemInformation.VerticalScrollBarWidth, 4)
            };
            column.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < cards.Length; ++i)
            {
                column.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                column.Controls.Add(cards[i], 0, i);
            }
            return column;
        }

        UiRecoveryCard IdentitySourceMap()
        {
            var card = new UiRecoveryCard
            {
                Name = "Device360V2IdentityMap",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(UiStyle.RecoveryCardPadding),
                Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
            };
            var grid = new TableLayoutPanel
            {
                Name = "Device360V2IdentityMapRows",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            Row(Tr("Device360V2CheckAd"), State(operational.Ad));
            Row(Tr("Device360V2CheckEntra"), State(operational.Entra));
            Row(Tr("Device360V2CheckIntune"), State(operational.Intune));

            // Intune can supply an Entra device ID as a relationship
            // attribute. That does NOT verify an AD-to-Entra/Intune link
            // and does not prove an independent Entra metadata search.
            var intuneReferencesEntra = snapshot.IdentityVerified &&
                !snapshot.IdentityAmbiguous && snapshot.InIntune == true &&
                snapshot.InEntra == true &&
                !string.IsNullOrWhiteSpace(snapshot.EntraDeviceId) &&
                !string.IsNullOrWhiteSpace(snapshot.IntuneManagedDeviceId);
            Row(Tr("Device360V2LinkEntraIntune"),
                intuneReferencesEntra
                    ? Tr("Device360V2EntraIntuneEvidence")
                    : Tr("Device360V2LinkUnverified"));
            Row(Tr("Device360V2LinkAdCloud"),
                Tr("Device360V2LinkUnverified"));
            Row(Tr("Device360V2IdentityRisk"),
                snapshot.IdentityAmbiguous
                    ? Tr("Device360V2IdentityConflict")
                    : snapshot.IdentityVerified
                        ? Tr("Device360V2IdentitySourceOnly")
                        : Tr("Device360NotVerified"));
            card.Controls.Add(grid);
            return card;

            string State(Device360SourceEvidence evidence) => evidence switch
            {
                Device360SourceEvidence.FoundInSearch =>
                    Tr("Device360SourcePresent"),
                Device360SourceEvidence.LookupFailed =>
                    Tr("Device360SourceLookupFailed"),
                Device360SourceEvidence.SessionPresentUnverified =>
                    Tr("Device360SourceUnverified"),
                _ => Tr("Device360SourceDisconnected")
            };
            void Row(string title, string description)
            {
                var index = grid.RowCount++;
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var caption = new Label
                {
                    Text = title,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    TextAlign = rtl ? ContentAlignment.MiddleRight :
                        ContentAlignment.MiddleLeft,
                    RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                    Margin = new Padding(3, 5, 8, 5)
                };
                var status = new Label
                {
                    Text = description,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    ForeColor = UiStyle.ModernForeground,
                    RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                    TextAlign = rtl ? ContentAlignment.MiddleRight :
                        ContentAlignment.MiddleLeft,
                    Margin = new Padding(3, 5, 3, 5)
                };
                UiStyle.ConfigureWrappingLabel(status);
                grid.Controls.Add(caption, 0, index);
                grid.Controls.Add(status, 1, index);
            }
        }

        UiRecoveryCard AuditEvidence()
        {
            // Configuration is visible. Effective rights and a durable audit
            // event are NOT tested by Device 360 and never claimed as green.
            return SimpleCard("Device360V2AuditEvidence",
                Tr("Device360V2AuditExplanation") + Environment.NewLine +
                Tr("Device360PolicyAudit") + ": " +
                Tr(operational.ProtectedAuditRequiredByConfiguration
                    ? "Device360PolicyRequired" : "Device360PolicyOptional") +
                Environment.NewLine +
                Tr("Device360PolicyBroker") + ": " +
                Tr(operational.AuditBrokerEnabledByConfiguration
                    ? "Device360PolicyEnabled" : "Device360PolicyDisabled") +
                Environment.NewLine +
                Tr("Device360PolicyRbac") + ": " +
                Tr(operational.RbacEnabledByConfiguration
                    ? "Device360PolicyEnabled" : "Device360PolicyDisabled") +
                Environment.NewLine +
                Tr("Device360AccessEffective") + ": " +
                Tr("Device360AccessUnchecked") + Environment.NewLine +
                Tr("Device360V2AuditNotVerified"));
        }

        UiRecoveryCard IdentityEvidence()
        {
            var card = SimpleCard("Device360V2IdentityEvidence",
                Tr("Device360V2IdentityMatch") + Environment.NewLine +
                (string.IsNullOrWhiteSpace(snapshot.IdentityLinkEvidence)
                    ? "Cross-source identity not established; matching names do not prove identity."
                    : snapshot.IdentityLinkEvidence));
            return card;
        }

        UiRecoveryCard HardwareFields()
        {
            var card = new UiRecoveryCard
            {
                Name = "Device360V2RemoteHardware",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(UiStyle.RecoveryCardPadding),
                Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
            };
            var note = new Label
            {
                Text = Tr("Device360V2HardwareHint"),
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = UiStyle.ModernForeground
            };
            UiStyle.ConfigureWrappingLabel(note);
            card.Controls.Add(note);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                RightToLeft = RightToLeft.No
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rtl ? 65 : 35));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rtl ? 35 : 65));
            Add("Device360V2RemoteManufacturer",
                Tr("Device360V2RemoteManufacturer"));
            Add("Device360V2RemoteModel", Tr("Device360V2RemoteModel"));
            Add("Device360V2BiosVersion", Tr("Device360V2Bios"));
            Add("Device360V2BiosManufacturer", Tr("Device360V2BiosManufacturer"));
            Add("Device360V2BiosReleaseDate", Tr("Device360V2BiosDate"));
            Add("Device360V2TpmVersion", Tr("Device360V2Tpm"));
            Add("Device360V2TpmEnabled", Tr("Device360V2TpmEnabled"));
            Add("Device360V2TpmActivated", Tr("Device360V2TpmActivated"));
            Add("Device360V2TpmOwned", Tr("Device360V2TpmOwned"));
            Add("Device360V2TpmDetail", Tr("Device360V2TpmStatus"));
            card.Controls.Add(grid);
            return card;

            void Add(string name, string label)
            {
                var index = grid.RowCount++;
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                grid.Controls.Add(new Label
                {
                    Text = label,
                    AutoSize = true,
                    TextAlign = rtl ? ContentAlignment.MiddleRight :
                        ContentAlignment.MiddleLeft,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(3, 5, 6, 5)
                }, rtl ? 1 : 0, index);
                grid.Controls.Add(new TextBox
                {
                    Name = name,
                    Text = Tr("Device360NotVerified"),
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    MinimumSize = new Size(0, 26),
                    BorderStyle = BorderStyle.FixedSingle,
                    RightToLeft = RightToLeft.No
                }, rtl ? 0 : 1, index);
            }
        }

        UiRecoveryCard SimpleCard(string name, string value)
        {
            var card = new UiRecoveryCard
            {
                Name = name,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(UiStyle.RecoveryCardPadding),
                Margin = new Padding(0, UiStyle.ControlGap, 0, 0)
            };
            var text = new Label
            {
                Text = value,
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = UiStyle.ModernForeground,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
            };
            UiStyle.ConfigureWrappingLabel(text);
            card.Controls.Add(text);
            return card;
        }
    }

    private static Control? FindIn(Control root, string name)
    {
        if (root.Name == name) return root;
        foreach (Control child in root.Controls)
        {
            var found = FindIn(child, name);
            if (found is not null) return found;
        }
        return null;
    }
}
