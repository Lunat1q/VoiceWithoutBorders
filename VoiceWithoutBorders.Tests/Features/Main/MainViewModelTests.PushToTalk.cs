using System.Windows.Input;
using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Tests.Features.Main;

public partial class MainViewModelTests
{
    private static readonly HotkeyBinding F13 = new(ModifierKeys.None, Key.F13);

    [Fact]
    public void PushToTalk_NotSet_MicAlwaysOpen()
    {
        var vm = Create();

        vm.MicOpen.Should().BeTrue();
        _streaming.Verify(s => s.SetMicOpen(false), Times.Never);
    }

    [Fact]
    public void PushToTalk_SavedBinding_ClosesTheMicUntilHeld()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings { SharedKey = "0123456789abcdef", PushToTalkHotkey = F13 });
        var vm = Create();
        _streaming.Verify(s => s.SetMicOpen(false));

        vm.IsTalking = true;
        _streaming.Verify(s => s.SetMicOpen(true));

        vm.IsTalking = false;
        _streaming.Verify(s => s.SetMicOpen(false), Times.Exactly(2));
    }

    [Fact]
    public void PushToTalk_Removed_OpensTheMicAndIsSaved()
    {
        var vm = Create();
        vm.PushToTalkHotkey = F13;
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved => saved.PushToTalkHotkey == F13)));

        vm.PushToTalkHotkey = null;

        vm.MicOpen.Should().BeTrue();
        _streaming.Verify(s => s.SetMicOpen(true));
    }
}
