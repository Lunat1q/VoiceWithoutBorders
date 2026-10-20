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

    public async Task OpenAsync(string peerHost, int port, string sharedKey, CancellationToken cancellationToken = default)
    {
        Close();
        var addresses = await Dns.GetHostAddressesAsync(peerHost, AddressFamily.InterNetwork, cancellationToken);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"Could not resolve '{peerHost}' to an IPv4 address.");

        var codec = await Task.Run(() => PacketCodec.FromSharedKey(sharedKey), cancellationToken);
        var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        udp.Client.IOControl(SioUdpConnReset, [0], null);

        _peer = new IPEndPoint(addresses[0], port);
        _codec = codec;
        _udp = udp;
        _cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _sent, 0);
        Interlocked.Exchange(ref _received, 0);
        Interlocked.Exchange(ref _rejected, 0);
        _ = ReceiveLoopAsync(udp, codec, _cts.Token);
        logger.LogInformation("Listening on UDP {Port}, peer {Peer}", port, _peer);
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
