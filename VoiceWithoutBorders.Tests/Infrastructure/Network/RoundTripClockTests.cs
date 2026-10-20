using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Infrastructure.Network;

public class RoundTripClockTests
{
    [Fact]
    public void Payload_RoundTripsThroughBytes()
    {
        var payload = new HeartbeatPayload(SentAt: 123_456_789, EchoOf: 42, HeldFor: 7);

        HeartbeatPayload.TryRead(payload.ToBytes(), out var read).Should().BeTrue();

        read.Should().Be(payload);
    }

    [Fact]
    public void Payload_EmptyHeartbeatFromEarlierBuild_IsNotReadable() =>
        HeartbeatPayload.TryRead([], out _).Should().BeFalse();

    // The two computers' clocks are unrelated (here 49 seconds apart); only differences on one clock are used.
    [Fact]
    public void MeasuresRoundTrip_WithUnsynchronisedClocks_AndSubtractsHoldTime()
    {
        var desk = new RoundTripClock();
        var laptop = new RoundTripClock();

        var ping = desk.NextOutgoing(now: 1_000);
        laptop.OnIncoming(ping, now: 50_000_000);
        var pong = laptop.NextOutgoing(now: 50_300_000);  // laptop held it 300 ms until its next heartbeat
        desk.OnIncoming(pong, now: 1_000 + 300_000 + 4_000);

        desk.RoundTrip.Should().Be(TimeSpan.FromMilliseconds(4));
        laptop.RoundTrip.Should().BeNull("the laptop has not had an echo of its own heartbeat yet");
    }

    [Fact]
    public void SmoothsSamples_SoOneLatePacketDoesNotJump()
    {
        var clock = new RoundTripClock();

        clock.OnIncoming(new HeartbeatPayload(SentAt: 1, EchoOf: 1_000, HeldFor: 0), now: 1_000 + 4_000);   // 4 ms
        clock.OnIncoming(new HeartbeatPayload(SentAt: 2, EchoOf: 2_000, HeldFor: 0), now: 2_000 + 40_000);  // 40 ms spike

        clock.RoundTrip.Should().Be(TimeSpan.FromMilliseconds(13)); // (3 x 4 + 40) / 4
    }

    [Fact]
    public void Reset_ForgetsMeasurementAndEcho()
    {
        var clock = new RoundTripClock();
        clock.OnIncoming(new HeartbeatPayload(SentAt: 5, EchoOf: 1_000, HeldFor: 0), now: 5_000);

        clock.Reset();

        clock.RoundTrip.Should().BeNull();
        clock.NextOutgoing(now: 9).EchoOf.Should().Be(0);
    }
}
