using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Streaming;

/// <summary>How late an incoming stream plays here, from the other computer's capture to this computer's output.</summary>
/// <param name="EndToEnd">Null until the network round trip has been measured.</param>
public readonly record struct StreamLatency(StreamKind Kind, TimeSpan? EndToEnd, TimeSpan Buffered, int Skips)
{
    public static readonly TimeSpan CaptureEstimate = TimeSpan.FromMilliseconds(WasapiAudioCapture.BufferMilliseconds);

    public LatencyHealth Health => EndToEnd?.TotalMilliseconds switch
    {
        null => LatencyHealth.Unknown,
        < 150 => LatencyHealth.Good,
        < 300 => LatencyHealth.Fair,
        _ => LatencyHealth.Poor,
    };

    /// <summary>
    /// Capture buffer on the sender + one-way network (half the round trip) + jitter buffer + output device.
    /// Leaves out what cannot be measured from here, such as the sender's driver and the headset itself.
    /// </summary>
    public static StreamLatency Estimate(StreamKind kind, TimeSpan? roundTrip, PlaybackStats playback) => new(
        kind,
        roundTrip is { } rtt ? CaptureEstimate + rtt / 2 + playback.Buffered + playback.DeviceLatency : null,
        playback.Buffered,
        playback.Skips);
}
