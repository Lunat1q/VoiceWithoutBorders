using System.Security.Cryptography;
using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Infrastructure.Network;

public class PacketCodecTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Payload = [1, 2, 3, 4, 5, 6, 7, 8];

    [Theory]
    [InlineData(PacketType.Microphone)]
    [InlineData(PacketType.SystemAudio)]
    public void RoundTrip_PreservesKindAndPayload(PacketType kind)
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);

        var ok = receiver.TryDecode(sender.Encode(kind, Payload), out var decodedKind, out var decoded);

        ok.Should().BeTrue();
        decodedKind.Should().Be(kind);
        decoded.Should().Equal(Payload);
    }

    [Fact]
    public void Encode_DoesNotLeakPlaintext()
    {
        using var sender = new PacketCodec(Key);

        var packet = sender.Encode(PacketType.Microphone, Payload);

        packet.Should().HaveCount(PacketCodec.Overhead + Payload.Length);
        packet[PacketCodec.Overhead..].Should().NotEqual(Payload);
    }

    [Fact]
    public void Heartbeat_RoundTripsWithEmptyPayload()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);

        receiver.TryDecode(sender.Encode(PacketType.Heartbeat, []), out var type, out var payload).Should().BeTrue();

        type.Should().Be(PacketType.Heartbeat);
        payload.Should().BeEmpty();
    }

    [Fact]
    public void TryDecode_RejectsUnknownPacketType()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);
        var packet = sender.Encode(PacketType.Heartbeat, []);

        packet[1] = 3;

        receiver.TryDecode(packet, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_RejectsWrongKey()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(RandomNumberGenerator.GetBytes(32));

        receiver.TryDecode(sender.Encode(PacketType.Microphone, Payload), out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]   // kind (authenticated header)
    [InlineData(7)]   // sequence (authenticated header)
    [InlineData(15)]  // nonce
    [InlineData(25)]  // tag
    [InlineData(40)]  // ciphertext
    public void TryDecode_RejectsTamperedByte(int index)
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);
        var packet = sender.Encode(PacketType.Microphone, new byte[16]);

        packet[index] ^= 0x01;

        receiver.TryDecode(packet, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_RejectsTruncatedPacket()
    {
        using var receiver = new PacketCodec(Key);

        receiver.TryDecode(new byte[PacketCodec.Overhead - 1], out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_RejectsDuplicateAndLatePackets()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);
        var first = sender.Encode(PacketType.Microphone, Payload);
        var second = sender.Encode(PacketType.Microphone, Payload);

        receiver.TryDecode(second, out _, out _).Should().BeTrue();
        receiver.TryDecode(second, out _, out _).Should().BeFalse("duplicate");
        receiver.TryDecode(first, out _, out _).Should().BeFalse("older than the last one played");
    }

    [Fact]
    public void TryDecode_TracksSequencePerStream()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);

        receiver.TryDecode(sender.Encode(PacketType.Microphone, Payload), out _, out _).Should().BeTrue();
        receiver.TryDecode(sender.Encode(PacketType.SystemAudio, Payload), out _, out _).Should().BeTrue();
    }

    [Fact]
    public void TryDecode_AcceptsRestartedPeer()
    {
        using var receiver = new PacketCodec(Key);
        using (var firstRun = new PacketCodec(Key))
        {
            firstRun.Encode(PacketType.Microphone, Payload);
            receiver.TryDecode(firstRun.Encode(PacketType.Microphone, Payload), out _, out _).Should().BeTrue();
        }

        using var secondRun = new PacketCodec(Key);

        receiver.TryDecode(secondRun.Encode(PacketType.Microphone, Payload), out _, out _).Should().BeTrue();
    }

    [Fact]
    public void TryDecode_RejectsOwnPacketsReflectedBack()
    {
        using var codec = new PacketCodec(Key);

        codec.TryDecode(codec.Encode(PacketType.Microphone, Payload), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void FromSharedKey_SameKeyInteroperates_DifferentKeyDoesNot()
    {
        using var sender = PacketCodec.FromSharedKey("correct horse battery staple");
        using var sameKey = PacketCodec.FromSharedKey("correct horse battery staple");
        using var otherKey = PacketCodec.FromSharedKey("correct horse battery stapler");
        var packet = sender.Encode(PacketType.SystemAudio, Payload);

        sameKey.TryDecode(packet, out _, out _).Should().BeTrue();
        otherKey.TryDecode(packet, out _, out _).Should().BeFalse();
    }
}
