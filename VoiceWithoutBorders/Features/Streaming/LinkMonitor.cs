using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Features.Streaming;

public enum PeerConnection
{
    /// <summary>Nothing authentic heard from the peer recently.</summary>
    Waiting,

    /// <summary>The peer answered (heartbeat or audio) within <see cref="LinkMonitor.PeerTimeout"/>.</summary>
    Connected,

    /// <summary>Packets arrive but fail authentication: different shared key or app version.</summary>
    KeyMismatch,
}

public readonly record struct LinkStatus(PeerConnection Connection, bool Sending, bool Receiving);

/// <summary>Turns cumulative transport counters into "is it working right now", sampled about once a second.</summary>
public sealed class LinkMonitor
{
    /// <summary>Three missed heartbeats.</summary>
    public static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(3);

    private TransportStats _previous;

    public LinkStatus Update(TransportStats now)
    {
        var connection = now.SincePeer is { } since && since < PeerTimeout ? PeerConnection.Connected
            : now.Rejected > _previous.Rejected ? PeerConnection.KeyMismatch
            : PeerConnection.Waiting;
        var status = new LinkStatus(connection, Sending: now.Sent > _previous.Sent, Receiving: now.Received > _previous.Received);
        _previous = now;
        return status;
    }

    /// <summary>Counters restart from zero with every Start.</summary>
    public void Reset() => _previous = default;
}
