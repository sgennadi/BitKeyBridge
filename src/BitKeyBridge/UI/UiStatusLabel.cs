using System.Drawing.Drawing2D;

namespace BitKeyBridge;

public sealed class UiStatusLabel : Label
{
    internal UiStatusKind StatusKind =
        UiStatusKind.Neutral;

    // Extra space between the label's existing left chrome and its glyph.
    // Controls in different nested panels can share one visible icon column
    // without relocating their borders, backgrounds or other controls.
    private int _glyphColumnInset;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int GlyphColumnInset
    {
        get => _glyphColumnInset;
        set
        {
            var next = Math.Max(0, value);
            if (next == _glyphColumnInset)
                return;
            _glyphColumnInset = next;
            // The status may wrap one line earlier after indentation.
            // Ask its layout container to remeasure before repainting.
            Parent?.PerformLayout(this, nameof(GlyphColumnInset));
            Invalidate();
        }
    }

    internal int GlyphBaseLeftInset =>
        (BorderStyle == BorderStyle.None ? 0 : 1) + Padding.Left;

    internal int GlyphLeftInset => GlyphBaseLeftInset + GlyphColumnInset;

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
            _glyphColumnInset +
            (icon?.Width ?? 0) +
            gap;

        var verticalChrome =
            Padding.Vertical +
            border;

        // TableLayoutPanel often asks for an unconstrained preferred
        // size (0 width). Use MaximumSize when present, so an AutoSize
        // status grows vertically instead of overflowing horizontally.
        var availableWidth =
            proposedSize.Width > 0
                ? proposedSize.Width
                : 4096;

        if (MaximumSize.Width > 0)
        {
            availableWidth = Math.Min(
                availableWidth,
                MaximumSize.Width);
        }

        var availableTextWidth =
            Math.Max(
                1,
                availableWidth -
                horizontalChrome);

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

        preferredWidth = Math.Min(
            preferredWidth,
            availableWidth);

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
        if (BorderStyle == BorderStyle.FixedSingle &&
            !SystemInformation.HighContrast && Width > 4 && Height > 4)
        {
            // Native ControlPaint draws a square border which is clipped by
            // a 1-bit rounded Region. Render the complete rounded outline
            // in one GDI+ pass instead, keeping its stroke inside bounds.
            var parent = Parent;
            while (parent is not null && parent.BackColor.A < 255)
                parent = parent.Parent;
            e.Graphics.Clear(parent?.BackColor ?? UiStyle.ModernCanvas);
            var state = e.Graphics.Save();
            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                var stroke = Math.Max(1f, DeviceDpi /
                    (float)UiStyle.BaselineDpi);
                using var path = UiRecoveryGraphics.RoundRect(
                    UiRecoveryGraphics.InsideStrokeBounds(Size, stroke),
                    UiStyle.ModernSmallRadius * DeviceDpi /
                    (float)UiStyle.BaselineDpi);
                using var fill = new SolidBrush(BackColor);
                using var border = new Pen(UiStyle.ModernBorder, stroke);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }
        else
        {
            e.Graphics.Clear(BackColor);
            DrawBorder(e.Graphics);
        }

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
                Padding.Left +
                _glyphColumnInset,
                content.Y +
                Padding.Top,
                Math.Max(
                    0,
                    content.Width -
                    Padding.Horizontal -
                    _glyphColumnInset),
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

        UiStyle.RefreshStatusLabel(
            this);
    }

    protected override void OnSystemColorsChanged(
        EventArgs e)
    {
        base.OnSystemColorsChanged(
            e);

        UiStyle.RefreshStatusLabel(
            this);
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
                    SystemInformation.HighContrast
                        ? SystemColors.WindowText
                        : SystemColors.ControlDark,
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
