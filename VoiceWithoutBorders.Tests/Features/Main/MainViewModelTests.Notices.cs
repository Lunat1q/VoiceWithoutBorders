using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Features.Main;

// Notices the tray pops up when the link comes up, drops or comes back.
public partial class MainViewModelTests
{
    [Fact]
    public async Task LinkNotice_AnnouncesConnected_ThenLostAfterThreeSamples_ThenConnectedAgain()
    {
        TransportStats stats = default;
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        var notices = new List<string>();
        vm.LinkNotice += notices.Add;
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        notices.Should().BeEmpty("the peer has not answered yet, which is no news");

        stats = PeerHeard(0.2);
        vm.UpdateLinkStatus();
        vm.UpdateLinkStatus();
        notices.Should().Equal("Connected to OFFICE-PC.");

        stats = PeerHeard(30);
        vm.UpdateLinkStatus();
        vm.UpdateLinkStatus();
        notices.Should().HaveCount(1, "a short gap is not announced");
        vm.UpdateLinkStatus();
        notices.Last().Should().StartWith("Lost the connection to OFFICE-PC.");

        stats = PeerHeard(0.2);
        vm.UpdateLinkStatus(); // also reopens streaming
        notices.Last().Should().Be("Connected to OFFICE-PC again.");

        stats = default; // the reopened transport has not heard the peer yet
        vm.UpdateLinkStatus();
        vm.UpdateLinkStatus();
        stats = PeerHeard(0.2);
        vm.UpdateLinkStatus();
        notices.Should().HaveCount(3, "the restart is not a lost link");
    }

    [Fact]
    public async Task LinkNotice_AfterStop_StartsAfresh()
    {
        _streaming.Setup(s => s.Stats).Returns(PeerHeard(0.2));
        var vm = Create();
        var notices = new List<string>();
        vm.LinkNotice += notices.Add;
        await vm.StartCommand.ExecuteAsync(null);
        vm.UpdateLinkStatus();

        vm.StopCommand.Execute(null);
        await vm.StartCommand.ExecuteAsync(null);
        vm.UpdateLinkStatus();

        notices.Should().Equal("Connected to OFFICE-PC.", "Connected to OFFICE-PC.");
    }

    [Fact]
    public async Task LinkNotice_AllOff_SaysNothing()
    {
        TransportStats stats = PeerHeard(0.2);
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        vm.NotificationsEnabled = false;
        var notices = new List<string>();
        vm.LinkNotice += notices.Add;
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        stats = PeerHeard(30);
        for (var i = 0; i < 3; i++)
            vm.UpdateLinkStatus();

        notices.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkNotice_ConnectedOff_StillAnnouncesLost()
    {
        TransportStats stats = PeerHeard(0.2);
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        vm.NotifyConnected = false;
        var notices = new List<string>();
        vm.LinkNotice += notices.Add;
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        stats = PeerHeard(30);
        for (var i = 0; i < 3; i++)
            vm.UpdateLinkStatus();

        notices.Should().ContainSingle().Which.Should().StartWith("Lost the connection to OFFICE-PC.");
    }

    [Fact]
    public async Task LinkNotice_KeyMismatchOff_SkipsOnlyThat()
    {
        TransportStats stats = PeerHeard(0.2);
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        vm.NotifyKeyMismatch = false;
        var notices = new List<string>();
        vm.LinkNotice += notices.Add;
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        for (var rejected = 1; rejected <= 3; rejected++)
        {
            stats = new TransportStats(10, 10, rejected); // peer answers, but nothing authenticates
            vm.UpdateLinkStatus();
        }

        notices.Should().Equal("Connected to OFFICE-PC.");
    }

    [Fact]
    public void NotificationSettings_AreLoadedAndSaved()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            SharedKey = "0123456789abcdef", NotificationsEnabled = false, NotifyConnected = false, NotifyLost = true, NotifyKeyMismatch = false,
        });
        var vm = Create();

        vm.NotificationsEnabled.Should().BeFalse();
        vm.NotifyConnected.Should().BeFalse();
        vm.NotifyLost.Should().BeTrue();
        vm.NotifyKeyMismatch.Should().BeFalse();

        vm.NotifyLost = false;

        _store.Verify(s => s.Save(It.Is<AppSettings>(saved =>
            !saved.NotificationsEnabled && !saved.NotifyConnected && !saved.NotifyLost && !saved.NotifyKeyMismatch)));
    }
}
