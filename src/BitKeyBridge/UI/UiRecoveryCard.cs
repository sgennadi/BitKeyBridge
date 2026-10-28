using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

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
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var stroke = Math.Max(1f, DeviceDpi / (float)UiStyle.BaselineDpi);
        using var path = UiRecoveryGraphics.RoundRect(
            UiRecoveryGraphics.InsideStrokeBounds(Size, stroke),
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
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var stroke = Math.Max(1f, DeviceDpi / (float)UiStyle.BaselineDpi);
            using var path = UiRecoveryGraphics.RoundRect(
                UiRecoveryGraphics.InsideStrokeBounds(Size, stroke),
                UiStyle.RecoveryCardCornerRadius * DeviceDpi / (float)UiStyle.BaselineDpi);
            using var pen = new Pen(
                SystemInformation.HighContrast ? SystemColors.WindowText : UiStyle.RecoveryCardBorder,
                stroke);
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
    // Draw pill-shaped backgrounds with GDI+ rather than binary Region
    // clipping, which loses subpixel edge coverage at 125%-200% DPI.

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
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoEllipsis = false;
        UseMnemonic = false;
        Font = UiStyle.CreateEmphasisFont(10F);
        TextAlign = ContentAlignment.MiddleCenter;
        Padding = new Padding(14, 6, 14, 6);
        Margin = new Padding(0, UiStyle.ControlGap, 0, UiStyle.SectionGap);
        RefreshPalette();
        DpiChangedAfterParent += (_, _) => Invalidate();
    }

    private void RefreshPalette()
    {
        var pair = UiStyle.ResolveRecoveryBadgeColors(_kind);
        BackColor = pair.Background;
        ForeColor = pair.Foreground;
        AccessibleDescription = _kind + " recovery availability.";
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (SystemInformation.HighContrast || Width < 3 || Height < 3)
        {
            base.OnPaintBackground(e);
            return;
        }

        var parent = Parent;
        while (parent is not null && parent.BackColor.A < 255)
            parent = parent.Parent;
        e.Graphics.Clear(parent?.BackColor ?? UiStyle.RecoveryCardBackground);
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var path = UiRecoveryGraphics.RoundRect(
                new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f),
                Height / 2f);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.FillPath(fill, path);
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }
}

internal static class UiRecoveryGraphics
{
    // A 1-2 pixel stroke centered on an unadjusted client edge is clipped
    // by GDI+ at the window boundary; the corner then looks unfinished.
    // Keep the entire antialiased stroke within the physical client pixels.
    internal static RectangleF InsideStrokeBounds(
        Size size, float stroke, float topInset = 0f)
    {
        var inset = Math.Max(1f, stroke * 0.5f + 0.5f);
        var topEdge = Math.Max(inset, topInset);
        return new RectangleF(inset, topEdge,
            Math.Max(0f, size.Width - 2f * inset),
            Math.Max(0f, size.Height - topEdge - inset));
    }

    private sealed class RoundedButtonChrome
    {
        internal float LogicalRadius;
        internal Color BorderColor;
    }

    private static readonly ConditionalWeakTable<Button, RoundedButtonChrome>
        RoundedButtons = new();

    // Keep native Button keyboard, focus, accessible name, text and image
    // drawing, but replace binary Region clipping with an antialiased GDI+
    // corner mask. Set up once per button; subsequent updates only change
    // radius/border colors and never attach duplicate Paint event handlers.
    internal static void ConfigureRoundedButton(
        Button button, float logicalRadius, Color borderColor)
    {
        if (!RoundedButtons.TryGetValue(button, out var chrome))
        {
            chrome = new RoundedButtonChrome();
            RoundedButtons.Add(button, chrome);
            var state = chrome;
            button.Paint += (_, e) => PaintRoundedButton(button, e.Graphics, state);
        }
        chrome.LogicalRadius = logicalRadius;
        chrome.BorderColor = borderColor;
        button.Invalidate();
    }

    private static void PaintRoundedButton(
        Button button, Graphics graphics, RoundedButtonChrome chrome)
    {
        if (button.IsDisposed || SystemInformation.HighContrast ||
            button.Width <= 4 || button.Height <= 4)
            return;

        var oldState = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var radius = chrome.LogicalRadius *
                Math.Max(UiStyle.BaselineDpi, button.DeviceDpi) /
                (float)UiStyle.BaselineDpi;

            // The native FlatStyle painter draws a rectangular background,
            // including accessible text, glyphs and focus cues. Paint only
            // the pixels outside the rounded surface with the parent color,
            // avoiding the jagged, 1-bit clipping of Control.Region.
            using var rounded = RoundRect(
                new RectangleF(0.5f, 0.5f,
                    button.Width - 1f, button.Height - 1f), radius);
            using var corners = new GraphicsPath(FillMode.Alternate);
            corners.AddRectangle(new RectangleF(
                0, 0, button.Width, button.Height));
            corners.AddPath(rounded, false);
            var parent = button.Parent;
            while (parent is not null && parent.BackColor.A < 255)
                parent = parent.Parent;
            var background = parent?.BackColor ?? UiStyle.ModernCanvas;
            using var cornerBrush = new SolidBrush(background);
            graphics.FillPath(cornerBrush, corners);

            if (chrome.BorderColor != Color.Empty)
            {
                var stroke = Math.Max(1f, button.DeviceDpi /
                    (float)UiStyle.BaselineDpi);
                using var outline = RoundRect(
                    InsideStrokeBounds(button.Size, stroke), radius);
                using var pen = new Pen(chrome.BorderColor, stroke);
                graphics.DrawPath(pen, outline);
            }
        }
        finally
        {
            graphics.Restore(oldState);
        }
    }

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
