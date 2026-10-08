using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public sealed class UpdateService : IDisposable
{
    private const long MaximumReleaseAssetBytes =
        512L * 1024L * 1024L;
    private const int MaximumChecksumBytes =
        1024 * 1024;

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
            if (string.IsNullOrWhiteSpace(tag))
                throw new InvalidOperationException(
                    "GitHub release metadata does not contain a release tag.");

            var latest = ParseVersion(tag);
            var current = GetCurrentVersion();
            info.LatestVersion = latest.ToString();
            info.ReleaseTag = tag;
            info.UpdateAvailable = latest > current;
            info.ReleaseUrl = GetString(release, "html_url");
            ValidateReleasePageUrl(
                info.ReleaseUrl,
                repository,
                tag);
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

            info.DownloadUrl =
                ValidateReleaseAssetUrl(
                    GetString(
                        selected,
                        "browser_download_url"),
                    repository,
                    tag,
                    assetName);

            info.AssetSizeBytes =
                GetInt64(
                    selected,
                    "size");

            if (info.AssetSizeBytes <= 0 ||
                info.AssetSizeBytes >
                MaximumReleaseAssetBytes)
            {
                throw new InvalidOperationException(
                    $"Release {tag} reports an invalid size for {assetName}: {info.AssetSizeBytes} bytes.");
            }

            var digest =
                GetString(
                    selected,
                    "digest");

            if (!string.IsNullOrWhiteSpace(
                    digest))
            {
                info.AssetDigestSha256 =
                    ParseGitHubAssetDigest(
                        digest,
                        assetName);
            }

            if (sums.ValueKind !=
                JsonValueKind.Undefined)
            {
                var sumsUrl =
                    ValidateReleaseAssetUrl(
                        GetString(
                            sums,
                            "browser_download_url"),
                        repository,
                        tag,
                        "SHA256SUMS.txt");

                var sumsSize =
                    GetInt64(
                        sums,
                        "size");

                if (sumsSize <= 0 ||
                    sumsSize >
                    MaximumChecksumBytes)
                {
                    throw new InvalidOperationException(
                        $"Release {tag} reports an invalid SHA256SUMS.txt size: {sumsSize} bytes.");
                }

                var sumsBytes =
                    await DownloadSmallReleaseAssetAsync(
                        sumsUrl,
                        sumsSize,
                        ct);

                try
                {
                    var sumsDigest =
                        GetString(
                            sums,
                            "digest");

                    if (!string.IsNullOrWhiteSpace(
                            sumsDigest))
                    {
                        var expectedSumsDigest =
                            ParseGitHubAssetDigest(
                                sumsDigest,
                                "SHA256SUMS.txt");

                        var actualSumsDigest =
                            Convert.ToHexString(
                                    SHA256.HashData(
                                        sumsBytes))
                                .ToLowerInvariant();

                        if (!CryptographicOperations.FixedTimeEquals(
                                Convert.FromHexString(
                                    expectedSumsDigest),
                                Convert.FromHexString(
                                    actualSumsDigest)))
                        {
                            throw new InvalidOperationException(
                                "GitHub SHA256SUMS.txt asset digest does not match the downloaded checksum file.");
                        }
                    }

                    var sumsText =
                        Encoding.UTF8.GetString(
                            sumsBytes);

                    info.ExpectedSha256 =
                        ParseChecksum(
                            sumsText,
                            assetName);

                    if (string.IsNullOrWhiteSpace(
                            info.ExpectedSha256))
                    {
                        throw new InvalidOperationException(
                            $"Release {tag} SHA256SUMS.txt does not contain a valid SHA-256 entry for {assetName}.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(
                        sumsBytes);
                }
            }

            if (string.IsNullOrWhiteSpace(
                    info.ExpectedSha256))
            {
                info.ExpectedSha256 =
                    info.AssetDigestSha256;
            }

            if (string.IsNullOrWhiteSpace(
                    info.ExpectedSha256))
            {
                throw new InvalidOperationException(
                    $"Release {tag} does not provide a SHA-256 checksum for {assetName}.");
            }

            if (!string.IsNullOrWhiteSpace(
                    info.AssetDigestSha256) &&
                !string.Equals(
                    info.ExpectedSha256,
                    info.AssetDigestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Release {tag} checksum sources disagree for {assetName}.");
            }

            try
            {
                JsonStore.WriteAtomic(
                    AppPaths.UpdateStatusFile,
                    info);
            }
            catch (Exception statusException)
            {
                // Update availability must not be turned into a false failure
                // only because the optional machine-wide status file is not
                // writable in the current session.
                Debug.WriteLine(
                    "Update status persistence failed: " +
                    DiagnosticRedaction.Sanitize(
                        statusException.Message));
            }

            return info;
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
            try
            {
                JsonStore.WriteAtomic(
                    AppPaths.UpdateStatusFile,
                    info);
            }
            catch (Exception statusException)
            {
                Debug.WriteLine(
                    "Update status persistence failed: " +
                    DiagnosticRedaction.Sanitize(
                        statusException.Message));
            }
            return info;
        }
    }

    public async Task<PreparedUpdate> PrepareAsync(UpdateInfo info, CancellationToken ct = default)
    {
        if (!info.UpdateAvailable)
            throw new InvalidOperationException("No newer BitKeyBridge release is available.");
        if (string.IsNullOrWhiteSpace(info.DownloadUrl) ||
            string.IsNullOrWhiteSpace(info.ExpectedSha256) ||
            string.IsNullOrWhiteSpace(info.ReleaseTag) ||
            info.AssetSizeBytes <= 0)
            throw new InvalidOperationException("Update metadata is incomplete.");

        await RevalidateUpdateInfoAsync(
            info,
            ct);

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
            ValidateTrustedDownloadResponse(
                response);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength != info.AssetSizeBytes)
            {
                throw new InvalidOperationException(
                    $"Release asset size changed before download. Expected {info.AssetSizeBytes} bytes, received {contentLength} bytes.");
            }

            await using var input =
                await response.Content.ReadAsStreamAsync(
                    ct);
            await using var output = new FileStream(
                zipPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);

            await CopyExactSizeAsync(
                input,
                output,
                info.AssetSizeBytes,
                ct);
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

        var executables =
            Directory
                .EnumerateFiles(
                    extractDir,
                    "BitKeyBridge.exe",
                    SearchOption.AllDirectories)
                .ToArray();

        if (executables.Length != 1)
        {
            throw new InvalidOperationException(
                $"The downloaded release must contain exactly one BitKeyBridge.exe; found {executables.Length}.");
        }

        var executable =
            executables[0];

        if (!string.Equals(
                Path.GetRelativePath(
                    extractDir,
                    executable),
                "BitKeyBridge.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The downloaded release contains BitKeyBridge.exe outside the package root.");
        }

        var fileVersion =
            FileVersionInfo.GetVersionInfo(
                executable)
                .FileVersion;

        if (!ReleaseVersionsMatch(
                fileVersion,
                info.LatestVersion))
        {
            throw new InvalidOperationException(
                $"Staged executable version '{fileVersion}' does not match release {info.LatestVersion}.");
        }

        var stagedHashBeforeSelfTest =
            await ComputeSha256Async(
                executable,
                ct);

        await RunStagedSelfTestAsync(
            executable,
            ct);

        var stagedHashAfterSelfTest =
            await ComputeSha256Async(
                executable,
                ct);

        if (!string.Equals(
                stagedHashBeforeSelfTest,
                stagedHashAfterSelfTest,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The staged BitKeyBridge executable changed while it was being verified.");
        }

        return new PreparedUpdate
        {
            Info = info,
            ZipPath = zipPath,
            StagedExecutable = executable,
            StagedExecutableSha256 =
                stagedHashAfterSelfTest,
            AuthenticodePublisher =
                UpdateHistoryService.GetAuthenticodePublisher(
                    executable)
        };
    }

    public string LaunchApplyHelper(
        PreparedUpdate prepared,
        bool restartGui,
        bool automatic = false)
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

        if (string.IsNullOrWhiteSpace(
                prepared.StagedExecutableSha256))
        {
            throw new InvalidOperationException(
                "The staged update executable does not have verification metadata.");
        }

        var plan = new UpdateApplyPlan
        {
            StagedExecutable = prepared.StagedExecutable,
            StagedExecutableSha256 =
                prepared.StagedExecutableSha256,
            ExpectedVersion =
                prepared.Info.LatestVersion,
            PreviousVersion =
                GetCurrentVersion().ToString(),
            Automatic =
                automatic,
            AuthenticodePublisher =
                prepared.AuthenticodePublisher,
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

        var planSha256 =
            ComputeSha256File(
                planPath);

        var psi = new ProcessStartInfo
        {
            FileName = helperExe,
            Arguments =
                $"--apply-update-plan \"{planPath}\" " +
                $"--apply-update-plan-sha256 {planSha256}",
            UseShellExecute = true,
            WorkingDirectory = helperDir
        };
        if (OperatingSystem.IsWindows())
            psi.Verb = "runas";

        // Keep the helper file non-writable until CreateProcess has opened it.
        using var helperLock =
            new FileStream(
                helperExe,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        _ = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the BitKeyBridge update helper.");
        return planPath;
    }

    public string LaunchRollbackHelper(
        bool restartGui)
    {
        var current =
            Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Current executable path is unavailable.");

        var backup =
            current + ".bak";

        if (!File.Exists(
                backup))
        {
            throw new FileNotFoundException(
                "No retained BitKeyBridge backup is available for rollback.",
                backup);
        }

        Directory.CreateDirectory(
            AppPaths.UpdatesDirectory);

        var rollbackDirectory =
            Path.Combine(
                AppPaths.UpdatesDirectory,
                "rollback-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rollbackDirectory);

        var staged =
            Path.Combine(
                rollbackDirectory,
                "BitKeyBridge.exe");

        File.Copy(
            backup,
            staged,
            overwrite:
                true);

        var version =
            FileVersionInfo.GetVersionInfo(
                staged)
                .FileVersion;

        if (string.IsNullOrWhiteSpace(
                version))
        {
            throw new InvalidDataException(
                "The rollback backup does not contain a valid file version.");
        }

        var prepared =
            new PreparedUpdate
            {
                Info =
                    new UpdateInfo
                    {
                        CurrentVersion =
                            GetCurrentVersion().ToString(),
                        LatestVersion =
                            version,
                        ReleaseTag =
                            "rollback",
                        Architecture =
                            GetRid()
                    },
                StagedExecutable =
                    staged,
                StagedExecutableSha256 =
                    ComputeSha256File(
                        staged),
                AuthenticodePublisher =
                    UpdateHistoryService.GetAuthenticodePublisher(
                        staged)
            };

        new UpdateHistoryService()
            .Append(
                new UpdateHistoryEntry
                {
                    Action =
                        "RollbackRequested",
                    FromVersion =
                        prepared.Info.CurrentVersion,
                    ToVersion =
                        prepared.Info.LatestVersion,
                    Result =
                        "Prepared",
                    Publisher =
                        prepared.AuthenticodePublisher,
                    Details =
                        "Rollback staged from the retained previous executable."
                });

        return LaunchApplyHelper(
            prepared,
            restartGui,
            automatic:
                false);
    }

    public static int ApplyPlan(
        string planPath,
        string expectedPlanSha256)
    {
        if (OperatingSystem.IsWindows() &&
            !SecurityContext.IsAdministrator())
        {
            WindowsEventLogService.TryWrite(
                "BitKeyBridge update apply was refused because the helper was not elevated.",
                EventLogSeverity.Warning,
                4197,
                "Update");
            return 5;
        }

        UpdateApplyPlan plan;
        try
        {
            plan =
                ReadVerifiedApplyPlan(
                    planPath,
                    expectedPlanSha256);
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "BitKeyBridge update plan verification failed: " +
                ex.Message,
                EventLogSeverity.Error,
                4198,
                "Update");
            return 1;
        }

        if (plan.WaitForProcessId > 0)
        {
            try
            {
                using var process = Process.GetProcessById(plan.WaitForProcessId);
                process.WaitForExit(60000);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(
                    "Updater wait target process already exited: " +
                    DiagnosticRedaction.Sanitize(ex.Message));
            }
        }

        if (plan.RestartService)
        {
            try
            {
                WindowsServiceHost.Stop();
            }
            catch (Exception ex)
            {
                WindowsEventLogService.TryWrite(
                    "Updater could not stop the BitKeyBridge service before replacement: " +
                    ex.Message,
                    EventLogSeverity.Warning,
                    4591,
                    "Update");
            }
        }

        var backups = new List<(string Target, string Backup)>();
        try
        {
            using var stagedExecutable =
                OpenVerifiedStagedExecutable(
                    plan);

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
                stagedExecutable.Position = 0;

                using (var output =
                       new FileStream(
                           replacement,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stagedExecutable.CopyTo(
                        output);
                    output.Flush(
                        flushToDisk: true);
                }

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

            // Keep the immediately previous executable as .bak so the
            // operator has a one-click rollback path. A later successful
            // update replaces that retained backup with the then-current build.
            new UpdateHistoryService()
                .Append(
                    new UpdateHistoryEntry
                    {
                        Action =
                            plan.ExpectedVersion.Equals(
                                plan.PreviousVersion,
                                StringComparison.OrdinalIgnoreCase)
                                ? "Replace"
                                : "Install",
                        FromVersion =
                            plan.PreviousVersion,
                        ToVersion =
                            plan.ExpectedVersion,
                        Result =
                            "Success",
                        Automatic =
                            plan.Automatic,
                        Publisher =
                            plan.AuthenticodePublisher,
                        Details =
                            backups.Count > 0
                                ? "Previous executable retained as .bak for rollback."
                                : "Installed without an existing target backup."
                    });

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
                catch (Exception rollbackException)
                {
                    WindowsEventLogService.TryWrite(
                        "Updater file rollback failed for " +
                        target +
                        ": " +
                        rollbackException.Message,
                        EventLogSeverity.Error,
                        4592,
                        "Update");
                }
            }

            new UpdateHistoryService()
                .Append(
                    new UpdateHistoryEntry
                    {
                        Action =
                            "Install",
                        FromVersion =
                            plan.PreviousVersion,
                        ToVersion =
                            plan.ExpectedVersion,
                        Result =
                            "Failed",
                        Automatic =
                            plan.Automatic,
                        Publisher =
                            plan.AuthenticodePublisher,
                        Details =
                            DiagnosticRedaction.Sanitize(
                                ex.Message)
                    });

            WindowsEventLogService.TryWrite(
                "BitKeyBridge update failed and rollback was attempted: " + ex.Message,
                EventLogSeverity.Error,
                4199);
            try
            {
                if (plan.RestartService)
                    WindowsServiceHost.Start();
            }
            catch (Exception restartException)
            {
                WindowsEventLogService.TryWrite(
                    "Updater could not restart the BitKeyBridge service after failure: " +
                    restartException.Message,
                    EventLogSeverity.Error,
                    4593,
                    "Update");
            }
            return 1;
        }
    }

    internal static UpdateApplyPlan ReadVerifiedApplyPlan(
        string planPath,
        string expectedPlanSha256)
    {
        var fullPath =
            Path.GetFullPath(
                planPath);

        if (!File.Exists(
                fullPath))
        {
            throw new FileNotFoundException(
                "Update apply plan was not found.",
                fullPath);
        }

        var expected =
            NormalizeSha256(
                expectedPlanSha256,
                "update apply plan");

        var bytes =
            File.ReadAllBytes(
                fullPath);
        try
        {
            var actual =
                SHA256.HashData(
                    bytes);

            if (!CryptographicOperations.FixedTimeEquals(
                    actual,
                    Convert.FromHexString(
                        expected)))
            {
                throw new InvalidDataException(
                    "Update apply plan SHA-256 does not match the value supplied to the elevated helper.");
            }

            return JsonSerializer.Deserialize<UpdateApplyPlan>(
                       bytes,
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       })
                   ?? throw new InvalidDataException(
                       "Update apply plan is empty or invalid.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                bytes);
        }
    }

    private static FileStream OpenVerifiedStagedExecutable(
        UpdateApplyPlan plan)
    {
        if (string.IsNullOrWhiteSpace(
                plan.StagedExecutable))
        {
            throw new InvalidDataException(
                "Update plan does not contain a staged executable.");
        }

        var expectedHash =
            NormalizeSha256(
                plan.StagedExecutableSha256,
                "staged executable");

        if (string.IsNullOrWhiteSpace(
                plan.ExpectedVersion))
        {
            throw new InvalidDataException(
                "Update plan does not contain the expected version.");
        }

        var fullPath =
            Path.GetFullPath(
                plan.StagedExecutable);

        var stream =
            new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        try
        {
            var actualHash =
                SHA256.HashData(
                    stream);
            var expectedBytes =
                Convert.FromHexString(
                    expectedHash);

            if (!CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedBytes))
            {
                throw new InvalidDataException(
                    "Staged BitKeyBridge executable SHA-256 changed after verification.");
            }

            var fileVersion =
                FileVersionInfo.GetVersionInfo(
                    fullPath)
                    .FileVersion;

            if (!ReleaseVersionsMatch(
                    fileVersion,
                    plan.ExpectedVersion))
            {
                throw new InvalidDataException(
                    $"Staged executable version '{fileVersion}' no longer matches expected version {plan.ExpectedVersion}.");
            }

            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static string NormalizeSha256(
        string value,
        string label)
    {
        var normalized =
            (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length != 64 ||
            !normalized.All(
                Uri.IsHexDigit))
        {
            throw new InvalidDataException(
                $"Expected SHA-256 for {label} is invalid.");
        }

        return normalized;
    }

    private async Task RevalidateUpdateInfoAsync(
        UpdateInfo expected,
        CancellationToken ct)
    {
        var fresh =
            await CheckAsync(
                ct);

        if (!string.IsNullOrWhiteSpace(
                fresh.Error))
        {
            throw new InvalidOperationException(
                "Update metadata revalidation failed: " +
                fresh.Error);
        }

        if (!fresh.UpdateAvailable)
        {
            throw new InvalidOperationException(
                "The selected update is no longer reported as available.");
        }

        var unchanged =
            string.Equals(
                fresh.ReleaseTag,
                expected.ReleaseTag,
                StringComparison.Ordinal) &&
            string.Equals(
                fresh.LatestVersion,
                expected.LatestVersion,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                fresh.Architecture,
                expected.Architecture,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                fresh.AssetName,
                expected.AssetName,
                StringComparison.Ordinal) &&
            fresh.AssetSizeBytes ==
            expected.AssetSizeBytes &&
            string.Equals(
                fresh.DownloadUrl,
                expected.DownloadUrl,
                StringComparison.Ordinal) &&
            string.Equals(
                fresh.ExpectedSha256,
                expected.ExpectedSha256,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                fresh.AssetDigestSha256,
                expected.AssetDigestSha256,
                StringComparison.OrdinalIgnoreCase);

        if (!unchanged)
        {
            throw new InvalidOperationException(
                "GitHub release metadata changed after the update was selected. Check for updates again before installing.");
        }
    }

    private async Task<byte[]> DownloadSmallReleaseAssetAsync(
        string url,
        long expectedSize,
        CancellationToken ct)
    {
        using var response =
            await _http.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

        ValidateTrustedDownloadResponse(
            response);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long contentLength &&
            contentLength != expectedSize)
        {
            throw new InvalidOperationException(
                $"Release metadata size mismatch. Expected {expectedSize} bytes, received {contentLength} bytes.");
        }

        await using var input =
            await response.Content.ReadAsStreamAsync(
                ct);

        using var output =
            new MemoryStream(
                checked((int)expectedSize));

        await CopyExactSizeAsync(
            input,
            output,
            expectedSize,
            ct);

        return output.ToArray();
    }

    private static async Task CopyExactSizeAsync(
        Stream input,
        Stream output,
        long expectedSize,
        CancellationToken ct)
    {
        if (expectedSize < 1 ||
            expectedSize >
            MaximumReleaseAssetBytes)
        {
            throw new InvalidDataException(
                $"Expected release asset size {expectedSize} is outside the allowed range.");
        }

        var buffer =
            new byte[128 * 1024];
        long total = 0;

        while (true)
        {
            var read =
                await input.ReadAsync(
                    buffer.AsMemory(
                        0,
                        buffer.Length),
                    ct);

            if (read == 0)
                break;

            total += read;

            if (total >
                expectedSize)
            {
                throw new InvalidDataException(
                    "Downloaded release asset exceeded the size declared by GitHub.");
            }

            await output.WriteAsync(
                buffer.AsMemory(
                    0,
                    read),
                ct);
        }

        if (total != expectedSize)
        {
            throw new InvalidDataException(
                $"Downloaded release asset size mismatch. Expected {expectedSize} bytes, received {total} bytes.");
        }

        await output.FlushAsync(
            ct);
    }

    internal static string ValidateReleaseAssetUrl(
        string value,
        string repository,
        string tag,
        string assetName)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(
                uri.UserInfo) ||
            !string.IsNullOrEmpty(
                uri.Query) ||
            !string.IsNullOrEmpty(
                uri.Fragment))
        {
            throw new InvalidOperationException(
                $"GitHub release asset URL for {assetName} is invalid.");
        }

        var expectedPath =
            "/" +
            repository +
            "/releases/download/" +
            tag +
            "/" +
            assetName;

        if (!string.Equals(
                Uri.UnescapeDataString(
                    uri.AbsolutePath),
                expectedPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"GitHub release asset URL for {assetName} does not match repository {repository} and release {tag}.");
        }

        return uri.AbsoluteUri;
    }

    internal static void ValidateReleasePageUrl(
        string value,
        string repository,
        string tag)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(
                uri.UserInfo) ||
            !string.IsNullOrEmpty(
                uri.Query) ||
            !string.IsNullOrEmpty(
                uri.Fragment))
        {
            throw new InvalidOperationException(
                "GitHub release page URL is invalid.");
        }

        var expectedPath =
            "/" +
            repository +
            "/releases/tag/" +
            tag;

        if (!string.Equals(
                Uri.UnescapeDataString(
                    uri.AbsolutePath),
                expectedPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"GitHub release page URL does not match repository {repository} and release {tag}.");
        }
    }

    private static void ValidateTrustedDownloadResponse(
        HttpResponseMessage response)
    {
        var uri =
            response.RequestMessage?.RequestUri;

        if (uri is null ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Release download did not remain on HTTPS.");
        }

        var host =
            uri.Host;

        if (!string.Equals(
                host,
                "github.com",
                StringComparison.OrdinalIgnoreCase) &&
            !host.EndsWith(
                ".githubusercontent.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Release download redirected to an untrusted host '{host}'.");
        }
    }

    internal static string ParseGitHubAssetDigest(
        string value,
        string assetName)
    {
        const string prefix =
            "sha256:";

        if (!value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GitHub asset digest for {assetName} is not SHA-256.");
        }

        var digest =
            value[prefix.Length..]
                .Trim()
                .ToLowerInvariant();

        if (digest.Length != 64 ||
            !digest.All(
                Uri.IsHexDigit))
        {
            throw new InvalidOperationException(
                $"GitHub asset digest for {assetName} is invalid.");
        }

        return digest;
    }

    private static long GetInt64(
        JsonElement element,
        string name)
    {
        if (!element.TryGetProperty(
                name,
                out var value) ||
            value.ValueKind !=
            JsonValueKind.Number ||
            !value.TryGetInt64(
                out var result))
        {
            return 0;
        }

        return result;
    }

    internal static string ComputeSha256File(
        string path)
    {
        using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        return Convert.ToHexString(
                SHA256.HashData(
                    stream))
            .ToLowerInvariant();
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
                    try
                    {
                        MoveFileEx(
                            file,
                            null,
                            MoveFileDelayUntilReboot);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            "Updater delayed file cleanup scheduling failed: " +
                            DiagnosticRedaction.Sanitize(ex.Message));
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(
                             directory,
                             "*",
                             SearchOption.AllDirectories)
                         .OrderByDescending(x => x.Length))
                {
                    try
                    {
                        MoveFileEx(
                            child,
                            null,
                            MoveFileDelayUntilReboot);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            "Updater delayed directory cleanup scheduling failed: " +
                            DiagnosticRedaction.Sanitize(ex.Message));
                    }
                }

                try
                {
                    MoveFileEx(
                        directory,
                        null,
                        MoveFileDelayUntilReboot);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        "Updater delayed root cleanup scheduling failed: " +
                        DiagnosticRedaction.Sanitize(ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "Updater helper cleanup scheduling failed: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
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

    internal static bool ReleaseVersionsMatch(
        string? actual,
        string? expected)
    {
        try
        {
            return ParseVersion(
                    actual ?? string.Empty)
                .Equals(
                    ParseVersion(
                        expected ?? string.Empty));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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
