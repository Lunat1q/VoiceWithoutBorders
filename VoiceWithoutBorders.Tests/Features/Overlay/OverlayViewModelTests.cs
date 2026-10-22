using FluentAssertions;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Tests.Features.Overlay;

public class OverlayViewModelTests
{
    private const double Frame = 1 / 60.0;

    private static OverlayViewModel Configured()
    {
        var overlay = new OverlayViewModel();
        overlay.Configure(new AppSettings { MicCaptureDeviceId = "mic", SystemPlaybackDeviceId = "headset" });
        return overlay;
    }

    [Fact]
    public void Configure_AddsOneIndicatorPerOpenStream()
    {
        var overlay = Configured();

        overlay.Indicators.Select(i => (i.Kind, i.Sending)).Should().Equal(
            (StreamKind.Microphone, true),
            (StreamKind.SystemAudio, false));
    }

    [Fact]
    public void Tick_LoudAudio_RaisesBars_ThenTheyFallBackWhenQuiet()
    {
        var overlay = Configured();
        var mic = overlay.Indicators[0];

        overlay.Tick(new StreamLevels(MicSent: 0.5f, 0, 0, 0), micEnabled: true, soundEnabled: true, Frame);
        var loud = mic.Bar1;

        loud.Should().BeGreaterThan(StreamIndicator.MinBar + 5);
        mic.Level.Should().BeApproximately(Math.Sqrt(0.5), 0.001);

        for (var i = 0; i < 60; i++)
            overlay.Tick(new StreamLevels(0, 0, 0, 0), micEnabled: true, soundEnabled: true, Frame);

        mic.Level.Should().Be(0);
        mic.Bar1.Should().Be(StreamIndicator.MinBar);
    }

    [Fact]
    public void Tick_OnlyMovesTheMatchingIndicator()
    {
        var overlay = Configured();

        overlay.Tick(new StreamLevels(MicSent: 0, SoundSent: 1, MicReceived: 1, SoundReceived: 0), true, true, Frame);

        overlay.Indicators.Should().OnlyContain(i => i.Level == 0, "only sent mic and received sound are shown");
    }

    [Fact]
    public void Tick_MutedStream_ShowsMutedIconAndFlatBars()
    {
        var overlay = Configured();
        var mic = overlay.Indicators[0];
        var normalGlyph = mic.Glyph;

        overlay.Tick(new StreamLevels(MicSent: 1, 0, 0, 0), micEnabled: false, soundEnabled: true, Frame);

        mic.IsMuted.Should().BeTrue();
        mic.Glyph.Should().NotBe(normalGlyph);
        mic.Level.Should().Be(0);
        overlay.Indicators[1].IsMuted.Should().BeFalse();
    }

    [Theory]
    [InlineData(PeerConnection.Connected, null)]
    [InlineData(PeerConnection.Waiting, "No connection")]
    [InlineData(PeerConnection.KeyMismatch, "Key mismatch")]
    public void LinkProblem_WarnsUnlessConnected(PeerConnection connection, string? expected)
    {
        var overlay = new OverlayViewModel { Connection = connection };

        overlay.LinkProblem.Should().Be(expected);
    }
}
