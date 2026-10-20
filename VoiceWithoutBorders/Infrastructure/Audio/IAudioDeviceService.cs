namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Lists the active Windows audio endpoints.</summary>
public interface IAudioDeviceService
{
    IReadOnlyList<AudioDevice> GetInputDevices();

    IReadOnlyList<AudioDevice> GetOutputDevices();
}
