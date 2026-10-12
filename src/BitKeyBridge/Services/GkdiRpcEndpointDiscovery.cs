using System.Globalization;
using System.Runtime.InteropServices;

namespace BitKeyBridge;

internal enum GkdiEndpointState
{
    Found,
    NotRegistered,
    Unavailable,
    Unsupported
}

internal sealed record GkdiEndpointDiscoveryResult(
    GkdiEndpointState State,
    IReadOnlyList<int> Ports,
    string Detail);

/// <summary>
/// Read-only RPC Endpoint Mapper (EPM) inquiry for MS-GKDI v1.0.
/// No GKDI password operations, RPC bind to the GKDI interface, broad port
/// scan, or remote service manipulation are performed.
/// </summary>
internal static class GkdiRpcEndpointDiscovery
{
    // MS-GKDI section 2.1: dynamically registered ncacn_ip_tcp interface.
    internal const string InterfaceUuid = "b9785960-524f-11df-8b6d-83dcded72085";

    private const int RpcOk = 0;
    private const int RpcNoMoreEntries = 1772;
    private const int RpcEndpointNotRegistered = 1753;
    private const int RpcServerUnavailable = 1722;
    private const int RpcCallCancelled = 1818;
    private const uint RpcEpMatchByIf = 1;
    private const uint RpcVersionExact = 3;
    private const uint RpcCallTimeoutOption = 12;
    private const int RpcTimeoutMilliseconds = 6000;
    private const int MaximumEntries = 24;

    [StructLayout(LayoutKind.Sequential)]
    private struct RpcIfId
    {
        public Guid Uuid;
        public ushort VersMajor;
        public ushort VersMinor;
    }

    internal static async Task<GkdiEndpointDiscoveryResult> DiscoverAsync(
        string domainController,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return new(
                GkdiEndpointState.Unsupported,
                [],
                "Native RPC Endpoint Mapper inquiry is only available on Windows.");
        }

        try
        {
            // Native RPC calls are synchronous. Bound their RPC call lifetime
            // AND the GUI's await time; never block the WinForms UI thread.
            return await Task.Run(
                    () => DiscoverNative(domainController),
                    cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(9), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new(
                GkdiEndpointState.Unavailable,
                [],
                "RPC Endpoint Mapper query timed out. TCP 135 may accept connections " +
                "even when the RPC endpoint-map operation is filtered or unresponsive.");
        }
        catch (DllNotFoundException)
        {
            return new(GkdiEndpointState.Unsupported, [],
                "Windows rpcrt4.dll is unavailable.");
        }
        catch (EntryPointNotFoundException)
        {
            return new(GkdiEndpointState.Unsupported, [],
                "Native RPC Endpoint Mapper inquiry APIs are unavailable.");
        }
        catch (Exception ex)
        {
            return new(GkdiEndpointState.Unavailable, [],
                "RPC inquiry failed: " + DiagnosticRedaction.Sanitize(ex.Message));
        }
    }

    private static GkdiEndpointDiscoveryResult DiscoverNative(string dc)
    {
        IntPtr stringBinding = IntPtr.Zero;
        IntPtr epBinding = IntPtr.Zero;
        IntPtr inquiry = IntPtr.Zero;
        var ports = new HashSet<int>();

        try
        {
            var status = RpcStringBindingComposeW(
                null, "ncacn_ip_tcp", dc, null, null, out stringBinding);
            if (status != RpcOk)
                return Failure(status, "Compose RPC Endpoint Mapper binding");

            status = RpcBindingFromStringBindingW(stringBinding, out epBinding);
            if (status != RpcOk)
                return Failure(status, "Create RPC Endpoint Mapper binding");

            // RPC_C_OPT_CALL_TIMEOUT is expressed in milliseconds.
            status = RpcBindingSetOption(
                epBinding, RpcCallTimeoutOption, (nuint)RpcTimeoutMilliseconds);
            if (status != RpcOk)
                return Failure(status, "Set bounded RPC call timeout");

            var requested = new RpcIfId
            {
                Uuid = Guid.Parse(InterfaceUuid),
                VersMajor = 1,
                VersMinor = 0
            };

            status = RpcMgmtEpEltInqBegin(
                epBinding,
                RpcEpMatchByIf,
                ref requested,
                RpcVersionExact,
                IntPtr.Zero,
                out inquiry);

            if (status == RpcEndpointNotRegistered)
                return NotRegistered();
            if (status != RpcOk)
                return Failure(status, "Open GKDI RPC Endpoint Mapper inquiry");

            for (var index = 0; index < MaximumEntries; index++)
            {
                IntPtr endpointBinding = IntPtr.Zero;
                try
                {
                    status = RpcMgmtEpEltInqNextW(
                        inquiry,
                        out var returnedInterface,
                        out endpointBinding,
                        IntPtr.Zero,
                        IntPtr.Zero);

                    if (status == RpcNoMoreEntries ||
                        status == RpcEndpointNotRegistered)
                    {
                        return Finished(ports);
                    }

                    if (status != RpcOk)
                        return Failure(status, "Read GKDI RPC endpoint entry");

                    if (returnedInterface.Uuid != requested.Uuid ||
                        returnedInterface.VersMajor != 1 ||
                        returnedInterface.VersMinor != 0)
                        continue;

                    if (endpointBinding != IntPtr.Zero &&
                        TryReadTcpEndpoint(endpointBinding, out var port))
                    {
                        ports.Add(port);
                    }
                }
                finally
                {
                    if (endpointBinding != IntPtr.Zero)
                        _ = RpcBindingFree(ref endpointBinding);
                }
            }

            return new(
                GkdiEndpointState.Unavailable,
                ports.OrderBy(x => x).ToArray(),
                "GKDI endpoint query exceeded its bounded 24-entry limit; " +
                "the partial result is not conclusive.");
        }
        finally
        {
            if (inquiry != IntPtr.Zero)
                _ = RpcMgmtEpEltInqDone(ref inquiry);
            if (epBinding != IntPtr.Zero)
                _ = RpcBindingFree(ref epBinding);
            if (stringBinding != IntPtr.Zero)
                _ = RpcStringFreeW(ref stringBinding);
        }
    }

    private static GkdiEndpointDiscoveryResult Finished(HashSet<int> ports)
    {
        if (ports.Count == 0)
            return NotRegistered();

        var sorted = ports.OrderBy(x => x).ToArray();
        return new(
            GkdiEndpointState.Found,
            sorted,
            "RPC Endpoint Mapper returned MS-GKDI v1.0 (" + InterfaceUuid +
            ") with ncacn_ip_tcp port(s): " +
            string.Join(", ", sorted.Select(x => x.ToString(CultureInfo.InvariantCulture))) +
            ". Endpoint registration alone does NOT verify GKDI authentication or decryption.");
    }

    private static GkdiEndpointDiscoveryResult NotRegistered() =>
        new(
            GkdiEndpointState.NotRegistered,
            [],
            "The selected DC did not advertise a usable ncacn_ip_tcp " +
            "MS-GKDI v1.0 endpoint. This can indicate a missing registration, " +
            "service availability or restricted endpoint enumeration; " +
            "it does not establish that LAPS data is absent.");

    private static GkdiEndpointDiscoveryResult Failure(int status, string operation)
    {
        var detail = status switch
        {
            RpcServerUnavailable =>
                "RPC server unavailable (1722). Verify TCP 135 and the endpoint-map service.",
            RpcCallCancelled =>
                "RPC inquiry call timed out or was canceled (1818). A network firewall may be filtering RPC.",
            _ => "RPC status " + status.ToString(CultureInfo.InvariantCulture) +
                 ". Investigate RPC service availability and access."
        };
        return new(GkdiEndpointState.Unavailable, [], operation + ": " + detail);
    }

    private static bool TryReadTcpEndpoint(IntPtr binding, out int port)
    {
        port = 0;
        IntPtr bindingString = IntPtr.Zero;
        IntPtr protocol = IntPtr.Zero;
        IntPtr endpoint = IntPtr.Zero;

        try
        {
            if (RpcBindingToStringBindingW(binding, out bindingString) != RpcOk ||
                bindingString == IntPtr.Zero)
                return false;

            if (RpcStringBindingParseW(
                    bindingString,
                    IntPtr.Zero,
                    out protocol,
                    IntPtr.Zero,
                    out endpoint,
                    IntPtr.Zero) != RpcOk)
                return false;

            var transport = Marshal.PtrToStringUni(protocol);
            var endpointText = Marshal.PtrToStringUni(endpoint);
            return TryParseTcpEndpoint(transport, endpointText, out port);
        }
        finally
        {
            if (endpoint != IntPtr.Zero)
                _ = RpcStringFreeW(ref endpoint);
            if (protocol != IntPtr.Zero)
                _ = RpcStringFreeW(ref protocol);
            if (bindingString != IntPtr.Zero)
                _ = RpcStringFreeW(ref bindingString);
        }
    }

    internal static bool TryParseTcpEndpoint(
        string? transport, string? endpoint, out int port)
    {
        port = 0;
        return string.Equals(
                   transport, "ncacn_ip_tcp", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(
                   endpoint, NumberStyles.None, CultureInfo.InvariantCulture, out port) &&
               port is >= 1 and <= 65535;
    }

    [DllImport("Rpcrt4.dll", EntryPoint = "RpcStringBindingComposeW",
        CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RpcStringBindingComposeW(
        string? objectUuid,
        string protocol,
        string networkAddress,
        string? endpoint,
        string? options,
        out IntPtr stringBinding);

    [DllImport("Rpcrt4.dll", EntryPoint = "RpcBindingFromStringBindingW",
        CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RpcBindingFromStringBindingW(
        string stringBinding, out IntPtr binding);

    [DllImport("Rpcrt4.dll", ExactSpelling = true)]
    private static extern int RpcBindingSetOption(
        IntPtr binding, uint option, nuint value);

    [DllImport("Rpcrt4.dll", ExactSpelling = true)]
    private static extern int RpcMgmtEpEltInqBegin(
        IntPtr endpointBinding, uint inquiryType, ref RpcIfId interfaceId,
        uint versionOption, IntPtr objectUuid, out IntPtr context);

    [DllImport("Rpcrt4.dll", EntryPoint = "RpcMgmtEpEltInqNextW",
        ExactSpelling = true)]
    private static extern int RpcMgmtEpEltInqNextW(
        IntPtr context, out RpcIfId interfaceId,
        out IntPtr binding, IntPtr objectUuid, IntPtr annotation);

    [DllImport("Rpcrt4.dll", ExactSpelling = true)]
    private static extern int RpcMgmtEpEltInqDone(ref IntPtr context);

    [DllImport("Rpcrt4.dll", EntryPoint = "RpcBindingToStringBindingW",
        ExactSpelling = true)]
    private static extern int RpcBindingToStringBindingW(
        IntPtr binding, out IntPtr stringBinding);

    [DllImport("Rpcrt4.dll", EntryPoint = "RpcStringBindingParseW",
        ExactSpelling = true)]
    private static extern int RpcStringBindingParseW(
        IntPtr stringBinding,
        IntPtr objectUuid,
        out IntPtr protocol,
        IntPtr networkAddress,
        out IntPtr endpoint,
        IntPtr networkOptions);

    [DllImport("Rpcrt4.dll", EntryPoint = "RpcStringFreeW",
        ExactSpelling = true)]
    private static extern int RpcStringFreeW(ref IntPtr value);

    [DllImport("Rpcrt4.dll", ExactSpelling = true)]
    private static extern int RpcBindingFree(ref IntPtr binding);
}
