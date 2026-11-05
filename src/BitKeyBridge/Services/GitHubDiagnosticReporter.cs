using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitKeyBridge;

/// <summary>
/// Per-user, strictly typed GitHub diagnostic counters. No raw logs, free-form
/// exception messages, stack traces, account/device names, paths, tokens or
/// recovery data are ever collected or uploaded. Upload is OFF by default
/// and requires operator consent plus their own authenticated GitHub CLI.
/// </summary>
internal static class GitHubDiagnosticReporter
{
    private const string Repository = "sgennadi/BitKeyBridge";
    private const string Schema = "BKB-DIAG/v1";
    private const int MaxBuckets = 64;
    private static readonly object Sync = new();
    private static readonly SemaphoreSlim Sending = new(1, 1);
    private static readonly TimeSpan AutomaticInterval = TimeSpan.FromHours(6);
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BitKeyBridge", "github-diagnostic-counters.json");

    private static readonly HashSet<string> AllowedComponents = new(StringComparer.Ordinal)
    {
        "Startup", "Devices", "Graph", "BitLocker", "LAPS", "Update", "UI", "Helpdesk", "Other"
    };
    private static readonly HashSet<string> AllowedFailures = new(StringComparer.Ordinal)
    {
        "AccessDenied", "Timeout", "Network", "NotFound", "InvalidConfig", "Unexpected", "IO"
    };
    private static readonly HashSet<string> AllowedExceptions = new(StringComparer.Ordinal)
    {
        "UnauthorizedAccessException", "SecurityException", "TimeoutException",
        "HttpRequestException", "IOException", "FileNotFoundException",
        "DirectoryNotFoundException", "ArgumentException",
        "InvalidOperationException", "Win32Exception", "Other"
    };
    private static readonly Regex FingerprintPattern = new(
        "^[0-9a-f]{16}$", RegexOptions.CultureInvariant);
    private static readonly Regex SafeGitHubIssueUrl = new(
        @"https://github[.]com/sgennadi/BitKeyBridge/issues/[0-9]+",
        RegexOptions.CultureInvariant);

    internal sealed class Bucket
    {
        public string Component { get; set; } = "Other";
        public string Failure { get; set; } = "Unexpected";
        public string ExceptionType { get; set; } = "Other";
        public string Hresult { get; set; } = "0x00000000";
        public int Count { get; set; } = 1;
        public string Fingerprint { get; set; } = string.Empty;
        public string FirstDay { get; set; } = string.Empty;
        public string LastDay { get; set; } = string.Empty;
    }

    internal sealed class Store
    {
        public bool OptIn { get; set; }
        public DateTimeOffset LastSubmissionUtc { get; set; }
        public List<Bucket> Buckets { get; set; } = [];
    }

    internal static bool OptedIn
    {
        get
        {
            lock (Sync)
                return Load().OptIn;
        }
    }

    internal static bool SetOptIn(bool allowed)
    {
        try
        {
            lock (Sync)
            {
                var store = Load();
                store.OptIn = allowed;
                Save(store);
            }
            if (allowed)
                TryAutomaticSend();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Non-secret report preference failed: " + ex.GetType().Name);
            return false;
        }
    }

    private static bool IsTestProcess()
    {
        // No synthetic offline/UI regression error may leak into a real queue.
        var arguments = Environment.GetCommandLineArgs();
        return arguments.Any(a =>
            a.Equals("--self-test", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--ui-self-test", StringComparison.OrdinalIgnoreCase));
    }

    internal static void Record(string? operation, Exception exception)
    {
        if (exception is OperationCanceledException || IsTestProcess())
            return;

        try
        {
            var newBucket = Classify(operation, exception);
            bool enabled;
            int waiting;
            lock (Sync)
            {
                var store = Load();
                var existing = store.Buckets.FirstOrDefault(b =>
                    string.Equals(b.Fingerprint, newBucket.Fingerprint,
                        StringComparison.Ordinal));
                if (existing is not null)
                {
                    existing.Count = Math.Min(10000, existing.Count + 1);
                    existing.LastDay = newBucket.LastDay;
                }
                else
                {
                    store.Buckets.Add(newBucket);
                    if (store.Buckets.Count > MaxBuckets)
                        store.Buckets.RemoveAt(0);
                }
                Save(store);
                enabled = store.OptIn;
                waiting = store.Buckets.Sum(b => b.Count);
            }
            if (enabled && waiting >= 3)
                TryAutomaticSend();
        }
        catch (Exception ex)
        {
            // Reporting must never change or block an authorized recovery.
            Debug.WriteLine("Non-secret diagnostic recording failed: " +
                ex.GetType().Name);
        }
    }

    private static Bucket Classify(string? operation, Exception exception)
    {
        var name = operation ?? string.Empty;
        // Categorize only by a fixed enumeration. Never retain the original
        // operation, which may contain usernames, paths or device identifiers.
        var component =
            name.Contains("laps", StringComparison.OrdinalIgnoreCase) ? "LAPS" :
            name.Contains("bitlocker", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("recoverykey", StringComparison.OrdinalIgnoreCase) ? "BitLocker" :
            name.Contains("device", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("unified", StringComparison.OrdinalIgnoreCase) ? "Devices" :
            name.Contains("graph", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("cloud", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("entra", StringComparison.OrdinalIgnoreCase) ? "Graph" :
            name.Contains("update", StringComparison.OrdinalIgnoreCase) ? "Update" :
            name.Contains("startup", StringComparison.OrdinalIgnoreCase) ? "Startup" :
            name.Contains("layout", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("window", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ui", StringComparison.OrdinalIgnoreCase) ? "UI" :
            name.Contains("helpdesk", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("support", StringComparison.OrdinalIgnoreCase) ? "Helpdesk" : "Other";

        string failure = exception switch
        {
            UnauthorizedAccessException or SecurityException => "AccessDenied",
            TimeoutException => "Timeout",
            HttpRequestException => "Network",
            FileNotFoundException or DirectoryNotFoundException => "NotFound",
            IOException => "IO",
            ArgumentException => "InvalidConfig",
            _ => "Unexpected"
        };
        var type = exception.GetType().Name;
        if (!AllowedExceptions.Contains(type))
            type = "Other";
        var hresult = "0x" +
            unchecked((uint)exception.HResult).ToString("X8",
                CultureInfo.InvariantCulture);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture);
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(component + "|" + failure + "|" +
                type + "|" + hresult)))[..16].ToLowerInvariant();

        return new Bucket
        {
            Component = component,
            Failure = failure,
            ExceptionType = type,
            Hresult = hresult,
            Count = 1,
            Fingerprint = fingerprint,
            FirstDay = today,
            LastDay = today
        };
    }

    private static bool Valid(Bucket? value) =>
        value is not null &&
        AllowedComponents.Contains(value.Component ?? string.Empty) &&
        AllowedFailures.Contains(value.Failure ?? string.Empty) &&
        AllowedExceptions.Contains(value.ExceptionType ?? string.Empty) &&
        Regex.IsMatch(value.Hresult ?? string.Empty, "^0x[0-9A-F]{8}$") &&
        value.Count is >= 1 and <= 10000 &&
        FingerprintPattern.IsMatch(value.Fingerprint ?? string.Empty) &&
        DateOnly.TryParseExact(value.FirstDay, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
        DateOnly.TryParseExact(value.LastDay, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static Store Load()
    {
        try
        {
            var file = new FileInfo(StorePath);
            if (!file.Exists)
                return new Store();
            if (file.Length > 128 * 1024)
                return new Store();
            var state = JsonSerializer.Deserialize<Store>(
                File.ReadAllText(StorePath)) ?? new Store();
            state.Buckets = (state.Buckets ?? []).Where(Valid)
                .TakeLast(MaxBuckets).ToList();
            return state;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Diagnostic counter state unavailable: " +
                ex.GetType().Name);
            return new Store();
        }
    }

    private static void Save(Store state)
    {
        var folder = Path.GetDirectoryName(StorePath)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder,
            "github-diagnostic-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, StorePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (IOException ex)
            {
                Debug.WriteLine("Diagnostic temp cleanup failed: " +
                    ex.GetType().Name);
            }
        }
    }

    private static object SafePayload(IReadOnlyList<Bucket> buckets)
    {
        var version = UpdateService.GetCurrentVersion().ToString();
        if (!Regex.IsMatch(version, @"^[0-9]{1,3}[.][0-9]{1,3}[.][0-9]{1,3}(-rc[.][0-9]{1,3})?$"))
            version = "0.0.0";
        var arch = RuntimeInformation.ProcessArchitecture.ToString();
        if (arch is not ("X64" or "X86" or "Arm64"))
            arch = "X64";
        return new
        {
            schema = Schema,
            product = "BitKeyBridge",
            version,
            architecture = arch,
            buckets = buckets.Where(Valid).Take(20).Select(b => new
            {
                component = b.Component,
                failure = b.Failure,
                exceptionType = b.ExceptionType,
                hresult = b.Hresult,
                count = b.Count,
                fingerprint = b.Fingerprint,
                firstDay = b.FirstDay,
                lastDay = b.LastDay
            }).ToArray(),
            privacy = "allowlisted-error-counters-only"
        };
    }

    private static string PayloadText(IReadOnlyList<Bucket> buckets) =>
        JsonSerializer.Serialize(SafePayload(buckets),
            new JsonSerializerOptions { WriteIndented = true });

    internal static string Preview()
    {
        lock (Sync)
        {
            var state = Load();
            var message =
                "Only these allowlisted error counters may be published to a GitHub Issue. " +
                "Full logs, exception messages, stack traces, filenames, computer/user names, " +
                "BitLocker/LAPS secrets, Graph tokens and support bundles are NEVER included." +
                Environment.NewLine +
                "Automatic submission: " + (state.OptIn ? "ON" : "OFF") +
                Environment.NewLine +
                "Pending occurrences: " + state.Buckets.Sum(b => b.Count) +
                Environment.NewLine + Environment.NewLine;
            return message + PayloadText(state.Buckets.Take(20).ToArray());
        }
    }

    internal static void TryAutomaticSend()
    {
        if (!OptedIn || IsTestProcess())
            return;
        // Worker cannot prevent normal GUI painting, sign-in or recovery.
        _ = Task.Run(async () => await SendAsync(automatic: true));
    }

    internal static async Task<string> SendAsync(bool automatic)
    {
        await Sending.WaitAsync().ConfigureAwait(false);
        string? temporary = null;
        try
        {
            Store snapshot;
            Bucket[] batch;
            lock (Sync)
            {
                snapshot = Load();
                if (!snapshot.OptIn)
                    return "GitHub diagnostics are disabled. Explicitly enable anonymous error reports first.";
                batch = snapshot.Buckets.Where(Valid).Take(20).ToArray();
                if (batch.Length == 0)
                    return "There are no pending anonymous error summaries.";
                if (automatic &&
                    (batch.Sum(b => b.Count) < 3 ||
                     DateTimeOffset.UtcNow - snapshot.LastSubmissionUtc < AutomaticInterval))
                    return "Automatic reporting is waiting for the next eligible batch.";
            }

            // gh must be installed and already authenticated by the operator.
            // No PAT, password, device login or background sign-in prompt.
            temporary = Path.Combine(Path.GetTempPath(),
                "BKB-diagnostic-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(temporary,
                "```json\n" + PayloadText(batch) + "\n```",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var title = "[BKB-DIAG/v1] " +
                DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                " " + Guid.NewGuid().ToString("N")[..12];
            var start = new ProcessStartInfo("gh")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            foreach (var part in new[]
            {
                "issue", "create", "--repo", Repository,
                "--title", title, "--body-file", temporary
            })
                start.ArgumentList.Add(part);

            using var process = Process.Start(start);
            if (process is null)
                return "GitHub CLI is unavailable. Install gh and run gh auth login.";
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine("GitHub process already exited: " + ex.GetType().Name);
                }
                return "GitHub request timed out. Anonymous counters were retained locally.";
            }
            var output = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                return "GitHub upload failed. Verify that gh is authenticated and can create Issues in " +
                    Repository + ". Anonymous counters were retained locally.";

            var matched = SafeGitHubIssueUrl.Match(output);
            lock (Sync)
            {
                var current = Load();
                foreach (var sent in batch)
                {
                    var pending = current.Buckets.FirstOrDefault(
                        b => b.Fingerprint == sent.Fingerprint);
                    if (pending is null)
                        continue;
                    pending.Count -= sent.Count;
                    if (pending.Count <= 0)
                        current.Buckets.Remove(pending);
                }
                current.LastSubmissionUtc = DateTimeOffset.UtcNow;
                Save(current);
            }
            return matched.Success ?
                "Anonymous error counters submitted: " + matched.Value :
                "GitHub accepted the diagnostic Issue. Anonymous counters were cleared locally.";
        }
        catch (Win32Exception)
        {
            return "GitHub CLI (gh) is missing or cannot run. Install gh and sign in explicitly.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GitHub non-secret submission failed: " +
                ex.GetType().Name);
            return "GitHub diagnostic submission failed (" +
                ex.GetType().Name + "). Counters were retained locally.";
        }
        finally
        {
            if (temporary is not null)
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine("GitHub temporary body cleanup failed: " +
                        ex.GetType().Name);
                }
            }
            Sending.Release();
        }
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var ex = new InvalidOperationException(
            "SECRET_MACHINE password=DO_NOT_UPLOAD C:\\Users\\Secret");
        var bucket = Classify("Devices SECRET_MACHINE", ex);
        var payload = PayloadText([bucket]);
        if (payload.Contains("SECRET_MACHINE", StringComparison.Ordinal) ||
            payload.Contains("DO_NOT_UPLOAD", StringComparison.Ordinal) ||
            payload.Contains("Users\\Secret", StringComparison.Ordinal) ||
            !payload.Contains("\"component\": \"Devices\"", StringComparison.Ordinal) ||
            !payload.Contains("\"privacy\": \"allowlisted-error-counters-only\"",
                StringComparison.Ordinal))
            errors.Add("GitHub diagnostic body contained free-form exception data or lost allowlist.");
        if (!Valid(bucket) ||
            !Regex.IsMatch(bucket.Fingerprint, "^[0-9a-f]{16}$"))
            errors.Add("Anonymous error fingerprint schema invalid.");
        if (Classify("Laps", new UnauthorizedAccessException("user@tenant.test")).Failure !=
            "AccessDenied")
            errors.Add("Diagnostic AccessDenied categorization failed.");
        if (new Store().OptIn)
            errors.Add("GitHub diagnostic automatic upload must default to opt-out.");
        return errors;
    }
}
