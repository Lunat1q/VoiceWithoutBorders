using System.Buffers.Binary;
using System.Text;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// Body of discovery packets: the sender's instance id (so a computer ignores its own broadcast), then, in replies,
/// its computer name as UTF-8.
/// </summary>
public readonly record struct DiscoveryPayload(ulong Instance, string Name)
{
    private const int MaxNameBytes = 64;

    /// <summary>Random per run of the app.</summary>
    public static readonly ulong ThisInstance = (ulong)Random.Shared.NextInt64();

    public byte[] ToBytes()
    {
        var name = Encoding.UTF8.GetBytes(Name);
        var bytes = new byte[8 + Math.Min(name.Length, MaxNameBytes)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, Instance);
        name.AsSpan(0, bytes.Length - 8).CopyTo(bytes.AsSpan(8));
        return bytes;
    }

    public static bool TryRead(ReadOnlySpan<byte> bytes, out DiscoveryPayload payload)
    {
        payload = default;
        if (bytes.Length is < 8 or > 8 + MaxNameBytes)
            return false;
        payload = new(BinaryPrimitives.ReadUInt64BigEndian(bytes), Encoding.UTF8.GetString(bytes[8..]));
        return true;
    }

    /// <summary>The encoded reply to an authentic request from another instance; null when there is nothing to answer.</summary>
    public static byte[]? Answer(PacketCodec codec, PacketType type, ReadOnlySpan<byte> payload)
    {
        if (type != PacketType.DiscoverRequest || !TryRead(payload, out var request) || request.Instance == ThisInstance)
            return null;
        return codec.Encode(PacketType.DiscoverReply, new DiscoveryPayload(ThisInstance, Environment.MachineName).ToBytes());
    }
}
