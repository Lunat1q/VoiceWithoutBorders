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
    public int MaxLatencyMs { get; init; } = 120;

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
