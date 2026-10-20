using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>Second byte of every packet. Audio values match <see cref="StreamKind"/>.</summary>
public enum PacketType : byte
{
    Microphone = StreamKind.Microphone,
    SystemAudio = StreamKind.SystemAudio,

    /// <summary>Empty "I'm here" packet sent every second: proves the link works and keeps firewalls open.</summary>
    Heartbeat = 2,
}
