using System.Windows.Input;

namespace VoiceWithoutBorders.Features.Hotkeys;

/// <summary>A global keyboard shortcut, e.g. Ctrl+Alt+M.</summary>
public sealed record HotkeyBinding(ModifierKeys Modifiers, Key Key)
{
    public static readonly HotkeyBinding DefaultToggleMic = new(ModifierKeys.Control | ModifierKeys.Alt, Key.M);
    public static readonly HotkeyBinding DefaultToggleSound = new(ModifierKeys.Control | ModifierKeys.Alt, Key.S);

    private const ModifierKeys TypingSafeModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows;

    /// <summary>
    /// Turns a key press into a binding. Returns null while only modifiers are held, and for combinations that
    /// would swallow normal typing everywhere (a letter with no modifier or only Shift). Function keys are fine alone.
    /// </summary>
    public static HotkeyBinding? FromKeyPress(Key key, ModifierKeys modifiers)
    {
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return null;
        var standalone = key is >= Key.F1 and <= Key.F24 or Key.Pause or Key.Scroll;
        if ((modifiers & TypingSafeModifiers) == 0 && !standalone)
            return null;
        return new HotkeyBinding(modifiers, key);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key is >= Key.D0 and <= Key.D9 ? ((int)(Key - Key.D0)).ToString() : Key.ToString());
        return string.Join("+", parts);
    }
}
