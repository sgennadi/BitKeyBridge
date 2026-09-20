using System.Security.Cryptography;
using System.Text;

namespace BitKeyBridge;

public sealed class ExportService
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;
    private readonly ReplicationService _replication;
    private readonly AclService _acl;
    private readonly AppLogger _log;

    public ExportService(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService(config);
        _replication = new ReplicationService(config);
        _acl = new AclService();
        _log = new AppLogger(config.ErrorLog, config.MaxLogSizeMb);
    }

    public async Task<ExportResult> RunAsync(
        bool dryRun,
        bool forcePublish,
        IReadOnlyCollection<BitLockerScope>? selectedScopes,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => RunCore(dryRun, forcePublish, selectedScopes, progress, cancellationToken), cancellationToken);
    }

    private ExportResult RunCore(
        bool dryRun,
        bool forcePublish,
        IReadOnlyCollection<BitLockerScope>? selectedScopes,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTime.Now;
        var result = new ExportResult
        {
            Started = started,
            DryRun = dryRun,
            OutputFile = _config.OutputCsv,
            ExitCode = 1
        };
        FileStream? lockStream = null;

        try
        {
            var outputRoot = _config.EffectiveOutputRoot;
            try
            {
                Directory.CreateDirectory(outputRoot);
                Directory.CreateDirectory(_config.OutputDirectory);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"Output root '{outputRoot}' is not available or cannot be created. " +
                    "Configure a writable local or UNC output path. " + ex.Message,
                    ex);
            }
            _log.Initialize();
            lockStream = new FileStream(_config.LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            var scopes = NormalizeScopes(selectedScopes);
            result.ScopeFingerprint = GetScopeFingerprint(scopes);
            result.PreviousRows = CsvUtility.CountDataRows(_config.OutputCsv);

            var lastSuccess = JsonStore.Read<LastSuccessInfo>(_config.LastSuccessFile);
            if (lastSuccess is not null)
            {
                result.LastSuccessFound = true;
                result.LastSuccessTimestamp = lastSuccess.Finished;
                result.LastSuccessAgeHours = (DateTime.Now - lastSuccess.Finished).TotalHours;
                result.LastSuccessWasStale = result.LastSuccessAgeHours > _config.StaleSuccessHours;
            }

            progress?.Report("Discovering writable domain controller...");
            var server = _ad.GetPreferredWritableDc();
            result.AdServer = server;
            _log.Info($"Export started on {Environment.MachineName} using {server}. DryRun={dryRun}.");

            progress?.Report($"Checking replication on {server}...");
            var repl = _replication.Check(server);
            result.ReplicationHealthy = repl.Healthy;
            result.ReplicationWarnings = repl.Warnings;
            result.ReplicationErrors = repl.Errors;
            result.ReplicationPartners = repl.Partners;
            if (!repl.Healthy)
            {
                _log.Warning($"Replication health on {server} reports {repl.Errors.Count} error(s).");
                if (_config.BlockExportOnReplicationErrors)
                    throw new InvalidOperationException("AD replication health check failed and blocking on replication errors is enabled.");
            }

            var domainSid = _ad.GetDomainSid(server);
            var aclWarnings = _acl.GetBroadReadWarnings(_config.OutputDirectory, domainSid);
            result.AclWarnings.AddRange(aclWarnings);
            foreach (var warning in aclWarnings) _log.Warning("Output ACL: " + warning);
            if (aclWarnings.Count > 0 && _config.FailIfOutputAclIsBroad)
                throw new UnauthorizedAccessException("Output directory has broad read access. Publishing is blocked by configuration.");

            var runTimestamp = DateTime.Now;
            var rows = new List<RecoveryRecord>();
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var queryFailed = false;
            var integrityFailed = false;

            foreach (var scope in scopes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Querying {scope.Name}...");
                var scopeResult = new ScopeResult { Name = scope.Name, SearchBase = scope.SearchBase };
                result.Containers.Add(scopeResult);

                List<RecoveryRecord> scopeRows;
                try
                {
                    scopeRows = _ad.GetRecoveryRecords(server, scope, runTimestamp, w => _log.Warning(w));
                    scopeResult.ObjectsFound = scopeRows.Count;
                    result.ObjectsFound += scopeRows.Count;
                }
                catch (Exception ex)
                {
                    queryFailed = true;
                    result.QueryFailures++;
                    scopeResult.Status = "ERROR";
                    scopeResult.Error = ex.Message;
                    _log.Error($"Query failed for '{scope.Name}': {ex.Message}");
                    continue;
                }

                foreach (var row in scopeRows)
                {
                    try
                    {
                        var dedup = row.ComputerName + "|" + row.BitLockerId;
                        if (seen.TryGetValue(dedup, out var existing))
                        {
                            if (!string.Equals(existing, row.RecoveryKey, StringComparison.Ordinal))
                                throw new InvalidDataException($"Duplicate recovery ID {row.BitLockerId} for {row.ComputerName} has a different password.");
                            result.Duplicates++;
                            scopeResult.Duplicates++;
                            continue;
                        }
                        seen[dedup] = row.RecoveryKey;
                        rows.Add(row);
                        scopeResult.ValidRows++;
                    }
                    catch (Exception ex)
                    {
                        integrityFailed = true;
                        result.InvalidObjects++;
                        scopeResult.Invalid++;
                        scopeResult.Status = "WARNING";
                        _log.Error(ex.Message);
                    }
                }
            }

            rows = rows.OrderBy(x => x.ComputerName, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(x => x.BitLockerId, StringComparer.OrdinalIgnoreCase)
                       .ToList();
            result.ValidRows = rows.Count;

            if ((queryFailed || integrityFailed) && !_config.AllowPartialExport)
                throw new InvalidOperationException("One or more AD queries/objects failed. Existing CSV was preserved because partial export is disabled.");
            if (rows.Count == 0 && !_config.AllowEmptyExport)
                throw new InvalidOperationException("No valid BitLocker records were collected. Existing CSV was preserved because empty export is disabled.");

            if (result.PreviousRows >= _config.MinimumRowsForDropGuard && result.PreviousRows > 0)
            {
                var ratio = (double)rows.Count / result.PreviousRows;
                if (ratio < _config.CriticalRowRatio)
                {
                    result.RowCountWarning = $"New row count is only {ratio * 100:0.0}% of previous CSV ({rows.Count} vs {result.PreviousRows}).";
                    _log.Warning(result.RowCountWarning);
                    if (!dryRun && _config.BlockSuspiciousRowDrop && !forcePublish)
                        throw new InvalidOperationException("Suspicious row-count drop detected. Existing CSV was preserved. Use --force-publish only after verifying the scope and result.");
                }
                else if (ratio < _config.WarningRowRatio)
                {
                    result.RowCountWarning = $"Row count decreased to {ratio * 100:0.0}% of previous CSV ({rows.Count} vs {result.PreviousRows}).";
                    _log.Warning(result.RowCountWarning);
                }
            }

            if (lastSuccess is not null &&
                !string.IsNullOrWhiteSpace(lastSuccess.ScopeFingerprint) &&
                !string.Equals(lastSuccess.ScopeFingerprint, result.ScopeFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                result.ScopeWarning = "The selected OU scope differs from the last successful published export.";
                _log.Warning(result.ScopeWarning);
                if (!dryRun && _config.BlockPublishOnScopeChange && !forcePublish)
                    throw new InvalidOperationException("Export scope changed. Existing CSV was preserved. Run Dry Run first, then use --force-publish if the change is intentional.");
            }

            if (!dryRun)
            {
                progress?.Report("Writing verified CSV atomically...");
                CsvUtility.WriteRecoveryCsvAtomic(_config.OutputCsv, rows);
                result.Published = true;
            }

            result.Success = true;
            result.ExitCode = 0;
            result.Finished = DateTime.Now;
            result.DurationSeconds = (result.Finished - started).TotalSeconds;
            JsonStore.WriteAtomic(_config.StatusFile, result);

            if (!dryRun)
            {
                var success = new LastSuccessInfo
                {
                    Finished = result.Finished,
                    HostName = result.HostName,
                    AdServer = result.AdServer ?? string.Empty,
                    ValidRows = result.ValidRows,
                    ObjectsFound = result.ObjectsFound,
                    DurationSeconds = result.DurationSeconds,
                    ScopeFingerprint = result.ScopeFingerprint,
                    Containers = result.Containers.Select(x => new ScopeResult
                    {
                        Name = x.Name,
                        SearchBase = x.SearchBase,
                        ObjectsFound = x.ObjectsFound,
                        ValidRows = x.ValidRows,
                        Status = x.Status
                    }).ToList()
                };
                JsonStore.WriteAtomic(_config.LastSuccessFile, success);
            }

            _log.Info($"Export completed. DryRun={dryRun}; Rows={result.ValidRows}; Objects={result.ObjectsFound}; Duplicates={result.Duplicates}; Invalid={result.InvalidObjects}.");
            progress?.Report(dryRun ? "Dry Run completed successfully." : "Export completed successfully.");
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Operation cancelled.";
            result.ExitCode = 2;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _log.Error(ex.ToString());
            progress?.Report("ERROR: " + ex.Message);
        }
        finally
        {
            result.Finished = result.Finished == default ? DateTime.Now : result.Finished;
            result.DurationSeconds = (result.Finished - started).TotalSeconds;
            try { JsonStore.WriteAtomic(_config.StatusFile, result); } catch { }
            lockStream?.Dispose();
            try { if (File.Exists(_config.LockFile)) File.Delete(_config.LockFile); } catch { }
        }

        return result;
    }

    private List<BitLockerScope> NormalizeScopes(IReadOnlyCollection<BitLockerScope>? scopes)
    {
        var selected = scopes is { Count: > 0 } ? scopes : _config.DefaultScopes;
        var normalized = selected
            .Where(x => !string.IsNullOrWhiteSpace(x.SearchBase))
            .GroupBy(x => x.SearchBase.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        if (normalized.Count == 0)
            throw new InvalidOperationException("No export OU scopes are configured. Add/select an OU in the GUI and save it as a default, or use --search-base in CLI mode.");
        return normalized;
    }

    public static string GetScopeFingerprint(IEnumerable<BitLockerScope> scopes)
    {
        var normalized = string.Join("\n", scopes
            .Select(x => x.SearchBase.Trim().ToLowerInvariant())
            .OrderBy(x => x, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
