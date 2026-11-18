using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Overlay;

/// <summary>State of the always-on-top activity pill: one indicator per stream that is actually in use.</summary>
public sealed partial class OverlayViewModel : ObservableObject
{
    /// <summary>With <see cref="HideWhenQuiet"/>, the pill starts to fade after this long without anything to show.</summary>
    public const double QuietSeconds = 5;

    public const double FadeSeconds = 0.5;

    /// <summary>Levels above this (about -40 dB) count as sound; room noise and dither stay below.</summary>
    public const double ActiveLevel = 0.2;

    private double _time;
    private double _quietFor;

    /// <summary>Gives the pill a move cursor while the user positions it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundOpacity))]
    private bool _isUnlocked;

    /// <summary>Link to the other computer; anything but Connected shows a warning in place of the indicators.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkProblem))]
    private PeerConnection _connection = PeerConnection.Connected;

    [ObservableProperty] private bool _isVertical;
    [ObservableProperty] private double _scale = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundOpacity))]
    private double _opacity = 0.85;

    [ObservableProperty] private bool _hideWhenQuiet;

    /// <summary>Opacity of the whole pill: 1 normally, falls to 0 while quiet when <see cref="HideWhenQuiet"/> is on.</summary>
    [ObservableProperty] private double _fade = 1;

    /// <summary>Unlocked, the background is solid so the pill can be found and dragged even when set to invisible.</summary>
    public double BackgroundOpacity => IsUnlocked ? 1 : Opacity;

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
        _quietFor = 0;
        Fade = 1;
    }

    public void Tick(StreamLevels levels, bool micEnabled, bool soundEnabled, double elapsedSeconds)
    {
        _time += elapsedSeconds;
        var active = IsUnlocked || LinkProblem is not null;
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
            var wasMuted = indicator.IsMuted;
            indicator.Update(peak, muted: !(mic ? micEnabled : soundEnabled), elapsedSeconds, _time);
            active |= indicator.Level > ActiveLevel || indicator.IsMuted != wasMuted;
        }

        _quietFor = active ? 0 : _quietFor + elapsedSeconds;
        Fade = !HideWhenQuiet || _quietFor <= QuietSeconds
            ? 1
            : Math.Max(0, 1 - (_quietFor - QuietSeconds) / FadeSeconds);
    }

    private void AddIf(string? deviceId, StreamKind kind, bool sending)
    {
        if (!string.IsNullOrEmpty(deviceId))
            Indicators.Add(new StreamIndicator(kind, sending));
    }
}
