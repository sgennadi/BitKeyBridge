using System.Drawing.Drawing2D;

namespace BitKeyBridge;

/// <summary>
/// Small NI Package Creator-inspired brand banner for the Start workspace.
/// Uses the same navy-to-blue gradient and green wave, while keeping actual
/// text in native Label controls for clear DPI fonts and EN/RU/HE translation.
/// This banner is decorative only: no device, account or recovery content.
/// </summary>
internal sealed class UiNipkgBrandBanner : Panel
{
    internal const string IntroCaption =
        "Connect once, search once, then view BitLocker and LAPS side by side. " +
        "Search reads metadata only — secrets are still read on demand.";

    internal Label Heading { get; }
    internal Label Tagline { get; }
    private bool _compactHeight;

    // Keep the brand title and its accessible description at short heights.
    // The decorative tagline, not any status or recovery control, yields
    // vertical room to the three operational panes.
    internal void SetCompactHeight(bool compact)
    {
        if (_compactHeight == compact)
            return;
        _compactHeight = compact;
        SuspendLayout();
        try
        {
            Tagline.Visible = !compact;
            Padding = compact
                ? new Padding(72, 8, 16, 8)
                : new Padding(80, 12, 20, 14);
            Margin = new Padding(0, 0, 0,
                compact ? UiStyle.ControlGap / 2 : UiStyle.SectionGap);
            AccessibleDescription = compact ? IntroCaption : string.Empty;
        }
        finally
        {
            ResumeLayout(true);
        }
        Invalidate();
    }

    internal UiNipkgBrandBanner()
    {
        Name = "HomeNipkgBrandBanner";
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = UiStyle.ModernCanvas;
        Margin = new Padding(0, 0, 0, UiStyle.SectionGap);
        Padding = new Padding(80, 12, 20, 14);
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        var content = new TableLayoutPanel
        {
            Name = "HomeBrandCaptionLayout",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Heading = new Label
        {
            Name = "HomeBrandTitle",
            Text = "BitKeyBridge",
            Font = UiStyle.CreatePageTitleFont(),
            ForeColor = SystemInformation.HighContrast
                ? SystemColors.WindowText : Color.White,
            BackColor = Color.Transparent,
            AccessibleRole = AccessibleRole.StaticText,
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, 0, 0, 3)
        };
        Tagline = new Label
        {
            Name = "HomeBrandTagline",
            Text = IntroCaption,
            Font = UiStyle.CreateBodyFont(10f),
            ForeColor = SystemInformation.HighContrast
                ? SystemColors.WindowText : Color.FromArgb(221, 239, 255),
            BackColor = Color.Transparent,
            AccessibleRole = AccessibleRole.StaticText,
            AutoSize = true,
            AutoEllipsis = false,
            UseMnemonic = false,
            MaximumSize = new Size(650, 0),
            Margin = Padding.Empty
        };

        content.Controls.Add(Heading, 0, 0);
        content.Controls.Add(Tagline, 0, 1);
        Controls.Add(content);
        SizeChanged += (_, _) => UpdateTaglineWrapWidth();
    }

    internal void SetCaptionDirection(bool rtl)
    {
        var direction = rtl ? RightToLeft.Yes : RightToLeft.No;
        var alignment = rtl
            ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
        if (Tagline.RightToLeft != direction)
            Tagline.RightToLeft = direction;
        if (Tagline.TextAlign != alignment)
            Tagline.TextAlign = alignment;
        // AutoSize labels need table-cell anchoring as well as TextAlign:
        // right-align Hebrew without mirroring the entire header HWND.
        var anchor = rtl ? AnchorStyles.Right : AnchorStyles.Left;
        if (Tagline.Anchor != anchor)
            Tagline.Anchor = anchor;
    }

    private void UpdateTaglineWrapWidth()
    {
        if (IsDisposed || ClientSize.Width < 160)
            return;
        var available = Math.Max(140, ClientSize.Width - Padding.Horizontal - 8);
        if (Tagline.MaximumSize.Width != available)
            Tagline.MaximumSize = new Size(available, 0);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SystemInformation.HighContrast
            ? SystemColors.Window
            : Parent?.BackColor ?? UiStyle.ModernCanvas);
        if (SystemInformation.HighContrast || Width < 5 || Height < 5)
            return;

        var previous = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var dpi = Math.Max(1f, DeviceDpi / (float)UiStyle.BaselineDpi);
            using var rounded = UiRecoveryGraphics.RoundRect(
                UiRecoveryGraphics.InsideStrokeBounds(Size, 1f),
                16f * dpi);
            using var gradient = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height),
                UiStyle.BrandNavy, UiStyle.BrandBlue,
                LinearGradientMode.Horizontal);
            g.FillPath(gradient, rounded);

            g.SetClip(rounded);
            // Decorative blue and green waves from the NI Creator look.
            // They are drawn behind ordinary native caption labels.
            using (var blueWave = new GraphicsPath())
            {
                blueWave.AddBezier(
                    new PointF(Width - 340f * dpi, Height),
                    new PointF(Width - 210f * dpi, Height - 65f * dpi),
                    new PointF(Width - 118f * dpi, Height - 44f * dpi),
                    new PointF(Width, Height - 24f * dpi));
                blueWave.AddLine(Width, Height - 24f * dpi, Width, Height);
                blueWave.CloseFigure();
                using var blue = new SolidBrush(Color.FromArgb(65, 37, 191, 255));
                g.FillPath(blue, blueWave);
            }
            using (var greenWave = new GraphicsPath())
            {
                greenWave.AddBezier(
                    new PointF(Width - 220f * dpi, Height),
                    new PointF(Width - 138f * dpi, Height - 59f * dpi),
                    new PointF(Width - 66f * dpi, Height - 54f * dpi),
                    new PointF(Width, Height - 68f * dpi));
                greenWave.AddLine(Width, Height - 68f * dpi, Width, Height);
                greenWave.CloseFigure();
                using var green = new SolidBrush(Color.FromArgb(105, 34, 211, 128));
                g.FillPath(green, greenWave);
            }

            // Simple antialiased vector shield; no bitmap assets or secrets.
            var middle = Height / 2f;
            var iconX = 22f * dpi;
            var size = 42f * dpi;
            using (var tile = UiRecoveryGraphics.RoundRect(
                new RectangleF(iconX, middle - size / 2f, size, size),
                11f * dpi))
            using (var tileBrush = new SolidBrush(Color.FromArgb(65, 255, 255, 255)))
                g.FillPath(tileBrush, tile);
            using var shield = new GraphicsPath();
            shield.AddLines(new[]
            {
                new PointF(iconX + 12f * dpi, middle - 11f * dpi),
                new PointF(iconX + 21f * dpi, middle - 15f * dpi),
                new PointF(iconX + 30f * dpi, middle - 11f * dpi),
                new PointF(iconX + 29f * dpi, middle + 3f * dpi),
                new PointF(iconX + 21f * dpi, middle + 13f * dpi),
                new PointF(iconX + 13f * dpi, middle + 3f * dpi)
            });
            shield.CloseFigure();
            using var outline = new Pen(Color.White, 2f * dpi)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawPath(outline, shield);
        }
        finally
        {
            g.Restore(previous);
        }
    }
}
