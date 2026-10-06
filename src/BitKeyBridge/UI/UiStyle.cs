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
    private sealed class StatusState
    {
        public UiStatusKind Kind { get; set; }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        Label,
        StatusState> StatusStates =
        new();

    public const int BaselineDpi = 96;
    public const int WindowMargin = 16;
    public const int PagePadding = 18;
    public const int SectionGap = 12;
    public const int ControlGap = 8;
    public const int MinimumButtonHeight = 32;
    public const int MinimumButtonWidth = 88;

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
        Label label)
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
        label.ImageAlign =
            ContentAlignment.MiddleLeft;
        label.TextImageRelation =
            TextImageRelation.ImageBeforeText;
        label.AccessibleRole =
            AccessibleRole.StaticText;
        label.TabStop =
            false;
    }

    public static void ApplyStatusLabel(
        Label label,
        UiStatusKind kind)
    {
        var state =
            StatusStates.GetOrCreateValue(
                label);

        state.Kind =
            kind;

        RefreshStatusLabel(
            label,
            kind);
    }

    public static void RefreshStatusLabel(
        Label label)
    {
        if (!StatusStates.TryGetValue(
                label,
                out var state))
        {
            return;
        }

        RefreshStatusLabel(
            label,
            state.Kind);
    }

    private static void RefreshStatusLabel(
        Label label,
        UiStatusKind kind)
    {
        (label.BackColor, label.ForeColor) =
            kind switch
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

        label.Image =
            UiStatusGlyphs.Get(
                kind,
                label.DeviceDpi,
                label.ForeColor);

        label.AccessibleName =
            $"{kind} status";

        label.AccessibleDescription =
            string.IsNullOrWhiteSpace(
                label.Text)
                ? $"{kind} status."
                : $"{kind} status. {label.Text}";
    }

    public static void ConfigureActionButton(
        Button button)
    {
        button.AutoSize = true;
        button.MinimumSize =
            new Size(
                MinimumButtonWidth,
                MinimumButtonHeight);
        button.Padding =
            new Padding(
                8,
                2,
                8,
                2);
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
