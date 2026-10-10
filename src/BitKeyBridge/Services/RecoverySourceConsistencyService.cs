namespace BitKeyBridge;

/// <summary>
/// Compare AD and Entra BitLocker recovery *metadata*. No recovery secrets,
/// rotation, enrollment or write permissions are required.
/// </summary>
public sealed class RecoverySourceConsistencyService
{
    private readonly ActiveDirectoryService _ad;
    public RecoverySourceConsistencyService(AppConfig config) =>
        _ad = new ActiveDirectoryService(config);

    public async Task<RecoverySourceComparison> CompareAsync(
        string exactComputer,
        string? entraDeviceId,
        string? graphToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(exactComputer))
            throw new ArgumentException("Enter an exact computer name.", nameof(exactComputer));

        var output = new RecoverySourceComparison { Computer = exactComputer.Trim() };
        try
        {
            var dc = _ad.GetPreferredWritableDc();
            output.DomainController = dc;
            var computer = await Task.Run(() =>
                _ad.FindComputerByName(dc, exactComputer), ct);
            if (computer is not null)
            {
                var adKeys = await Task.Run(() =>
                    _ad.GetRecoveryMetadataForComputer(dc, computer.DistinguishedName), ct);
                output.AdRecoveryIds.AddRange(adKeys.Select(x => x.RecoveryId)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                output.AdQueried = true;
            }
            else
                output.Findings.Add(new("AD_COMPUTER_UNVERIFIED", DiagnosticSeverity.Warning,
                    EvidenceStrength.NotVerified, "AD computer could not be resolved",
                    "The requested AD computer was not found or not visible. Escrow status cannot be established.",
                    "Check exact computer name, forest/domain and AD read access.", "DC / RSAT workstation"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            output.Findings.Add(new("AD_QUERY_FAILED", DiagnosticSeverity.Warning,
                EvidenceStrength.NotVerified, "AD source lookup failed",
                DiagnosticRedaction.Sanitize(ex.Message), "Verify DC bind and AD computer scope.",
                "Operator workstation"));
        }

        if (string.IsNullOrWhiteSpace(graphToken))
        {
            output.Findings.Add(new("GRAPH_NOT_CONNECTED", DiagnosticSeverity.Info,
                EvidenceStrength.NotVerified, "Microsoft Graph was not authenticated",
                "No cloud recovery objects were requested; this is not an Entra backup failure.",
                "Connect the configured Entra application/account and retry.", "Operator workstation"));
        }
        else
        {
            try
            {
                using var graph = new CloudGraphService();
                var deviceId = entraDeviceId?.Trim() ?? string.Empty;
                if (!Guid.TryParse(deviceId, out _))
                {
                    var devices = await graph.SearchManagedDevicesAsync(
                        graphToken, exactComputer, 100, ct);
                    var matches = devices
                        .Where(x => x.DeviceName.Equals(
                            exactComputer, StringComparison.OrdinalIgnoreCase) &&
                            Guid.TryParse(x.EntraDeviceId, out _))
                        .Select(x => x.EntraDeviceId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (matches.Count != 1)
                    {
                        output.Findings.Add(new("ENTRA_DEVICE_ID_UNVERIFIED", DiagnosticSeverity.Warning,
                            EvidenceStrength.NotVerified,
                            "Entra device ID could not be uniquely established",
                            matches.Count == 0
                                ? "No exact-name Intune device with an Entra device ID was visible."
                                : "More than one exact-name device ID was returned. Do not compare guessed identities.",
                            "Provide the unique Entra deviceId (not directory object id) before comparing.",
                            "Entra / Intune administration"));
                    }
                    else deviceId = matches[0];
                }

                if (Guid.TryParse(deviceId, out _))
                {
                    var resolved = await graph.FindEntraDeviceByDeviceIdAsync(
                        graphToken, deviceId, ct);
                    if (resolved is null)
                    {
                        output.Findings.Add(new("ENTRA_DEVICE_ID_NOT_CONFIRMED",
                            DiagnosticSeverity.Warning, EvidenceStrength.NotVerified,
                            "Supplied GUID was not confirmed as an Entra deviceId",
                            "Microsoft Graph did not return an Entra device whose deviceId matches. " +
                            "The GUID may be an object id or correspond to a deleted/different device.",
                            "Confirm the deviceId in Entra ID / Intune and Graph Device.Read.All permission.",
                            "Entra / Intune administration"));
                    }
                    else if (!resolved.Value.DisplayName.Equals(
                        exactComputer, StringComparison.OrdinalIgnoreCase))
                    {
                        output.Findings.Add(new("ENTRA_DEVICE_NAME_MISMATCH",
                            DiagnosticSeverity.Warning, EvidenceStrength.NotVerified,
                            "Entra deviceId points to a different display name",
                            "AD computer " + exactComputer + " and Entra device " +
                            resolved.Value.DisplayName +
                            " do not have matching names. A rename might be legitimate, " +
                            "but comparison needs explicit identity confirmation.",
                            "Confirm AD/Entra physical device identity before comparing keys.",
                            "Entra / Intune administration"));
                    }
                    else
                    {
                        output.EntraDeviceId = resolved.Value.DeviceId;
                        var keys = await graph.GetRecoveryMetadataForDeviceAsync(
                            graphToken, output.EntraDeviceId, exactComputer, ct);
                        output.EntraRecoveryIds.AddRange(keys
                            .Select(x => x.RecoveryId)
                            .Distinct(StringComparer.OrdinalIgnoreCase));
                        output.EntraQueried = true;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                output.Findings.Add(new("ENTRA_QUERY_FAILED", DiagnosticSeverity.Warning,
                    EvidenceStrength.NotVerified, "Entra recovery metadata query failed",
                    DiagnosticRedaction.Sanitize(ex.Message),
                    "Check Graph BitlockerKey.ReadBasic.All/Read.All consent and the target device ID.",
                    "Entra / Intune admin"));
            }
        }

        output.Findings.AddRange(SmartDiagnosticEngine.ExplainSources(output));
        return output;
    }
}
