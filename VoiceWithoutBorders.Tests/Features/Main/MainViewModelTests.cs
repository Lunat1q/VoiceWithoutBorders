using System.Net;
using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Tests.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Interop;
using VoiceWithoutBorders.Infrastructure.Network;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Tests.Features.Main;

public class MainViewModelTests
{
    private static readonly AudioDevice Mic = new("mic", "USB Microphone");
    private static readonly AudioDevice Headset = new("headset", "Headset");
    private static readonly AudioDevice CableInput = new("cable", "CABLE Input");
    // Named after the microphone apps see, id of the cable's playback side.
    private static readonly AudioDevice Cable = new("cable", "CABLE Output");

    private readonly Mock<IStreamingService> _streaming = new();
    private readonly Mock<ISettingsStore> _store = new();
    private readonly Mock<IAudioDeviceService> _devices = new();
    private readonly Mock<IStartupRegistration> _startup = new();

    public MainViewModelTests()
    {
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IPEndPoint(IPAddress.Parse("192.168.1.20"), 15300));
        _streaming.Setup(s => s.GetLatency()).Returns([]);
        _devices.Setup(d => d.GetInputDevices()).Returns([Mic]);
        _devices.Setup(d => d.GetOutputDevices()).Returns([Headset, CableInput]);
        _devices.Setup(d => d.GetVirtualMicrophones()).Returns([Cable]);
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            PeerHost = "OFFICE-PC",
            SharedKey = "0123456789abcdef",
            MicCaptureDeviceId = "mic",
            MicPlaybackDeviceId = "unplugged-device",
            SystemPlaybackDeviceId = "headset",
        });
    }

    private MainViewModel Create() => new(_streaming.Object, _store.Object, _devices.Object, _startup.Object);

    [Fact]
    public void Constructor_LoadsSettingsAndSelectsSavedDevices()
    {
        var vm = Create();

        vm.PeerHost.Should().Be("OFFICE-PC");
        vm.MicCapture.Should().Be(Mic);
        vm.SystemPlayback.Should().Be(Headset);
        vm.MicPlayback.Should().Be(MainViewModel.Off, "the saved device is no longer present");
        vm.SystemCapture.Should().Be(MainViewModel.Off);
        vm.OutputDevices.Should().Equal(MainViewModel.Off, Headset, CableInput);
        vm.VirtualMicrophones.Should().Equal(MainViewModel.Off, Cable);
        vm.NoOutputDevices.Should().BeFalse();
        vm.NoVirtualMicrophone.Should().BeFalse();
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
    public void NoVirtualMicrophone_WhenNoCableInstalled()
    {
        _devices.Setup(d => d.GetVirtualMicrophones()).Returns([]);

        var vm = Create();

        vm.NoVirtualMicrophone.Should().BeTrue();
        vm.VirtualMicrophones.Should().Equal(MainViewModel.Off);
    }

    [Fact]
    public void SavedCable_IsSelectedAsMicrophone()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings { SharedKey = "0123456789abcdef", MicPlaybackDeviceId = "cable" });

        Create().MicPlayback.Should().Be(Cable);
    }

    // Regression: settings were only saved by a successful Start, so closing the app after setting up lost everything.
    [Fact]
    public void EditingSettings_SavesImmediatelyWithoutStart()
    {
        var vm = Create();

        vm.PeerHost = "LAPTOP";
        vm.SystemCapture = Headset;

        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.PeerHost == "LAPTOP" && saved.SystemCaptureDeviceId == "headset")));
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void LoadingAndRefreshingDevices_DoNotOverwriteSavedSettings()
    {
        var vm = Create();

        vm.RefreshDevicesCommand.Execute(null);

        _store.Verify(s => s.Save(It.IsAny<AppSettings>()), Times.Never);
    }

    [Fact]
    public void SaveFailure_ShowsStatusInsteadOfCrashing()
    {
        _store.Setup(s => s.Save(It.IsAny<AppSettings>())).Throws(new IOException("disk full"));
        var vm = Create();

        vm.PeerHost = "LAPTOP";

        vm.Status.Should().Contain("disk full");
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
    public void ToggleMicAndSound_ApplyLiveAndAreSaved()
    {
        var vm = Create();

        vm.MicEnabled = false;
        vm.SoundEnabled = false;

        _streaming.Verify(s => s.SetEnabled(StreamKind.Microphone, false));
        _streaming.Verify(s => s.SetEnabled(StreamKind.SystemAudio, false));
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => !saved.MicEnabled && !saved.SoundEnabled)));
    }

    [Fact]
    public void Constructor_RestoresSavedToggles()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            SharedKey = "0123456789abcdef", MicEnabled = false, SoundEnabled = true, ShowOverlay = false,
        });

        var vm = Create();

        vm.MicEnabled.Should().BeFalse();
        vm.SoundEnabled.Should().BeTrue();
        vm.ShowOverlay.Should().BeFalse();
    }

    [Fact]
    public async Task OverlayVisible_OnlyWhileStreamingAndEnabled()
    {
        var vm = Create();
        vm.OverlayVisible.Should().BeFalse("not streaming yet");

        await vm.StartCommand.ExecuteAsync(null);
        vm.OverlayVisible.Should().BeTrue();
        vm.Overlay.Indicators.Should().HaveCount(2, "mic is sent and sound is received");

        vm.ShowOverlay = false;
        vm.OverlayVisible.Should().BeFalse();

        vm.ShowOverlay = true;
        vm.StopCommand.Execute(null);
        vm.OverlayVisible.Should().BeFalse();
    }

    [Fact]
    public void UnlockingOverlay_ShowsPreviewWhileStopped_AndPositionIsSaved()
    {
        var vm = Create();

        vm.OverlayUnlocked = true;

        vm.OverlayVisible.Should().BeTrue("so it can be dragged even when not streaming");
        vm.Overlay.IsUnlocked.Should().BeTrue();
        vm.Overlay.Indicators.Should().HaveCount(2, "preview of the configured streams");

        vm.MoveOverlay(120, 40);
        vm.OverlayUnlocked = false;

        vm.OverlayVisible.Should().BeFalse();
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.OverlayLeft == 120 && saved.OverlayTop == 40)));
    }

    [Fact]
    public void ResetOverlayPosition_ClearsSavedSpot()
    {
        var vm = Create();
        vm.MoveOverlay(120, 40);

        vm.ResetOverlayPositionCommand.Execute(null);

        vm.OverlayLeft.Should().BeNull();
        vm.OverlayTop.Should().BeNull();
    }

    [Fact]
    public async Task ToggleStreaming_StartsThenStops()
    {
        var vm = Create();

        await vm.ToggleStreamingCommand.ExecuteAsync(null);
        vm.IsRunning.Should().BeTrue();

        await vm.ToggleStreamingCommand.ExecuteAsync(null);
        vm.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Hotkeys_LoadDefaultsAndPersistChanges()
    {
        var vm = Create();
        vm.ToggleMicHotkey.Should().Be(HotkeyBinding.DefaultToggleMic);
        vm.StartStopHotkey.Should().BeNull();

        vm.StartStopHotkey = new HotkeyBinding(System.Windows.Input.ModifierKeys.Control, System.Windows.Input.Key.F8);

        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.StartStopHotkey!.ToString() == "Ctrl+F8")));
    }

    [Fact]
    public async Task UpdateLinkStatus_RejectedPackets_ExplainsKeyMismatch()
    {
        _streaming.Setup(s => s.Stats).Returns(new TransportStats(Sent: 10, Received: 0, Rejected: 5));
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();

        vm.Connection.Should().Be(PeerConnection.KeyMismatch);
        vm.ConnectionText.Should().Contain("different shared key");
        vm.IsSending.Should().BeTrue();
        vm.IsReceiving.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateLinkStatus_ShowsLatencyPerIncomingStream()
    {
        _streaming.Setup(s => s.Stats).Returns(new TransportStats(1, 1, 0, TimeSpan.Zero, RoundTrip: TimeSpan.FromMilliseconds(4)));
        _streaming.Setup(s => s.GetLatency()).Returns(
        [
            new StreamLatency(StreamKind.Microphone, TimeSpan.FromMilliseconds(78), TimeSpan.FromMilliseconds(30), 0),
            new StreamLatency(StreamKind.SystemAudio, TimeSpan.FromMilliseconds(210), TimeSpan.FromMilliseconds(160), 0),
        ]);
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);
        vm.LatencyText.Should().Be("Measuring latency…");

        vm.UpdateLinkStatus();

        vm.LatencyText.Should().Be("Network round trip 4 ms · their mic → here ≈ 78 ms · their sound → here ≈ 210 ms");
        vm.LatencyHealth.Should().Be(LatencyHealth.Fair, "the worst stream decides");
    }

    [Fact]
    public async Task UpdateLinkStatus_NewSkips_WarnForTenSecondsThenClear()
    {
        var skips = 0;
        _streaming.Setup(s => s.Stats).Returns(new TransportStats(1, 1, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(4)));
        _streaming.Setup(s => s.GetLatency()).Returns(() =>
            [new StreamLatency(StreamKind.Microphone, TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(40), skips)]);
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        vm.AudioSkipping.Should().BeFalse();

        skips = 3;
        vm.UpdateLinkStatus();
        vm.AudioSkipping.Should().BeTrue();

        for (var second = 0; second < 10; second++)
            vm.UpdateLinkStatus();
        vm.AudioSkipping.Should().BeFalse("no new skips for ten seconds");
    }

    [Fact]
    public async Task UpdateLinkStatus_PeerAnswering_ShowsConnected_AndStopClearsFlow()
    {
        _streaming.Setup(s => s.Stats).Returns(new TransportStats(Sent: 10, Received: 10, Rejected: 0, TimeSpan.FromMilliseconds(200)));
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);
        vm.Connection.Should().Be(PeerConnection.Waiting, "nothing heard right after Start");

        vm.UpdateLinkStatus();
        vm.Connection.Should().Be(PeerConnection.Connected);
        vm.IsReceiving.Should().BeTrue();

        vm.StopCommand.Execute(null);
        vm.IsSending.Should().BeFalse();
        vm.IsReceiving.Should().BeFalse();
    }

    private static TransportStats PeerHeard(double secondsAgo) => new(10, 10, 0, TimeSpan.FromSeconds(secondsAgo));

    [Fact]
    public async Task UpdateLinkStatus_PeerBackAfterGoingQuiet_ReopensStreaming()
    {
        // Regression: the other computer hibernated; when it came back its audio devices were reset and it got a new
        // address, but this side kept playing into the old devices and sending to the old address.
        var stats = PeerHeard(0.2);
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        stats = PeerHeard(30);
        vm.UpdateLinkStatus();
        vm.Connection.Should().Be(PeerConnection.Waiting);
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Once);

        stats = PeerHeard(0.2);
        vm.UpdateLinkStatus();

        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        vm.IsRunning.Should().BeTrue();
        vm.UpdateLinkStatus();
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(2), "only once per reconnect");
    }

    [Fact]
    public async Task UpdateLinkStatus_FirstAnswer_DoesNotRestart()
    {
        var stats = new TransportStats(1, 0, 0);
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        vm.UpdateLinkStatus();
        stats = PeerHeard(0.2);
        vm.UpdateLinkStatus();

        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnSystemResumed_ReopensStreamingOnceThePeerAnswers()
    {
        _streaming.Setup(s => s.Stats).Returns(PeerHeard(0.2));
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);
        vm.UpdateLinkStatus();

        vm.OnSystemResumed();
        vm.UpdateLinkStatus();

        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Reconnect_RestartFails_StopsWithReason()
    {
        var stats = PeerHeard(0.2);
        _streaming.Setup(s => s.Stats).Returns(() => stats);
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);
        vm.UpdateLinkStatus();
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Headset is gone"));

        vm.OnSystemResumed();
        vm.UpdateLinkStatus();

        vm.IsRunning.Should().BeFalse();
        vm.Status.Should().Contain("Headset is gone");
    }

    [Fact]
    public void StartWithWindows_ReadFromWindowsAndRefreshedWithTheCurrentExe()
    {
        _startup.Setup(s => s.IsEnabled).Returns(true);

        var vm = Create();

        vm.StartWithWindows.Should().BeTrue();
        _startup.Verify(s => s.SetEnabled(true), Times.Once);
    }

    [Fact]
    public void StartWithWindows_Toggling_ChangesWindowsEntryNotSettingsFile()
    {
        var vm = Create();

        vm.StartWithWindows = true;
        vm.StartWithWindows = false;

        _startup.Verify(s => s.SetEnabled(true), Times.Once);
        _startup.Verify(s => s.SetEnabled(false), Times.Once);
        _store.Verify(s => s.Save(It.IsAny<AppSettings>()), Times.Never);
    }

    [Fact]
    public void StartWithWindows_RegistryFails_ShowsStatus()
    {
        _startup.Setup(s => s.SetEnabled(true)).Throws(new UnauthorizedAccessException("denied"));
        var vm = Create();

        vm.StartWithWindows = true;

        vm.Status.Should().Contain("denied");
    }

    [Fact]
    public void AutoConnect_IsSaved()
    {
        var vm = Create();

        vm.AutoConnect = true;

        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.AutoConnect)));
    }

    [Fact]
    public async Task OnStartedWithWindows_AutoConnectOff_DoesNotStart()
    {
        var vm = Create();

        await vm.OnStartedWithWindowsAsync(TimeSpan.Zero);

        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnStartedWithWindows_RetriesUntilTheNetworkIsUp()
    {
        _store.Setup(s => s.Load()).Returns(AppSettingsTests.Valid with { AutoConnect = true });
        _streaming.SetupSequence(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Can't find 'OFFICE-PC'"))
            .ReturnsAsync(new IPEndPoint(IPAddress.Loopback, 15300));
        var vm = Create();

        await vm.OnStartedWithWindowsAsync(TimeSpan.Zero);

        vm.IsRunning.Should().BeTrue();
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task OnStartedWithWindows_GivesUpAfterTheLastAttempt()
    {
        _store.Setup(s => s.Load()).Returns(AppSettingsTests.Valid with { AutoConnect = true });
        _streaming.Setup(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("offline"));
        var vm = Create();

        await vm.OnStartedWithWindowsAsync(TimeSpan.Zero);

        vm.IsRunning.Should().BeFalse();
        _streaming.Verify(s => s.StartAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(MainViewModel.AutoConnectAttempts));
    }
}
