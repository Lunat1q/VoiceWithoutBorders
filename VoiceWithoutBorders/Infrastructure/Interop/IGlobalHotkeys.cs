using System.Windows;
using System.Windows.Input;

namespace VoiceWithoutBorders.Infrastructure.Interop;

/// <summary>System-wide shortcuts that work while another app has focus or the window is in the tray.</summary>
public interface IGlobalHotkeys : IDisposable
{
    /// <summary>Hotkey messages are delivered to this window, so it must live as long as the hotkeys.</summary>
    void Attach(Window window);

    /// <returns>False when another app already owns the combination.</returns>
    bool Register(ModifierKeys modifiers, Key key, Action action);

    void UnregisterAll();
}
