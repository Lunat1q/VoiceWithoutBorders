using System.Net;
using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Infrastructure.Network;

public class UdpPeerTransportTests
{
    private static readonly IPAddress V4 = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress V6 = IPAddress.Parse("2601:601:a500:63fa::341d");
    private static readonly IPAddress LinkLocal = IPAddress.Parse("fe80::560a:ae4a:8d98:c011%16");

    [Fact]
    public void ChooseAddress_PrefersIPv4() =>
        UdpPeerTransport.ChooseAddress([V6, V4], ipv6Supported: true).Should().Be(V4);

    // Regression: a computer name resolving only to IPv6 failed with "No such host is known".
    [Fact]
    public void ChooseAddress_IPv6Only_UsesResolverOrder() =>
        UdpPeerTransport.ChooseAddress([LinkLocal, V6], ipv6Supported: true).Should().Be(LinkLocal);

    [Fact]
    public void ChooseAddress_IPv6Only_WithoutIPv6Support_ReturnsNull() =>
        UdpPeerTransport.ChooseAddress([V6], ipv6Supported: false).Should().BeNull();

    [Fact]
    public void ChooseAddress_Nothing_ReturnsNull() =>
        UdpPeerTransport.ChooseAddress([], ipv6Supported: true).Should().BeNull();
}
