using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Infrastructure.Network;

public class DiscoveryPayloadTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void ToBytes_TryRead_RoundTrips()
    {
        var payload = new DiscoveryPayload(0x0102030405060708, "OFFICE-PC");

        DiscoveryPayload.TryRead(payload.ToBytes(), out var read).Should().BeTrue();

        read.Should().Be(payload);
    }

    [Fact]
    public void ToBytes_LongName_IsCutToFit()
    {
        var bytes = new DiscoveryPayload(1, new string('x', 200)).ToBytes();

        DiscoveryPayload.TryRead(bytes, out var read).Should().BeTrue();
        read.Name.Should().HaveLength(64);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8 + 65)]
    public void TryRead_WrongLength_Fails(int length) =>
        DiscoveryPayload.TryRead(new byte[length], out _).Should().BeFalse();

    [Fact]
    public void Answer_RequestFromAnotherComputer_RepliesWithThisInstanceAndName()
    {
        using var searcher = new PacketCodec(Key);
        using var responder = new PacketCodec(Key);
        var request = searcher.Encode(PacketType.DiscoverRequest, new DiscoveryPayload(42, "").ToBytes());
        responder.TryDecode(request, out var type, out var payload).Should().BeTrue();

        var reply = DiscoveryPayload.Answer(responder, type, payload);

        searcher.TryDecode(reply, out var replyType, out var replyPayload).Should().BeTrue("the searcher shares the key");
        replyType.Should().Be(PacketType.DiscoverReply);
        DiscoveryPayload.TryRead(replyPayload, out var answer).Should().BeTrue();
        answer.Should().Be(new DiscoveryPayload(DiscoveryPayload.ThisInstance, Environment.MachineName));
    }

    [Fact]
    public void Answer_OwnRequestOrOtherPackets_AreNotAnswered()
    {
        using var codec = new PacketCodec(Key);
        var own = new DiscoveryPayload(DiscoveryPayload.ThisInstance, "").ToBytes();

        DiscoveryPayload.Answer(codec, PacketType.DiscoverRequest, own).Should().BeNull("a computer hears its own broadcast");
        DiscoveryPayload.Answer(codec, PacketType.DiscoverReply, new DiscoveryPayload(42, "PC").ToBytes()).Should().BeNull();
        DiscoveryPayload.Answer(codec, PacketType.Heartbeat, new byte[24]).Should().BeNull();
    }

    [Fact]
    public void Request_WithAnotherKey_IsRejectedBeforeAnyAnswer()
    {
        using var stranger = new PacketCodec(Enumerable.Repeat((byte)7, 32).ToArray());
        using var responder = new PacketCodec(Key);
        var request = stranger.Encode(PacketType.DiscoverRequest, new DiscoveryPayload(42, "").ToBytes());

        responder.TryDecode(request, out _, out _).Should().BeFalse();
    }
}
