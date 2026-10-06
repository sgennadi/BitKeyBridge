namespace BitKeyBridge;

internal static class UiStatusGlyphs
{
    private static readonly object Sync =
        new();

    private static readonly Dictionary<
        (UiStatusKind Kind, int Dpi, int Argb),
        Image> Cache =
        new();

    public static Image Get(
        UiStatusKind kind,
        int dpi,
        Color color)
    {
        var normalizedDpi =
            Math.Max(
                UiStyle.BaselineDpi,
                dpi);

        var key =
            (
                kind,
                normalizedDpi,
                color.ToArgb());

        lock (Sync)
        {
            if (Cache.TryGetValue(
                    key,
                    out var existing))
            {
                return existing;
            }

            var created =
                Create(
                    kind,
                    normalizedDpi,
                    color);

            Cache[key] =
                created;

            return created;
        }
    }

    private static Image Create(
        UiStatusKind kind,
        int dpi,
        Color color)
    {
        var size =
            Math.Max(
                14,
                (int)Math.Round(
                    16D *
                    dpi /
                    UiStyle.BaselineDpi));

        var bitmap =
            new Bitmap(
                size,
                size);

        bitmap.SetResolution(
            dpi,
            dpi);

        using var graphics =
            Graphics.FromImage(
                bitmap);

        graphics.SmoothingMode =
            System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode =
            System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        var stroke =
            Math.Max(
                1.5F,
                size /
                10F);

        using var pen =
            new Pen(
                color,
                stroke)
            {
                StartCap =
                    System.Drawing.Drawing2D.LineCap.Round,
                EndCap =
                    System.Drawing.Drawing2D.LineCap.Round,
                LineJoin =
                    System.Drawing.Drawing2D.LineJoin.Round
            };

        using var brush =
            new SolidBrush(
                color);

        var inset =
            stroke;
        var diameter =
            size -
            inset *
            2F;

        switch (kind)
        {
            case UiStatusKind.Success:
                graphics.DrawEllipse(
                    pen,
                    inset,
                    inset,
                    diameter,
                    diameter);

                graphics.DrawLines(
                    pen,
                    [
                        new PointF(
                            size * 0.25F,
                            size * 0.52F),
                        new PointF(
                            size * 0.43F,
                            size * 0.70F),
                        new PointF(
                            size * 0.76F,
                            size * 0.34F)
                    ]);
                break;

            case UiStatusKind.Warning:
                graphics.DrawPolygon(
                    pen,
                    [
                        new PointF(
                            size * 0.50F,
                            size * 0.10F),
                        new PointF(
                            size * 0.90F,
                            size * 0.84F),
                        new PointF(
                            size * 0.10F,
                            size * 0.84F)
                    ]);

                graphics.DrawLine(
                    pen,
                    size * 0.50F,
                    size * 0.34F,
                    size * 0.50F,
                    size * 0.58F);

                graphics.FillEllipse(
                    brush,
                    size * 0.45F,
                    size * 0.68F,
                    size * 0.10F,
                    size * 0.10F);
                break;

            case UiStatusKind.Error:
                graphics.DrawEllipse(
                    pen,
                    inset,
                    inset,
                    diameter,
                    diameter);

                graphics.DrawLine(
                    pen,
                    size * 0.31F,
                    size * 0.31F,
                    size * 0.69F,
                    size * 0.69F);

                graphics.DrawLine(
                    pen,
                    size * 0.69F,
                    size * 0.31F,
                    size * 0.31F,
                    size * 0.69F);
                break;

            case UiStatusKind.Busy:
                graphics.DrawArc(
                    pen,
                    inset,
                    inset,
                    diameter,
                    diameter,
                    -80F,
                    275F);

                graphics.FillEllipse(
                    brush,
                    size * 0.69F,
                    size * 0.14F,
                    size * 0.14F,
                    size * 0.14F);
                break;

            default:
                graphics.DrawEllipse(
                    pen,
                    inset,
                    inset,
                    diameter,
                    diameter);

                graphics.DrawLine(
                    pen,
                    size * 0.50F,
                    size * 0.43F,
                    size * 0.50F,
                    size * 0.72F);

                graphics.FillEllipse(
                    brush,
                    size * 0.44F,
                    size * 0.24F,
                    size * 0.12F,
                    size * 0.12F);
                break;
        }

        return bitmap;
    }
}
