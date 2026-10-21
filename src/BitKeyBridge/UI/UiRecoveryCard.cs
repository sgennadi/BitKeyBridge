using System.Drawing.Drawing2D;

namespace BitKeyBridge;

public enum RecoveryGlyphKind
{
    Shield,
    Key,
    Eye,
    Copy,
    History
}

/// <summary>
/// Rounded, DPI-aware recovery surface. The actual data and controls remain
/// standard accessible WinForms children, not text painted onto a bitmap.
/// </summary>
public sealed class UiRecoveryCard : Panel
{
    public UiRecoveryCard()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        BackColor = UiStyle.RecoveryCardBackground;
        Padding = new Padding(UiStyle.RecoveryCardPadding);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        if (Width <= 1 || Height <= 1)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiRecoveryGraphics.RoundRect(
            new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f),
            UiStyle.RecoveryCardCornerRadius * DeviceDpi / (float)UiStyle.BaselineDpi);
        using var fill = new SolidBrush(
            SystemInformation.HighContrast ? SystemColors.Window : UiStyle.RecoveryCardBackground);
        e.Graphics.FillPath(fill, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width > 1 && Height > 1)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = UiRecoveryGraphics.RoundRect(
                new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f),
                UiStyle.RecoveryCardCornerRadius * DeviceDpi / (float)UiStyle.BaselineDpi);
            using var pen = new Pen(
                SystemInformation.HighContrast ? SystemColors.WindowText : UiStyle.RecoveryCardBorder,
                Math.Max(1f, DeviceDpi / (float)UiStyle.BaselineDpi));
            e.Graphics.DrawPath(pen, path);
        }

        base.OnPaint(e);
    }
}

/// <summary>Drawn SVG-like glyphs stay crisp on 100-200% display scaling.</summary>
public sealed class UiRecoveryGlyph : Control
{
    public RecoveryGlyphKind Glyph { get; }

    public UiRecoveryGlyph(RecoveryGlyphKind glyph)
    {
        Glyph = glyph;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(UiStyle.RecoveryTitleGlyphSize, UiStyle.RecoveryTitleGlyphSize);
        Margin = Padding.Empty;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = glyph == RecoveryGlyphKind.Shield ? "BitLocker" : glyph.ToString();
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        UiRecoveryGraphics.DrawGlyph(e.Graphics, Glyph, ClientRectangle,
            SystemInformation.HighContrast ? SystemColors.WindowText : UiStyle.RecoveryCardForeground);
    }
}

public sealed class UiRecoveryBadge : Label
{
    private UiStatusKind _kind = UiStatusKind.Neutral;
    private Size _lastRoundedSize;
    private int _lastRoundedDpi;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public UiStatusKind Kind
    {
        get => _kind;
        set
        {
            var colors = UiStyle.ResolveRecoveryBadgeColors(value);
            if (_kind == value && BackColor == colors.Background &&
                ForeColor == colors.Foreground)
                return;
            _kind = value;
            RefreshPalette();
        }
    }

    public UiRecoveryBadge()
    {
        AutoSize = true;
        AutoEllipsis = false;
        UseMnemonic = false;
        Font = UiStyle.CreateEmphasisFont(10F);
        TextAlign = ContentAlignment.MiddleCenter;
        Padding = new Padding(14, 6, 14, 6);
        Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.SectionGap);
        RefreshPalette();
        SizeChanged += (_, _) => RoundCorners();
        DpiChangedAfterParent += (_, _) => RoundCorners();
    }

    private void RefreshPalette()
    {
        var pair = UiStyle.ResolveRecoveryBadgeColors(_kind);
        BackColor = pair.Background;
        ForeColor = pair.Foreground;
        AccessibleDescription = _kind + " recovery availability.";
        RoundCorners();
    }

    private void RoundCorners()
    {
        if (Width < 2 || Height < 2)
            return;
        if (_lastRoundedSize == Size && _lastRoundedDpi == DeviceDpi)
            return;
        using var path = UiRecoveryGraphics.RoundRect(
            new RectangleF(0, 0, Width, Height), Height / 2f);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
        _lastRoundedSize = Size;
        _lastRoundedDpi = DeviceDpi;
    }
}

internal static class UiRecoveryGraphics
{
    public static GraphicsPath RoundRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1f, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Bitmap CreateGlyphImage(RecoveryGlyphKind glyph, int pixels, Color color)
    {
        var image = new Bitmap(pixels, pixels);
        using var graphics = Graphics.FromImage(image);
        DrawGlyph(graphics, glyph, new Rectangle(0, 0, pixels, pixels), color);
        return image;
    }

    public static void DrawGlyph(Graphics g, RecoveryGlyphKind glyph, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var old = g.SmoothingMode;
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(bounds.Width, bounds.Height);
        g.TranslateTransform(bounds.X + (bounds.Width - size) / 2f,
                             bounds.Y + (bounds.Height - size) / 2f);
        g.ScaleTransform(size / 32f, size / 32f);
        using var pen = new Pen(color, 2.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (glyph)
        {
            case RecoveryGlyphKind.Shield:
                using (var shield = new GraphicsPath())
                {
                    shield.AddLines([
                        new PointF(16, 2.5f), new PointF(27, 7), new PointF(27, 17),
                        new PointF(24, 24), new PointF(16, 29),
                        new PointF(8, 24), new PointF(5, 17), new PointF(5, 7),
                        new PointF(16, 2.5f)
                    ]);
                    g.DrawPath(pen, shield);
                }
                g.DrawLines(pen, [new PointF(11, 16), new PointF(15, 20), new PointF(22, 12)]);
                break;
            case RecoveryGlyphKind.Key:
                g.DrawEllipse(pen, 15, 3, 13, 13);
                g.DrawLine(pen, 17, 15, 4, 28);
                g.DrawLines(pen, [new PointF(4, 28), new PointF(4, 21), new PointF(10, 21)]);
                g.DrawLine(pen, 9, 23, 9, 17);
                using (var dot = new SolidBrush(color))
                    g.FillEllipse(dot, 20, 8, 3, 3);
                break;
            case RecoveryGlyphKind.Eye:
                using (var eye = new GraphicsPath())
                {
                    eye.AddBezier(new PointF(2, 16), new PointF(9, 5),
                                  new PointF(23, 5), new PointF(30, 16));
                    eye.AddBezier(new PointF(30, 16), new PointF(23, 27),
                                  new PointF(9, 27), new PointF(2, 16));
                    g.DrawPath(pen, eye);
                }
                g.DrawEllipse(pen, 12, 12, 8, 8);
                break;
            case RecoveryGlyphKind.Copy:
                g.DrawRectangle(pen, 10, 9, 18, 20);
                g.DrawLines(pen, [
                    new PointF(7, 23), new PointF(4, 23), new PointF(4, 4),
                    new PointF(22, 4), new PointF(22, 7)
                ]);
                break;
            case RecoveryGlyphKind.History:
                g.DrawArc(pen, 4, 4, 24, 24, -65, 310);
                g.DrawLines(pen, [new PointF(16, 9), new PointF(16, 16), new PointF(21, 19)]);
                break;
        }
        g.Restore(state);
        g.SmoothingMode = old;
    }
}
