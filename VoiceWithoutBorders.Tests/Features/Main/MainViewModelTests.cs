using System.Net;
using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Tests.Features.Main;

public class MainViewModelTests
{
    private static readonly AudioDevice Mic = new("mic", "USB Microphone");
    private static readonly AudioDevice Headset = new("headset", "Headset");
    private static readonly AudioDevice Cable = new("cable", "CABLE Input");

    private readonly Mock<IStreamingService> _streaming = new();
    private readonly Mock<ISettingsStore> _store = new();
    private readonly Mock<IAudioDeviceService> _devices = new();

    public MainViewModelTests()
    {
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IPEndPoint(IPAddress.Parse("192.168.1.20"), 15300));
        _devices.Setup(d => d.GetInputDevices()).Returns([Mic]);
        _devices.Setup(d => d.GetOutputDevices()).Returns([Headset, Cable]);
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            PeerHost = "OFFICE-PC",
            SharedKey = "0123456789abcdef",
            MicCaptureDeviceId = "mic",
            MicPlaybackDeviceId = "unplugged-device",
            SystemPlaybackDeviceId = "headset",
        });
    }

    private MainViewModel Create() => new(_streaming.Object, _store.Object, _devices.Object);

    [Fact]
    public void Constructor_LoadsSettingsAndSelectsSavedDevices()
    {
        var vm = Create();

        vm.PeerHost.Should().Be("OFFICE-PC");
        vm.MicCapture.Should().Be(Mic);
        vm.SystemPlayback.Should().Be(Headset);
        vm.MicPlayback.Should().Be(MainViewModel.Off, "the saved device is no longer present");
        vm.SystemCapture.Should().Be(MainViewModel.Off);
        vm.OutputDevices.Should().Equal(MainViewModel.Off, Headset, Cable);
        vm.NoOutputDevices.Should().BeFalse();
        _store.Verify(s => s.Save(It.IsAny<AppSettings>()), Times.Never, "an existing key is kept as is");
    }

    [Fact]
    public void Constructor_NoSavedKey_GeneratesAndSavesOneKeepingOtherSettings()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings { PeerHost = "OFFICE-PC", MicCaptureDeviceId = "unplugged-device" });

        var vm = Create();

        vm.SharedKey.Should().HaveLength(24);
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved =>
            saved.SharedKey == vm.SharedKey && saved.PeerHost == "OFFICE-PC" && saved.MicCaptureDeviceId == "unplugged-device")));
    }

    [Fact]
    public void NewSharedKey_ReplacesKey()
    {
        var vm = Create();

        vm.NewSharedKeyCommand.Execute(null);

        vm.SharedKey.Should().NotBe("0123456789abcdef").And.HaveLength(24);
    }

    [Fact]
    public void NoOutputDevices_WhenOnlyOffIsListed()
    {
        _devices.Setup(d => d.GetOutputDevices()).Returns([]);

        Create().NoOutputDevices.Should().BeTrue();
    }

    [Theory]
    [InlineData("OFFICE-PC", "Streaming with OFFICE-PC (192.168.1.20) on UDP port 15300.")]
    [InlineData("192.168.1.20", "Streaming with 192.168.1.20 on UDP port 15300.")]
    public async Task Start_ShowsResolvedAddress(string host, string expected)
    {
        var vm = Create();
        vm.PeerHost = host;

        await vm.StartCommand.ExecuteAsync(null);

        vm.Status.Should().Be(expected);
    }

    [Fact]
    public async Task Start_InvalidSettings_ShowsErrorsWithoutStarting()
    {
        var vm = Create();
        vm.SharedKey = "short";

        await vm.StartCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("at least 16");
        vm.IsRunning.Should().BeFalse();
        _store.Verify(s => s.Save(It.IsAny<AppSettings>()), Times.Never);
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_Valid_SavesAndStartsWithSelectedDevices()
    {
        var vm = Create();
        vm.MicPlayback = Cable;

        await vm.StartCommand.ExecuteAsync(null);

        var expected = new AppSettings
        {
            PeerHost = "OFFICE-PC",
            SharedKey = "0123456789abcdef",
            MicCaptureDeviceId = "mic",
            MicPlaybackDeviceId = "cable",
            SystemPlaybackDeviceId = "headset",
        };
        _store.Verify(s => s.Save(expected));
        _streaming.Verify(s => s.StartAsync(expected, It.IsAny<CancellationToken>()));
        vm.IsRunning.Should().BeTrue();
        vm.IsIdle.Should().BeFalse();
        vm.StartCommand.CanExecute(null).Should().BeFalse();
        vm.StopCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Start_ServiceFails_ShowsReasonAndStaysIdle()
    {
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Could not resolve 'OFFICE-PC'"));
        var vm = Create();

        await vm.StartCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("Could not resolve");
        vm.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_StopsStreaming()
    {
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        vm.StopCommand.Execute(null);

        _streaming.Verify(s => s.Stop());
        vm.IsRunning.Should().BeFalse();
        vm.Status.Should().Be("Stopped");
    }

    [Fact]
    public async Task UpdateStats_AllRejected_HintsAtKeyMismatch()
    {
        _streaming.Setup(s => s.Stats).Returns(new TransportStats(Sent: 10, Received: 0, Rejected: 5));
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateStats();

        vm.Stats.Should().Contain("rejected 5").And.Contain("same shared key");
    }
}
