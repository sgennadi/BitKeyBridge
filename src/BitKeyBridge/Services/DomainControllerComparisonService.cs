namespace BitKeyBridge;

public sealed class DomainControllerComparisonService
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;
    private readonly ReplicationService _replication;

    public DomainControllerComparisonService(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService();
        _replication = new ReplicationService(config);
    }

    public async Task<List<DomainControllerComparisonRow>> RunAsync(
        IReadOnlyCollection<BitLockerScope>? selectedScopes,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var scopes = selectedScopes is { Count: > 0 } ? selectedScopes.ToList() : _config.DefaultScopes.ToList();
            if (scopes.Count == 0)
                throw new InvalidOperationException("No OU scopes are configured. Add an OU in the GUI or pass --search-base.");

            var domain = _ad.GetCurrentDomainName();
            progress?.Report($"Discovering domain controllers in {domain}...");
            var controllers = _ad.DiscoverDomainControllers(domain);
            var rows = new List<DomainControllerComparisonRow>();

            foreach (var dc in controllers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Testing {dc.HostName}...");
                var row = new DomainControllerComparisonRow
                {
                    Name = dc.Name,
                    HostName = dc.HostName,
                    Site = dc.Site,
                    IPv4Address = dc.IPv4Address,
                    IsReadOnly = dc.IsReadOnly,
                    IsGlobalCatalog = dc.IsGlobalCatalog,
                    OperatingSystem = dc.OperatingSystem,
                    Reachable = dc.Reachable
                };

                try
                {
                    if (!dc.Reachable)
                        throw new InvalidOperationException("LDAP RootDSE is not reachable.");

                    var repl = _replication.Check(dc.HostName);
                    row.ReplicationHealthy = repl.Healthy;
                    row.ReplicationErrors = repl.Errors.Count;
                    row.ReplicationWarnings = repl.Warnings.Count;
                    row.MaxReplicationAgeHours = repl.Partners.Where(x => x.AgeHours.HasValue)
                        .Select(x => x.AgeHours!.Value)
                        .DefaultIfEmpty(0)
                        .Max();

                    if (!dc.IsReadOnly || _config.TestBitLockerCountsOnReadOnlyDcs)
                    {
                        foreach (var scope in scopes)
                        {
                            try
                            {
                                var count = _ad.CountRecoveryObjects(dc.HostName, scope);
                                row.ObjectsFound += count;
                                row.Scopes.Add(new ScopeResult
                                {
                                    Name = scope.Name,
                                    SearchBase = scope.SearchBase,
                                    ObjectsFound = count,
                                    Status = "OK"
                                });
                            }
                            catch (Exception ex)
                            {
                                row.Scopes.Add(new ScopeResult
                                {
                                    Name = scope.Name,
                                    SearchBase = scope.SearchBase,
                                    Status = "ERROR",
                                    Error = ex.Message
                                });
                                row.Error = ex.Message;
                            }
                        }
                    }

                    row.Status = row.Error is not null || repl.Errors.Count > 0
                        ? "ERROR"
                        : repl.Warnings.Count > 0 ? "WARNING" : "OK";
                }
                catch (Exception ex)
                {
                    row.Status = "ERROR";
                    row.Error = ex.Message;
                }

                rows.Add(row);
            }

            var comparable = rows.Where(x => !x.IsReadOnly && x.Reachable && x.Error is null).ToList();
            if (comparable.Count > 0)
            {
                var max = comparable.Max(x => x.ObjectsFound);
                foreach (var row in comparable)
                {
                    row.DifferenceFromMax = max - row.ObjectsFound;
                    if (row.DifferenceFromMax > 0 && row.Status == "OK") row.Status = "WARNING";
                }
            }

            Directory.CreateDirectory(_config.OutputDirectory);
            JsonStore.WriteAtomic(_config.DcTestStatusFile, rows);
            return rows;
        }, cancellationToken);
    }
}
