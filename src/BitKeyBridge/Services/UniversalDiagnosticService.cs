using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// BitKeyBridge adapter to the canonical UniversalDiagnostics embedded queue.
/// No GitHub authentication, HTTP calls, external EXE, background upload, raw
/// exception text, paths, machine/user identifiers or recovery secrets.
/// </summary>
internal static class UniversalDiagnosticService
{
    internal const string Project = "sgennadi/BitKeyBridge";
    internal const string ModuleVersion = "0.1.0-rc.1";
    private static readonly object Sync = new();
    private static readonly TimeSpan CaptureCooldown = TimeSpan.FromMinutes(10);
    private static readonly Dictionary<string, DateTime> LastCaptured = new(StringComparer.Ordinal);
    private static bool? _captureEnabled;
    // Revocation generation: scheduled background writes cannot outlive consent.
    private static long _captureConsentRevision;

    private static readonly HashSet<string> Components = new(StringComparer.Ordinal)
    {
        "Startup", "Devices", "Graph", "BitLocker", "LAPS", "Update",
        "UI", "Helpdesk", "Service", "Other"
    };
    private static readonly HashSet<string> ErrorClasses = new(StringComparer.Ordinal)
    {
        "UnauthorizedAccessException", "SecurityException", "TimeoutException",
        "HttpRequestException", "IOException", "FileNotFoundException",
        "DirectoryNotFoundException", "ArgumentException",
        "InvalidOperationException", "Win32Exception", "Other"
    };
    private sealed class Preferences
    {
        public bool CaptureLocally { get; set; }
    }

    private static string PreferencesPath =>
        Path.Combine(AppPaths.LocalConfigDirectory, "universal-diagnostics-preferences.json");

    internal static string LocalQueueDirectory =>
        UniversalDiagnostics.UniversalDiagnosticsEmbedded.QueueDirectory(Project);

    internal static bool LocalCaptureEnabled
    {
        get
        {
            lock (Sync)
            {
                if (_captureEnabled.HasValue)
                    return _captureEnabled.Value;
                try
                {
                    if (File.Exists(PreferencesPath) &&
                        new FileInfo(PreferencesPath).Length <= 4096)
                    {
                        var settings = JsonSerializer.Deserialize<Preferences>(
                            File.ReadAllText(PreferencesPath));
                        _captureEnabled = settings?.CaptureLocally == true;
                    }
                }
                catch (Exception ex) when (ex is IOException or
                    UnauthorizedAccessException or SecurityException or JsonException)
                {
                    Debug.WriteLine("UDX preference load unavailable: " +
                        ex.GetType().Name);
                }
                _captureEnabled ??= false;
                return _captureEnabled.Value;
            }
        }
    }

    internal static bool SetLocalCapture(bool enabled)
    {
        // Do not opt-in silently based on legacy GitHub diagnostics consent.
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppPaths.LocalConfigDirectory);
                var path = PreferencesPath;
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary,
                        JsonSerializer.Serialize(new Preferences { CaptureLocally = enabled }));
                    File.Move(temporary, path, true);
                    _captureEnabled = enabled;
                    ++_captureConsentRevision;
                    LastCaptured.Clear();
                    return true;
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or
            UnauthorizedAccessException or SecurityException)
        {
            Debug.WriteLine("UDX preference save unavailable: " + ex.GetType().Name);
            return false;
        }
    }

    private static bool IsOfflineTest()
    {
        return Environment.GetCommandLineArgs().Any(a =>
            a.Equals("--self-test", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--ui-self-test", StringComparison.OrdinalIgnoreCase));
    }

    private static string Component(string? raw)
    {
        // Never use an arbitrary operation string: it might be a device name.
        if (string.IsNullOrWhiteSpace(raw))
            return "Other";
        var operation = raw.Trim();
        if (Components.Contains(operation))
            return operation;
        if (operation.Contains("Laps", StringComparison.OrdinalIgnoreCase))
            return "LAPS";
        if (operation.Contains("BitLocker", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Recovery", StringComparison.OrdinalIgnoreCase))
            return "BitLocker";
        if (operation.Contains("Graph", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Entra", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Intune", StringComparison.OrdinalIgnoreCase))
            return "Graph";
        if (operation.Contains("Device", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
            return "Devices";
        if (operation.Contains("Update", StringComparison.OrdinalIgnoreCase))
            return "Update";
        if (operation.Contains("Helpdesk", StringComparison.OrdinalIgnoreCase))
            return "Helpdesk";
        if (operation.Contains("Display", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Layout", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Window", StringComparison.OrdinalIgnoreCase))
            return "UI";
        return "Other";
    }

    private static string ErrorType(Exception ex)
    {
        var value = ex.GetType().Name;
        return ErrorClasses.Contains(value) ? value : "Other";
    }

    private static object Event(string component, string errorType, string code,
        string summary)
    {
        return new
        {
            project = Project,
            component,
            version = typeof(UniversalDiagnosticService).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "0.35.0",
            error_type = errorType,
            code,
            summary,
            level = "error"
        };
    }

    private static void WriteEvent(string component, string errorType,
        string code, string summary)
    {
        var payload = JsonSerializer.Serialize(Event(component, errorType, code, summary));
        _ = UniversalDiagnostics.UniversalDiagnosticsEmbedded.Capture(payload);
    }

    private static bool WriteCaptureIfConsented(long expectedRevision,
        string component, string errorType, string code, string summary)
    {
        // Consent changes and the actual disk write share this lock.
        // Disabling local collection revokes pending worker tasks; subsequent
        // opt-in cannot revive work scheduled under an older permission.
        lock (Sync)
        {
            if (_captureEnabled != true ||
                _captureConsentRevision != expectedRevision)
                return false;
            WriteEvent(component, errorType, code, summary);
            return true;
        }
    }

    internal static void TryCapture(string? operation, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (IsOfflineTest() || !LocalCaptureEnabled)
            return;
        var component = Component(operation);
        var errorType = ErrorType(ex);
        var code = "0x" + unchecked((uint)ex.HResult).ToString("X8");
        var key = component + "|" + errorType + "|" + code;
        long consentRevision;
        lock (Sync)
        {
            if (_captureEnabled != true)
                return;
            var now = DateTime.UtcNow;
            if (LastCaptured.TryGetValue(key, out var last) &&
                now - last < CaptureCooldown)
                return;
            if (LastCaptured.Count >= 256)
                LastCaptured.Clear();
            LastCaptured[key] = now;
            consentRevision = _captureConsentRevision;
        }
        // Optional local capture never blocks recovery, sign-in or WinForms painting.
        _ = Task.Run(() =>
        {
            try
            {
                _ = WriteCaptureIfConsented(consentRevision, component, errorType,
                    code, "Classified local application error; no exception text retained.");
            }
            catch (Exception writeError)
            {
                Debug.WriteLine("UDX local capture unavailable: " +
                    writeError.GetType().Name);
            }
        });
    }

    internal static void TryCaptureServiceError()
    {
        // A service log line may contain user/device names; never read it.
        if (IsOfflineTest() || !LocalCaptureEnabled)
            return;
        TryCapture("Service", new InvalidOperationException());
    }

    internal static Task<string> CaptureManualSnapshotAsync()
    {
        // Explicit operator action: records only the report category and app version.
        return Task.Run(() =>
        {
            try
            {
                WriteEvent("Helpdesk", "Other", "MANUAL",
                    "Operator approved a non-secret diagnostic snapshot.");
                return "Local diagnostic event saved. Nothing was sent to GitHub.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UDX manual snapshot failed: " + ex.GetType().Name);
                return "Could not save local diagnostics (" + ex.GetType().Name + ").";
            }
        });
    }

    internal static string Preview()
    {
        var path = LocalQueueDirectory;
        var items = new List<(string Component, string Type, string Code)>();
        try
        {
            if (Directory.Exists(path))
            {
                foreach (var entry in Directory.EnumerateFiles(path, "*.json").Take(500))
                {
                    try
                    {
                        if (new FileInfo(entry).Length > 65536)
                            continue;
                        using var doc = JsonDocument.Parse(File.ReadAllText(entry));
                        var root = doc.RootElement;
                        if (!root.TryGetProperty("event", out var ev) ||
                            ev.GetProperty("schema").GetString() !=
                                UniversalDiagnostics.UniversalDiagnosticsEmbedded.Schema ||
                            ev.GetProperty("project").GetString() != Project)
                            continue;
                        var component = ev.GetProperty("component").GetString() ?? "Other";
                        var errorType = ev.GetProperty("error_type").GetString() ?? "Other";
                        var code = ev.GetProperty("code").GetString() ?? "unknown";
                        if (!root.TryGetProperty("delivery", out var delivery) ||
                            delivery.GetProperty("status").GetString() != "pending" ||
                            !Components.Contains(component) ||
                            !ErrorClasses.Contains(errorType) ||
                            (code != "MANUAL" &&
                             !System.Text.RegularExpressions.Regex.IsMatch(
                                 code, @"^(?:0x[0-9A-Fa-f]{8}|[0-9]{1,6})$",
                                 System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
                            continue;
                        items.Add((component, errorType, code));
                    }
                    catch (Exception ex) when (ex is IOException or
                        UnauthorizedAccessException or JsonException or
                        InvalidOperationException or KeyNotFoundException)
                    {
                        // Ignore corrupt/malformed records; never include their text.
                        Debug.WriteLine("UDX record skipped: " + ex.GetType().Name);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or
            UnauthorizedAccessException or SecurityException)
        {
            return "Local diagnostic queue is not accessible (" + ex.GetType().Name + ").";
        }
        var lines = new List<string>
        {
            "UniversalDiagnostics " + ModuleVersion + " | universal-diagnostics/v1",
            "Project: " + Project,
            "Local capture: " + (LocalCaptureEnabled ? "enabled" : "disabled"),
            "Pending local events scanned: " + items.Count,
            "Automatic uploads: disabled. No GitHub token is stored.",
            "Preview contains only technical categories; original exception text is not copied."
        };
        foreach (var group in items.GroupBy(x => (x.Component, x.Type, x.Code))
                     .OrderByDescending(x => x.Count()).Take(12))
        {
            lines.Add(group.Key.Component + " | " + group.Key.Type + " | " +
                group.Key.Code + " | " + group.Count());
        }
        var legacy = Path.Combine(AppPaths.LocalConfigDirectory,
            "github-diagnostic-counters.json");
        if (File.Exists(legacy))
            lines.Add("Legacy BKB-DIAG queue retained locally for manual migration; not uploaded or deleted.");
        return string.Join(Environment.NewLine, lines);
    }

    internal static IEnumerable<string> RunOfflineTests(string scratch)
    {
        var errors = new List<string>();
        // Do not record synthetic tests in real user's queue.
        var previous = Environment.GetEnvironmentVariable("UDX_STATE_DIR");
        try
        {
            var testRoot = Path.Combine(scratch, "udx-self-test");
            Environment.SetEnvironmentVariable("UDX_STATE_DIR", testRoot);
            var receipt = UniversalDiagnostics.UniversalDiagnosticsEmbedded.Capture(
                JsonSerializer.Serialize(Event("UI", "TimeoutException",
                    "0x80131505", "Synthetic non-secret error")));
            using var doc = JsonDocument.Parse(File.ReadAllText(receipt.Path));
            var ev = doc.RootElement.GetProperty("event");
            var delivery = doc.RootElement.GetProperty("delivery");
            // Cross-language SHA-256 contract must be identical to the
            // canonical Python implementation in the shared repository.
            var signature = string.Join("|", Project.ToLowerInvariant(),
                "ui", "timeoutexception", "0x80131505");
            var expectedFingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(signature))).ToLowerInvariant();
            if (ev.GetProperty("fingerprint").GetString() != expectedFingerprint)
                errors.Add("UniversalDiagnostics .NET/Python fingerprint mismatch.");
            if (ev.GetProperty("schema").GetString() != "universal-diagnostics/v1" ||
                ev.GetProperty("project").GetString() != Project ||
                ev.GetProperty("component").GetString() != "UI" ||
                ev.GetProperty("fingerprint").GetString()?.Length != 64 ||
                delivery.GetProperty("status").GetString() != "pending" ||
                !string.IsNullOrEmpty(delivery.GetProperty("url").GetString()))
                errors.Add("UniversalDiagnostics local contract or delivery status is invalid.");
            if (Component(@"CN=SECRET,DC=internal\host") != "Other" ||
                Component("Get-LAPS Password") != "LAPS" ||
                Component("ConnectMicrosoftGraph") != "Graph")
                errors.Add("UniversalDiagnostics operation classifier is unsafe.");
            // Exercise the canonical embedded redactor with a deliberately
            // hostile sample; it must not survive on disk.
            const string fakeRecovery = "001122-334455-667788-990011-223344-556677-889900-112233";
            const string fakeToken = "TEST_ONLY_SECRET_BEARER_0123456789";
            var probe = UniversalDiagnostics.UniversalDiagnosticsEmbedded.Capture(
                JsonSerializer.Serialize(Event("UI", "Other", "0x80000000",
                    "Authorization: Bearer " + fakeToken + "\n" + fakeRecovery)));
            var probeJson = File.ReadAllText(probe.Path);
            if (probeJson.Contains(fakeRecovery, StringComparison.Ordinal) ||
                probeJson.Contains(fakeToken, StringComparison.Ordinal))
                errors.Add("UniversalDiagnostics embedded redactor wrote a secret to disk.");
            var sensitive = File.ReadAllText(receipt.Path);
            foreach (var forbidden in new[] { "recovery_password", "access_token",
                "computer_name", "tenant_id", "raw_exception", "hostname" })
                if (sensitive.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    errors.Add("UniversalDiagnostics emitted a forbidden field: " + forbidden);

            // No real user preferences or queue are touched in --self-test.
            // Both OFF and subsequent ON must reject writes queued under
            // the prior consent revision.
            bool? priorConsent;
            long queuedRevision;
            lock (Sync)
            {
                priorConsent = _captureEnabled;
                _captureEnabled = true;
                queuedRevision = ++_captureConsentRevision;
                _captureEnabled = false;
                ++_captureConsentRevision;
            }
            try
            {
                var countBefore = Directory.EnumerateFiles(
                    LocalQueueDirectory, "*.json").Count();
                if (WriteCaptureIfConsented(queuedRevision, "UI", "Other",
                    "0x80000001", "Synthetic stale consent"))
                    errors.Add("UniversalDiagnostics accepted a capture after opt-out.");
                lock (Sync)
                {
                    _captureEnabled = true;
                    ++_captureConsentRevision;
                }
                if (WriteCaptureIfConsented(queuedRevision, "UI", "Other",
                    "0x80000001", "Synthetic stale consent"))
                    errors.Add("UniversalDiagnostics accepted a capture after re-consent.");
                if (Directory.EnumerateFiles(
                    LocalQueueDirectory, "*.json").Count() != countBefore)
                    errors.Add("UniversalDiagnostics persisted a stale queued error.");
            }
            finally
            {
                lock (Sync)
                {
                    _captureEnabled = priorConsent;
                    ++_captureConsentRevision;
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add("UniversalDiagnostics embedded runtime self-test failed: " +
                ex.GetType().Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UDX_STATE_DIR", previous);
        }
        return errors;
    }
}
