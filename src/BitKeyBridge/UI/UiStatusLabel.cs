namespace BitKeyBridge;

public sealed class UiStatusLabel : Label
{
    public override Size GetPreferredSize(
        Size proposedSize)
    {
        var border =
            BorderStyle ==
            BorderStyle.None
                ? 0
                : 2;

        var icon =
            Image;
        var gap =
            icon is null
                ? 0
                : ScaleLogical(
                    UiStyle.ControlGap);

        var horizontalChrome =
            Padding.Horizontal +
            border +
            (icon?.Width ?? 0) +
            gap;

        var verticalChrome =
            Padding.Vertical +
            border;

        var availableTextWidth =
            proposedSize.Width > 0
                ? Math.Max(
                    1,
                    proposedSize.Width -
                    horizontalChrome)
                : 4096;

        var measured =
            TextRenderer.MeasureText(
                Text ?? string.Empty,
                Font,
                new Size(
                    availableTextWidth,
                    16384),
                TextFormatFlags.Left |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

        var preferredWidth =
            horizontalChrome +
            measured.Width;

        if (proposedSize.Width > 0)
        {
            preferredWidth =
                Math.Min(
                    preferredWidth,
                    proposedSize.Width);
        }

        var preferredHeight =
            verticalChrome +
            Math.Max(
                icon?.Height ?? 0,
                measured.Height);

        return new Size(
            Math.Max(
                1,
                preferredWidth),
            Math.Max(
                1,
                preferredHeight));
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        e.Graphics.Clear(
            BackColor);

        DrawBorder(
            e.Graphics);

        var borderInset =
            BorderStyle ==
            BorderStyle.None
                ? 0
                : 1;

        var content =
            Rectangle.Inflate(
                ClientRectangle,
                -borderInset,
                -borderInset);

        content =
            new Rectangle(
                content.X +
                Padding.Left,
                content.Y +
                Padding.Top,
                Math.Max(
                    0,
                    content.Width -
                    Padding.Horizontal),
                Math.Max(
                    0,
                    content.Height -
                    Padding.Vertical));

        var icon =
            Image;

        var gap =
            icon is null
                ? 0
                : ScaleLogical(
                    UiStyle.ControlGap);

        var iconWidth =
            icon?.Width ?? 0;
        var iconHeight =
            icon?.Height ?? 0;

        if (icon is not null &&
            content.Width > 0 &&
            content.Height > 0)
        {
            var iconY =
                content.Y +
                Math.Max(
                    0,
                    (content.Height -
                     iconHeight) /
                    2);

            e.Graphics.DrawImage(
                icon,
                new Rectangle(
                    content.X,
                    iconY,
                    iconWidth,
                    iconHeight));
        }

        var textX =
            content.X +
            iconWidth +
            gap;

        var textRectangle =
            new Rectangle(
                textX,
                content.Y,
                Math.Max(
                    0,
                    content.Right -
                    textX),
                content.Height);

        TextRenderer.DrawText(
            e.Graphics,
            Text ?? string.Empty,
            Font,
            textRectangle,
            ForeColor,
            BackColor,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.WordBreak |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.PreserveGraphicsClipping);
    }

    protected override void OnTextChanged(
        EventArgs e)
    {
        base.OnTextChanged(
            e);

        Invalidate();
    }

    protected override void OnFontChanged(
        EventArgs e)
    {
        base.OnFontChanged(
            e);

        Invalidate();
    }

    protected override void OnPaddingChanged(
        EventArgs e)
    {
        base.OnPaddingChanged(
            e);

        Invalidate();
    }

    protected override void OnForeColorChanged(
        EventArgs e)
    {
        base.OnForeColorChanged(
            e);

        Invalidate();
    }

    protected override void OnBackColorChanged(
        EventArgs e)
    {
        base.OnBackColorChanged(
            e);

        Invalidate();
    }

    private void DrawBorder(
        Graphics graphics)
    {
        switch (BorderStyle)
        {
            case BorderStyle.FixedSingle:
                ControlPaint.DrawBorder(
                    graphics,
                    ClientRectangle,
                    SystemColors.ControlDark,
                    ButtonBorderStyle.Solid);
                break;

            case BorderStyle.Fixed3D:
                ControlPaint.DrawBorder3D(
                    graphics,
                    ClientRectangle,
                    Border3DStyle.Sunken);
                break;
        }
    }

    private int ScaleLogical(
        int value) =>
        Math.Max(
            1,
            (int)Math.Round(
                value *
                DeviceDpi /
                (double)UiStyle.BaselineDpi));
}
