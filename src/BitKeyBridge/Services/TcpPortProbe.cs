using System.Net.Sockets;

namespace BitKeyBridge;

internal enum TcpProbeState
{
    Connected,
    Refused,
    TimedOut,
    Unreachable,
    NameResolutionFailed,
    Failed
}

internal sealed record TcpPortProbeResult(
    TcpProbeState State,
    string Detail)
{
    public bool Connected => State == TcpProbeState.Connected;
}

/// <summary>
/// Read-only TCP connect probe with explicit failure classification.
/// Neither a successful connect nor a timeout establishes whether a
/// particular RPC interface is reachable or a firewall is responsible.
/// </summary>
internal static class TcpPortProbe
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    public static async Task<TcpPortProbeResult> CheckAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(DefaultTimeout);

            await client.ConnectAsync(host, port, timeout.Token);

            return new(
                TcpProbeState.Connected,
                "Connected: TCP handshake succeeded; this does not establish " +
                "the remote service identity, RPC authorization or decryption rights.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Describe(TcpProbeState.TimedOut);
        }
        catch (SocketException ex)
        {
            return Describe(ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => TcpProbeState.Refused,
                SocketError.TimedOut => TcpProbeState.TimedOut,
                SocketError.HostUnreachable or
                SocketError.NetworkUnreachable or
                SocketError.NetworkDown => TcpProbeState.Unreachable,
                SocketError.HostNotFound or
                SocketError.NoData or
                SocketError.TryAgain => TcpProbeState.NameResolutionFailed,
                _ => TcpProbeState.Failed
            });
        }
        catch (Exception)
        {
            return Describe(TcpProbeState.Failed);
        }
    }

    internal static TcpPortProbeResult Describe(TcpProbeState state) =>
        state switch
        {
            TcpProbeState.Connected =>
                new(state, "Connected: TCP handshake succeeded."),
            TcpProbeState.Refused =>
                new(state,
                    "Connection refused: the destination actively rejected the TCP connection. " +
                    "This may mean no listener or an explicit firewall REJECT; check the DC listener."),
            TcpProbeState.TimedOut =>
                new(state,
                    "Timed out: no TCP response arrived within four seconds. " +
                    "Firewall DROP, routing, target availability or packet loss are possibilities; " +
                    "this alone does not prove a firewall rule is blocking the port."),
            TcpProbeState.Unreachable =>
                new(state,
                    "Unreachable: the host/network path could not be reached. " +
                    "Check DNS, routing and network policies."),
            TcpProbeState.NameResolutionFailed =>
                new(state,
                    "Name resolution failed: verify the selected DC name and DNS."),
            _ =>
                new(state,
                    "TCP connection failed; check the network path and remote listener.")
        };
}
