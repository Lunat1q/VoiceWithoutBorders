using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Features.Settings;

/// <summary>Everything the user configures. A null device id means that stream is off.</summary>
public sealed record AppSettings
{
    public const int DefaultPort = 15300;
    public const int MinLatencyMs = 20;
    public const int MaxLatencyLimitMs = 2000;

    public string PeerHost { get; init; } = "";
    public int Port { get; init; } = DefaultPort;
    public string SharedKey { get; init; } = "";

    /// <summary>Microphone sent to the peer.</summary>
    public string? MicCaptureDeviceId { get; init; }

    /// <summary>Output device whose sound (loopback) is sent to the peer.</summary>
    public string? SystemCaptureDeviceId { get; init; }

    /// <summary>Where the peer's microphone plays, typically a virtual cable input.</summary>
    public string? MicPlaybackDeviceId { get; init; }

    /// <summary>Where the peer's system sound plays, typically headphones.</summary>
    public string? SystemPlaybackDeviceId { get; init; }

    /// <summary>Jitter buffer cap. Lower is snappier, higher survives a busy network.</summary>
    public int MaxLatencyMs { get; init; } = 200;

    /// <summary>Microphone stream on (sent and played). Toggled with a hotkey while streaming.</summary>
    public bool MicEnabled { get; init; } = true;

    /// <summary>System sound stream on (sent and played). Toggled with a hotkey while streaming.</summary>
    public bool SoundEnabled { get; init; } = true;

    /// <summary>Volume of the peer's mic as played here, in percent (100 = as sent, up to <see cref="MaxVolumePercent"/>).</summary>
    public int PeerMicVolume { get; init; } = 100;

    /// <summary>Volume of the peer's system sound as played here, in percent.</summary>
    public int PeerSoundVolume { get; init; } = 100;

    /// <summary>Send silence while this computer's mic stays under <see cref="NoiseGateDb"/>.</summary>
    public bool NoiseGateEnabled { get; init; }

    /// <summary>Noise gate threshold in dB below full scale, <see cref="MinNoiseGateDb"/> to <see cref="MaxNoiseGateDb"/>.</summary>
    public int NoiseGateDb { get; init; } = -45;

    public const int MaxVolumePercent = 200;
    public const int MinNoiseGateDb = -70;
    public const int MaxNoiseGateDb = -20;

    /// <summary>Small always-on-top activity indicator while streaming.</summary>
    public bool ShowOverlay { get; init; } = true;

    /// <summary>Overlay position in device-independent pixels; null means top centre of the primary screen.</summary>
    public double? OverlayLeft { get; init; }

    public double? OverlayTop { get; init; }

    /// <summary>Indicators stacked in a column instead of side by side.</summary>
    public bool OverlayVertical { get; init; }

    /// <summary>Overlay size, 1 = normal. Clamped to <see cref="MinOverlayScale"/>-<see cref="MaxOverlayScale"/> on load.</summary>
    public double OverlayScale { get; init; } = 1;

    /// <summary>Opacity of the pill's background, 0 (icons only) to 1. The indicators stay fully opaque.</summary>
    public double OverlayOpacity { get; init; } = 0.85;

    /// <summary>Fade the overlay out while nothing is heard, back in on sound, a mute change or a lost link.</summary>
    public bool OverlayHideWhenQuiet { get; init; }

    public const double MinOverlayScale = 0.75;
    public const double MaxOverlayScale = 2;

    /// <summary>Global shortcuts; null means none.</summary>
    public HotkeyBinding? ToggleMicHotkey { get; init; } = HotkeyBinding.DefaultToggleMic;

    public HotkeyBinding? ToggleSoundHotkey { get; init; } = HotkeyBinding.DefaultToggleSound;

    public HotkeyBinding? StartStopHotkey { get; init; }

    /// <summary>Start streaming by itself when Windows launches the app at sign-in.</summary>
    public bool AutoConnect { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(PeerHost))
            errors.Add("Enter the other computer's name or IP address.");
        if (Port is < 1 or > 65535)
            errors.Add("Port must be between 1 and 65535.");
        if (SharedKey.Length < PacketCodec.MinSharedKeyLength)
            errors.Add($"Shared key must be at least {PacketCodec.MinSharedKeyLength} characters.");
        if (MaxLatencyMs is < MinLatencyMs or > MaxLatencyLimitMs)
            errors.Add($"Max latency must be between {MinLatencyMs} and {MaxLatencyLimitMs} ms.");
        if (new[] { MicCaptureDeviceId, SystemCaptureDeviceId, MicPlaybackDeviceId, SystemPlaybackDeviceId }.All(string.IsNullOrEmpty))
            errors.Add("Pick at least one device to send or receive.");
        return errors;
    }
}
