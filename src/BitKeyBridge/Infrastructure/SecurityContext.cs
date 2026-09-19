using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace BitKeyBridge;

public static class SecurityContext
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool RelaunchElevated(string[] args)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return false;
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        try
        {
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

}

public static class ConsoleHelper
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    public static void EnsureConsole()
    {
        if (!AttachConsole(AttachParentProcess)) AllocConsole();
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch { }
    }
}
