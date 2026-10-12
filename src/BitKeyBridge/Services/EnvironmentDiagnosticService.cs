using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.Runtime.InteropServices;

namespace BitKeyBridge;

/// <summary>
/// Probes connection readiness from the machine running BitKeyBridge.
/// No secret attributes are requested and no remote Windows services are started.
/// </summary>
public sealed class EnvironmentDiagnosticService
{
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(5);

    public async Task<EnvironmentDiagnosticReport> CheckAsync(
        AppConfig config,
        string computer,
        bool cloudConfigured,
        CancellationToken cancellationToken)
    {
        var report = new EnvironmentDiagnosticReport
        {
            ComputerName = computer.Trim()
        };

        void Add(string area, string name, EnvironmentCheckState state, string detail)
        {
            report.Checks.Add(new EnvironmentCheckRow(area, name, state,
                DiagnosticRedaction.Sanitize(detail)));
        }

        Add("Client", "Operating system",
            OperatingSystem.IsWindows() ? EnvironmentCheckState.Available : EnvironmentCheckState.Failed,
            Environment.OSVersion.VersionString + " / " + RuntimeInformation.ProcessArchitecture +
            ". Self-contained BitKeyBridge does not require an installed .NET runtime or RSAT.");

        try
        {
            var localDomain = await Task.Run(
                () => Domain.GetComputerDomain().Name, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            Add("Client", "Domain membership", EnvironmentCheckState.Available,
                "Joined to " + localDomain + ". Explicit credentials also support workgroup machines.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            Add("Client", "Domain membership", EnvironmentCheckState.NotVerified,
                "No joined AD domain was confirmed. Workgroup is supported with an explicit DC and AD credentials.");
        }

        var directory = new ActiveDirectoryService(config);
        var dc = string.Empty;
        var canBind = false;
        try
        {
            dc = await Task.Run(() => directory.GetPreferredWritableDc(),
                cancellationToken).WaitAsync(cancellationToken);
            Add("AD", "Writable DC discovery", EnvironmentCheckState.Available,
                "Selected " + dc + ".");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            dc = config.AdServer?.Trim() ?? string.Empty;
            var source = "configured";
            if (string.IsNullOrWhiteSpace(dc))
            {
                var logonServer = Environment.GetEnvironmentVariable("LOGONSERVER")?.Trim();
                if (!string.IsNullOrWhiteSpace(logonServer))
                {
                    dc = logonServer.TrimStart('\\');
                    source = "Windows LOGONSERVER candidate";
                }
            }
            Add("AD", "Writable DC discovery", EnvironmentCheckState.Warning,
                "Discovery/bind was not completed: " + ex.Message +
                (string.IsNullOrWhiteSpace(dc)
                    ? " Set an explicit DC and domain under Start > Advanced when on a workgroup PC."
                    : " Testing " + source + " DC " + dc +
                      " for network reachability only; writable-DC discovery and authentication remain unverified."));
        }

        report.DomainController = dc;
        if (!string.IsNullOrWhiteSpace(dc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(dc, cancellationToken)
                    .WaitAsync(DnsTimeout, cancellationToken);
                var addressText = string.Join(", ", addresses.Select(a => a.ToString()).Take(4));
                Add("Network", "DC DNS resolution",
                    addresses.Length > 0 ? EnvironmentCheckState.Available : EnvironmentCheckState.Failed,
                    addresses.Length > 0 ? dc + " -> " + addressText :
                        "No addresses returned. Check DNS client settings and DC host records.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Add("Network", "DC DNS resolution", EnvironmentCheckState.Failed,
                    "DNS lookup failed: " + ex.Message);
            }

            var configuredPort = Math.Clamp(config.AdPort, 1, 65535);
            var useLdaps = config.AdUseLdaps || configuredPort == 636;
            Add("AD", "Transport configuration", EnvironmentCheckState.Available,
                (useLdaps ? "LDAPS/TLS" : "LDAP signing/sealing") +
                " selected, TCP " + configuredPort +
                ". TLS certificate trust and authenticated identity require an actual LDAP bind.");

            TcpPortProbeResult? rpcMapperProbe = null;
            TcpPortProbeResult? observedKdsProbe = null;

            foreach (var port in EnvironmentDiagnosticPolicy.DomainPorts(useLdaps))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var number = port.Essential ? configuredPort : port.Port;
                var probe = await TcpPortProbe.CheckAsync(dc, number, cancellationToken);

                if (number == 135)
                    rpcMapperProbe = probe;
                if (number == EnvironmentDiagnosticPolicy.ObservedKdsRpcPort)
                    observedKdsProbe = probe;

                // TCP 1344 mattered here previously, but is NOT the defined
                // GKDI endpoint. A closed historical port is informational
                // unless the Endpoint Mapper currently advertises it.
                var observedCandidate =
                    number == EnvironmentDiagnosticPolicy.ObservedKdsRpcPort;

                var state = EnvironmentDiagnosticPolicy.ClassifyPort(
                    probe.State, port.Essential, observedCandidate);

                Add("Network", port.Check + " TCP " + number,
                    state,
                    probe.Detail + " " + port.Purpose);
            }

            if (rpcMapperProbe?.Connected != true)
            {
                Add("RPC", "GKDI / KDS endpoint discovery", EnvironmentCheckState.NotVerified,
                    "Skipped native RPC Endpoint Mapper inquiry because TCP 135 did not connect. " +
                    (rpcMapperProbe?.Detail ?? "TCP 135 was not probed.") +
                    " Diagnose the RPC endpoint mapper/network before investigating a dynamic port.");
            }
            else
            {
                var discovery = await GkdiRpcEndpointDiscovery.DiscoverAsync(
                    dc, cancellationToken);

                var discoveryState = discovery.State switch
                {
                    GkdiEndpointState.Found => EnvironmentCheckState.Available,
                    GkdiEndpointState.Unsupported => EnvironmentCheckState.NotVerified,
                    _ => EnvironmentCheckState.Warning
                };

                Add("RPC", "GKDI / KDS endpoint discovery",
                    discoveryState, discovery.Detail);

                // Probe ONLY the port(s) returned for the exact GKDI UUID.
                // Never scan an entire dynamic RPC range or open firewall rules.
                foreach (var dynamicPort in discovery.Ports)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var probe = dynamicPort == EnvironmentDiagnosticPolicy.ObservedKdsRpcPort &&
                                observedKdsProbe is not null
                        ? observedKdsProbe
                        : await TcpPortProbe.CheckAsync(dc, dynamicPort, cancellationToken);

                    Add("RPC", "GKDI advertised TCP " + dynamicPort,
                        EnvironmentDiagnosticPolicy.ClassifyPort(probe.State, essential: false),
                        probe.Detail +
                        " This port was advertised by RPC Endpoint Mapper for MS-GKDI v1.0. " +
                        "Successful TCP connect alone does not verify decryption permission.");
                }
            }

            try
            {
                var root = await Task.Run(() => directory.TestConnection(dc),
                    cancellationToken).WaitAsync(cancellationToken);
                canBind = true;
                Add("AD", "Authenticated LDAP bind", EnvironmentCheckState.Available,
                    "LDAPv3 Negotiate bind succeeded; default naming context: " +
                    root.GetValueOrDefault("defaultNamingContext", "(not returned)") +
                    ". This does not test confidential BitLocker/LAPS attributes.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Add("AD", "Authenticated LDAP bind", EnvironmentCheckState.Failed,
                    "Bind failed: " + ex.Message +
                    " Check selected credentials, signing/LDAPS, DC certificate and port.");
            }

            Add("DC", "KDS / DPAPI-NG decryption", EnvironmentCheckState.NotVerified,
                EnvironmentDiagnosticPolicy.KdsVerificationDetail);
            Add("DC", "Server-side services", EnvironmentCheckState.NotVerified,
                "TCP probes cannot prove NTDS, Kdc, Netlogon, RpcSs, RpcEptMapper or KdsSvc health. " +
                "ON DC (PowerShell): Get-Service NTDS,Kdc,Netlogon,RpcSs,RpcEptMapper,KdsSvc,W32Time. " +
                "KdsSvc may be trigger-started; Stopped alone is not proof of failure.");

            if (!string.IsNullOrWhiteSpace(report.ComputerName))
            {
                if (!canBind)
                {
                    Add("AD", "Computer / recovery metadata", EnvironmentCheckState.Skipped,
                        "Skipped until authenticated LDAP bind works.");
                    Add("AD LAPS", "Backup indicators", EnvironmentCheckState.Skipped,
                        "Skipped until authenticated LDAP bind works.");
                }
                else
                {
                    await CheckComputerAsync(directory, config, dc, report, Add, cancellationToken);
                }
            }
        }
        else
        {
            Add("Network", "DC DNS and ports", EnvironmentCheckState.Skipped,
                "No DC identified. Specify a DC/domain in Start > Advanced.");
            Add("AD", "Authenticated LDAP bind", EnvironmentCheckState.Skipped,
                "No DC identified.");
            Add("DC", "KDS / DPAPI-NG decryption", EnvironmentCheckState.NotVerified,
                EnvironmentDiagnosticPolicy.KdsVerificationDetail);
        }

        if (cloudConfigured)
        {
            foreach (var host in new[] { "login.microsoftonline.com", "graph.microsoft.com" })
            {
                var probe = await TcpPortProbe.CheckAsync(host, 443, cancellationToken);
                Add("Cloud", host + " TCP 443",
                    probe.Connected ? EnvironmentCheckState.Available : EnvironmentCheckState.Warning,
                    probe.Detail +
                    " Microsoft Graph consent, roles and sign-in are NOT verified.");
            }
        }
        else
        {
            Add("Cloud", "Entra/Intune connectivity", EnvironmentCheckState.Skipped,
                "No cloud configuration. Cloud endpoints are not required for on-premises recovery.");
        }

        Add("Permissions", "BitLocker recovery-password read", EnvironmentCheckState.NotVerified,
            "No msFVE-RecoveryPassword value was requested. Metadata visibility cannot establish read rights.");
        Add("Permissions", "LAPS password read/decrypt", EnvironmentCheckState.NotVerified,
            "No password attributes or DPAPI-NG decrypt tests were attempted. Use the LAPS diagnostics for separate authorizations.");

        return report;
    }

    private static async Task CheckComputerAsync(
        ActiveDirectoryService directory,
        AppConfig config,
        string dc,
        EnvironmentDiagnosticReport report,
        Action<string, string, EnvironmentCheckState, string> add,
        CancellationToken ct)
    {
        try
        {
            var computer = await Task.Run(
                () => directory.FindComputerByName(dc, report.ComputerName), ct).WaitAsync(ct);

            if (computer is null)
            {
                add("AD", "Computer object", EnvironmentCheckState.Warning,
                    "Exact computer name not found or not visible. Enter the real AD computer name; " +
                    "a Recovery ID, partial name or Entra device ID is not an exact AD computer name.");
                return;
            }

            add("AD", "Computer object", EnvironmentCheckState.Available,
                "Resolved " + computer.ComputerName + " at " + computer.DistinguishedName + ".");

            try
            {
                var metadata = await Task.Run(
                    () => directory.GetRecoveryMetadataForComputer(dc, computer.DistinguishedName),
                    ct).WaitAsync(ct);
                add("BitLocker", "Recovery-object metadata",
                    metadata.Count > 0 ? EnvironmentCheckState.Available : EnvironmentCheckState.Warning,
                    EnvironmentDiagnosticPolicy.BitLockerMetadataDetail(metadata.Count));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                add("BitLocker", "Recovery-object metadata", EnvironmentCheckState.Warning,
                    "Metadata lookup failed: " + ex.Message +
                    ". This does not prove a backup is absent.");
            }

            try
            {
                var credential = AdSessionCredentials.CreateNetworkCredential(config);
                var laps = await Task.Run(
                    () => new LapsDirectoryService(config, credential)
                        .CheckAccess(computer.DistinguishedName, ct),
                    ct).WaitAsync(ct);
                var indicator = laps.Checks.Any(x =>
                    x.Name.EndsWith("backup indicator", StringComparison.OrdinalIgnoreCase) &&
                    x.State == LapsAccessState.Available);
                add("AD LAPS", "Backup indicators",
                    indicator ? EnvironmentCheckState.Available : EnvironmentCheckState.Warning,
                    EnvironmentDiagnosticPolicy.LapsMetadataDetail(indicator));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                add("AD LAPS", "Backup indicators", EnvironmentCheckState.Warning,
                    "Metadata check failed: " + ex.Message +
                    ". No secret read or decryption was attempted.");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            add("AD", "Computer object", EnvironmentCheckState.Warning,
                "Unable to resolve the exact AD computer: " + ex.Message);
        }
    }

}
