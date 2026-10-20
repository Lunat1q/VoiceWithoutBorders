using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace VoiceWithoutBorders.Infrastructure.Interop;

/// <summary>System-wide hotkeys that work while another app has focus or the window is in the tray.</summary>
public sealed class GlobalHotkeys : IDisposable
{
    private readonly Dictionary<int, Action> _actions = [];
    private HwndSource? _source;

    /// <summary>Hotkey messages are delivered to this window, so it must live as long as the hotkeys.</summary>
    public void Attach(Window window)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        _source.AddHook(WndProc);
    }

    /// <returns>False when another app already owns the combination.</returns>
    public bool Register(ModifierKeys modifiers, Key key, Action action)
    {
        if (_source is null)
            throw new InvalidOperationException("Attach a window first.");
        var id = _actions.Count + 1;
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, (uint)modifiers | NativeMethods.ModNoRepeat, (uint)KeyInterop.VirtualKeyFromKey(key)))
            return false;
        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        // On shutdown the window may already be destroyed, which releases its hotkeys anyway.
        if (_source is { IsDisposed: false } source)
        {
            foreach (var id in _actions.Keys)
                NativeMethods.UnregisterHotKey(source.Handle, id);
            source.RemoveHook(WndProc);
        }
        _actions.Clear();
        _source = null;
    }
}
