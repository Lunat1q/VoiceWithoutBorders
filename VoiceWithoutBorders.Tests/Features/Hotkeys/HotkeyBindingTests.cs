using System.Windows.Input;
using FluentAssertions;
using VoiceWithoutBorders.Features.Hotkeys;

namespace VoiceWithoutBorders.Tests.Features.Hotkeys;

public class HotkeyBindingTests
{
    [Theory]
    [InlineData(Key.M, ModifierKeys.Control | ModifierKeys.Alt, "Ctrl+Alt+M")]
    [InlineData(Key.D5, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+5")]
    [InlineData(Key.F9, ModifierKeys.None, "F9")]
    [InlineData(Key.K, ModifierKeys.Windows | ModifierKeys.Alt, "Alt+Win+K")]
    public void FromKeyPress_AcceptsSafeCombinations(Key key, ModifierKeys modifiers, string expected) =>
        HotkeyBinding.FromKeyPress(key, modifiers)!.ToString().Should().Be(expected);

    [Theory]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control)]       // still choosing modifiers
    [InlineData(Key.RightAlt, ModifierKeys.Alt)]
    [InlineData(Key.M, ModifierKeys.None)]                 // would swallow every "m" typed anywhere
    [InlineData(Key.M, ModifierKeys.Shift)]                // would swallow every "M"
    public void FromKeyPress_RejectsModifiersAloneAndTypingKeys(Key key, ModifierKeys modifiers) =>
        HotkeyBinding.FromKeyPress(key, modifiers).Should().BeNull();
}
