using System.Diagnostics;
using System.Security.Principal;

namespace BitKeyBridge;

/// <summary>
/// Optional independently scheduled automatic updater. This is NOT a network
/// service or recovery secret broker. The only published binary allowed as a
/// task action is the machine-installed Program Files executable.
/// Signed/unsigned trust and rollback gates are checked again at apply time.
/// </summary>
internal static class ScheduledUpdateService
{
    internal const string TaskName = "BitKeyBridge-AutoUpdate";

    internal static bool IsEligible(AppConfig config) =>
        config.CheckForUpdatesOnStart &&
        config.AutoInstallUpdatesOnStart &&
        UpdateTrustPolicy.CanInstallUnattended(config);

    internal static int InstallTask(string[] args)
    {
        if (!OperatingSystem.IsWindows())
            return Fail("Windows Task Scheduler is required.", 2);
        if (!SecurityContext.IsAdministrator())
            return Fail("Administrator rights are required to register a SYSTEM task.", 5);
        if (!IsMachineInstalledExecutable())
            return Fail("Install BitKeyBridge to Program Files before registering a SYSTEM task.", 5);

        AppConfig config;
        try { config = ConfigService.LoadAppConfig(); }
        catch (Exception ex) { return Fail("Machine policy unavailable: " + DiagnosticRedaction.Sanitize(ex.Message), 12); }
        if (!IsEligible(config))
            return Fail("Enable auto-install with explicitly configured trusted or unsigned official-repository consent before scheduling.", 3);

        var hour = 3;
        var at = Array.FindIndex(args, x =>
            x.Equals("--update-task-hour", StringComparison.OrdinalIgnoreCase));
        if (at >= 0 && (at + 1 >= args.Length ||
                        !int.TryParse(args[at + 1], out hour) ||
                        hour < 0 || hour > 23))
            return Fail("--update-task-hour must be an integer from 0 through 23.", 2);

        // Absolute Program Files path, validated at registration and every run.
        // Task Scheduler does not get a user-controlled executable or command.
        var action = "\"" + AppPaths.ServiceExecutable + "\" --scheduled-update --no-elevation";
        return InvokeScheduler(
            ["/Create", "/F", "/TN", TaskName, "/SC", "DAILY",
             "/ST", hour.ToString("00") + ":00", "/RU", "SYSTEM",
             "/RL", "HIGHEST", "/TR", action],
            "Background auto-update scheduled daily at " + hour.ToString("00") + ":00.");
    }

    internal static int RemoveTask()
    {
        if (!OperatingSystem.IsWindows())
            return Fail("Windows Task Scheduler is required.", 2);
        if (!SecurityContext.IsAdministrator())
            return Fail("Administrator rights are required to remove the SYSTEM task.", 5);
        return InvokeScheduler(["/Delete", "/F", "/TN", TaskName],
            "Background auto-update task removed.");
    }

    internal static int ShowTaskStatus()
    {
        if (!OperatingSystem.IsWindows())
            return Fail("Windows Task Scheduler is required.", 2);
        return InvokeScheduler(["/Query", "/TN", TaskName, "/FO", "LIST"],
            "Task query completed.");
    }

    internal static async Task<int> RunOnceAsync(AppConfig config)
    {
        if (!OperatingSystem.IsWindows())
            return Fail("Windows is required.", 2);
        if (!SecurityContext.IsAdministrator() ||
            !WindowsIdentity.GetCurrent().IsSystem ||
            !IsMachineInstalledExecutable())
            return Fail("Background updates require the machine-installed Program Files executable running as LocalSystem.", 5);
        if (!IsEligible(config))
            return Fail("Background auto-install is disabled or the active trust policy is not approved.", 3);

        if (UpdateRollbackHoldService.IsActiveForCurrentBuild())
        {
            Console.WriteLine("Background auto-install paused after a deliberate rollback.");
            return 0;
        }

        // Mutexes are thread-affine. The downloader awaits network and disk I/O,
        // so the continuation can run on another thread. Use the cross-process
        // semaphore guard instead; it remains owned until the async method exits.
        using var gate = ScheduledUpdateGate.TryAcquire();
        if (gate is null)
        {
            Console.WriteLine("Another automatic BitKeyBridge updater is running.");
            return 0;
        }

        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            using var updater = new UpdateService(config);
            var info = await updater.CheckAsync(limit.Token);
            if (!string.IsNullOrWhiteSpace(info.Error))
                return Fail("Update check failed: " + DiagnosticRedaction.Sanitize(info.Error), 1);
            if (!info.UpdateAvailable)
            {
                Console.WriteLine("BitKeyBridge already current.");
                return 0;
            }

            // Re-read machine policy immediately before staging. An update
            // must not continue if the admin revoked unsigned consent.
            var active = ConfigService.LoadAppConfig();
            if (!IsEligible(active) || UpdateRollbackHoldService.IsActiveForCurrentBuild())
                return Fail("Background update canceled: machine policy or rollback hold changed.", 3);
            if (!string.Equals(active.UpdateRepository, config.UpdateRepository,
                    StringComparison.OrdinalIgnoreCase))
                return Fail("Background update canceled: configured repository changed.", 3);

            var staged = await updater.PrepareAsync(info, limit.Token);
            active = ConfigService.LoadAppConfig();
            if (!IsEligible(active) || UpdateRollbackHoldService.IsActiveForCurrentBuild())
                return Fail("Background update canceled: machine approval was revoked during staging.", 3);

            // SYSTEM task launches helper in the same privileged identity;
            // the helper rechecks plan hash, executable hash, active policy,
            // version and Authenticode configuration before any replacement.
            updater.LaunchApplyHelper(staged, restartGui: false,
                automatic: true, scheduledBackground: true);
            WindowsEventLogService.TryWrite(
                "BitKeyBridge background update verified and staged for " + info.LatestVersion,
                EventLogSeverity.Information, 4104, "Update");
            Console.WriteLine("Verified automatic update " + info.LatestVersion + " staged.");
            return 0;
        }
        catch (Exception ex)
        {
            var reason = DiagnosticRedaction.Sanitize(ex.Message);
            WindowsEventLogService.TryWrite("BitKeyBridge scheduled update failed: " + reason,
                EventLogSeverity.Warning, 4105, "Update");
            return Fail("Background update failed: " + reason, 1);
        }
    }

    private static bool IsMachineInstalledExecutable()
    {
        var path = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(path) &&
               string.Equals(Path.GetFullPath(path),
                   Path.GetFullPath(AppPaths.ServiceExecutable),
                   StringComparison.OrdinalIgnoreCase) &&
               File.Exists(AppPaths.ServiceExecutable);
    }

    private static int InvokeScheduler(string[] arguments, string successMessage)
    {
        try
        {
            using var process = new Process();
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var part in arguments)
                start.ArgumentList.Add(part);
            process.StartInfo = start;
            if (!process.Start())
                return Fail("Windows Task Scheduler process did not start.", 1);
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                return Fail("Task Scheduler command timed out.", 1);
            }
            if (process.ExitCode != 0)
                return Fail("Task Scheduler rejected the request: " +
                    DiagnosticRedaction.Sanitize(stderr), process.ExitCode);
            if (!string.IsNullOrWhiteSpace(stdout))
                Console.WriteLine(DiagnosticRedaction.Sanitize(stdout));
            Console.WriteLine(successMessage);
            return 0;
        }
        catch (Exception ex)
        {
            return Fail("Task Scheduler error: " + DiagnosticRedaction.Sanitize(ex.Message), 1);
        }
    }

    private static int Fail(string message, int exitCode)
    {
        Console.Error.WriteLine(message);
        return exitCode;
    }
}
