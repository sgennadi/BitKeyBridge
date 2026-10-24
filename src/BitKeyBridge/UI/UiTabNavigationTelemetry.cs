using System.Diagnostics;

namespace BitKeyBridge;

/// <summary>
/// Measures the elapsed time from a user-visible workspace switch until
/// the selected WinForms page has completed its first geometry reflow.
/// A bounded, in-memory sample window contains ONLY fixed page labels and
/// millisecond durations. It never stores devices, usernames or secrets.
/// </summary>
internal static class UiTabNavigationTelemetry
{
    private static readonly string[] Pages =
    [
        "Start", "BitLocker", "LAPS", "Devices",
        "Helpdesk", "Diagnostics", "Settings"
    ];

    private sealed class Window
    {
        private const int Capacity = 128;
        private readonly object _gate = new();
        private readonly double[] _samples = new double[Capacity];
        private int _next;
        private int _size;
        private long _total;

        internal void Add(long started)
        {
            if (started <= 0)
                return;
            var ticks = Math.Max(0L, Stopwatch.GetTimestamp() - started);
            AddMilliseconds(ticks * 1000.0 / Stopwatch.Frequency);
        }

        internal void AddMilliseconds(double milliseconds)
        {
            if (!double.IsFinite(milliseconds) || milliseconds < 0)
                return;

            lock (_gate)
            {
                _samples[_next] = milliseconds;
                _next = (_next + 1) % Capacity;
                if (_size < Capacity)
                    _size++;
                _total++;
            }
        }

        internal (long Count, int SampleSize, double P50, double P95, double Max) Snapshot()
        {
            double[] sorted;
            long total;
            lock (_gate)
            {
                total = _total;
                sorted = new double[_size];
                Array.Copy(_samples, sorted, _size);
            }
            if (sorted.Length == 0)
                return (total, 0, 0, 0, 0);

            Array.Sort(sorted);
            return (total, sorted.Length,
                Percentile(sorted, 0.50), Percentile(sorted, 0.95), sorted[^1]);
        }
    }

    private static readonly Window[] Windows =
        Pages.Select(_ => new Window()).ToArray();
    // Timing only: no account, device, selection contents or secrets.
    private static readonly Window[] SelectionToCallback =
        Pages.Select(_ => new Window()).ToArray();
    private static readonly Window[] ViewportReflow =
        Pages.Select(_ => new Window()).ToArray();
    internal static void RecordDispatch(int pageIndex, long started)
    {
        if ((uint)pageIndex < (uint)SelectionToCallback.Length)
            SelectionToCallback[pageIndex].Add(started);
    }
    internal static void RecordLayout(int pageIndex, long started)
    {
        if ((uint)pageIndex < (uint)ViewportReflow.Length)
            ViewportReflow[pageIndex].Add(started);
    }

    internal static void Record(int pageIndex, long started)
    {
        if ((uint)pageIndex < (uint)Windows.Length)
            Windows[pageIndex].Add(started);
    }

    internal static string Report()
    {
        var lines = new List<string>
        {
            "Workspace switch to layout ready (last 128 switches per page; ms).",
            "Includes the UI dispatch and layout; excludes native painting, AD/Graph requests and RDP display latency."
        };
        for (var index = 0; index < Pages.Length; index++)
        {
            var result = Windows[index].Snapshot();
            if (result.SampleSize == 0)
                continue;
            lines.Add(FormattableString.Invariant(
                $"{Pages[index]}: runs={result.Count}, window={result.SampleSize}, p50={result.P50:F1} ms, p95={result.P95:F1} ms, max={result.Max:F1} ms"));
            var dispatch = SelectionToCallback[index].Snapshot();
            var layout = ViewportReflow[index].Snapshot();
            if (dispatch.SampleSize > 0)
                lines.Add(FormattableString.Invariant(
                    $"{Pages[index]} - selection to UI callback: runs={dispatch.Count}, p50={dispatch.P50:F1} ms, p95={dispatch.P95:F1} ms, max={dispatch.Max:F1} ms"));
            if (layout.SampleSize > 0)
                lines.Add(FormattableString.Invariant(
                    $"{Pages[index]} - viewport reflow: runs={layout.Count}, p50={layout.P50:F1} ms, p95={layout.P95:F1} ms, max={layout.Max:F1} ms"));
        }
        if (lines.Count == 2)
            lines.Add("No completed workspace switches recorded yet.");
        return string.Join(Environment.NewLine, lines);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0)
            return 0;
        var index = Math.Clamp(
            (int)Math.Ceiling(sorted.Length * percentile) - 1,
            0, sorted.Length - 1);
        return sorted[index];
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        var window = new Window();

        // Deterministic nearest-rank percentile calculation.
        foreach (var ms in new[] { 4d, 10d, 30d, 90d, 110d })
            window.AddMilliseconds(ms);
        var first = window.Snapshot();
        if (first.Count != 5 || first.SampleSize != 5 ||
            first.P50 != 30 || first.P95 != 110 || first.Max != 110)
            failures.Add("Workspace navigation p50/p95 computation is incorrect.");

        // The most recent 128 samples survive; older values are discarded.
        for (var i = 0; i < 256; i++)
            window.AddMilliseconds(i);
        var bounded = window.Snapshot();
        if (bounded.Count != 261 || bounded.SampleSize != 128 ||
            bounded.P50 != 191 || bounded.P95 != 249 || bounded.Max != 255)
            failures.Add("Workspace navigation ring window is not bounded.");

        window.AddMilliseconds(-5);
        window.AddMilliseconds(double.NaN);
        window.AddMilliseconds(double.PositiveInfinity);
        if (window.Snapshot().Count != bounded.Count)
            failures.Add("Invalid UI latency values were recorded.");

        if (SelectionToCallback.Length != Pages.Length ||
            ViewportReflow.Length != Pages.Length)
            failures.Add("Navigation phase counters must cover each fixed tab.");

        if (Report().Contains("device=", StringComparison.OrdinalIgnoreCase) ||
            Pages.Distinct(StringComparer.Ordinal).Count() != Pages.Length)
            failures.Add("Workspace telemetry must use fixed, non-secret labels.");

        return failures;
    }
}
