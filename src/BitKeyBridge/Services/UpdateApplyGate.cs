namespace BitKeyBridge;

/// <summary>
/// Exclusive cross-process gate for the actual executable replacement.
/// The scheduler's download/staging mutex protects a different phase and
/// cannot substitute for locking the privileged apply/rollback transaction.
/// </summary>
internal sealed class UpdateApplyGate : IDisposable
{
    private const string GlobalName = @"Global\BitKeyBridge-UpdateApply";
    private readonly Mutex _mutex;
    private bool _owned;

    private UpdateApplyGate(Mutex mutex)
    {
        _mutex = mutex;
        _owned = true;
    }

    internal static UpdateApplyGate? TryAcquire(
        string? testOnlyName = null,
        TimeSpan? timeout = null)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(false, testOnlyName ?? GlobalName);
            bool acquired;
            try
            {
                acquired = mutex.WaitOne(timeout ?? TimeSpan.FromSeconds(2));
            }
            catch (AbandonedMutexException)
            {
                // A crashed previous updater has already released the OS
                // mutex. Reverify plan, hashes and machine policy normally.
                acquired = true;
            }
            if (!acquired)
            {
                mutex.Dispose();
                return null;
            }
            return new UpdateApplyGate(mutex);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or
                                   WaitHandleCannotBeOpenedException or
                                   IOException)
        {
            mutex?.Dispose();
            // Never fall back to unprotected concurrent replacement.
            WindowsEventLogService.TryWrite(
                "BitKeyBridge update apply mutex cannot be acquired: " +
                DiagnosticRedaction.Sanitize(ex.Message),
                EventLogSeverity.Warning, 4194, "Update");
            return null;
        }
    }

    public void Dispose()
    {
        if (_owned)
        {
            _owned = false;
            try { _mutex.ReleaseMutex(); }
            finally { _mutex.Dispose(); }
        }
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        if (!OperatingSystem.IsWindows())
            return errors;

        // Never collide with an active production update during --self-test.
        var name = @"Local\BitKeyBridge-ApplySelfTest-" +
                   Guid.NewGuid().ToString("N");
        try
        {
            using var first = TryAcquire(name, TimeSpan.Zero);
            if (first is null)
            {
                errors.Add("Exclusive updater gate could not be created.");
                return errors;
            }

            // Mutexes are recursive ON THE SAME THREAD. The second contender
            // must run on a different thread to exercise real exclusion.
            var contender = Task.Run(() =>
            {
                using var second = TryAcquire(
                    name, TimeSpan.FromMilliseconds(150));
                return second is null;
            });
            if (!contender.Wait(TimeSpan.FromSeconds(3)) ||
                !contender.Result)
                errors.Add("Concurrent update helper acquired an occupied apply mutex.");
        }
        catch (Exception ex)
        {
            errors.Add("Exclusive updater gate: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return errors;
    }
}
