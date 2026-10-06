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
        SystemFonts.MessageBoxFont;

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
