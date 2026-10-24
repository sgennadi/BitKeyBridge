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
    private sealed class Counter
    {
        private long _count;
        private long _ticks;
        private long _maxTicks;
        private long _slowCount;

        internal void Record(long started)
        {
            var elapsed = Math.Max(0, Stopwatch.GetTimestamp() - started);
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
        }

        internal string Summarize(string label)
        {
            var count = Interlocked.Read(ref _count);
            var total = Interlocked.Read(ref _ticks);
            var max = Interlocked.Read(ref _maxTicks);
            var slow = Interlocked.Read(ref _slowCount);
            var ms = 1000.0 / Stopwatch.Frequency;
            return $"{label}: runs={count}, mean={(count == 0 ? 0 : total * ms / count):F1} ms, " +
                   $"max={max * ms:F1} ms, over 50 ms={slow}";
        }
    }

    private static readonly Counter MainViewport = new();
    private static readonly Counter ResponsiveForm = new();
    private static readonly Counter HomeCards = new();
    private static readonly Counter ColdResponsiveForm = new();
    private static readonly Counter WarmResponsiveForm = new();
    private static readonly Counter ColdLanguageApply = new();
    private static readonly Counter WarmLanguageSwitch = new();
    private static readonly Counter LanguageChrome = new();
    private static readonly Counter LanguageTabDirection = new();
    private static readonly Counter LanguageTabMirroring = new();
    private static readonly Counter LanguageMenuDirection = new();
    private static readonly Counter LanguageCaptions = new();
    private static readonly Counter LanguageResume = new();

    internal static void RecordViewport(long start) => MainViewport.Record(start);
    internal static void RecordResponsiveForm(long start) => ResponsiveForm.Record(start);
    internal static void RecordHomeCards(long start) => HomeCards.Record(start);
    internal static void RecordResponsivePhase(long start, bool initial) =>
        (initial ? ColdResponsiveForm : WarmResponsiveForm).Record(start);
    internal static void RecordLanguageSwitch(long start, bool initial) =>
        (initial ? ColdLanguageApply : WarmLanguageSwitch).Record(start);
    internal static void RecordLanguageChrome(long start) => LanguageChrome.Record(start);
    internal static void RecordLanguageTabDirection(long start) =>
        LanguageTabDirection.Record(start);
    internal static void RecordLanguageTabMirroring(long start) =>
        LanguageTabMirroring.Record(start);
    internal static void RecordLanguageMenuDirection(long start) =>
        LanguageMenuDirection.Record(start);
    internal static void RecordLanguageCaptions(long start) => LanguageCaptions.Record(start);
    internal static void RecordLanguageResume(long start) => LanguageResume.Record(start);

    internal static string Report() =>
        "WinForms layout timings for this process (non-secret, since startup). " +
        "Measurements exclude waiting for AD/Graph and native OS painting.\n\n" +
        MainViewport.Summarize("Main window viewport") + "\n" +
        HomeCards.Summarize("Start recovery-card arrangement") + "\n" +
        ResponsiveForm.Summarize("Full DPI/font refresh (all)") + "\n" +
        ColdResponsiveForm.Summarize("Initial DPI/font refresh") + "\n" +
        WarmResponsiveForm.Summarize("Subsequent DPI/font refresh") + "\n" +
        ColdLanguageApply.Summarize("Initial language labels") + "\n" +
        WarmLanguageSwitch.Summarize("Language changes (warm)") + "\n" +
        LanguageChrome.Summarize("Language chrome / tab mirroring") + "\n" +
        LanguageTabDirection.Summarize("Tab RTL direction setter") + "\n" +
        LanguageTabMirroring.Summarize("Tab native mirror setter") + "\n" +
        LanguageMenuDirection.Summarize("Top menu RTL setter") + "\n" +
        LanguageCaptions.Summarize("Language captions") + "\n" +
        LanguageResume.Summarize("Language layout resume") + "\n\n" +
        "If a tab still feels slow but these numbers are low, " +
        "the delay may be a synchronous data operation or native paint. " +
        "This report contains no device names, passwords or tokens.";
}
