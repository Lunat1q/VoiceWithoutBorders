using System.Net;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <param name="Name">Computer name the other app reported.</param>
public sealed record DiscoveredPeer(string Name, IPAddress Address)
{
    public override string ToString() => $"{Name} ({Address})";
}
