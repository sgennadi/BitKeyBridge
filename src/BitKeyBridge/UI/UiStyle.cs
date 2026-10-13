namespace BitKeyBridge;

public enum UiStatusKind
{
    Neutral,
    Busy,
    Success,
    Warning,
    Error
}

/// <summary>
/// Shared visual and layout standard for BitKeyBridge WinForms UI.
/// Values are expressed in 96-DPI logical pixels and are scaled by WinForms.
/// </summary>
public static class UiStyle
{
    public const int BaselineDpi = 96;
    public const int WindowMargin = 16;
    public const int PagePadding = 18;
    public const int SectionGap = 12;
    public const int ControlGap = 8;
    public const int MinimumButtonHeight = 32;
    public const int MinimumButtonWidth = 88;
    // Start's BitLocker/LAPS tab body, with room for wrapped actions at high DPI.
    // Responsive two-column recovery cards on Start.
    public const int HomeRecoveryCardsPairMinimumWidth = 820;
    public const int HomeRecoveryCardMaxTextWidth = 740;
    // Application-wide modern Windows treatment. Controls use these tokens,
    // not per-window hardcoded fonts, colors or radii.
    public const int ModernButtonRadius = 11;
    public const int ModernInputRadius = 9;
    public const int ModernSmallRadius = 7;
    public const int ModernSurfaceRadius = 15;
    public const int ModernTabRadius = 11;
    public const int ModernTabHorizontalPadding = 17;
    public const int ModernTabVerticalPadding = 7;

    public static Color ModernCanvas =>
        SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(247, 248, 251);
    public static Color ModernSurface => SystemColors.Window;
    public static Color ModernForeground => SystemColors.WindowText;
    public static Color ModernAccent =>
        SystemInformation.HighContrast ? SystemColors.HotTrack : Color.FromArgb(27, 91, 164);
    public static Color ModernMuted =>
        SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(100, 106, 113);
    public static Color ModernBorder =>
        SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(224, 228, 233);
    public static Color ModernHeader =>
        SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(239, 242, 247);
    public static Color ModernAlternateRow =>
        SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(251, 252, 254);
    public static Color ModernHover =>
        SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(232, 238, 247);
    public static Color ModernPressed =>
        SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(218, 229, 242);

    // Shared Windows 11-inspired helpdesk card design at 96 DPI.
    public const int RecoveryCardCornerRadius = 20;
    public const int RecoveryCardPadding = 20;
    public const int RecoveryTitleGlyphSize = 31;
    public const int RecoveryActionGlyphSize = 20;
    public const int RecoveryActionMinimumHeight = 46;
    public const int RecoveryHistoryGridHeight = 155;
    public const int RecoveryHistoryGridMinimumHeight = 120;
    public const int RecoveryCardFieldSpacing = 10;

    public static Color RecoveryCardBackground => SystemColors.Window;
    public static Color RecoveryCardBorder => Color.FromArgb(226, 228, 231);
    public static Color RecoveryCardForeground => SystemColors.WindowText;
    public static Color RecoveryCardMutedText =>
        SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(103, 107, 113);
    public static Color RecoveryActionBackground =>
        SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(241, 243, 245);

    public static (Color Background, Color Foreground) ResolveRecoveryBadgeColors(
        UiStatusKind kind)
    {
        if (SystemInformation.HighContrast)
            return (SystemColors.Window, SystemColors.WindowText);
        return kind switch
        {
            UiStatusKind.Success => (Color.FromArgb(220, 247, 234), Color.FromArgb(21, 127, 61)),
            UiStatusKind.Warning => (Color.FromArgb(255, 243, 216), Color.FromArgb(152, 88, 14)),
            UiStatusKind.Error => (Color.FromArgb(255, 225, 226), Color.FromArgb(167, 43, 43)),
            UiStatusKind.Busy => (Color.FromArgb(228, 238, 253), Color.FromArgb(43, 87, 170)),
            _ => (Color.FromArgb(237, 238, 240), SystemColors.ControlText)
        };
    }

    public static void ConfigureRecoveryActionButton(
        Button button, RecoveryGlyphKind glyph, bool secondary = false)
    {
        button.AutoSize = true;
        button.MinimumSize = new Size(
            Math.Max(button.MinimumSize.Width, 148),
            Math.Max(button.MinimumSize.Height, RecoveryActionMinimumHeight));
        button.Font = CreateEmphasisFont(10F);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor =
            SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(229, 232, 236);
        button.FlatAppearance.MouseDownBackColor =
            SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(218, 223, 228);
        button.BackColor = RecoveryActionBackground;
        button.ForeColor = RecoveryCardForeground;
        button.UseVisualStyleBackColor = false;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Padding = new Padding(14, 6, 12, 6);
        button.Margin = new Padding(0, 0, 0, secondary ? 0 : ControlGap);
        var image = UiRecoveryGraphics.CreateGlyphImage(
            glyph, RecoveryActionGlyphSize, RecoveryCardForeground);
        var oldImage = button.Image;
        button.Image = image;
        oldImage?.Dispose();
        button.Disposed += (_, _) => image.Dispose();

        void RefreshCorners()
        {
            if (button.IsDisposed || button.Width < 2 || button.Height < 2)
                return;
            using var path = UiRecoveryGraphics.RoundRect(
                new RectangleF(0, 0, button.Width, button.Height),
                12f * button.DeviceDpi / BaselineDpi);
            var old = button.Region;
            button.Region = new Region(path);
            old?.Dispose();
        }

        button.SizeChanged += (_, _) => RefreshCorners();
        button.DpiChangedAfterParent += (_, _) => RefreshCorners();
        RefreshCorners();
    }

    public static void ConfigureRecoveryFieldCaption(Label caption)
    {
        ConfigureWrappingLabel(caption);
        caption.Font = CreateBodyFont(9.5F);
        caption.ForeColor = RecoveryCardMutedText;
        caption.MaximumSize = new Size(HomeRecoveryCardMaxTextWidth, 0);
        caption.Margin = new Padding(0, RecoveryCardFieldSpacing, 0, 2);
        caption.Dock = DockStyle.Top;
    }

    public static void ConfigureRecoveryFieldValue(Label value)
    {
        ConfigureWrappingLabel(value);
        value.Font = CreateEmphasisFont(11F);
        value.ForeColor = RecoveryCardForeground;
        value.MaximumSize = new Size(HomeRecoveryCardMaxTextWidth, 0);
        value.Margin = new Padding(0, 0, 0, ControlGap);
        value.Dock = DockStyle.Top;
    }

    public const int AdCredentialDialogWidth = 800;
    public const int AdCredentialDialogHeight = 520;
    public const int AdCredentialDialogMinimumWidth = 640;
    public const int AdCredentialDialogMinimumHeight = 420;
    public const int AdCredentialDialogTextWidth = 740;
    // Height of the LAPS scrollable grids/details at the 96-DPI baseline.
    // Keep these values in the shared style instead of individual forms.
    public const int LapsSearchGridHeight = 170;
    public const int LapsSearchGridMinimumHeight = 120;
    public const int LapsKeyGridHeight = 200;
    public const int LapsKeyGridMinimumHeight = 160;
    public const int LapsDetailsHeight = 130;
    public const int LapsDetailsMinimumHeight = 90;

    private sealed class ListViewColumnState
    {
        public int[] LogicalWidths { get; set; } =
            [];

        public bool Updating { get; set; }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        ListView,
        ListViewColumnState> ListViewColumnStates =
        new();

    public static Font BodyFont =>
        SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

    public static Font CreateBodyFont(
        float size) =>
        new(
            BodyFont.FontFamily,
            size,
            FontStyle.Regular,
            GraphicsUnit.Point);

    public static Font CreateDialogTitleFont() =>
        new(
            "Segoe UI Semibold",
            15F,
            FontStyle.Regular,
            GraphicsUnit.Point);

    public static Font CreatePageTitleFont() =>
        new(
            "Segoe UI Semibold",
            18F,
            FontStyle.Regular,
            GraphicsUnit.Point);

    public static Font CreateSectionTitleFont() =>
        new(
            "Segoe UI Semibold",
            14F,
            FontStyle.Regular,
            GraphicsUnit.Point);

    public static Font CreateEmphasisFont(
        float size = 9F) =>
        new(
            "Segoe UI Semibold",
            size,
            FontStyle.Regular,
            GraphicsUnit.Point);

    public static Font CreateMonospaceFont(
        float size = 9.5F) =>
        new(
            "Consolas",
            size,
            FontStyle.Regular,
            GraphicsUnit.Point);

    public static void ConfigureStatusLabel(
        UiStatusLabel label)
    {
        label.AutoSize =
            true;
        label.Dock =
            DockStyle.Fill;
        label.BorderStyle =
            BorderStyle.FixedSingle;
        label.Padding =
            new Padding(
                ControlGap,
                6,
                ControlGap,
                6);
        label.Margin =
            new Padding(
                0,
                ControlGap,
                0,
                0);
        label.TextAlign =
            ContentAlignment.MiddleLeft;
        label.AccessibleRole =
            AccessibleRole.StaticText;
        label.TabStop =
            false;

        RefreshStatusLabel(
            label);
    }

    public static void ConfigureInlineStatusLabel(
        UiStatusLabel label)
    {
        label.AutoSize =
            true;
        label.BorderStyle =
            BorderStyle.None;
        label.Padding =
            Padding.Empty;
        label.TextAlign =
            ContentAlignment.MiddleLeft;
        label.AccessibleRole =
            AccessibleRole.StaticText;
        label.TabStop =
            false;

        RefreshStatusLabel(
            label);
    }

    /// <summary>
    /// Allow long, diagnostic status messages to grow in height rather
    /// than being measured as a single 4096-pixel-wide line.
    /// DpiAwareForm constrains MaximumSize to the available parent width.
    /// </summary>
    public static void ConfigureWrappingStatusLabel(
        UiStatusLabel label,
        bool bordered = true)
    {
        if (bordered)
            ConfigureStatusLabel(label);
        else
            ConfigureInlineStatusLabel(label);

        label.AutoSize = true;
        label.AutoEllipsis = false;
        label.UseMnemonic = false;
        label.Dock = DockStyle.Fill;
        label.MaximumSize = new Size(2400, 0);
    }

    public static void SetStatus(
        UiStatusLabel label,
        string text,
        UiStatusKind kind)
    {
        label.StatusKind =
            kind;
        label.Text =
            text;

        RefreshStatusLabel(
            label);
    }

    public static void ApplyStatusLabel(
        UiStatusLabel label,
        UiStatusKind kind)
    {
        label.StatusKind =
            kind;

        RefreshStatusLabel(
            label);
    }

    public static void RefreshStatusLabel(
        UiStatusLabel label)
    {
        var kind =
            label.StatusKind;

        (label.BackColor, label.ForeColor) =
            ResolveStatusColors(
                kind,
                SystemInformation.HighContrast);

        label.Image =
            UiStatusGlyphs.Get(
                kind,
                label.DeviceDpi,
                label.ForeColor);

        if (string.IsNullOrWhiteSpace(
                label.AccessibleName))
        {
            label.AccessibleName =
                "Status";
        }

        label.AccessibleDescription =
            string.IsNullOrWhiteSpace(
                label.Text)
                ? $"{kind} status."
                : $"{kind} status. {label.Text}";

        label.Invalidate();
    }

    internal static (
        Color BackColor,
        Color ForeColor)
        ResolveStatusColors(
            UiStatusKind kind,
            bool highContrast)
    {
        if (highContrast)
        {
            return (
                SystemColors.Window,
                SystemColors.WindowText);
        }

        return kind switch
        {
            UiStatusKind.Busy =>
                (
                    Color.LemonChiffon,
                    Color.DarkGoldenrod),
            UiStatusKind.Success =>
                (
                    Color.Honeydew,
                    Color.DarkGreen),
            UiStatusKind.Warning =>
                (
                    Color.LemonChiffon,
                    Color.DarkOrange),
            UiStatusKind.Error =>
                (
                    Color.MistyRose,
                    Color.DarkRed),
            _ =>
                (
                    SystemColors.Window,
                    SystemColors.ControlText)
        };
    }

    public static void BindBalancedWidths(
        Control first,
        Control second,
        int minimumLogicalWidth = 160)
    {
        var updating =
            false;

        void Refresh()
        {
            if (updating ||
                first.IsDisposed ||
                second.IsDisposed)
            {
                return;
            }

            updating =
                true;

            try
            {
                var dpi =
                    Math.Max(
                        first.DeviceDpi,
                        second.DeviceDpi);

                var scaledMinimum =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            minimumLogicalWidth *
                            dpi /
                            (double)BaselineDpi));

                var firstPreferred =
                    first.GetPreferredSize(
                        Size.Empty);
                var secondPreferred =
                    second.GetPreferredSize(
                        Size.Empty);

                var width =
                    Math.Max(
                        scaledMinimum,
                        Math.Max(
                            firstPreferred.Width,
                            secondPreferred.Width));

                first.MinimumSize =
                    new Size(
                        width,
                        first.MinimumSize.Height);
                second.MinimumSize =
                    new Size(
                        width,
                        second.MinimumSize.Height);

                if (!first.AutoSize)
                    first.Width = width;

                if (!second.AutoSize)
                    second.Width = width;
            }
            finally
            {
                updating =
                    false;
            }
        }

        first.FontChanged +=
            (_, _) =>
                Refresh();
        second.FontChanged +=
            (_, _) =>
                Refresh();

        if (first.Parent is not null &&
            ReferenceEquals(
                first.Parent,
                second.Parent))
        {
            first.Parent.Layout +=
                (_, _) =>
                    Refresh();
        }

        Refresh();
    }

    public static void ConfigureDataGridView(
        DataGridView grid)
    {
        grid.AutoGenerateColumns =
            false;
        grid.AllowUserToAddRows =
            false;
        grid.AllowUserToDeleteRows =
            false;
        grid.AllowUserToResizeRows =
            false;
        grid.MultiSelect =
            false;
        grid.ReadOnly =
            true;
        grid.RowHeadersVisible =
            false;
        grid.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeRowsMode =
            DataGridViewAutoSizeRowsMode.None;
        grid.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.BackgroundColor =
            SystemColors.Window;
        grid.BorderStyle =
            BorderStyle.FixedSingle;
        grid.Font =
            BodyFont;
        grid.DefaultCellStyle.Font =
            BodyFont;
        grid.ColumnHeadersDefaultCellStyle.Font =
            CreateEmphasisFont();
        grid.DefaultCellStyle.SelectionBackColor =
            SystemColors.Highlight;
        grid.DefaultCellStyle.SelectionForeColor =
            SystemColors.HighlightText;
    }

    public static DataGridViewTextBoxColumn CreateSortableTextColumn(
        string name,
        string headerText,
        int fillWeight = 100)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = headerText,
            SortMode = DataGridViewColumnSortMode.Automatic,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = Math.Max(
                1,
                fillWeight)
        };
    }

    /// <summary>
    /// Reserve enough width for important grid text at every DPI. If the
    /// columns cannot fit, the grid scrolls horizontally instead of
    /// truncating status labels or changing the full-screen layout.
    /// </summary>
    public static void ConfigureReadableDataGridColumns(
        DataGridView grid,
        params (string Name, string SampleText, int LogicalMinWidth)[] columns)
    {
        void UpdateMinimumWidths()
        {
            if (grid.IsDisposed)
                return;

            var flags =
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix;

            var dataFont =
                grid.DefaultCellStyle.Font ??
                grid.Font;
            var headerFont =
                grid.ColumnHeadersDefaultCellStyle.Font ??
                grid.Font;
            var extra =
                ScaleLogicalPixels(26, grid.DeviceDpi);

            foreach (var (name, sample, logicalWidth) in columns)
            {
                if (grid.Columns[name] is not DataGridViewColumn column)
                    continue;

                var headerWidth =
                    TextRenderer.MeasureText(
                        column.HeaderText,
                        headerFont,
                        new Size(4096, 1024),
                        flags).Width;
                var exampleWidth =
                    TextRenderer.MeasureText(
                        sample,
                        dataFont,
                        new Size(4096, 1024),
                        flags).Width;
                column.MinimumWidth =
                    Math.Max(
                        ScaleLogicalPixels(logicalWidth, grid.DeviceDpi),
                        Math.Max(headerWidth, exampleWidth) + extra);
            }
        }

        UpdateMinimumWidths();
        grid.FontChanged += (_, _) => UpdateMinimumWidths();
        grid.DpiChangedAfterParent += (_, _) => UpdateMinimumWidths();
        grid.HandleCreated += (_, _) => UpdateMinimumWidths();
    }

    public static void ConfigureListViewColumns(
        ListView view,
        params (string Name, int Width)[] columns)
    {
        if (columns.Length == 0)
            return;

        var state =
            new ListViewColumnState
            {
                LogicalWidths =
                    columns
                        .Select(
                            column =>
                                Math.Max(
                                    1,
                                    column.Width))
                        .ToArray()
            };

        ListViewColumnStates.Remove(
            view);
        ListViewColumnStates.Add(
            view,
            state);

        state.Updating =
            true;

        try
        {
            view.Columns.Clear();

            foreach (var column in
                     columns)
            {
                view.Columns.Add(
                    column.Name,
                    GetListViewColumnWidth(
                        view,
                        column.Name,
                        column.Width));
            }
        }
        finally
        {
            state.Updating =
                false;
        }

        view.ColumnWidthChanged +=
            (_, args) =>
            {
                if (state.Updating ||
                    args.ColumnIndex < 0 ||
                    args.ColumnIndex >=
                        state.LogicalWidths.Length ||
                    args.ColumnIndex >=
                        view.Columns.Count)
                {
                    return;
                }

                state.LogicalWidths[
                    args.ColumnIndex] =
                    ToLogicalPixels(
                        view.Columns[
                            args.ColumnIndex]
                            .Width,
                        view.DeviceDpi);
            };

        view.FontChanged +=
            (_, _) =>
                RefreshListViewColumns(
                    view);

        view.HandleCreated +=
            (_, _) =>
                RefreshListViewColumns(
                    view);
    }

    public static void RefreshListViewColumns(
        ListView view)
    {
        if (!ListViewColumnStates.TryGetValue(
                view,
                out var state))
        {
            return;
        }

        var count =
            Math.Min(
                view.Columns.Count,
                state.LogicalWidths.Length);

        if (count <= 0)
            return;

        state.Updating =
            true;

        try
        {
            for (var index = 0;
                 index < count;
                 index++)
            {
                var width =
                    GetListViewColumnWidth(
                        view,
                        view.Columns[index].Text,
                        state.LogicalWidths[index]);

                if (view.Columns[index].Width !=
                    width)
                {
                    view.Columns[index].Width =
                        width;
                }
            }
        }
        finally
        {
            state.Updating =
                false;
        }
    }

    internal static int GetMinimumListViewHeaderWidth(
        ListView view,
        string headerText)
    {
        var measured =
            TextRenderer.MeasureText(
                headerText ?? string.Empty,
                view.Font,
                new Size(
                    4096,
                    1024),
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

        return Math.Max(
            1,
            measured.Width +
            ScaleLogicalPixels(
                24,
                view.DeviceDpi));
    }

    private static int GetListViewColumnWidth(
        ListView view,
        string headerText,
        int logicalWidth) =>
        Math.Max(
            ScaleLogicalPixels(
                logicalWidth,
                view.DeviceDpi),
            GetMinimumListViewHeaderWidth(
                view,
                headerText));

    internal static bool HasConfiguredListViewColumns(
        ListView view) =>
        ListViewColumnStates.TryGetValue(
            view,
            out _);

    internal static int ScaleLogicalPixels(
        int logicalPixels,
        int dpi) =>
        Math.Max(
            1,
            (int)Math.Round(
                Math.Max(
                    1,
                    logicalPixels) *
                Math.Max(
                    BaselineDpi,
                    dpi) /
                (double)BaselineDpi));

    internal static int ToLogicalPixels(
        int physicalPixels,
        int dpi) =>
        Math.Max(
            1,
            (int)Math.Round(
                Math.Max(
                    1,
                    physicalPixels) *
                BaselineDpi /
                (double)Math.Max(
                    BaselineDpi,
                    dpi)));

    public static Button CreateActionButton(
        string text,
        DialogResult dialogResult =
            DialogResult.None)
    {
        var button =
            new Button
            {
                Text =
                    text,
                DialogResult =
                    dialogResult
            };

        ConfigureActionButton(
            button);

        return button;
    }

    public static void ConfigureActionButton(
        Button button)
    {
        button.AutoSize =
            true;
        button.MinimumSize =
            new Size(
                Math.Max(
                    button.MinimumSize.Width,
                    MinimumButtonWidth),
                Math.Max(
                    button.MinimumSize.Height,
                    MinimumButtonHeight));
        button.Padding =
            new Padding(
                8,
                2,
                8,
                2);
        button.UseVisualStyleBackColor =
            true;
    }

    public static void ConfigureWrappingLabel(
        Label label)
    {
        label.AutoSize = true;
        label.AutoEllipsis = false;
        label.UseMnemonic = false;
    }

    public static void MakeVerticalWorkspaceResponsive(
        FlowLayoutPanel panel)
    {
        panel.FlowDirection =
            FlowDirection.TopDown;
        panel.WrapContents =
            false;

        void ResizeChildren()
        {
            var scrollbar =
                panel.VerticalScroll.Visible
                    ? SystemInformation.VerticalScrollBarWidth
                    : 0;

            var available =
                Math.Max(
                    0,
                    panel.ClientSize.Width -
                    panel.Padding.Horizontal -
                    scrollbar -
                    2);

            foreach (Control child in
                     panel.Controls)
            {
                var width =
                    Math.Max(
                        0,
                        available -
                        child.Margin.Horizontal);

                if (width <= 0)
                    continue;

                if (child is TableLayoutPanel table &&
                    table.AutoSize)
                {
                    // AutoSize tables otherwise grow back to their preferred
                    // content width and can overflow the workspace at high DPI
                    // or with enlarged text. Keep the width responsive while
                    // allowing the height to continue growing naturally.
                    table.MinimumSize =
                        new Size(
                            width,
                            0);
                    table.MaximumSize =
                        new Size(
                            width,
                            0);
                }

                if (child.Width != width)
                {
                    child.Width =
                        width;
                }
            }
        }

        panel.Layout +=
            (_, _) =>
                ResizeChildren();

        panel.ClientSizeChanged +=
            (_, _) =>
                ResizeChildren();
    }
}
