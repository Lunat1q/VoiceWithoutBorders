using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace VoiceWithoutBorders.Infrastructure.Interop;

public sealed class GlobalHotkeys : IGlobalHotkeys
{
    // How often a held shortcut's key is checked; also the most a release can lag behind.
    private static readonly TimeSpan HoldPollInterval = TimeSpan.FromMilliseconds(15);

    private readonly Dictionary<int, Action> _actions = [];
    private HwndSource? _source;
    private DispatcherTimer? _holdTimer;
    private Action? _release;

    public void Attach(Window window)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        _source.AddHook(WndProc);
    }

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

    /// <summary>
    /// RegisterHotKey only reports presses, so while held the key itself is polled until it goes up.
    /// No keyboard hook: nothing watches other keys, and games that dislike hooks are not bothered.
    /// </summary>
    public bool RegisterHold(ModifierKeys modifiers, Key key, Action pressed, Action released)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return Register(modifiers, key, () =>
        {
            if (_release is not null)
                return;
            _release = released;
            pressed();
            // This constructor starts the timer.
            _holdTimer = new DispatcherTimer(HoldPollInterval, DispatcherPriority.Input, (_, _) => PollHeld(virtualKey), Dispatcher.CurrentDispatcher);
        });
    }

    private void PollHeld(int virtualKey)
    {
        if ((NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) == 0)
            EndHold();
    }

    private void EndHold()
    {
        _holdTimer?.Stop();
        _holdTimer = null;
        var release = _release;
        _release = null;
        release?.Invoke();
    }

    public void UnregisterAll()
    {
        EndHold();
        // On shutdown the window may already be destroyed, which releases its hotkeys anyway.
        if (_source is { IsDisposed: false } source)
        {
            foreach (var id in _actions.Keys)
                NativeMethods.UnregisterHotKey(source.Handle, id);
        }
        _actions.Clear();
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
        UnregisterAll();
        if (_source is { IsDisposed: false } source)
            source.RemoveHook(WndProc);
        _source = null;
    }
}
