using System.Windows.Input;
using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Interop;

namespace VoiceWithoutBorders.Tests.Features.Hotkeys;

public class HotkeyControllerTests
{
    private readonly Mock<IGlobalHotkeys> _hotkeys = new();
    private readonly Dictionary<Key, Action> _registered = [];
    private readonly HashSet<Key> _ownedByOtherApps = [];
    private readonly MainViewModel _vm;
    private readonly HotkeyController _sut;

    public HotkeyControllerTests()
    {
        _hotkeys.Setup(h => h.Register(It.IsAny<ModifierKeys>(), It.IsAny<Key>(), It.IsAny<Action>()))
            .Returns((ModifierKeys _, Key key, Action action) => !_ownedByOtherApps.Contains(key) && _registered.TryAdd(key, action));
        _hotkeys.Setup(h => h.UnregisterAll()).Callback(() => _registered.Clear());

        var store = new Mock<ISettingsStore>();
        store.Setup(s => s.Load()).Returns(new AppSettings { SharedKey = "0123456789abcdef" });
        var devices = new Mock<IAudioDeviceService>();
        devices.Setup(d => d.GetInputDevices()).Returns([]);
        devices.Setup(d => d.GetOutputDevices()).Returns([]);
        devices.Setup(d => d.GetVirtualMicrophones()).Returns([]);
        _vm = new MainViewModel(new Mock<IStreamingService>().Object, store.Object, devices.Object);
        _sut = new HotkeyController(_hotkeys.Object, _vm);
    }

    [Fact]
    public void RegisterAll_DefaultShortcutsToggleMicAndSound()
    {
        _sut.RegisterAll();

        _registered.Keys.Should().BeEquivalentTo([Key.M, Key.S]);
        _vm.HotkeyStatus.Should().Be("Shortcuts work from any app.");

        _registered[Key.M]();
        _registered[Key.S]();

        _vm.MicEnabled.Should().BeFalse();
        _vm.SoundEnabled.Should().BeFalse();
    }

    [Fact]
    public void RegisterAll_ShortcutOwnedByAnotherApp_IsReportedByName()
    {
        _ownedByOtherApps.Add(Key.S);

        _sut.RegisterAll();

        _vm.HotkeyStatus.Should().Contain("Ctrl+Alt+S").And.Contain("another app");
    }

    [Fact]
    public void RegisterAll_SameShortcutTwice_KeepsFirstAndExplains()
    {
        _vm.StartStopHotkey = HotkeyBinding.DefaultToggleMic;

        _sut.RegisterAll();

        _hotkeys.Verify(h => h.Register(It.IsAny<ModifierKeys>(), Key.M, It.IsAny<Action>()), Times.Once);
        _vm.HotkeyStatus.Should().Contain("set for both");
    }

    [Fact]
    public void Start_RebindingAShortcut_ReRegistersImmediately()
    {
        _sut.Start(null!);

        _vm.ToggleMicHotkey = new HotkeyBinding(ModifierKeys.Control, Key.F8);

        _registered.Keys.Should().BeEquivalentTo([Key.F8, Key.S]);
    }

    [Fact]
    public void Start_RemovedShortcut_IsNotRegistered()
    {
        _sut.Start(null!);

        _vm.ToggleSoundHotkey = null;

        _registered.Keys.Should().BeEquivalentTo([Key.M]);
    }
}
