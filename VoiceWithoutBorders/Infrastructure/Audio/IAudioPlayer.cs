namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <param name="Buffered">Audio waiting in the jitter buffer right now.</param>
/// <param name="DeviceLatency">Latency the sound card reported for playback.</param>
/// <param name="Skips">Times audio was dropped to keep latency under the cap, since the player opened.</param>
public readonly record struct PlaybackStats(TimeSpan Buffered, TimeSpan DeviceLatency, int Skips);

/// <summary>Plays wire-format PCM on a device.</summary>
public interface IAudioPlayer : IDisposable
{
    PlaybackStats Stats { get; }

    void Write(ArraySegment<byte> pcm);
}
