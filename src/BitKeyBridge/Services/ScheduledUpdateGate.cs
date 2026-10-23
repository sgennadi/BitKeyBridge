namespace BitKeyBridge;

/// <summary>
/// Cross-process, asynchronous-safe exclusion for the unattended download and
/// staging phase. A Windows Mutex is thread-affine and MUST NOT be held over
/// an await: its continuation can run on a different thread. A named Semaphore
/// can be released by any thread, and the apply transaction has its own
/// independent exclusive gate.
/// </summary>
internal sealed class ScheduledUpdateGate : IDisposable
{
    private const string Name = @"Global\BitKeyBridge-ScheduledUpdate-Semaphore-v1";
    private Semaphore? _semaphore;

    private ScheduledUpdateGate(Semaphore semaphore) => _semaphore = semaphore;

    internal static ScheduledUpdateGate? TryAcquire(
        string? testOnlyName = null,
        TimeSpan? timeout = null)
    {
        var semaphore = new Semaphore(1, 1, testOnlyName ?? Name);
        try
        {
            if (!semaphore.WaitOne(timeout ?? TimeSpan.FromSeconds(2)))
            {
                semaphore.Dispose();
                return null;
            }

            return new ScheduledUpdateGate(semaphore);
        }
        catch
        {
            semaphore.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        // Both the worker thread and the finalizer path can observe disposal
        // after an async continuation. Release exactly once, regardless of
        // which managed thread ultimately completes the scheduled update.
        var semaphore = Interlocked.Exchange(ref _semaphore, null);
        if (semaphore is null)
            return;

        try { semaphore.Release(); }
        finally { semaphore.Dispose(); }
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        if (!OperatingSystem.IsWindows())
            return errors;

        var name = @"Local\BitKeyBridge-ScheduledUpdate-GateTest-" +
                   Guid.NewGuid().ToString("N");

        try
        {
            using var held = TryAcquire(name, TimeSpan.Zero);
            if (held is null)
            {
                errors.Add("Scheduled updater gate could not be acquired.");
                return errors;
            }

            // The contender must be blocked while the first owner is alive,
            // then be allowed through after disposal on ANOTHER thread.
            var contender = Task.Run(() =>
            {
                using var blocked = TryAcquire(
                    name, TimeSpan.FromMilliseconds(150));
                if (blocked is not null)
                    return false;

                held.Dispose(); // Different thread from the one that acquired it.
                using var acquired = TryAcquire(
                    name, TimeSpan.FromSeconds(1));
                return acquired is not null;
            });

            if (!contender.Wait(TimeSpan.FromSeconds(4)) ||
                !contender.Result)
            {
                errors.Add("Scheduled update gate failed cross-thread release or exclusion.");
            }
        }
        catch (Exception ex)
        {
            errors.Add("Scheduled update cross-thread gate: " +
                       DiagnosticRedaction.Sanitize(ex.Message));
        }

        return errors;
    }
}
