using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VoiceWithoutBorders.Features.Hotkeys;

/// <summary>
/// Read-only box that records a key combination. Recording starts only on a click (or Enter/Space when reached
/// with Tab), never on focus alone: switching tabs focuses the first box, which must not look like "unset".
/// While recording, the next valid combination is taken; Esc cancels; Backspace or Delete removes the shortcut.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    private const string RecordingText = "Press a combination… (Esc cancels)";

    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(HotkeyBinding), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).ShowHotkey()));

    /// <summary>
    /// Raised with true while a box is recording: registered shortcuts must be paused, otherwise pressing an
    /// existing one fires it instead of reaching the box.
    /// </summary>
    public static event Action<bool>? CapturingChanged;

    private bool _recording;

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

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Focus();
        SetRecording(true);
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var noModifiers = Keyboard.Modifiers == ModifierKeys.None;
        if (key == Key.Tab && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0)
        {
            SetRecording(false);
            return; // keyboard navigation
        }

        e.Handled = true;
        if (key is Key.Back or Key.Delete && noModifiers)
        {
            Hotkey = null;
            SetRecording(false);
        }
        else if (!_recording)
        {
            if (key is Key.Enter or Key.Space && noModifiers)
                SetRecording(true);
        }
        else if (key == Key.Escape && noModifiers)
        {
            SetRecording(false);
        }
        else if (HotkeyBinding.FromKeyPress(key, Keyboard.Modifiers) is { } binding)
        {
            Hotkey = binding;
            SetRecording(false);
        }
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetRecording(false);
    }

    private void SetRecording(bool recording)
    {
        if (_recording == recording)
            return;
        _recording = recording;
        ShowHotkey();
        CapturingChanged?.Invoke(recording);
    }

    private void ShowHotkey() => Text = _recording ? RecordingText : Hotkey?.ToString() ?? "None";
}
