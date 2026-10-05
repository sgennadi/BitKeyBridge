namespace BitKeyBridge;

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

                if (width > 0 &&
                    child.Width != width)
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
