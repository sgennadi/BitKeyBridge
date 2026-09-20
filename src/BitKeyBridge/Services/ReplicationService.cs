using System.DirectoryServices.ActiveDirectory;

namespace BitKeyBridge;

public sealed class ReplicationService
{
    private readonly AppConfig _config;

    public ReplicationService(AppConfig config) => _config = config;

    public ReplicationHealthResult Check(string server)
    {
        var result = new ReplicationHealthResult
        {
            Server = server,
            Checked = DateTime.Now,
            Healthy = true
        };

        try
        {
            var context = new ActiveDirectoryService(_config)
                .CreateDirectoryContext(DirectoryContextType.DirectoryServer, server);
            using var dc = DomainController.GetDomainController(context);
            var neighbors = dc.GetAllReplicationNeighbors();
            if (neighbors.Count == 0)
                result.Warnings.Add("No inbound replication neighbors were returned.");

            foreach (ReplicationNeighbor neighbor in neighbors)
            {
                var lastSuccess = neighbor.LastSuccessfulSync == DateTime.MinValue ? (DateTime?)null : neighbor.LastSuccessfulSync;
                var age = lastSuccess.HasValue ? (DateTime.Now - lastSuccess.Value).TotalHours : (double?)null;
                var status = "OK";

                if (neighbor.LastSyncResult != 0)
                {
                    status = "ERROR";
                    result.Errors.Add($"{neighbor.SourceServer} | {neighbor.PartitionName} | LastSyncResult={neighbor.LastSyncResult} | {neighbor.LastSyncMessage}");
                }
                else if (!lastSuccess.HasValue)
                {
                    status = "WARNING";
                    result.Warnings.Add($"{neighbor.SourceServer} | {neighbor.PartitionName} | Last successful sync unavailable");
                }
                else if (age > _config.ReplicationStaleHours)
                {
                    status = "STALE";
                    result.Warnings.Add($"{neighbor.SourceServer} | {neighbor.PartitionName} | Last success is {age:0.00} hour(s) old");
                }

                result.Partners.Add(new ReplicationPartnerInfo
                {
                    SourceServer = neighbor.SourceServer,
                    Partition = neighbor.PartitionName,
                    LastAttemptedSync = neighbor.LastAttemptedSync == DateTime.MinValue ? null : neighbor.LastAttemptedSync,
                    LastSuccessfulSync = lastSuccess,
                    LastSyncResult = neighbor.LastSyncResult,
                    ConsecutiveFailureCount = neighbor.ConsecutiveFailureCount,
                    AgeHours = age,
                    Status = status,
                    Message = neighbor.LastSyncMessage
                });
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add("Replication query failed: " + ex.Message);
        }

        result.Healthy = result.Errors.Count == 0;
        result.HasWarnings = result.Errors.Count > 0 || result.Warnings.Count > 0;
        return result;
    }
}
