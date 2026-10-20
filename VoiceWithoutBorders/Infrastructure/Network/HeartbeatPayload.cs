using System.Buffers.Binary;
using System.Diagnostics;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// Heartbeat body. Each side echoes the peer's latest timestamp and says how long it held it, so the original
/// sender can compute the round trip on its own clock: no clock sync needed (the NTP trick). Times are
/// microseconds on the sender's monotonic clock.
/// </summary>
public readonly record struct HeartbeatPayload(long SentAt, long EchoOf, long HeldFor)
{
    public const int Size = 24;

    public static long Now() => (long)(Stopwatch.GetTimestamp() * (1_000_000.0 / Stopwatch.Frequency));

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        BinaryPrimitives.WriteInt64BigEndian(bytes, SentAt);
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(8), EchoOf);
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(16), HeldFor);
        return bytes;
    }

    /// <summary>False for an empty heartbeat from an earlier build: the link still counts as up, just unmeasured.</summary>
    public static bool TryRead(ReadOnlySpan<byte> data, out HeartbeatPayload payload)
    {
        payload = default;
        if (data.Length < Size)
            return false;
        payload = new HeartbeatPayload(
            BinaryPrimitives.ReadInt64BigEndian(data),
            BinaryPrimitives.ReadInt64BigEndian(data[8..]),
            BinaryPrimitives.ReadInt64BigEndian(data[16..]));
        return true;
    }

    /// <summary>
    /// Round trip of the heartbeat we sent at <see cref="EchoOf"/>: time since then, minus the peer's hold time.
    /// Null when the peer has not heard from us yet.
    /// </summary>
    public TimeSpan? RoundTripAt(long now) =>
        EchoOf == 0 ? null : TimeSpan.FromMicroseconds(Math.Max(0, now - EchoOf - HeldFor));
}
