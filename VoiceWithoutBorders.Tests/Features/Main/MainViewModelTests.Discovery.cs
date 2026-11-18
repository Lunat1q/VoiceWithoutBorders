using System.Net;
using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Features.Main;

// Find on the Connection tab, and the idle-time listener that lets other computers find this one.
public partial class MainViewModelTests
{
    private static readonly DiscoveredPeer Office = new("OFFICE-PC", IPAddress.Parse("192.168.1.20"));
    private static readonly DiscoveredPeer Den = new("DEN-PC", IPAddress.Parse("192.168.1.31"));

    [Fact]
    public void Constructor_ListensForSearchesWhileIdle()
    {
        Create();

        _discovery.Verify(d => d.Listen(15300, "0123456789abcdef"));
    }

    [Fact]
    public void KeyTooShort_StopsListening_ValidAgainListens()
    {
        var vm = Create();

        vm.SharedKey = "short";
        _discovery.Verify(d => d.StopListening());

        vm.SharedKey = "fedcba9876543210";
        _discovery.Verify(d => d.Listen(15300, "fedcba9876543210"));
    }

    [Fact]
    public async Task Start_ReleasesThePortBeforeStreamingTakesIt_StopListensAgain()
    {
        var order = new List<string>();
        _discovery.Setup(d => d.StopListening()).Callback(() => order.Add("stop listening"));
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("stream"))
            .ReturnsAsync(new IPEndPoint(Office.Address, 15300));
        var vm = Create();

        await vm.StartCommand.ExecuteAsync(null);
        order.Should().StartWith(["stop listening", "stream"]);

        _discovery.Invocations.Clear();
        vm.StopCommand.Execute(null);
        _discovery.Verify(d => d.Listen(15300, "0123456789abcdef"));
    }

    [Fact]
    public async Task Start_Fails_ListensAgain()
    {
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("no"));
        var vm = Create();
        _discovery.Invocations.Clear();

        await vm.StartCommand.ExecuteAsync(null);

        _discovery.Verify(d => d.Listen(15300, "0123456789abcdef"));
    }

    [Fact]
    public async Task FindPeers_OneAnswer_FillsInItsAddress()
    {
        _discovery.Setup(d => d.SearchAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Office]);
        var vm = Create();
        vm.PeerHost = "";

        await vm.FindPeersCommand.ExecuteAsync(null);

        vm.PeerHost.Should().Be("192.168.1.20", "the address skips name resolution");
        vm.SearchStatus.Should().Contain("OFFICE-PC");
        vm.IsSearching.Should().BeFalse();
    }

    [Fact]
    public async Task FindPeers_SeveralAnswers_ListsThemToPick()
    {
        _discovery.Setup(d => d.SearchAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Den, Office]);
        var vm = Create();

        await vm.FindPeersCommand.ExecuteAsync(null);

        vm.PeerHost.Should().Be("OFFICE-PC", "nothing is picked for the user");
        vm.FoundPeers.Should().Equal(Den, Office);

        vm.UsePeerCommand.Execute(Den);
        vm.PeerHost.Should().Be("192.168.1.31");
    }

    [Fact]
    public async Task FindPeers_NoAnswer_SaysWhatToCheck()
    {
        _discovery.Setup(d => d.SearchAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var vm = Create();

        await vm.FindPeersCommand.ExecuteAsync(null);

        vm.SearchStatus.Should().Contain("No computer answered").And.Contain("15300");
    }

    [Fact]
    public async Task FindPeers_KeyTooShort_DoesNotSearch()
    {
        var vm = Create();
        vm.SharedKey = "short";

        await vm.FindPeersCommand.ExecuteAsync(null);

        vm.SearchStatus.Should().Contain("shared key");
        _discovery.Verify(d => d.SearchAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindPeers_PortTaken_ReportsWhy()
    {
        _discovery.Setup(d => d.SearchAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("UDP port 15300 is in use by another program."));
        var vm = Create();

        await vm.FindPeersCommand.ExecuteAsync(null);

        vm.SearchStatus.Should().Be("Could not search: UDP port 15300 is in use by another program.");
        vm.IsSearching.Should().BeFalse();
    }
}
