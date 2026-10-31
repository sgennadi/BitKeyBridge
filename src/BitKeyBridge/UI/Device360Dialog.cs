namespace BitKeyBridge;

internal enum Device360Route
{
    None,
    BitLocker,
    Laps,
    Devices,
    AccessHealth
}

/// <summary>
/// A source-qualified, read-only computer overview shared by Start and Devices.
/// Opening it never searches directories, retrieves keys, reads LAPS, copies
/// secrets, approves access, or rotates an Intune key. Navigation is returned
/// to the caller for revalidation against the ORIGINAL selected result.
/// </summary>
internal sealed class Device360Dialog : DpiAwareForm
{
    internal Device360Route SelectedRoute { get; private set; }

    internal Device360Dialog(Device360Snapshot snapshot,
        string? languageCode = null, bool allowNavigation = true,
        Device360OperationalStatus? operational = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        operational ??= Device360OperationalStatus.Capture(snapshot,
            adConnected: false, cloudSessionPresent: false,
            rbacEnabled: false, ticketRequired: false,
            protectedAuditRequired: false, auditBrokerConfigured: false);
        var language = UiLanguageService.Normalize(languageCode);
        string Tr(string key) => UiLanguageService.Translate(language, key);
        var rtl = UiLanguageService.IsRightToLeft(language);
        Text = Tr("Device360CardTitle");
        Name = "Device360CardDialog";
        RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = rtl;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(960, 700);
        MinimumSize = new Size(610, 440);
        Font = UiStyle.BodyFont;
        BackColor = UiStyle.ModernCanvas;

        var root = new TableLayoutPanel
        {
            Name = "Device360CardRoot",
            Dock = DockStyle.Fill,
            Padding = new Padding(UiStyle.PagePadding),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiStyle.ModernCanvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var heading = new TableLayoutPanel
        {
            Name = "Device360CardHeader",
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
            Margin = new Padding(0, 0, 0, UiStyle.SectionGap)
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption = new Label
        {
            Name = "Device360CardHeading",
            Text = Tr("Device360CardTitle"),
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = UiStyle.CreatePageTitleFont(),
            ForeColor = UiStyle.ModernForeground,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
            TextAlign = rtl ? ContentAlignment.MiddleRight :
                ContentAlignment.MiddleLeft
        };
        var computer = new Label
        {
            Name = "Device360CardComputer",
            Text = snapshot.Computer,
            AccessibleName = Tr("Device360CardComputer"),
            RightToLeft = RightToLeft.No,
            TextAlign = rtl ? ContentAlignment.MiddleRight :
                ContentAlignment.MiddleLeft,
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = UiStyle.CreateEmphasisFont(13),
            ForeColor = UiStyle.ModernAccent
        };
        UiStyle.ConfigureWrappingLabel(computer);
        heading.Controls.Add(caption, 0, 0);
        heading.Controls.Add(computer, 0, 1);
        root.Controls.Add(heading, 0, 0);

        var scroll = new Panel
        {
            Name = "Device360CardScroll",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiStyle.ModernCanvas
        };
        root.Controls.Add(scroll, 0, 1);
        var content = new TableLayoutPanel
        {
            Name = "Device360CardSections",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 6),
            BackColor = UiStyle.ModernCanvas
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < content.RowCount; i++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroll.Controls.Add(content);

        var sourceStatus = new TableLayoutPanel
        {
            Name = "Device360SourceStates",
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            // Explicit physical source positions are assigned below; do not
            // double-mirror the TableLayoutPanel under Hebrew WinForms RTL.
            RightToLeft = RightToLeft.No,
            Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
        };
        for (var i = 0; i < 3; i++)
            sourceStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
        sourceStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        AddSource(sourceStatus, "AD", operational.Ad, "Device360SourceAd",
            rtl ? 2 : 0);
        AddSource(sourceStatus, "Entra", operational.Entra, "Device360SourceEntra", 1);
        AddSource(sourceStatus, "Intune", operational.Intune, "Device360SourceIntune",
            rtl ? 0 : 2);
        content.Controls.Add(sourceStatus, 0, 0);

        if (!snapshot.IdentityVerified || snapshot.IdentityAmbiguous)
        {
            var warning = new Label
            {
                Name = "Device360CardIdentityWarning",
                Text = Tr("Device360IdentityWarning"),
                ForeColor = UiStyle.ModernForeground,
                BackColor = UiStyle.BrandSoftBlue,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
            };
            UiStyle.ConfigureWrappingLabel(warning);
            content.Controls.Add(warning, 0, 1);
        }
        else
        {
            content.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Top }, 0, 1);
        }

        var identity = CreateSection("Device360CardIdentitySection",
            Tr("Device360Identity"));
        AddField(identity, "Device360QualifiedIdentity", Tr("Device360QualifiedIdentity"),
            Value(snapshot.QualifiedIdentity));
        AddField(identity, "Device360AdGuid", Tr("Device360AdGuid"),
            Value(snapshot.AdObjectGuid));
        AddField(identity, "Device360AdDn", Tr("Device360AdDn"),
            Value(snapshot.AdDistinguishedName));
        AddField(identity, "Device360EntraId", Tr("Device360EntraId"),
            Value(snapshot.EntraDeviceId));
        AddField(identity, "Device360IntuneId", Tr("Device360IntuneId"),
            Value(snapshot.IntuneManagedDeviceId));
        content.Controls.Add(identity.Card, 0, 2);

        var recovery = CreateSection("Device360CardRecoverySection",
            Tr("Device360Recovery"));
        var bitlockerStatus = snapshot.BitLockerMetadataCount > 0
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Tr(snapshot.BitLockerMetadataIncomplete
                    ? "Device360BitLockerPartial" : "Device360BitLockerFound"),
                snapshot.BitLockerMetadataCount)
            : Tr("Device360NotVerified");
        AddField(recovery, "Device360BitLockerMetadata",
            "BitLocker", bitlockerStatus);
        var lapsStatus = snapshot.LapsMetadataDetected
            ? Tr(snapshot.LapsMetadataExpired
                ? "Device360LapsExpired" : "Device360LapsDetected")
            : Tr("Device360NotVerified");
        AddField(recovery, "Device360LapsMetadata", "LAPS", lapsStatus);
        AddField(recovery, "Device360CardRecoveryNote",
            Tr("Device360Permission"), Tr("Device360PermissionNotice"));
        content.Controls.Add(recovery.Card, 0, 3);

        var posture = CreateSection("Device360CardPostureSection",
            Tr("Device360Inventory"));
        AddField(posture, "Device360Os", Tr("Device360Os"), Value(snapshot.OperatingSystem));
        AddField(posture, "Device360Hardware", Tr("Device360Hardware"), Value(snapshot.Hardware));
        AddField(posture, "Device360Serial", Tr("Device360Serial"), Value(snapshot.SerialNumber));
        AddField(posture, "Device360AssignedUser", Tr("Device360AssignedUser"),
            Value(snapshot.AssignedUser));
        AddField(posture, "Device360Compliance", Tr("Device360Compliance"),
            Value(snapshot.Compliance));
        AddField(posture, "Device360Encrypted", Tr("Device360Encrypted"),
            snapshot.Encrypted.HasValue
                ? Tr(snapshot.Encrypted.Value ? "Device360Yes" : "Device360No")
                : Tr("Device360NotVerified"));
        AddField(posture, "Device360LastSync", Tr("Device360LastSync"),
            Date(snapshot.LastSyncUtc));
        AddField(posture, "Device360AdLastLogon", Tr("Device360AdLastLogon"),
            Date(snapshot.AdLastLogonUtc));
        // This section describes settings, never actual permission to
        // retrieve a secret. The card performs no AD/Graph authorization probe.
        var access = CreateSection("Device360CardAccessSection",
            Tr("Device360AccessSection"));
        AddField(access, "Device360AuthorizationResult",
            Tr("Device360AccessEffective"), Tr("Device360AccessUnchecked"));
        AddField(access, "Device360RbacPolicy",
            Tr("Device360PolicyRbac"), Tr(operational.RbacEnabledByConfiguration
                ? "Device360PolicyEnabled" : "Device360PolicyDisabled"));
        AddField(access, "Device360TicketPolicy",
            Tr("Device360PolicyTicket"), Tr(operational.TicketRequiredByConfiguration
                ? "Device360PolicyRequired" : "Device360PolicyOptional"));
        AddField(access, "Device360ProtectedAuditPolicy",
            Tr("Device360PolicyAudit"), Tr(operational.ProtectedAuditRequiredByConfiguration
                ? "Device360PolicyRequired" : "Device360PolicyOptional"));
        AddField(access, "Device360AuditBrokerPolicy",
            Tr("Device360PolicyBroker"), Tr(operational.AuditBrokerEnabledByConfiguration
                ? "Device360PolicyEnabled" : "Device360PolicyDisabled"));
        AddField(access, "Device360SearchCompleteness",
            Tr("Device360HealthSection"), Tr(operational.SearchPartial
                ? "Device360PartialWarning" : "Device360CompleteMetadata"));
        content.Controls.Add(access.Card, 0, 4);
        content.Controls.Add(posture.Card, 0, 5);

        var actions = new FlowLayoutPanel
        {
            Name = "Device360CardActions",
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            Padding = new Padding(0, UiStyle.ControlGap, 0, 0),
            Margin = Padding.Empty
        };
        var close = UiStyle.CreateActionButton(Tr("Device360Close"), DialogResult.Cancel);
        close.Name = "Device360CardClose";
        actions.Controls.Add(close);
        CancelButton = close;
        // Avoid WinForms selecting the first read-only identity field when
        // opening the modal (the field is for copying identifiers on demand).
        Shown += (_, _) => close.Focus();
        // Navigation to the existing Access Health workspace only; the card
        // never tests privileges or fetches secrets automatically.
        AddRoute("Device360RouteAccessHealth", Tr("Device360OpenAccessHealth"),
            Device360Route.AccessHealth);

        // Navigation is not authorization. Callers recheck original exact
        // identity and source before invoking the existing workspaces.
        if (allowNavigation && snapshot.IdentityVerified && !snapshot.IdentityAmbiguous)
        {
            if (snapshot.InAd == true)
            {
                AddRoute("Device360RouteBitLocker",
                    Tr("Device360OpenBitLocker"), Device360Route.BitLocker);
                AddRoute("Device360RouteLaps",
                    Tr("Device360OpenLaps"), Device360Route.Laps);
            }
            else if (snapshot.InEntra == true || snapshot.InIntune == true)
            {
                AddRoute("Device360RouteDevices",
                    Tr("Device360OpenDevices"), Device360Route.Devices);
            }
        }
        root.Controls.Add(actions, 0, 2);

        string Value(string? text) =>
            string.IsNullOrWhiteSpace(text) ? Tr("Device360NotVerified") : text;

        string Date(DateTime? date) =>
            date?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'",
                System.Globalization.CultureInfo.InvariantCulture) ??
            Tr("Device360NotVerified");

        void AddSource(TableLayoutPanel table, string source,
            Device360SourceEvidence evidence, string name, int column)
        {
            var stateKey = evidence switch
            {
                Device360SourceEvidence.FoundInSearch => "Device360SourcePresent",
                Device360SourceEvidence.SessionPresentUnverified => "Device360SourceUnverified",
                _ => "Device360SourceDisconnected"
            };
            var card = new UiRecoveryCard
            {
                Name = name + "Card",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, UiStyle.ControlGap, 0)
            };
            var statusLabel = new Label
            {
                Name = name,
                Text = source + Environment.NewLine + Tr(stateKey),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiStyle.CreateEmphasisFont(10),
                ForeColor = evidence == Device360SourceEvidence.FoundInSearch
                    ? UiStyle.ModernPositiveAccent : UiStyle.ModernMuted,
                AccessibleName = source + ": " + Tr(stateKey)
            };
            card.Controls.Add(statusLabel);
            table.Controls.Add(card, column, 0);
        }

        void AddRoute(string name, string label, Device360Route route)
        {
            var button = UiStyle.CreateActionButton(label);
            button.Name = name;
            button.AccessibleName = label + " — " +
                Tr("Device360NavigationOnly");
            button.Click += (_, _) =>
            {
                SelectedRoute = route;
                DialogResult = DialogResult.OK;
                Close();
            };
            actions.Controls.Add(button);
        }

        (UiRecoveryCard Card, TableLayoutPanel Fields) CreateSection(
            string name, string title)
        {
            var card = new UiRecoveryCard
            {
                Name = name,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(UiStyle.RecoveryCardPadding),
                Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
            };
            var area = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                // Invert the physical field columns explicitly for Hebrew.
                // The identifier values themselves remain LTR and selectable.
                RightToLeft = RightToLeft.No,
                BackColor = SystemInformation.HighContrast
                    ? SystemColors.Window : UiStyle.RecoveryCardBackground
            };
            area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,
                rtl ? 66 : 34));
            area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,
                rtl ? 34 : 66));
            area.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label
            {
                Text = title,
                Font = UiStyle.CreateSectionTitleFont(),
                Dock = DockStyle.Top,
                AutoSize = true,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                TextAlign = rtl ? ContentAlignment.MiddleRight :
                    ContentAlignment.MiddleLeft,
                ForeColor = UiStyle.ModernForeground,
                Margin = new Padding(0, 0, 0, UiStyle.ControlGap)
            };
            area.Controls.Add(label, 0, 0);
            area.SetColumnSpan(label, 2);
            card.Controls.Add(area);
            return (card, area);
        }

        void AddField((UiRecoveryCard Card, TableLayoutPanel Fields) section,
            string name, string title, string value)
        {
            var area = section.Fields;
            var row = area.RowCount++;
            area.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var key = new Label
            {
                Text = title,
                AutoSize = true,
                Dock = DockStyle.Fill,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                TextAlign = rtl ? ContentAlignment.MiddleRight :
                    ContentAlignment.MiddleLeft,
                ForeColor = UiStyle.ModernForeground,
                Margin = rtl ? new Padding(10, 5, 0, 5) :
                    new Padding(0, 5, 10, 5)
            };
            UiStyle.ConfigureWrappingLabel(key);
            var field = new TextBox
            {
                Name = name,
                Text = value,
                ReadOnly = true,
                Multiline = name is "Device360CardRecoveryNote" or "Device360SearchCompleteness",
                WordWrap = name is "Device360CardRecoveryNote" or "Device360SearchCompleteness",
                ScrollBars = ScrollBars.None,
                RightToLeft = RightToLeft.No,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0,
                    name is "Device360CardRecoveryNote" or "Device360SearchCompleteness"
                        ? 76 : 26),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemInformation.HighContrast
                    ? SystemColors.Window : UiStyle.ModernSurface,
                ForeColor = UiStyle.ModernForeground,
                AccessibleName = title,
                Margin = new Padding(0, 3, 0, 3)
            };
            area.Controls.Add(key, rtl ? 1 : 0, row);
            area.Controls.Add(field, rtl ? 0 : 1, row);
        }
    }
}
