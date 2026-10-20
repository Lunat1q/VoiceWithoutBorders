using System.Net;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>Audio packet counters plus how long ago the peer was last heard (any authentic packet, heartbeats included).</summary>
/// <param name="SincePeer">Null until the peer has answered at least once.</param>
/// <param name="RoundTrip">Smoothed network round trip measured with heartbeats; null until measured.</param>
public readonly record struct TransportStats(long Sent, long Received, long Rejected, TimeSpan? SincePeer = null, TimeSpan? RoundTrip = null);

/// <summary>Encrypted, connectionless audio link to one peer machine.</summary>
public interface IPeerTransport : IDisposable
{
    /// <summary>Raised on the receive thread for every authentic packet.</summary>
    event Action<StreamKind, ArraySegment<byte>>? AudioReceived;

    TransportStats Stats { get; }

    /// <summary>
    /// Resolves the peer, derives the key, starts listening on <paramref name="port"/> and sends a heartbeat every
    /// second, so the link is proven (and firewalls opened) before any audio flows.
    /// </summary>
    /// <returns>The address audio is sent to, so the user can spot a name resolving to the wrong adapter.</returns>
    Task<IPEndPoint> OpenAsync(string peerHost, int port, string sharedKey, CancellationToken cancellationToken = default);

    void Send(StreamKind kind, ArraySegment<byte> pcm);

    void Close();
}
