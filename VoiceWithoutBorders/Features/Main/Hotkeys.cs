using System.Windows.Input;

namespace VoiceWithoutBorders.Features.Main;

/// <summary>Fixed global shortcuts, shown in the window and the tray menu.</summary>
public static class Hotkeys
{
    public const ModifierKeys Modifiers = ModifierKeys.Control | ModifierKeys.Alt;

    public const Key ToggleMicKey = Key.M;
    public const Key ToggleSoundKey = Key.S;

    public const string ToggleMicText = "Ctrl+Alt+M";
    public const string ToggleSoundText = "Ctrl+Alt+S";
}
