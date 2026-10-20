using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VoiceWithoutBorders.Features.Hotkeys;

/// <summary>
/// Read-only box that records the next key combination pressed in it. Backspace or Delete removes the shortcut;
/// Tab still moves focus.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(HotkeyBinding), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).ShowHotkey()));

    /// <summary>
    /// Raised with true while any box has focus: registered shortcuts must be paused, otherwise pressing an
    /// existing one fires it instead of reaching the box.
    /// </summary>
    public static event Action<bool>? CapturingChanged;

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        ContextMenu = null;
        ShowHotkey();
    }

    public HotkeyBinding? Hotkey
    {
        get => (HotkeyBinding?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0)
            return; // keyboard navigation
        e.Handled = true;
        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
            Hotkey = null;
        else if (HotkeyBinding.FromKeyPress(key, Keyboard.Modifiers) is { } binding)
            Hotkey = binding;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = "Press a combination…";
        CapturingChanged?.Invoke(true);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        ShowHotkey();
        CapturingChanged?.Invoke(false);
    }

    private void ShowHotkey()
    {
        if (!IsKeyboardFocused)
            Text = Hotkey?.ToString() ?? "None";
        else if (Hotkey is { } hotkey)
            Text = hotkey.ToString();
    }
}
