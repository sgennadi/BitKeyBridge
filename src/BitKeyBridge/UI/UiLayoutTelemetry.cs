using System.Diagnostics;
using System.Threading;

namespace BitKeyBridge;

/// <summary>
/// Non-secret, allocation-light counters for native WinForms layout work.
/// Only counts/durations; never collects hostname, username, settings,
/// secrets, LDAP/Graph queries, or session identifiers. No I/O on UI thread.
/// </summary>
internal static class UiLayoutTelemetry
{
    // Only a handful of slow/diagnostic phases use a percentile window.
    // Most high-frequency layout counters remain lock-free and unchanged.
    private sealed class Counter
    {
        private const int RecentCapacity = 128;
        private readonly object _recentGate = new();
        private readonly long[]? _recentTicks;
        private int _recentNext;
        private int _recentCount;
        private long _count;
        private long _ticks;
        private long _maxTicks;
        private long _slowCount;

        internal Counter(bool recentPercentiles = false)
        {
            if (recentPercentiles)
                _recentTicks = new long[RecentCapacity];
        }

        internal long Runs => Interlocked.Read(ref _count);

        internal void Record(long started) =>
            RecordDurationTicks(Math.Max(0, Stopwatch.GetTimestamp() - started));

        // Accept elapsed ticks, not user or window identifiers. Also permits
        // deterministic tests of the bounded percentile algorithm.
        internal void RecordDurationTicks(long durationTicks)
        {
            var elapsed = Math.Max(0L, durationTicks);
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _ticks, elapsed);
            if (elapsed > Stopwatch.Frequency / 20) // >50 ms
                Interlocked.Increment(ref _slowCount);
            long previous;
            do
            {
                previous = Interlocked.Read(ref _maxTicks);
                if (elapsed <= previous)
                    break;
            } while (Interlocked.CompareExchange(
                         ref _maxTicks, elapsed, previous) != previous);

            if (_recentTicks is { } ticks)
                lock (_recentGate)
                {
                    ticks[_recentNext] = elapsed;
                    _recentNext = (_recentNext + 1) % ticks.Length;
                    if (_recentCount < ticks.Length)
                        _recentCount++;
                }
        }

        internal (int Samples, double P50, double P95) RecentPercentiles()
        {
            if (_recentTicks is not { } ticks)
                return (0, 0, 0);
            long[] snapshot;
            lock (_recentGate)
            {
                snapshot = new long[_recentCount];
                Array.Copy(ticks, snapshot, _recentCount);
            }
            if (snapshot.Length == 0)
                return (0, 0, 0);

            Array.Sort(snapshot);
            static int Rank(int size, double percentile) =>
                Math.Clamp((int)Math.Ceiling(size * percentile) - 1, 0, size - 1);
            var toMs = 1000.0 / Stopwatch.Frequency;
            return (snapshot.Length,
                snapshot[Rank(snapshot.Length, 0.50)] * toMs,
                snapshot[Rank(snapshot.Length, 0.95)] * toMs);
        }

        internal string Summarize(string label)
        {
            var count = Interlocked.Read(ref _count);
            var total = Interlocked.Read(ref _ticks);
            var max = Interlocked.Read(ref _maxTicks);
            var slow = Interlocked.Read(ref _slowCount);
            var ms = 1000.0 / Stopwatch.Frequency;
            var summary = $"{label}: runs={count}, mean={(count == 0 ? 0 : total * ms / count):F1} ms, " +
                          $"max={max * ms:F1} ms, over 50 ms={slow}";
            if (_recentTicks is null)
                return summary;
            var (samples, p50, p95) = RecentPercentiles();
            return summary + $", recent={samples}/{RecentCapacity}, p50={p50:F1} ms, p95={p95:F1} ms";
        }
    }

    private static readonly Counter MainViewport = new();
    private static readonly Counter ResponsiveForm = new();
    private static readonly Counter HomeCards = new();
    private static readonly Counter ColdResponsiveForm = new();
    private static readonly Counter WarmResponsiveForm = new(recentPercentiles: true);
    // "Initial" means each DpiAwareForm instance is seeing its first
    // full pass. Two cold calls do NOT imply the main window was laid out
    // twice. Separate the main shell from a modal/secondary dialog.
    private static readonly Counter ColdMainForm = new();
    private static readonly Counter ColdOtherForms = new();

    // These phases deliberately capture timing ONLY, never window captions,
    // device/account identifiers, secrets, or dynamically translated text.
    private static readonly Counter DpiThemeInstall = new();
    private static readonly Counter DpiControlDefaults = new();
    private static readonly Counter DpiWorkArea = new();
    private static readonly Counter DpiLayout = new();
    private static readonly Counter DpiLabelWidths = new();
    private static readonly Counter DpiThemeRefresh = new();
    private static readonly Counter ColdLanguageApply = new();
    private static readonly Counter WarmLanguageSwitch = new(recentPercentiles: true);
    // Track transitions by direction; Hebrew's native RTL/TabControl mirror
    // may dominate the delay while EN <-> RU changes don't mirror the HWND.
    private static readonly Counter WarmLanguageToHebrew = new(recentPercentiles: true);
    private static readonly Counter WarmLanguageFromHebrew = new(recentPercentiles: true);
    private static readonly Counter WarmLanguageLtrOnly = new(recentPercentiles: true);
    private static readonly Counter LanguageChrome = new();
    private static readonly Counter LanguageTabDirection = new(recentPercentiles: true);
    private static readonly Counter LanguageTabMirroring = new(recentPercentiles: true);
    // Counters below record only a true EN/RU <-> HE change after startup.
    private static readonly Counter WarmTabDirectionChanges = new();
    private static readonly Counter WarmTabMirrorChanges = new(recentPercentiles: true);
    // Native tab HWND recreation counts only, not the HWND identifiers.
    private static long _mainTabHandleCreated;
    private static long _mainTabHandleDestroyed;
    private static readonly Counter LanguageMenuDirection = new();
    private static readonly Counter LanguageCaptions = new(recentPercentiles: true);
    private static readonly Counter LanguageResume = new(recentPercentiles: true);
    private static long _visibleLabelBatches;
    private static long _visibleLabelWidthChanges;
    private static long _visibleLabelParentReflows;
    // Counts only: never retain header text, font names or device metadata.
    private static long _headerWidthCacheHits;
    private static long _headerWidthCacheMisses;

    internal static void RecordViewport(long start) => MainViewport.Record(start);
    internal static void RecordResponsiveForm(long start) => ResponsiveForm.Record(start);
    internal static void RecordHomeCards(long start) => HomeCards.Record(start);
    internal static void RecordResponsivePhase(
        long start, bool initial, bool mainWindow)
    {
        (initial ? ColdResponsiveForm : WarmResponsiveForm).Record(start);
        if (initial)
            (mainWindow ? ColdMainForm : ColdOtherForms).Record(start);
    }

    internal enum DpiPhase
    {
        ThemeInstall,
        ControlDefaults,
        WorkArea,
        Layout,
        LabelWidths,
        ThemeRefresh
    }

    internal static void RecordDpiPhase(long start, DpiPhase phase)
    {
        switch (phase)
        {
            case DpiPhase.ThemeInstall: DpiThemeInstall.Record(start); break;
            case DpiPhase.ControlDefaults: DpiControlDefaults.Record(start); break;
            case DpiPhase.WorkArea: DpiWorkArea.Record(start); break;
            case DpiPhase.Layout: DpiLayout.Record(start); break;
            case DpiPhase.LabelWidths: DpiLabelWidths.Record(start); break;
            case DpiPhase.ThemeRefresh: DpiThemeRefresh.Record(start); break;
        }
    }
    internal static void RecordLanguageSwitch(
        long start, bool initial, bool previousRtl, bool currentRtl)
    {
        (initial ? ColdLanguageApply : WarmLanguageSwitch).Record(start);
        if (initial)
            return;
        if (!previousRtl && currentRtl)
            WarmLanguageToHebrew.Record(start);
        else if (previousRtl && !currentRtl)
            WarmLanguageFromHebrew.Record(start);
        else if (!previousRtl && !currentRtl)
            WarmLanguageLtrOnly.Record(start);
    }
    internal static void RecordLanguageChrome(long start) => LanguageChrome.Record(start);
    internal static void RecordLanguageTabDirection(
        long start, bool initial, bool changed)
    {
        LanguageTabDirection.Record(start);
        if (!initial && changed)
            WarmTabDirectionChanges.Record(start);
    }
    internal static void RecordLanguageTabMirroring(
        long start, bool initial, bool changed)
    {
        LanguageTabMirroring.Record(start);
        if (!initial && changed)
            WarmTabMirrorChanges.Record(start);
    }
    internal static void RecordMainTabHandleCreated() =>
        Interlocked.Increment(ref _mainTabHandleCreated);
    internal static void RecordMainTabHandleDestroyed() =>
        Interlocked.Increment(ref _mainTabHandleDestroyed);
    internal static void RecordLanguageMenuDirection(long start) =>
        LanguageMenuDirection.Record(start);
    internal static void RecordLanguageCaptions(long start) => LanguageCaptions.Record(start);
    internal static void RecordLanguageResume(long start) => LanguageResume.Record(start);
    internal static void RecordVisibleLabelBatch(int changed, int parentReflows)
    {
        Interlocked.Increment(ref _visibleLabelBatches);
        Interlocked.Add(ref _visibleLabelWidthChanges, Math.Max(0, changed));
        Interlocked.Add(ref _visibleLabelParentReflows, Math.Max(0, parentReflows));
    }
    internal static void RecordHeaderWidthCacheHit() =>
        Interlocked.Increment(ref _headerWidthCacheHits);
    internal static void RecordHeaderWidthCacheMiss() =>
        Interlocked.Increment(ref _headerWidthCacheMisses);

    internal static IEnumerable<string> RunOfflineTests()
    {
        // Exact finite numeric samples only: neither identity nor text.
        var failures = new List<string>();
        var counter = new Counter(recentPercentiles: true);
        static long Ticks(double milliseconds) =>
            (long)Math.Round(milliseconds * Stopwatch.Frequency / 1000.0);

        foreach (var ms in new[] { 4d, 10d, 30d, 90d, 110d })
            counter.RecordDurationTicks(Ticks(ms));
        var first = counter.RecentPercentiles();
        if (first.Samples != 5 || Math.Abs(first.P50 - 30) > 0.1 ||
            Math.Abs(first.P95 - 110) > 0.1)
            failures.Add("Warm UI language p50/p95 computation is incorrect.");

        // After exceeding 128 entries only the last 128 are retained.
        for (var i = 0; i < 256; i++)
            counter.RecordDurationTicks(Ticks(i));
        var latest = counter.RecentPercentiles();
        if (latest.Samples != 128 || Math.Abs(latest.P50 - 191) > 0.1 ||
            Math.Abs(latest.P95 - 249) > 0.1 ||
            !counter.Summarize("Synthetic non-secret phase").Contains(
                "recent=128/128", StringComparison.Ordinal))
            failures.Add("Warm UI latency percentile window is not bounded.");

        var defaultCounter = new Counter();
        defaultCounter.RecordDurationTicks(Ticks(10));
        if (defaultCounter.Summarize("Untracked phase").Contains(
                "p95=", StringComparison.Ordinal))
            failures.Add("High-frequency UI layout counters unexpectedly allocate percentile windows.");

        // Initial apply and Hebrew -> Hebrew must not be counted as a
        // direction change. Test the three categories without user data.
        var priorTo = WarmLanguageToHebrew.Runs;
        var priorFrom = WarmLanguageFromHebrew.Runs;
        var priorLtr = WarmLanguageLtrOnly.Runs;
        var started = Stopwatch.GetTimestamp();
        RecordLanguageSwitch(started, initial: true, previousRtl: false, currentRtl: true);
        RecordLanguageSwitch(started, initial: false, previousRtl: true, currentRtl: true);
        RecordLanguageSwitch(started, initial: false, previousRtl: false, currentRtl: true);
        RecordLanguageSwitch(started, initial: false, previousRtl: true, currentRtl: false);
        RecordLanguageSwitch(started, initial: false, previousRtl: false, currentRtl: false);
        if (WarmLanguageToHebrew.Runs != priorTo + 1 ||
            WarmLanguageFromHebrew.Runs != priorFrom + 1 ||
            WarmLanguageLtrOnly.Runs != priorLtr + 1)
            failures.Add("Warm UI direction transitions were classified incorrectly.");

        // All buckets are UI-direction booleans, never tenant/username/device metadata.
        var report = Report();
        foreach (var expected in new[] {
            "Language changes (warm)", "Language EN/RU -> Hebrew",
            "Language Hebrew -> EN/RU", "Language EN <-> RU",
            "recent=", "p50=", "p95="
        })
            if (!report.Contains(expected, StringComparison.Ordinal))
                failures.Add("UI diagnostics missing quantile phase: " + expected);
        return failures;
    }

    internal static string Report() =>
        "WinForms layout timings for this process (non-secret, since startup). " +
        "Measurements exclude waiting for AD/Graph and native OS painting.\n\n" +
        MainViewport.Summarize("Main window viewport") + "\n" +
        HomeCards.Summarize("Start recovery-card arrangement") + "\n" +
        ResponsiveForm.Summarize("Full DPI/font refresh (all)") + "\n" +
        ColdResponsiveForm.Summarize("Initial DPI/font refresh") + "\n" +
        WarmResponsiveForm.Summarize("Subsequent DPI/font refresh") + "\n" +
        ColdMainForm.Summarize("Initial DPI - main window") + "\n" +
        ColdOtherForms.Summarize("Initial DPI - other windows") + "\n" +
        DpiThemeInstall.Summarize("DPI phase - theme installation") + "\n" +
        DpiControlDefaults.Summarize("DPI phase - control defaults") + "\n" +
        DpiWorkArea.Summarize("DPI phase - work-area adjustment") + "\n" +
        DpiLayout.Summarize("DPI phase - WinForms PerformLayout") + "\n" +
        DpiLabelWidths.Summarize("DPI phase - label widths") + "\n" +
        DpiThemeRefresh.Summarize("DPI phase - theme refresh") + "\n" +
        ColdLanguageApply.Summarize("Initial language labels") + "\n" +
        WarmLanguageSwitch.Summarize("Language changes (warm)") + "\n" +
        WarmLanguageToHebrew.Summarize("Language EN/RU -> Hebrew") + "\n" +
        WarmLanguageFromHebrew.Summarize("Language Hebrew -> EN/RU") + "\n" +
        WarmLanguageLtrOnly.Summarize("Language EN <-> RU") + "\n" +
        LanguageChrome.Summarize("Language chrome / tab mirroring") + "\n" +
        LanguageTabDirection.Summarize("Tab RTL direction setter") + "\n" +
        LanguageTabMirroring.Summarize("Tab native mirror setter") + "\n" +
        WarmTabDirectionChanges.Summarize("Tab RTL setter - actual warm changes") + "\n" +
        WarmTabMirrorChanges.Summarize("Tab native mirror - actual warm changes") + "\n" +
        $"Main tab native handles (lifetime): created={Interlocked.Read(ref _mainTabHandleCreated)}, destroyed={Interlocked.Read(ref _mainTabHandleDestroyed)}\n" +
        LanguageMenuDirection.Summarize("Top menu RTL setter") + "\n" +
        LanguageCaptions.Summarize("Language captions") + "\n" +
        LanguageResume.Summarize("Language layout resume") + "\n" +
        $"Visible-label width batches: batches={Interlocked.Read(ref _visibleLabelBatches)}, " +
        $"widthChanges={Interlocked.Read(ref _visibleLabelWidthChanges)}, " +
        $"parentReflows={Interlocked.Read(ref _visibleLabelParentReflows)}\n" +
        $"Fixed-header width cache: hits={Interlocked.Read(ref _headerWidthCacheHits)}, " +
        $"misses={Interlocked.Read(ref _headerWidthCacheMisses)}\n\n" +
        "If a tab still feels slow but these numbers are low, " +
        "the delay may be a synchronous data operation or native paint. " +
        "This report contains no device names, passwords or tokens.";
}
