using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// Idle-time discovery on the configured port: answers authentic requests and runs searches by IPv4 broadcast.
/// Only a computer with the shared key can ask or answer; others get nothing back.
/// </summary>
// ponytail: IPv4 broadcast only. Add an ff02::1 multicast per interface if an IPv6-only LAN ever needs it.
public sealed class LanDiscovery(ILogger<LanDiscovery> logger) : ILanDiscovery
{
    // Windows reports an ICMP "port unreachable" as a reset on the next receive.
    private const int SioUdpConnReset = -1744830452;

    public const int SearchRounds = 4;
    public static readonly TimeSpan RoundInterval = TimeSpan.FromMilliseconds(500);

    private readonly Lock _codecLock = new();
    private PacketCodec? _codec;
    private string? _codecKey;

    private UdpClient? _udp;
    private int _port;
    private CancellationTokenSource? _cts;
    private volatile string _sharedKey = "";
    private volatile Action<ulong, DiscoveredPeer>? _onReply;

    public bool IsListening => _udp is not null;

    public void Listen(int port, string sharedKey)
    {
        _sharedKey = sharedKey;
        if (_udp is not null && _port == port)
            return;
        StopListening();
        _port = port;
        var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        try
        {
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            udp.Client.IOControl(SioUdpConnReset, [0], null);
        }
        catch (SocketException ex)
        {
            logger.LogInformation(ex, "Discovery cannot take UDP {Port}", port);
            udp.Dispose();
            return;
        }
        (_udp, _cts) = (udp, new CancellationTokenSource());
        _ = ReceiveLoopAsync(udp, _cts.Token);
    }

    public void StopListening()
    {
        _cts?.Cancel();
        _udp?.Dispose();
        _cts?.Dispose();
        (_udp, _cts) = (null, null);
    }

    public async Task<IReadOnlyList<DiscoveredPeer>> SearchAsync(CancellationToken cancellationToken = default)
    {
        if (_udp is not { } udp)
            throw new InvalidOperationException($"UDP port {_port} is in use by another program.");
        var key = _sharedKey;
        var codec = await Task.Run(() => CodecFor(key), cancellationToken);
        var targets = BroadcastTargets(LocalNetworks()).Select(address => new IPEndPoint(address, _port)).ToList();
        var found = new ConcurrentDictionary<ulong, DiscoveredPeer>();
        _onReply = (instance, peer) => found.TryAdd(instance, peer);
        try
        {
            var request = new DiscoveryPayload(DiscoveryPayload.ThisInstance, "").ToBytes();
            for (var round = 0; round < SearchRounds; round++)
            {
                foreach (var target in targets)
                    TrySend(udp, codec.Encode(PacketType.DiscoverRequest, request), target);
                await Task.Delay(RoundInterval, cancellationToken);
            }
        }
        finally
        {
            _onReply = null;
        }
        return found.Values.OrderBy(peer => peer.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The limited broadcast plus each network's own broadcast address: Windows sends the limited one out of a
    /// single adapter only.
    /// </summary>
    public static IReadOnlyList<IPAddress> BroadcastTargets(IEnumerable<(IPAddress Address, IPAddress Mask)> networks) =>
        networks
            .Where(n => n.Address.AddressFamily == AddressFamily.InterNetwork && n.Mask.AddressFamily == AddressFamily.InterNetwork)
            .Select(n =>
            {
                var address = n.Address.GetAddressBytes();
                var mask = n.Mask.GetAddressBytes();
                for (var i = 0; i < 4; i++)
                    address[i] |= (byte)~mask[i];
                return new IPAddress(address);
            })
            .Prepend(IPAddress.Broadcast)
            .Distinct()
            .ToList();

    private static IEnumerable<(IPAddress, IPAddress)> LocalNetworks() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(unicast => (unicast.Address, unicast.IPv4Mask));

    /// <summary>PBKDF2 is slow, so one codec per key, made when first needed.</summary>
    private PacketCodec CodecFor(string key)
    {
        lock (_codecLock)
        {
            if (_codec is null || _codecKey != key)
            {
                // ponytail: a replaced codec is left to the GC rather than disposed, since a search may still hold it.
                _codec = PacketCodec.FromSharedKey(key);
                _codecKey = key;
            }
            return _codec;
        }
    }

    private async Task ReceiveLoopAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(cancellationToken);
                var packet = result.Buffer;
                // Cheap filter first: a peer streaming at this computer sends hundreds of audio packets a second.
                if (packet.Length < 2 || packet[1] is not ((byte)PacketType.DiscoverRequest or (byte)PacketType.DiscoverReply)
                    || _sharedKey.Length < PacketCodec.MinSharedKeyLength)
                    continue;
                var codec = CodecFor(_sharedKey);
                if (!codec.TryDecode(packet, out var type, out var payload))
                    continue;
                if (DiscoveryPayload.Answer(codec, type, payload) is { } reply)
                    TrySend(udp, reply, result.RemoteEndPoint);
                else if (type == PacketType.DiscoverReply && DiscoveryPayload.TryRead(payload, out var answer)
                         && answer.Instance != DiscoveryPayload.ThisInstance)
                    _onReply?.Invoke(answer.Instance, new DiscoveredPeer(answer.Name, result.RemoteEndPoint.Address));
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discovery receive failed");
            }
        }
    }

    private void TrySend(UdpClient udp, byte[] packet, IPEndPoint target)
    {
        try
        {
            udp.Client.SendTo(packet, target);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            logger.LogDebug(ex, "Discovery send to {Target} failed", target);
        }
    }

    public void Dispose() => StopListening();
}
