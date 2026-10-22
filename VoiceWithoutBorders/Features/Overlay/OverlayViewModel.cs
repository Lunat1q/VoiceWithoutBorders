using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Overlay;

/// <summary>State of the always-on-top activity pill: one indicator per stream that is actually in use.</summary>
public sealed partial class OverlayViewModel : ObservableObject
{
    private double _time;

    /// <summary>Shows the drag hint and outline while the user positions the overlay.</summary>
    [ObservableProperty] private bool _isUnlocked;

    /// <summary>Link to the other computer; anything but Connected shows a warning in place of the indicators.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkProblem))]
    private PeerConnection _connection = PeerConnection.Connected;

    public string? LinkProblem => Connection switch
    {
        PeerConnection.Waiting => "No connection",
        PeerConnection.KeyMismatch => "Key mismatch",
        _ => null,
    };

    public ObservableCollection<StreamIndicator> Indicators { get; } = [];

    /// <summary>Rebuilds the indicators for the streams these settings open.</summary>
    public void Configure(AppSettings settings)
    {
        Indicators.Clear();
        AddIf(settings.MicCaptureDeviceId, StreamKind.Microphone, sending: true);
        AddIf(settings.SystemCaptureDeviceId, StreamKind.SystemAudio, sending: true);
        AddIf(settings.MicPlaybackDeviceId, StreamKind.Microphone, sending: false);
        AddIf(settings.SystemPlaybackDeviceId, StreamKind.SystemAudio, sending: false);
    }

    public void Tick(StreamLevels levels, bool micEnabled, bool soundEnabled, double elapsedSeconds)
    {
        _time += elapsedSeconds;
        foreach (var indicator in Indicators)
        {
            var mic = indicator.Kind == StreamKind.Microphone;
            var peak = (mic, indicator.Sending) switch
            {
                (true, true) => levels.MicSent,
                (false, true) => levels.SoundSent,
                (true, false) => levels.MicReceived,
                (false, false) => levels.SoundReceived,
            };
            indicator.Update(peak, muted: !(mic ? micEnabled : soundEnabled), elapsedSeconds, _time);
        }
    }

    private void AddIf(string? deviceId, StreamKind kind, bool sending)
    {
        if (!string.IsNullOrEmpty(deviceId))
            Indicators.Add(new StreamIndicator(kind, sending));
    }
}
