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
        mic.Level.Should().BeApproximately(StreamIndicator.ToLevel(0.5f), 0.001);

        for (var i = 0; i < 60; i++)
            overlay.Tick(new StreamLevels(0, 0, 0, 0), micEnabled: true, soundEnabled: true, Frame);

        mic.Level.Should().Be(0);
        mic.Bar1.Should().Be(StreamIndicator.MinBar);
    }

    [Fact]
    public void Tick_QuietSpeech_FillsAThirdOfTheBarAtLeast()
    {
        var overlay = Configured();
        var mic = overlay.Indicators[0];

        // Regression: on a square-root scale a -26 dB peak barely moved the bars.
        overlay.Tick(new StreamLevels(MicSent: 0.05f, 0, 0, 0), micEnabled: true, soundEnabled: true, Frame);

        mic.Bar1.Should().BeGreaterThan(StreamIndicator.MinBar + (StreamIndicator.MaxBar - StreamIndicator.MinBar) / 3);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.002f, 0)]
    [InlineData(1f, 1)]
    [InlineData(2f, 1)]
    public void ToLevel_FlatBelowTheFloor_FullAtFullScale(float peak, double expected)
    {
        StreamIndicator.ToLevel(peak).Should().Be(expected);
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

    [Fact]
    public void BackgroundOpacity_SolidWhileUnlocked_SoAnInvisiblePillCanBeFound()
    {
        var overlay = new OverlayViewModel { Opacity = 0 };
        overlay.BackgroundOpacity.Should().Be(0);

        overlay.IsUnlocked = true;

        overlay.BackgroundOpacity.Should().Be(1);
    }

    [Fact]
    public void Tick_HideWhenQuietOff_NeverFades()
    {
        var overlay = Configured();

        TickSilence(overlay, seconds: 30);

        overlay.Fade.Should().Be(1);
    }

    [Fact]
    public void Tick_HideWhenQuiet_FadesOutAfterQuietSeconds_AndSoundBringsItBack()
    {
        var overlay = Configured();
        overlay.HideWhenQuiet = true;

        TickSilence(overlay, OverlayViewModel.QuietSeconds - 0.5);
        overlay.Fade.Should().Be(1, "not quiet for long enough yet");

        TickSilence(overlay, 0.5 + OverlayViewModel.FadeSeconds / 2);
        overlay.Fade.Should().BeInRange(0.3, 0.7, "half way through the fade");

        TickSilence(overlay, OverlayViewModel.FadeSeconds);
        overlay.Fade.Should().Be(0);

        overlay.Tick(new StreamLevels(MicSent: 0.5f, 0, 0, 0), true, true, Frame);
        overlay.Fade.Should().Be(1);
    }

    [Fact]
    public void Tick_HideWhenQuiet_RoomNoiseDoesNotKeepItVisible()
    {
        var overlay = Configured();
        overlay.HideWhenQuiet = true;

        for (var i = 0; i < 60 * 7; i++)
            overlay.Tick(new StreamLevels(MicSent: 0.005f, 0, 0, 0), true, true, Frame); // about -46 dB

        overlay.Fade.Should().Be(0);
    }

    [Fact]
    public void Tick_HideWhenQuiet_MuteChangeAndLostLinkShowIt()
    {
        var overlay = Configured();
        overlay.HideWhenQuiet = true;
        TickSilence(overlay, 10);

        overlay.Tick(new StreamLevels(0, 0, 0, 0), micEnabled: false, soundEnabled: true, Frame);
        overlay.Fade.Should().Be(1, "the user just toggled the mic and wants to see it");

        TickSilence(overlay, 10, micEnabled: false);
        overlay.Fade.Should().Be(0, "staying muted is not news");

        overlay.Connection = PeerConnection.Waiting;
        TickSilence(overlay, 10, micEnabled: false);
        overlay.Fade.Should().Be(1, "a lost link stays visible");
    }

    [Fact]
    public void Configure_ShowsAFadedPillAgain()
    {
        var overlay = Configured();
        overlay.HideWhenQuiet = true;
        TickSilence(overlay, 10);

        overlay.Configure(new AppSettings { MicCaptureDeviceId = "mic" });

        overlay.Fade.Should().Be(1);
    }

    private static void TickSilence(OverlayViewModel overlay, double seconds, bool micEnabled = true)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += Frame)
            overlay.Tick(new StreamLevels(0, 0, 0, 0), micEnabled, soundEnabled: true, Frame);
    }
}
