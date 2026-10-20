namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Plays wire-format PCM on a device.</summary>
public interface IAudioPlayer : IDisposable
{
    void Write(ArraySegment<byte> pcm);
}
