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

    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint FileTypeDisk = 1;
    private const uint FileTypePipe = 3;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(nint hFile);

    private static bool HasRedirectedHandle(int id)
    {
        var handle = GetStdHandle(id);
        if (handle == nint.Zero || handle == new nint(-1))
            return false;
        // WinExe may inherit a caller-owned file/pipe for stdout or stderr.
        // AttachConsole can replace that handle with the parent console.
        var type = GetFileType(handle);
        return type is FileTypeDisk or FileTypePipe;
    }

    public static void EnsureConsole()
    {
        // Retain redirected output for --version, startup JSON and preflight.
        // Interactive launches still attach to CMD or allocate a console.
        if (!HasRedirectedHandle(StdOutputHandle) &&
            !HasRedirectedHandle(StdErrorHandle))
        {
            if (!AttachConsole(AttachParentProcess))
                AllocConsole();
        }
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Console encoding/output initialization failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }
}
