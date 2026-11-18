using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Features.Main;

// A device unplugged while streaming.
public partial class MainViewModelTests
{
    [Fact]
    public async Task UpdateLinkStatus_DeviceFailed_ClosesAndReopensEverything()
    {
        var failed = false;
        _streaming.Setup(s => s.DeviceFailed).Returns(() => failed);
        _streaming.Setup(s => s.Stats).Returns(new TransportStats(1, 1, 0, TimeSpan.FromSeconds(0.2)));
        _streaming.Setup(s => s.Stop()).Callback(() => failed = false);
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        failed = true;
        vm.UpdateLinkStatus();

        _streaming.Verify(s => s.Stop(), Times.Once);
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        vm.IsRunning.Should().BeTrue();
        vm.Status.Should().StartWith("Streaming with");
    }

    [Fact]
    public void UpdateLinkStatus_DeviceFailedWhileStopped_DoesNothing()
    {
        _streaming.Setup(s => s.DeviceFailed).Returns(true);
        var vm = Create();

        vm.UpdateLinkStatus();

        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
