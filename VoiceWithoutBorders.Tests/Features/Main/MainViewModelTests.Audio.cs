using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Tests.Features.Main;

// Audio tab: peer stream volumes and the mic noise gate.
public partial class MainViewModelTests
{
    [Fact]
    public void Constructor_AppliesSavedVolumesAndNoiseGate()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            SharedKey = "0123456789abcdef", PeerMicVolume = 150, PeerSoundVolume = 40, NoiseGateEnabled = true, NoiseGateDb = -50,
        });

        Create();

        _streaming.Verify(s => s.SetVolume(StreamKind.Microphone, 1.5));
        _streaming.Verify(s => s.SetVolume(StreamKind.SystemAudio, 0.4));
        _streaming.Verify(s => s.SetNoiseGate(-50));
    }

    [Fact]
    public void Constructor_HandEditedAudioOutOfRange_IsClamped()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            SharedKey = "0123456789abcdef", PeerMicVolume = 900, PeerSoundVolume = -5, NoiseGateDb = 10,
        });

        var vm = Create();

        vm.PeerMicVolume.Should().Be(AppSettings.MaxVolumePercent);
        vm.PeerSoundVolume.Should().Be(0);
        vm.NoiseGateDb.Should().Be(AppSettings.MaxNoiseGateDb);
    }

    [Fact]
    public void ChangingVolume_TakesEffectLiveAndIsSaved()
    {
        var vm = Create();

        vm.PeerSoundVolume = 75;

        _streaming.Verify(s => s.SetVolume(StreamKind.SystemAudio, 0.75));
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.PeerSoundVolume == 75)));
    }

    [Fact]
    public void NoiseGate_OffSendsNull_ThresholdOnlyAppliesWhileOn()
    {
        var vm = Create();
        _streaming.Invocations.Clear();

        vm.NoiseGateDb = -40;
        _streaming.Verify(s => s.SetNoiseGate(null));

        vm.NoiseGateEnabled = true;
        _streaming.Verify(s => s.SetNoiseGate(-40));
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.NoiseGateEnabled && saved.NoiseGateDb == -40)));
    }
}
