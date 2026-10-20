namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Opens capture and playback streams on devices picked by id.</summary>
public interface IAudioEndpointFactory
{
    /// <param name="loopback">True to record what an output device is playing instead of a microphone.</param>
    IAudioCapture CreateCapture(string deviceId, bool loopback);

    IAudioPlayer CreatePlayer(string deviceId, TimeSpan maxLatency);
}
