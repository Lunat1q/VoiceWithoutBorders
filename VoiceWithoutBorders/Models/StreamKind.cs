namespace VoiceWithoutBorders.Models;

/// <summary>The two audio streams a machine can share. The value is sent on the wire.</summary>
public enum StreamKind : byte
{
    Microphone = 0,
    SystemAudio = 1,
}
