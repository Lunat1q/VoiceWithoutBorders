namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Lists the active Windows audio endpoints.</summary>
public interface IAudioDeviceService
{
    IReadOnlyList<AudioDevice> GetInputDevices();

    IReadOnlyList<AudioDevice> GetOutputDevices();

    /// <summary>
    /// Microphones created by a virtual cable driver. Each item is named after the microphone apps will pick, and its
    /// <see cref="AudioDevice.Id"/> is the cable's playback side, which is where incoming audio must be played.
    /// </summary>
    IReadOnlyList<AudioDevice> GetVirtualMicrophones();
}
