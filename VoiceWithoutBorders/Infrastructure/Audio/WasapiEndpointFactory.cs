using NAudio.CoreAudioApi;

namespace VoiceWithoutBorders.Infrastructure.Audio;

public sealed class WasapiEndpointFactory : IAudioEndpointFactory
{
    public IAudioCapture CreateCapture(string deviceId, bool loopback) => new WasapiAudioCapture(GetDevice(deviceId), loopback);

    public IAudioPlayer CreatePlayer(string deviceId, TimeSpan maxLatency) => new WasapiAudioPlayer(GetDevice(deviceId), maxLatency);

    private static MMDevice GetDevice(string deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDevice(deviceId);
    }
}
