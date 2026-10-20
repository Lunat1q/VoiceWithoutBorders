using FluentAssertions;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Features.Streaming;

public class LinkMonitorTests
{
    private readonly LinkMonitor _sut = new();

    [Fact]
    public void PeerNeverHeard_IsWaiting()
    {
        _sut.Update(new TransportStats(Sent: 50, Received: 0, Rejected: 0, SincePeer: null))
            .Should().Be(new LinkStatus(PeerConnection.Waiting, Sending: true, Receiving: false));
    }

    // Regression for "nothing arrives until the other PC sends": heartbeats mark the peer as connected
    // even while no audio flows in either direction.
    [Fact]
    public void RecentHeartbeat_IsConnectedEvenWithoutAudio()
    {
        _sut.Update(new TransportStats(0, 0, 0, TimeSpan.FromMilliseconds(400)))
            .Should().Be(new LinkStatus(PeerConnection.Connected, Sending: false, Receiving: false));
    }

    [Fact]
    public void PeerSilentForThreeSeconds_FallsBackToWaiting()
    {
        _sut.Update(new TransportStats(0, 0, 0, TimeSpan.FromSeconds(3.5))).Connection.Should().Be(PeerConnection.Waiting);
    }

    [Fact]
    public void NewRejectedPacketsAndNoAuthenticOnes_IsKeyMismatch()
    {
        _sut.Update(new TransportStats(0, 0, Rejected: 4)).Connection.Should().Be(PeerConnection.KeyMismatch);
        _sut.Update(new TransportStats(0, 0, Rejected: 4)).Connection.Should().Be(PeerConnection.Waiting, "no new rejections since");
    }

    [Fact]
    public void FlowIndicators_LightOnlyWhenCountersMovedSinceLastSample()
    {
        _sut.Update(new TransportStats(Sent: 100, Received: 100, Rejected: 0, TimeSpan.Zero));

        _sut.Update(new TransportStats(Sent: 100, Received: 300, Rejected: 0, TimeSpan.Zero))
            .Should().Be(new LinkStatus(PeerConnection.Connected, Sending: false, Receiving: true));
    }

    [Fact]
    public void Reset_ForgetsPreviousCounters()
    {
        _sut.Update(new TransportStats(Sent: 500, Received: 500, Rejected: 0));
        _sut.Reset();

        _sut.Update(new TransportStats(Sent: 10, Received: 10, Rejected: 0)).Sending.Should().BeTrue("counters restart at zero after Start");
    }
}
