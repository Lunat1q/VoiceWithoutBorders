using System.Security.Cryptography;
using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Network;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Tests.Infrastructure.Network;

public class PacketCodecTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Payload = [1, 2, 3, 4, 5, 6, 7, 8];

    [Theory]
    [InlineData(StreamKind.Microphone)]
    [InlineData(StreamKind.SystemAudio)]
    public void RoundTrip_PreservesKindAndPayload(StreamKind kind)
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

        var packet = sender.Encode(StreamKind.Microphone, Payload);

        packet.Should().HaveCount(PacketCodec.Overhead + Payload.Length);
        packet[PacketCodec.Overhead..].Should().NotEqual(Payload);
    }

    [Fact]
    public void TryDecode_RejectsWrongKey()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(RandomNumberGenerator.GetBytes(32));

        receiver.TryDecode(sender.Encode(StreamKind.Microphone, Payload), out _, out _).Should().BeFalse();
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
        var packet = sender.Encode(StreamKind.Microphone, new byte[16]);

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
        var first = sender.Encode(StreamKind.Microphone, Payload);
        var second = sender.Encode(StreamKind.Microphone, Payload);

        receiver.TryDecode(second, out _, out _).Should().BeTrue();
        receiver.TryDecode(second, out _, out _).Should().BeFalse("duplicate");
        receiver.TryDecode(first, out _, out _).Should().BeFalse("older than the last one played");
    }

    [Fact]
    public void TryDecode_TracksSequencePerStream()
    {
        using var sender = new PacketCodec(Key);
        using var receiver = new PacketCodec(Key);

        receiver.TryDecode(sender.Encode(StreamKind.Microphone, Payload), out _, out _).Should().BeTrue();
        receiver.TryDecode(sender.Encode(StreamKind.SystemAudio, Payload), out _, out _).Should().BeTrue();
    }

    [Fact]
    public void TryDecode_AcceptsRestartedPeer()
    {
        using var receiver = new PacketCodec(Key);
        using (var firstRun = new PacketCodec(Key))
        {
            firstRun.Encode(StreamKind.Microphone, Payload);
            receiver.TryDecode(firstRun.Encode(StreamKind.Microphone, Payload), out _, out _).Should().BeTrue();
        }

        using var secondRun = new PacketCodec(Key);

        receiver.TryDecode(secondRun.Encode(StreamKind.Microphone, Payload), out _, out _).Should().BeTrue();
    }

    [Fact]
    public void TryDecode_RejectsOwnPacketsReflectedBack()
    {
        using var codec = new PacketCodec(Key);

        codec.TryDecode(codec.Encode(StreamKind.Microphone, Payload), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void FromSharedKey_SameKeyInteroperates_DifferentKeyDoesNot()
    {
        using var sender = PacketCodec.FromSharedKey("correct horse battery staple");
        using var sameKey = PacketCodec.FromSharedKey("correct horse battery staple");
        using var otherKey = PacketCodec.FromSharedKey("correct horse battery stapler");
        var packet = sender.Encode(StreamKind.SystemAudio, Payload);

        sameKey.TryDecode(packet, out _, out _).Should().BeTrue();
        otherKey.TryDecode(packet, out _, out _).Should().BeFalse();
    }
}
