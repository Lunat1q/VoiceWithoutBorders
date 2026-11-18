using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Main;

// Audio tab: how loud the peer's streams play here, and the noise gate on this computer's mic. All live.
public sealed partial class MainViewModel
{
    [ObservableProperty] private int _peerMicVolume = 100;
    [ObservableProperty] private int _peerSoundVolume = 100;
    [ObservableProperty] private bool _noiseGateEnabled;
    [ObservableProperty] private int _noiseGateDb = -45;

    partial void OnPeerMicVolumeChanged(int value) => _streaming.SetVolume(StreamKind.Microphone, value / 100.0);

    partial void OnPeerSoundVolumeChanged(int value) => _streaming.SetVolume(StreamKind.SystemAudio, value / 100.0);

    partial void OnNoiseGateEnabledChanged(bool value) => ApplyNoiseGate();

    partial void OnNoiseGateDbChanged(int value) => ApplyNoiseGate();

    private void ApplyNoiseGate() => _streaming.SetNoiseGate(NoiseGateEnabled ? NoiseGateDb : null);

    private void LoadAudio(AppSettings settings)
    {
        // Clamped, not rejected: a hand-edited settings file must not keep streaming from starting.
        PeerMicVolume = Math.Clamp(settings.PeerMicVolume, 0, AppSettings.MaxVolumePercent);
        PeerSoundVolume = Math.Clamp(settings.PeerSoundVolume, 0, AppSettings.MaxVolumePercent);
        NoiseGateEnabled = settings.NoiseGateEnabled;
        NoiseGateDb = Math.Clamp(settings.NoiseGateDb, AppSettings.MinNoiseGateDb, AppSettings.MaxNoiseGateDb);
    }
}
