namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Produces wire-format PCM chunks from a device.</summary>
public interface IAudioCapture : IDisposable
{
    /// <summary>Raised on the capture thread. The segment is reused, so consume it before returning.</summary>
    event Action<ArraySegment<byte>>? ChunkReady;

    void Start();
}
