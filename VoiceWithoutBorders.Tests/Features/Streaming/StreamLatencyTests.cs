using FluentAssertions;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Tests.Features.Streaming;

public class StreamLatencyTests
{
    private static readonly PlaybackStats Playback = new(Buffered: TimeSpan.FromMilliseconds(40), DeviceLatency: TimeSpan.FromMilliseconds(30), Skips: 2);

    [Fact]
    public void Estimate_AddsCaptureHalfRoundTripBufferAndDevice()
    {
        var latency = StreamLatency.Estimate(StreamKind.Microphone, TimeSpan.FromMilliseconds(6), Playback);

        latency.EndToEnd.Should().Be(TimeSpan.FromMilliseconds(20 + 3 + 40 + 30));
        latency.Buffered.Should().Be(TimeSpan.FromMilliseconds(40));
        latency.Skips.Should().Be(2);
        latency.Health.Should().Be(LatencyHealth.Good);
    }

    [Fact]
    public void Estimate_BeforeRoundTripIsMeasured_IsUnknown()
    {
        var latency = StreamLatency.Estimate(StreamKind.SystemAudio, roundTrip: null, Playback);

        latency.EndToEnd.Should().BeNull();
        latency.Health.Should().Be(LatencyHealth.Unknown);
    }

    [Theory]
    [InlineData(149, LatencyHealth.Good)]
    [InlineData(150, LatencyHealth.Fair)]
    [InlineData(299, LatencyHealth.Fair)]
    [InlineData(300, LatencyHealth.Poor)]
    public void Health_FollowsVoiceCallThresholds(int milliseconds, LatencyHealth expected) =>
        new StreamLatency(StreamKind.Microphone, TimeSpan.FromMilliseconds(milliseconds), TimeSpan.Zero, 0).Health.Should().Be(expected);
}
