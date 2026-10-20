namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// Measures network round-trip time from heartbeats: remembers the peer's latest timestamp to echo back and
/// smooths the measured round trips. Thread-safe (receive and heartbeat loops both use it).
/// </summary>
public sealed class RoundTripClock
{
    private readonly Lock _lock = new();
    private long _peerSentAt;
    private long _peerReceivedAt;
    private TimeSpan? _roundTrip;

    public TimeSpan? RoundTrip
    {
        get
        {
            lock (_lock)
                return _roundTrip;
        }
    }

    public HeartbeatPayload NextOutgoing(long now)
    {
        lock (_lock)
            return _peerSentAt == 0
                ? new HeartbeatPayload(now, 0, 0)
                : new HeartbeatPayload(now, _peerSentAt, now - _peerReceivedAt);
    }

    public void OnIncoming(HeartbeatPayload payload, long now)
    {
        lock (_lock)
        {
            _peerSentAt = payload.SentAt;
            _peerReceivedAt = now;
            if (payload.RoundTripAt(now) is not { } sample)
                return;
            // Light smoothing so one late packet does not make the number jump around.
            _roundTrip = _roundTrip is { } previous ? (previous * 3 + sample) / 4 : sample;
        }
    }

    public void Reset()
    {
        lock (_lock)
            (_peerSentAt, _peerReceivedAt, _roundTrip) = (0, 0, null);
    }
}
