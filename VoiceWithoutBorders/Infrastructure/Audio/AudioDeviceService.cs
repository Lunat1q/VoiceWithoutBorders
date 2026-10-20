using NAudio.CoreAudioApi;

namespace VoiceWithoutBorders.Infrastructure.Audio;

public sealed class AudioDeviceService : IAudioDeviceService
{
    public IReadOnlyList<AudioDevice> GetInputDevices() => ToDevices(List(DataFlow.Capture));

    public IReadOnlyList<AudioDevice> GetOutputDevices() => ToDevices(List(DataFlow.Render));

    public IReadOnlyList<AudioDevice> GetVirtualMicrophones() =>
        PairVirtualMicrophones(List(DataFlow.Render), List(DataFlow.Capture));

    /// <summary>
    /// Matches each virtual microphone with the playback endpoint of the same driver: audio played into
    /// "CABLE Input (VB-Audio Virtual Cable)" comes out of the microphone "CABLE Output (VB-Audio Virtual Cable)".
    /// A driver with several playback endpoints prefers one named exactly like the mic (SteelSeries Sonar), then one
    /// named "Input" (VB-CABLE also has "CABLE In 16ch").
    /// </summary>
    public static IReadOnlyList<AudioDevice> PairVirtualMicrophones(IReadOnlyList<AudioEndpoint> renders, IReadOnlyList<AudioEndpoint> captures) =>
        captures
            .Where(mic => AudioDevice.LooksVirtual(mic.Name) || AudioDevice.LooksVirtual(mic.AdapterName))
            .Select(mic => (mic, playback: renders
                .Where(r => r.AdapterName == mic.AdapterName)
                .OrderBy(r => r.Name != mic.Name)
                .ThenBy(r => !r.Name.Contains("Input", StringComparison.OrdinalIgnoreCase))
                .ThenBy(r => r.Name)
                .FirstOrDefault()))
            .Where(pair => pair.playback is not null)
            .Select(pair => new AudioDevice(pair.playback!.Id, pair.mic.Name))
            .OrderBy(device => device.Name)
            .ToList();

    private static List<AudioDevice> ToDevices(IEnumerable<AudioEndpoint> endpoints) =>
        endpoints.Select(e => new AudioDevice(e.Id, e.Name)).OrderBy(d => d.Name).ToList();

    private static List<AudioEndpoint> List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Select(device =>
            {
                using (device)
                    return new AudioEndpoint(device.ID, device.FriendlyName, device.DeviceFriendlyName);
            })
            .ToList();
    }
}
