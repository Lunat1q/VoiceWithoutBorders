using NAudio.CoreAudioApi;

namespace VoiceWithoutBorders.Infrastructure.Audio;

public sealed class AudioDeviceService : IAudioDeviceService
{
    public IReadOnlyList<AudioDevice> GetInputDevices() => List(DataFlow.Capture);

    public IReadOnlyList<AudioDevice> GetOutputDevices() => List(DataFlow.Render);

    private static List<AudioDevice> List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Select(device =>
            {
                using (device)
                    return new AudioDevice(device.ID, device.FriendlyName);
            })
            .OrderBy(device => device.Name)
            .ToList();
    }
}
