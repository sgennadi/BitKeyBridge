using System.DirectoryServices.Protocols;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace BitKeyBridge;

public sealed record ConnectionInspectionRow(
    string Check, string Server, string Status, string Detail, double? LatencyMs = null);

/// <summary>DNS SRV, site/DC, LDAP and TLS examination. Read-only; no failover
/// or directory configuration modifications are performed automatically.</summary>
public sealed class AdvancedConnectionDiagnosticService
{
    private readonly AppConfig _config;
    private readonly ActiveDirectoryService _ad;

    public AdvancedConnectionDiagnosticService(AppConfig config)
    {
        _config = config;
        _ad = new ActiveDirectoryService(config);
    }

    public async Task<IReadOnlyList<ConnectionInspectionRow>> RunAsync(
        CancellationToken ct = default)
    {
        var checks = new List<ConnectionInspectionRow>();
        var domain = !string.IsNullOrWhiteSpace(_config.AdDomain)
            ? _config.AdDomain : _ad.GetCurrentDomainName();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var srv = WindowsDnsSrvLookup("_ldap._tcp.dc._msdcs." + domain);
                foreach (var record in srv.Take(24))
                    checks.Add(new("AD DNS SRV", record.Target, "Observed",
                        "Port " + record.Port + ", priority " + record.Priority +
                        ", weight " + record.Weight + ". Existence does not validate connectivity."));
                if (srv.Count == 0)
                    checks.Add(new("AD DNS SRV", domain, "Warning",
                        "DNS returned no DC service records. Check AD-integrated DNS and client resolver."));
            }
            catch (Exception ex)
            {
                checks.Add(new("AD DNS SRV", domain, "NotVerified",
                    DiagnosticRedaction.Sanitize(ex.Message)));
            }
        }
        else
            checks.Add(new("AD DNS SRV", domain, "Skipped", "Native Windows DNS resolver is required."));

        var dcs = await Task.Run(() => _ad.DiscoverDomainControllers(domain), ct);
        foreach (var dc in dcs
            .Where(x => !string.IsNullOrWhiteSpace(x.HostName))
            .GroupBy(x => x.HostName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First()).Take(12))
        {
            ct.ThrowIfCancellationRequested();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var root = await Task.Run(() => _ad.TestConnection(dc.HostName), ct);
                watch.Stop();
                checks.Add(new("Authenticated LDAP", dc.HostName, "OK",
                    "Site: " + dc.Site + "; " + (dc.IsReadOnly ? "RODC" : "writable DC") +
                    "; naming context: " + root.GetValueOrDefault("defaultNamingContext", "not returned") +
                    ". TCP 389/636 and bind are validated; passwords not read.",
                    Math.Round(watch.Elapsed.TotalMilliseconds)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                checks.Add(new("Authenticated LDAP", dc.HostName, "Failed",
                    "Site: " + dc.Site + ". " +
                    DiagnosticRedaction.Sanitize(ex.Message) +
                    ". Do not treat an authentication failure as a reason to retry with weaker security."));
            }
        }

        var preferred = _config.AdConnectionMode.Equals(
            "Explicit", StringComparison.OrdinalIgnoreCase)
            ? _config.AdServer : _ad.GetPreferredWritableDc();

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var cert = await CheckTlsAsync(preferred, ct);
            checks.Add(cert);
        }
        return checks;
    }

    private static async Task<ConnectionInspectionRow> CheckTlsAsync(string host, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(7));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, 636, timeout.Token);
            string details = "No server certificate was received.";
            SslPolicyErrors errors = SslPolicyErrors.None;

            using var tls = new SslStream(tcp.GetStream(), false, (sender, certificate, chain, policyErrors) =>
            {
                errors = policyErrors;
                if (certificate is not null)
                {
                    using var x509 = new X509Certificate2(certificate);
                    var chainErrors = chain is null ? "" :
                        string.Join(",", chain.ChainStatus.Select(x => x.Status.ToString()));
                    details = "Subject: " + x509.Subject +
                              "; expiry UTC: " + x509.NotAfter.ToUniversalTime().ToString("O") +
                              "; SAN: " + (x509.Extensions.OfType<X509SubjectAlternativeNameExtension>()
                                  .FirstOrDefault()?.Format(false) ?? "not returned") +
                              "; chain: " + chainErrors +
                              "; policy: " + policyErrors;
                }
                return policyErrors == SslPolicyErrors.None;
            });
            try
            {
                await tls.AuthenticateAsClientAsync(host, timeout.Token);
                return new("LDAPS TLS identity", host, "OK",
                    "Certificate trusted for this hostname. " + details);
            }
            catch (Exception ex) when (ex is System.Security.Authentication.AuthenticationException or IOException)
            {
                return new("LDAPS TLS identity", host, "Warning",
                    "TLS validation failed; SSL trust was never bypassed. " +
                    details + "; " + DiagnosticRedaction.Sanitize(ex.Message));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new("LDAPS TLS identity", host, "NotVerified",
                "Port 636 or TLS handshake unavailable: " + DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static List<(string Target, int Port, int Priority, int Weight)> WindowsDnsSrvLookup(string query)
    {
        const ushort srvType = 33;
        var result = new List<(string, int, int, int)>();
        var code = DnsQueryW(query, srvType, 0, IntPtr.Zero, out var head, IntPtr.Zero);
        if (code != 0)
            throw new InvalidOperationException("DnsQuery SRV returned error " + code + ".");
        try
        {
            var record = head;
            for (var index = 0; record != IntPtr.Zero && index < 256; index++)
            {
                var entry = Marshal.PtrToStructure<DnsRecord>(record);
                if (entry.Type == srvType)
                {
                    var srvPointer = IntPtr.Add(record, Marshal.SizeOf<DnsRecord>());
                    var srv = Marshal.PtrToStructure<DnsSrvData>(srvPointer);
                    var target = Marshal.PtrToStringUni(srv.TargetName) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(target))
                        result.Add((target.TrimEnd('.'), srv.Port, srv.Priority, srv.Weight));
                }
                record = entry.Next;
            }
        }
        finally
        {
            if (head != IntPtr.Zero) DnsRecordListFree(head, 1);
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DnsRecord
    {
        public IntPtr Next;
        public IntPtr Name;
        public ushort Type;
        public ushort DataLength;
        public uint Flags;
        public uint TimeToLive;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DnsSrvData
    {
        public IntPtr TargetName;
        public ushort Priority;
        public ushort Weight;
        public ushort Port;
        public ushort Padding;
    }

    [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode,
        ExactSpelling = true)]
    private static extern int DnsQueryW(
        string name, ushort type, uint options, IntPtr extra, out IntPtr results, IntPtr reserved);

    [DllImport("dnsapi.dll", ExactSpelling = true)]
    private static extern void DnsRecordListFree(IntPtr records, int freeType);
}
