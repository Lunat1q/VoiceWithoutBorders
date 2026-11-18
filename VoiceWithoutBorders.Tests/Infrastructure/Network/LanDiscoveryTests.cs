using System.Net;
using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Infrastructure.Network;

public class LanDiscoveryTests
{
    [Fact]
    public void BroadcastTargets_LimitedBroadcastPlusEachIPv4NetworksOwn()
    {
        var targets = LanDiscovery.BroadcastTargets(
        [
            (IPAddress.Parse("192.168.1.20"), IPAddress.Parse("255.255.255.0")),
            (IPAddress.Parse("10.1.2.3"), IPAddress.Parse("255.255.0.0")),
            (IPAddress.Parse("192.168.1.30"), IPAddress.Parse("255.255.255.0")), // second address, same network
            (IPAddress.Parse("fe80::1"), IPAddress.Any),
        ]);

        targets.Should().Equal(IPAddress.Broadcast, IPAddress.Parse("192.168.1.255"), IPAddress.Parse("10.1.255.255"));
    }
}
