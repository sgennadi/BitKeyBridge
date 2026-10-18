using System.Runtime.InteropServices;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Read-only status for GUI and CLI. Does not check the network, install
/// packages, expose recovery secrets, or relax managed update restrictions.
/// </summary>
internal sealed class UpdateCenterSnapshot
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string LatestCheckedVersion { get; set; } = "Not checked";
    public string LatestCheckedTag { get; set; } = string.Empty;
    public DateTime? LastCheckUtc { get; set; }
    public bool CachedUpdateAvailable { get; set; }
    public string CheckError { get; set; } = string.Empty;
    public string TrustMode { get; set; } = string.Empty;
    public bool AutomaticChecksEnabled { get; set; }
    public bool AutomaticInstallConfigured { get; set; }
    public bool AutomaticInstallationPermitted { get; set; }
    public bool RollbackOrFailureHold { get; set; }
    public string InstallationState { get; set; } = string.Empty;
    public string LastUpdateAction { get; set; } = "None recorded";
    public string LastUpdateResult { get; set; } = string.Empty;
    public DateTime? LastUpdateUtc { get; set; }
    public DateTime? LastSuccessfulUpdateUtc { get; set; }
    public string LastSuccessfulVersion { get; set; } = string.Empty;
    public DateTime? LastFailedUpdateUtc { get; set; }
    public string LastFailureSummary { get; set; } = string.Empty;
    public ScheduledTaskSnapshot Task { get; set; } = new();
}

internal sealed class ScheduledTaskSnapshot
{
    public string Status { get; set; } = "Not checked";
    public string NextRunLocal { get; set; } = string.Empty;
    public string LastRunLocal { get; set; } = string.Empty;
    public string LastResult { get; set; } = string.Empty;
}

internal static class UpdateCenterStatusService
{
    internal static UpdateCenterSnapshot Capture(
        AppConfig config, bool includeScheduledTask = true)
    {
        var hold = UpdateRollbackHoldService.IsActiveForCurrentBuild();
        var permitted = config.CheckForUpdatesOnStart &&
            config.AutoInstallUpdatesOnStart &&
            !hold &&
            UpdateTrustPolicy.CanInstallUnattended(config);
        var result = new UpdateCenterSnapshot
        {
            CurrentVersion = UpdateService.GetCurrentVersion().ToString(),
            Channel = config.AllowPrereleaseUpdates ? "Prerelease (opt-in)" : "Stable",
            TrustMode = UpdateTrustPolicy.DescribeMode(config),
            AutomaticChecksEnabled = config.CheckForUpdatesOnStart,
            AutomaticInstallConfigured = config.AutoInstallUpdatesOnStart,
            AutomaticInstallationPermitted = permitted,
            RollbackOrFailureHold = hold,
            InstallationState = ResolveInstallationState(config, hold, permitted),
            Task = includeScheduledTask ? ReadScheduledTask() : new ScheduledTaskSnapshot()
        };

        try
        {
            var info = JsonStore.Read<UpdateInfo>(AppPaths.UpdateStatusFile);
            if (info is not null)
            {
                result.LatestCheckedVersion = string.IsNullOrWhiteSpace(info.LatestVersion)
                    ? "Unknown" : info.LatestVersion;
                result.LatestCheckedTag = info.ReleaseTag;
                result.LastCheckUtc = info.CheckedAtUtc;
                result.CachedUpdateAvailable = info.UpdateAvailable;
                result.CheckError = DiagnosticRedaction.Sanitize(info.Error);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   JsonException or System.Security.SecurityException)
        {
            result.CheckError = "Cached update status is unreadable (" +
                DiagnosticRedaction.Sanitize(ex.GetType().Name) + ").";
        }

        var history = new UpdateHistoryService().Read(200);
        var last = history.FirstOrDefault(x =>
            x.Action is "Install" or "Rollback" or "RollbackRequested");
        if (last is not null)
        {
            result.LastUpdateAction = last.Action +
                (string.IsNullOrWhiteSpace(last.ToVersion)
                    ? string.Empty : " " + last.ToVersion);
            result.LastUpdateResult = last.Result;
            result.LastUpdateUtc = last.TimestampUtc;
        }

        var successful = history.FirstOrDefault(x =>
            (x.Action is "Install" or "Rollback") &&
            x.Result == "Success");
        if (successful is not null)
        {
            result.LastSuccessfulUpdateUtc = successful.TimestampUtc;
            result.LastSuccessfulVersion = successful.ToVersion;
        }
        var failed = history.FirstOrDefault(x =>
            (x.Action is "Install" or "Rollback") &&
            x.Result == "Failed");
        if (failed is not null)
        {
            result.LastFailedUpdateUtc = failed.TimestampUtc;
            var sanitized = DiagnosticRedaction.Sanitize(failed.Details);
            result.LastFailureSummary = sanitized.Length > 240
                ? sanitized[..240] + "..." : sanitized;
        }
        return result;
    }

    internal static string ResolveInstallationState(
        AppConfig config, bool hold, bool permitted)
    {
        if (!config.CheckForUpdatesOnStart)
            return "Paused: automatic checks are disabled.";
        if (!config.AutoInstallUpdatesOnStart)
            return "Paused: automatic installation is disabled.";
        if (hold)
            return "Blocked: rollback/failure hold. Investigate before explicit Resume.";
        if (!permitted)
            return "Blocked: trusted publisher/unsigned consent or machine policy.";
        return "Enabled: automatic installation is permitted by active policy.";
    }

    private static ScheduledTaskSnapshot ReadScheduledTask()
    {
        var status = new ScheduledTaskSnapshot();
        if (!OperatingSystem.IsWindows())
        {
            status.Status = "Windows only";
            return status;
        }

        object? scheduler = null;
        object? folder = null;
        object? registered = null;
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            if (type is null)
                throw new InvalidOperationException("Task Scheduler COM unavailable.");
            scheduler = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Task Scheduler COM initialization failed.");
            ((dynamic)scheduler).Connect();
            folder = ((dynamic)scheduler).GetFolder(@"\");
            try
            {
                registered = ((dynamic)folder).GetTask(ScheduledUpdateService.TaskName);
            }
            catch (COMException ex) when ((uint)ex.HResult is 0x80070002u or 0x8004130Fu)
            {
                status.Status = "Not installed";
                return status;
            }

            // "Installed" must not imply that an unexpected task action is
            // safe: a different executable running as SYSTEM is material.
            dynamic definition = ((dynamic)registered).Definition;
            dynamic principal = definition.Principal;
            dynamic actions = definition.Actions;
            var user = (string)principal.UserId;
            var highest = Convert.ToInt32(principal.RunLevel) == 1;
            var actionCount = Convert.ToInt32(actions.Count);
            string actionPath = string.Empty;
            string actionArgs = string.Empty;
            if (actionCount == 1)
            {
                dynamic action = actions.Item(1);
                actionPath = (string)action.Path;
                actionArgs = (string)action.Arguments;
            }
            status.Status = IsExpectedBackgroundTask(
                user, highest, actionCount, actionPath, actionArgs)
                ? "Installed (verified SYSTEM action)"
                : "WARNING: task principal/action differs from installed BitKeyBridge";
            var next = (DateTime)((dynamic)registered).NextRunTime;
            var last = (DateTime)((dynamic)registered).LastRunTime;
            status.NextRunLocal = FormatTaskDate(next);
            status.LastRunLocal = FormatTaskDate(last);
            if (!string.IsNullOrEmpty(status.LastRunLocal))
                status.LastResult = "0x" +
                    unchecked((uint)(int)((dynamic)registered).LastTaskResult).ToString("X8");
        }
        catch (Exception ex)
        {
            // Task visibility is diagnostic only; no assumption of "missing"
            // is made for access denied or an unavailable scheduler.
            status.Status = "Unavailable (" +
                DiagnosticRedaction.Sanitize(ex.GetType().Name) + ")";
        }
        finally
        {
            ReleaseCom(registered);
            ReleaseCom(folder);
            ReleaseCom(scheduler);
        }
        return status;
    }

    internal static bool IsExpectedBackgroundTask(
        string? principal, bool highest, int actionCount,
        string? executable, string? arguments)
    {
        var account = principal?.Trim() ?? string.Empty;
        var isSystem = account.Equals("S-1-5-18", StringComparison.OrdinalIgnoreCase) ||
            account.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
            account.Equals(@"NT AUTHORITY\SYSTEM", StringComparison.OrdinalIgnoreCase);
        if (!isSystem || !highest || actionCount != 1 ||
            !string.Equals(arguments?.Trim(), "--scheduled-update --no-elevation",
                StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(executable?.Trim().Trim('"') ?? string.Empty),
                Path.GetFullPath(AppPaths.ServiceExecutable),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or
                                   PathTooLongException)
        {
            return false;
        }
    }

    private static string FormatTaskDate(DateTime value) =>
        value.Year < 2000 ? string.Empty : value.ToString("yyyy-MM-dd HH:mm");

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.ReleaseComObject(value);
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var config = new AppConfig();
        if (!ResolveInstallationState(config, false, false).Contains("disabled", StringComparison.Ordinal))
            errors.Add("Update Center did not report disabled installation.");
        config.AutoInstallUpdatesOnStart = true;
        config.CheckForUpdatesOnStart = true;
        if (!ResolveInstallationState(config, true, false).Contains("hold", StringComparison.Ordinal) ||
            !ResolveInstallationState(config, false, false).Contains("Blocked", StringComparison.Ordinal) ||
            !ResolveInstallationState(config, false, true).Contains("Enabled", StringComparison.Ordinal))
            errors.Add("Update Center missed rollback/policy blocking precedence.");
        if (!IsExpectedBackgroundTask(
                "S-1-5-18", true, 1,
                AppPaths.ServiceExecutable, "--scheduled-update --no-elevation") ||
            IsExpectedBackgroundTask(
                "BUILTIN\\Administrators", true, 1,
                AppPaths.ServiceExecutable, "--scheduled-update --no-elevation") ||
            IsExpectedBackgroundTask(
                "SYSTEM", false, 1,
                AppPaths.ServiceExecutable, "--scheduled-update --no-elevation") ||
            IsExpectedBackgroundTask(
                "SYSTEM", true, 1,
                "C:\\Temp\\FakeUpdater.exe", "--scheduled-update --no-elevation") ||
            IsExpectedBackgroundTask(
                "SYSTEM", true, 1,
                AppPaths.ServiceExecutable, "--scheduled-update --no-elevation --extra") ||
            IsExpectedBackgroundTask(
                "SYSTEM", true, 2,
                AppPaths.ServiceExecutable, "--scheduled-update --no-elevation"))
            errors.Add("Update Center accepted a modified/unsafe scheduled task.");
        return errors;
    }
}
