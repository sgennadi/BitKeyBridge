using System.Drawing;
using System.Windows.Forms;

namespace BitKeyBridge;

/// <summary>
/// Bounded cache of text measurements for fixed, catalogued UI headers.
/// Stores no user data or fonts and lives only as long as its MainForm.
/// Font properties and DPI form the cache key so RDP/HiDPI changes remeasure.
/// </summary>
internal sealed class UiHeaderWidthCache
{
    internal enum HeaderKind
    {
        DataGrid,
        ListView
    }

    private readonly record struct MeasurementKey(
        string Caption, string FontName, float FontSize, FontStyle Style,
        GraphicsUnit Unit, byte CharacterSet, bool Vertical, int Dpi,
        HeaderKind Kind);

    private const int DefaultMaxMeasurements = 512;
    private readonly int _capacity;
    private readonly Dictionary<MeasurementKey, int> _widths = new();
    private readonly Queue<MeasurementKey> _insertionOrder = new();

    internal UiHeaderWidthCache(int capacity = DefaultMaxMeasurements)
    {
        if (capacity < 1 || capacity > DefaultMaxMeasurements)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    internal int CacheHits { get; private set; }
    internal int CacheMisses { get; private set; }
    internal int EntryCount => _widths.Count;
    internal int Capacity => _capacity;

    internal int Measure(
        string sourceEnglish, string localizedCaption, Font font, int dpi,
        HeaderKind kind)
    {
        // Only immutable catalog keys discovered from fixed column captions.
        // Never cache a device name, SID, account, recovery key or live result.
        if (!UiStaticCaptionCatalog.Contains(sourceEnglish))
            throw new ArgumentException(
                "Header measurement requires a fixed UI caption.",
                nameof(sourceEnglish));

        ArgumentNullException.ThrowIfNull(localizedCaption);
        ArgumentNullException.ThrowIfNull(font);
        var key = new MeasurementKey(
            localizedCaption, font.Name, font.Size, font.Style, font.Unit,
            font.GdiCharSet, font.GdiVerticalFont, dpi, kind);
        if (_widths.TryGetValue(key, out var cached))
        {
            CacheHits++;
            UiLayoutTelemetry.RecordHeaderWidthCacheHit();
            return cached;
        }

        var width = kind switch
        {
            HeaderKind.DataGrid => TextRenderer.MeasureText(
                localizedCaption, font, new Size(4096, 1024),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix).Width +
                UiStyle.ScaleLogicalPixels(26, dpi),
            _ => TextRenderer.MeasureText(localizedCaption, font).Width +
                UiStyle.ScaleLogicalPixels(18, dpi)
        };

        if (_widths.Count >= _capacity)
        {
            var oldest = _insertionOrder.Dequeue();
            _widths.Remove(oldest);
        }
        _widths.Add(key, width);
        _insertionOrder.Enqueue(key);
        CacheMisses++;
        UiLayoutTelemetry.RecordHeaderWidthCacheMiss();
        return width;
    }
}
