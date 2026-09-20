using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class UpdateService : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;

    public UpdateService(AppConfig config)
    {
        _config = config;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BitKeyBridge/0.4");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = GetCurrentVersion().ToString(),
            Architecture = GetRid(),
            CheckedAtUtc = DateTime.UtcNow
        };

        try
        {
            var repository = NormalizeRepository(_config.UpdateRepository);
            var uri = _config.AllowPrereleaseUpdates
                ? $"https://api.github.com/repos/{repository}/releases?per_page=20"
                : $"https://api.github.com/repos/{repository}/releases/latest";

            using var response = await _http.GetAsync(uri, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"GitHub release check failed ({(int)response.StatusCode}): {body}");

            using var json = JsonDocument.Parse(body);
            JsonElement release;
            if (_config.AllowPrereleaseUpdates)
            {
                if (json.RootElement.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("GitHub returned an unexpected release-list response.");
                var found = json.RootElement.EnumerateArray()
                    .FirstOrDefault(x => !GetBool(x, "draft"));
                if (found.ValueKind == JsonValueKind.Undefined)
                    throw new InvalidOperationException("No GitHub release was found.");
                release = found;
            }
            else
            {
                release = json.RootElement;
            }

            var tag = GetString(release, "tag_name");
            var latest = ParseVersion(tag);
            var current = GetCurrentVersion();
            info.LatestVersion = latest.ToString();
            info.UpdateAvailable = latest > current;
            info.ReleaseUrl = GetString(release, "html_url");
            info.ReleaseNotes = GetString(release, "body");
            if (release.TryGetProperty("published_at", out var published) &&
                DateTime.TryParse(published.GetString(), out var publishedAt))
                info.PublishedAt = publishedAt;

            var assetName = $"BitKeyBridge-{info.Architecture}.zip";
            info.AssetName = assetName;

            if (!release.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("GitHub release contains no assets.");

            JsonElement selected = default;
            JsonElement sums = default;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = GetString(asset, "name");
                if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                    selected = asset;
                else if (string.Equals(name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    sums = asset;
            }

            if (selected.ValueKind == JsonValueKind.Undefined)
                throw new InvalidOperationException(
                    $"Release {tag} does not contain the required asset {assetName}.");

            info.DownloadUrl = GetString(selected, "browser_download_url");
            var digest = GetString(selected, "digest");
            if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                info.AssetDigestSha256 = digest["sha256:".Length..].Trim().ToLowerInvariant();

            if (sums.ValueKind != JsonValueKind.Undefined)
            {
                var sumsUrl = GetString(sums, "browser_download_url");
                if (!string.IsNullOrWhiteSpace(sumsUrl))
                {
                    var sumsText = await _http.GetStringAsync(sumsUrl, ct);
                    info.ExpectedSha256 = ParseChecksum(sumsText, assetName);
                }
            }

            if (string.IsNullOrWhiteSpace(info.ExpectedSha256))
                info.ExpectedSha256 = info.AssetDigestSha256;

            if (string.IsNullOrWhiteSpace(info.ExpectedSha256))
                throw new InvalidOperationException(
                    $"Release {tag} does not provide a SHA-256 checksum for {assetName}.");

            JsonStore.WriteAtomic(AppPaths.UpdateStatusFile, info);
            return info;
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
            try { JsonStore.WriteAtomic(AppPaths.UpdateStatusFile, info); } catch { }
            return info;
        }
    }

    public async Task<PreparedUpdate> PrepareAsync(UpdateInfo info, CancellationToken ct = default)
    {
        if (!info.UpdateAvailable)
            throw new InvalidOperationException("No newer BitKeyBridge release is available.");
        if (string.IsNullOrWhiteSpace(info.DownloadUrl) ||
            string.IsNullOrWhiteSpace(info.ExpectedSha256))
            throw new InvalidOperationException("Update metadata is incomplete.");

        Directory.CreateDirectory(AppPaths.UpdatesDirectory);
        var versionDir = Path.Combine(AppPaths.UpdatesDirectory, "v" + info.LatestVersion);
        if (Directory.Exists(versionDir))
            Directory.Delete(versionDir, true);
        Directory.CreateDirectory(versionDir);

        var zipPath = Path.Combine(versionDir, info.AssetName);
        using (var response = await _http.GetAsync(
                   info.DownloadUrl,
                   HttpCompletionOption.ResponseHeadersRead,
                   ct))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(
                zipPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            await input.CopyToAsync(output, ct);
        }

        var actual = await ComputeSha256Async(zipPath, ct);
        if (!string.Equals(actual, info.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"SHA256 mismatch for {info.AssetName}. Expected {info.ExpectedSha256}, got {actual}.");
        if (!string.IsNullOrWhiteSpace(info.AssetDigestSha256) &&
            !string.Equals(actual, info.AssetDigestSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"GitHub asset digest mismatch for {info.AssetName}. Expected {info.AssetDigestSha256}, got {actual}.");

        var extractDir = Path.Combine(versionDir, "extract");
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
        var executable = Directory
            .EnumerateFiles(extractDir, "BitKeyBridge.exe", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The downloaded release does not contain BitKeyBridge.exe.");

        var fileVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
        if (!Version.TryParse(NormalizeVersionText(fileVersion), out var stagedVersion) ||
            stagedVersion < ParseVersion(info.LatestVersion))
            throw new InvalidOperationException(
                $"Staged executable version '{fileVersion}' does not match release {info.LatestVersion}.");

        await RunStagedSelfTestAsync(executable, ct);

        return new PreparedUpdate
        {
            Info = info,
            ZipPath = zipPath,
            StagedExecutable = executable
        };
    }

    public string LaunchApplyHelper(PreparedUpdate prepared, bool restartGui)
    {
        var current = Environment.ProcessPath
            ?? throw new InvalidOperationException("Current executable path is unavailable.");

        var service = WindowsServiceHost.GetInfo();
        var targets = new List<string>();
        AddUnique(targets, current);
        if (service.Installed)
            AddUnique(targets, AppPaths.ServiceExecutable);

        Directory.CreateDirectory(AppPaths.UpdatesDirectory);
        var helperDir = Path.Combine(
            AppPaths.UpdatesDirectory,
            "helper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(helperDir);
        var helperExe = Path.Combine(helperDir, "BitKeyBridge-Updater.exe");
        File.Copy(current, helperExe, true);

        var plan = new UpdateApplyPlan
        {
            StagedExecutable = prepared.StagedExecutable,
            TargetExecutables = targets,
            WaitForProcessId = Environment.ProcessId,
            RestartService = service.Installed &&
                             string.Equals(service.State, "Running", StringComparison.OrdinalIgnoreCase),
            RestartGui = restartGui,
            GuiExecutable = current,
            CleanupDirectory = helperDir
        };
        var planPath = Path.Combine(helperDir, "update-plan.json");
        JsonStore.WriteAtomic(planPath, plan);

        var psi = new ProcessStartInfo
        {
            FileName = helperExe,
            Arguments = $"--apply-update-plan \"{planPath}\"",
            UseShellExecute = true,
            WorkingDirectory = helperDir
        };
        if (OperatingSystem.IsWindows())
            psi.Verb = "runas";

        _ = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the BitKeyBridge update helper.");
        return planPath;
    }

    public static int ApplyPlan(string planPath)
    {
        var plan = JsonStore.Read<UpdateApplyPlan>(planPath)
            ?? throw new InvalidOperationException("Update apply plan could not be read.");

        if (plan.WaitForProcessId > 0)
        {
            try
            {
                using var process = Process.GetProcessById(plan.WaitForProcessId);
                process.WaitForExit(60000);
            }
            catch (ArgumentException) { }
        }

        if (plan.RestartService)
        {
            try { WindowsServiceHost.Stop(); }
            catch { }
        }

        var backups = new List<(string Target, string Backup)>();
        try
        {
            foreach (var target in plan.TargetExecutables
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fullTarget = Path.GetFullPath(target);
                var directory = Path.GetDirectoryName(fullTarget)
                    ?? throw new InvalidOperationException($"Target directory is unavailable for {fullTarget}.");
                Directory.CreateDirectory(directory);

                var backup = fullTarget + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                if (File.Exists(fullTarget))
                {
                    File.Copy(fullTarget, backup, true);
                    backups.Add((fullTarget, backup));
                }

                var replacement = fullTarget + ".new";
                File.Copy(plan.StagedExecutable, replacement, true);
                File.Move(replacement, fullTarget, true);
            }

            WindowsEventLogService.TryWrite(
                "BitKeyBridge update installed successfully.",
                EventLogSeverity.Information,
                4100);

            if (plan.RestartService)
                WindowsServiceHost.Start();

            if (plan.RestartGui && !string.IsNullOrWhiteSpace(plan.GuiExecutable))
            {
                Process.Start(new ProcessStartInfo(plan.GuiExecutable)
                {
                    UseShellExecute = true
                });
            }

            foreach (var (_, backup) in backups)
            {
                try { File.Delete(backup); } catch { }
            }

            TryScheduleHelperCleanup(plan.CleanupDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            foreach (var (target, backup) in backups.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(backup))
                        File.Copy(backup, target, true);
                }
                catch { }
            }

            WindowsEventLogService.TryWrite(
                "BitKeyBridge update failed and rollback was attempted: " + ex.Message,
                EventLogSeverity.Error,
                4199);
            try
            {
                if (plan.RestartService)
                    WindowsServiceHost.Start();
            }
            catch { }
            return 1;
        }
    }

    private static void TryScheduleHelperCleanup(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !OperatingSystem.IsWindows()) return;

        try
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(
                             directory,
                             "*",
                             SearchOption.AllDirectories))
                {
                    try { MoveFileEx(file, null, MoveFileDelayUntilReboot); } catch { }
                }

                foreach (var child in Directory.EnumerateDirectories(
                             directory,
                             "*",
                             SearchOption.AllDirectories)
                         .OrderByDescending(x => x.Length))
                {
                    try { MoveFileEx(child, null, MoveFileDelayUntilReboot); } catch { }
                }

                try { MoveFileEx(directory, null, MoveFileDelayUntilReboot); } catch { }
            }
        }
        catch { }
    }

    private static async Task RunStagedSelfTestAsync(string executable, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--self-test",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Downloaded BitKeyBridge self-test failed with exit code {process.ExitCode}. {output} {error}".Trim());
    }

    private static void AddUnique(ICollection<string> list, string value)
    {
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);
    }

    private static Version GetCurrentVersion()
    {
        var version = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        return new Version(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);
    }

    internal static string GetRid() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.X86 => "win-x86",
        Architecture.Arm64 => "win-arm64",
        _ => throw new PlatformNotSupportedException(
            $"Unsupported Windows architecture: {RuntimeInformation.ProcessArchitecture}.")
    };

    internal static string NormalizeRepository(string value)
    {
        var text = (value ?? string.Empty).Trim().Trim('/');
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            parts.Any(x => x.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'))))
            throw new InvalidOperationException("UpdateRepository must be in owner/repository format.");
        return parts[0] + "/" + parts[1];
    }

    internal static Version ParseVersion(string value)
    {
        var text = NormalizeVersionText(value);
        if (!Version.TryParse(text, out var version))
            throw new InvalidOperationException($"Invalid release version '{value}'.");
        return new Version(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);
    }

    private static string NormalizeVersionText(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            text = text[1..];
        var dash = text.IndexOf('-');
        if (dash > 0) text = text[..dash];
        var plus = text.IndexOf('+');
        if (plus > 0) text = text[..plus];
        return text;
    }

    internal static string ParseChecksum(string text, string assetName)
    {
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var split = trimmed.IndexOfAny([' ', '\t']);
            if (split <= 0) continue;
            var hash = trimmed[..split].Trim();
            var name = trimmed[(split + 1)..].Trim().TrimStart('*');
            if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase) &&
                hash.Length == 64 &&
                hash.All(Uri.IsHexDigit))
                return hash.ToLowerInvariant();
        }
        return string.Empty;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True ||
         (value.ValueKind == JsonValueKind.False ? false : false));

    private const uint MoveFileDelayUntilReboot = 0x00000004;

    [System.Runtime.InteropServices.DllImport(
        "kernel32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool MoveFileEx(
        string existingFileName,
        string? newFileName,
        uint flags);

    public void Dispose() => _http.Dispose();
}
