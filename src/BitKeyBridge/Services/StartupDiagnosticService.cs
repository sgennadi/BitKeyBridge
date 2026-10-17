using System.Runtime.InteropServices;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Non-elevating, secret-free startup checks. Runs before machine configuration
/// is parsed or migrated, so a denied machine policy cannot suppress diagnosis.
/// No directory authentication, broker event, network request or ACL change.
/// </summary>
internal static class StartupDiagnosticService
{
    private sealed record Finding(string Check, string Status, string Explanation);

    internal static int Run()
    {
        var checks = new List<Finding>();
        var token = "Unavailable";
        try
        {
            token = SecurityContext.IsAdministrator() ? "Elevated" : "StandardOrFiltered";
        }
        catch (Exception)
        {
            // Some endpoint controls restrict token inspection. Unknown is never
            // interpreted as elevated or authorization for a secret read.
        }

        if (!OperatingSystem.IsWindows())
            checks.Add(new Finding("OperatingSystem", "Unsupported", "Windows is required."));
        else
            checks.Add(new Finding("OperatingSystem", "Windows", "This process started successfully."));

        // Probe only whether the file can be opened for READ access.
        // Do not deserialize, copy, log or emit any configuration contents.
        try
        {
            using var stream = new FileStream(
                AppPaths.AppSettingsFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            checks.Add(new Finding("MachineConfiguration", "Readable",
                "File read access exists. Schema, audit policy and delegation were not validated."));
        }
        catch (FileNotFoundException)
        {
            checks.Add(new Finding("MachineConfiguration", "Missing",
                "No machine configuration is present; ask an administrator to provision it."));
        }
        catch (DirectoryNotFoundException)
        {
            checks.Add(new Finding("MachineConfiguration", "Missing",
                "Machine configuration directory is absent or unavailable."));
        }
        catch (UnauthorizedAccessException)
        {
            checks.Add(new Finding("MachineConfiguration", "Denied",
                "The current token cannot open machine configuration for read. Repair only the required read permission; never grant write access."));
        }
        catch (System.Security.SecurityException)
        {
            checks.Add(new Finding("MachineConfiguration", "Denied",
                "Windows security policy blocked read access; contact the endpoint security administrator."));
        }
        catch (IOException)
        {
            checks.Add(new Finding("MachineConfiguration", "Unavailable",
                "An I/O error prevented a read-access check; no configuration values were opened."));
        }
        catch (Exception)
        {
            checks.Add(new Finding("MachineConfiguration", "Unavailable",
                "A local error prevented a read-access check; no configuration values were emitted."));
        }

        // File.Exists is only a hint; it may return false when access is denied.
        var log = Path.Combine(AppPaths.LocalConfigDirectory, "Logs", "startup-errors.log");
        checks.Add(new Finding("UserStartupLog",
            File.Exists(log) ? "Visible" : "NotVisible",
            "If the GUI exits, review %LOCALAPPDATA%\\BitKeyBridge\\Logs\\startup-errors.log. " +
            "Absence does not prove that endpoint protection allowed the EXE to start."));

        var report = new
        {
            Component = "BitKeyBridge",
            Version = UpdateService.GetCurrentVersion().ToString(),
            Mode = "NonElevatingStartupDiagnostics",
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            WindowsToken = token,
            Checks = checks,
            ImportantLimit =
                "The diagnostic is not authorization to reveal BitLocker/LAPS secrets. " +
                "It cannot run or identify the rule if CyberArk EPM, AppLocker or WDAC blocks process creation. " +
                "No security controls are disabled or bypassed."
        };
        Console.WriteLine(JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
