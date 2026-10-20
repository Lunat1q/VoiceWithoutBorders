using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// UDP link: both machines bind the same port and send to each other. Lost packets are simply skipped,
/// which beats waiting for retransmits in live audio.
/// </summary>
public sealed class UdpPeerTransport(ILogger<UdpPeerTransport> logger) : IPeerTransport
{
    // Windows reports an ICMP "port unreachable" as a reset on the next receive while the peer is offline.
    private const int SioUdpConnReset = -1744830452;

    private UdpClient? _udp;
    private IPEndPoint? _peer;
    private PacketCodec? _codec;
    private CancellationTokenSource? _cts;
    private long _sent;
    private long _received;
    private long _rejected;

    public event Action<StreamKind, ArraySegment<byte>>? AudioReceived;

    public TransportStats Stats => new(Interlocked.Read(ref _sent), Interlocked.Read(ref _received), Interlocked.Read(ref _rejected));

    public async Task<IPEndPoint> OpenAsync(string peerHost, int port, string sharedKey, CancellationToken cancellationToken = default)
    {
        Close();
        var address = ChooseAddress(await ResolveAsync(peerHost, cancellationToken), Socket.OSSupportsIPv6)
            ?? throw NotFound(peerHost);

        var codec = await Task.Run(() => PacketCodec.FromSharedKey(sharedKey), cancellationToken);
        var udp = CreateSocket(port);
        var peer = new IPEndPoint(address, port);
        _peer = peer;
        _codec = codec;
        _udp = udp;
        _cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _sent, 0);
        Interlocked.Exchange(ref _received, 0);
        Interlocked.Exchange(ref _rejected, 0);
        _ = ReceiveLoopAsync(udp, codec, _cts.Token);
        logger.LogInformation("Listening on UDP {Port}, peer {Peer}", port, peer);
        return peer;
    }

    /// <summary>
    /// IPv4 first (what most LANs route best), otherwise IPv6 in the resolver's preferred order: computer names on
    /// home networks often resolve over LLMNR/mDNS to IPv6 addresses only.
    /// </summary>
    public static IPAddress? ChooseAddress(IEnumerable<IPAddress> addresses, bool ipv6Supported) =>
        addresses
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork || (ipv6Supported && a.AddressFamily == AddressFamily.InterNetworkV6))
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetworkV6)
            .FirstOrDefault();

    private static async Task<IPAddress[]> ResolveAsync(string peerHost, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(peerHost, cancellationToken);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain)
        {
            throw NotFound(peerHost);
        }
    }

    private static InvalidOperationException NotFound(string peerHost) =>
        new($"Can't find '{peerHost}' on the network. Enter its IP address instead (run ipconfig on that computer).");

    /// <summary>Dual-stack: one port takes both IPv4 and IPv6, so either peer address works.</summary>
    private static UdpClient CreateSocket(int port)
    {
        var udp = Socket.OSSupportsIPv6 ? new UdpClient(AddressFamily.InterNetworkV6) : new UdpClient(AddressFamily.InterNetwork);
        try
        {
            if (Socket.OSSupportsIPv6)
                udp.Client.DualMode = true;
            udp.Client.Bind(new IPEndPoint(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, port));
            udp.Client.IOControl(SioUdpConnReset, [0], null);
            return udp;
        }
        catch
        {
            udp.Dispose();
            throw;
        }
    }

    public void Send(StreamKind kind, ArraySegment<byte> pcm)
    {
        if (_udp is not { } udp || _codec is not { } codec || _peer is not { } peer)
            return;
        try
        {
            udp.Client.SendTo(codec.Encode(kind, pcm), peer);
            Interlocked.Increment(ref _sent);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            logger.LogDebug(ex, "Send failed");
        }
    }

    private async Task ReceiveLoopAsync(UdpClient udp, PacketCodec codec, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(cancellationToken);
                if (!codec.TryDecode(result.Buffer, out var kind, out var pcm))
                {
                    Interlocked.Increment(ref _rejected);
                    continue;
                }
                Interlocked.Increment(ref _received);
                AudioReceived?.Invoke(kind, pcm);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Receive failed");
            }
        }
    }

    public void Close()
    {
        _cts?.Cancel();
        _udp?.Dispose();
        _codec?.Dispose();
        _cts?.Dispose();
        (_cts, _udp, _codec, _peer) = (null, null, null, null);
    }

    public void Dispose() => Close();
}
