using System.Diagnostics;
using System.Security.Cryptography;

namespace BitKeyBridge;

/// <summary>
/// Runs only AFTER the staged bytes have replaced their intended targets.
/// A successful pre-install smoke test cannot prove that WDAC/EPM, the file
/// system and service identity will accept the installed executable.
/// No recovery keys, LAPS passwords or tokens are read by this probe.
/// </summary>
internal static class InstalledUpdateHealthService
{
    private static readonly TimeSpan MaximumProbeTime = TimeSpan.FromMinutes(2);

    internal static void VerifyInstalledExecutable(
        string fullPath, string verifiedStagedSha256)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Installed health verification requires Windows.");

        ValidateBytes(fullPath, verifiedStagedSha256);

        using var process = new Process();
        var start = new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Inherit rather than pipe output: verbose offline diagnostics
            // must never deadlock on a full stdout/stderr pipe in Session 0.
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };
        start.ArgumentList.Add("--self-test");
        start.ArgumentList.Add("--no-elevation");
        process.StartInfo = start;

        if (!process.Start())
            throw new InvalidOperationException(
                "Newly installed BitKeyBridge did not start its offline health probe.");
        try
        {
            if (!process.WaitForExit((int)MaximumProbeTime.TotalMilliseconds))
                throw new TimeoutException(
                    "Newly installed BitKeyBridge health probe timed out.");
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    "Newly installed BitKeyBridge health probe failed. ExitCode=" +
                    process.ExitCode + ". Probe output is intentionally not logged.");
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                process.WaitForExit(5000);
            }
        }

        // Catch changes between verifying the installed file and execution.
        ValidateBytes(fullPath, verifiedStagedSha256);
    }

    internal static void ValidateBytes(
        string fullPath, string verifiedStagedSha256)
    {
        if (string.IsNullOrWhiteSpace(verifiedStagedSha256) ||
            verifiedStagedSha256.Length != 64 ||
            !verifiedStagedSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Missing or invalid staged SHA-256 digest.");

        using var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(actual, verifiedStagedSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Post-install executable SHA-256 mismatch. Rollback required.");
    }

    internal static IEnumerable<string> RunOfflineTests(string scratchDirectory)
    {
        var failures = new List<string>();
        var path = Path.Combine(scratchDirectory, "update-health-bytes.bin");
        try
        {
            File.WriteAllText(path, "BitKeyBridge isolated update integrity fixture");
            string expected;
            using (var stream = File.OpenRead(path))
                expected = Convert.ToHexString(SHA256.HashData(stream));
            ValidateBytes(path, expected);
            File.AppendAllText(path, "-modified");
            var rejected = false;
            try { ValidateBytes(path, expected); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected)
                failures.Add("Post-install health probe accepted modified executable bytes.");
        }
        catch (Exception ex)
        {
            failures.Add("Installed update hash verification: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return failures;
    }
}
