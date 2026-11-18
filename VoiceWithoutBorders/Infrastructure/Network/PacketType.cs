using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>Second byte of every packet. Audio values match <see cref="StreamKind"/>.</summary>
public enum PacketType : byte
{
    Microphone = StreamKind.Microphone,
    SystemAudio = StreamKind.SystemAudio,

    /// <summary>Empty "I'm here" packet sent every second: proves the link works and keeps firewalls open.</summary>
    Heartbeat = 2,

    /// <summary>"Who is out there with this key?", broadcast by Find on the Connection tab. Payload: <see cref="DiscoveryPayload"/>.</summary>
    DiscoverRequest = 3,

    /// <summary>Answer to a request, sent back to its sender: instance id and computer name.</summary>
    DiscoverReply = 4,
}
