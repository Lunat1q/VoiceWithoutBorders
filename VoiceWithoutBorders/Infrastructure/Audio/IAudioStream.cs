namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>An open capture or playback stream on one device.</summary>
public interface IAudioStream : IDisposable
{
    /// <summary>The device stopped by itself (unplugged, disabled, its format changed) and this stream is dead.</summary>
    bool Faulted { get; }
}
