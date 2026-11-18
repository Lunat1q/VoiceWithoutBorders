using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Interop;

namespace VoiceWithoutBorders.Features.Tray;

/// <summary>
/// Notification-area icon: double-click restores the window, right-click offers the everyday actions.
/// Menu state is refreshed each time it opens, so it never drifts from the view model. While streaming, a dot on the
/// icon shows the link (green connected, amber waiting, red key mismatch), and link changes pop up as notifications
/// when the window is not in use.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int MaxTooltipLength = 63;

    private readonly NotifyIcon _icon;
    private readonly MainViewModel _vm;
    private readonly MainWindow _window;
    private readonly Icon _stopped;
    private readonly Icon _connected;
    private readonly Icon _waiting;
    private readonly Icon _keyMismatch;

    public TrayIcon(MainViewModel vm, MainWindow window)
    {
        _vm = vm;
        _window = window;
        using (var appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!)
        {
            _stopped = new Icon(appIcon, SystemInformation.SmallIconSize);
            _connected = WithDot(_stopped, Color.FromArgb(0x2E, 0xA0, 0x43));
            _waiting = WithDot(_stopped, Color.FromArgb(0xFF, 0xA0, 0x00));
            _keyMismatch = WithDot(_stopped, Color.FromArgb(0xE5, 0x53, 0x4B));
        }

        var menu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
        var open = new ToolStripMenuItem("Open Voice Without Borders", null, (_, _) => window.Restore()) { Font = new Font(menu.Font, FontStyle.Bold) };
        var startStop = new ToolStripMenuItem("Start", null, async (_, _) => await vm.ToggleStreamingCommand.ExecuteAsync(null));
        var mic = new ToolStripMenuItem("Microphone on", null, (_, _) => vm.MicEnabled = !vm.MicEnabled);
        var sound = new ToolStripMenuItem("Sound on", null, (_, _) => vm.SoundEnabled = !vm.SoundEnabled);
        var overlay = new ToolStripMenuItem("Show overlay while streaming", null, (_, _) => vm.ShowOverlay = !vm.ShowOverlay);
        var unlock = new ToolStripMenuItem("Unlock overlay to move it", null, (_, _) => vm.OverlayUnlocked = !vm.OverlayUnlocked);
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        menu.Items.AddRange([open, startStop, new ToolStripSeparator(), mic, sound, new ToolStripSeparator(), overlay, unlock, new ToolStripSeparator(), exit]);

        menu.Opening += (_, _) =>
        {
            startStop.Text = vm.IsRunning ? "Stop" : "Start";
            startStop.ShortcutKeyDisplayString = vm.StartStopHotkey?.ToString();
            mic.Checked = vm.MicEnabled;
            mic.ShortcutKeyDisplayString = vm.ToggleMicHotkey?.ToString();
            sound.Checked = vm.SoundEnabled;
            sound.ShortcutKeyDisplayString = vm.ToggleSoundHotkey?.ToString();
            overlay.Checked = vm.ShowOverlay;
            unlock.Checked = vm.OverlayUnlocked;
        };
        // The taskbar is topmost too and, activated by the right-click, can end up above the menu. Re-raise the menu
        // to the top of the topmost band once it is shown, so every item (Exit included) is clickable.
        menu.Opened += (_, _) => NativeMethods.SetWindowPos(menu.Handle, NativeMethods.HwndTopmost, 0, 0, 0, 0, NativeMethods.SwpNoSizeMoveActivate);

        _icon = new NotifyIcon
        {
            Icon = _stopped,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => window.Restore();
        vm.PropertyChanged += OnPropertyChanged;
        vm.LinkNotice += OnLinkNotice;
        UpdateState();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.Connection)
            or nameof(MainViewModel.MicEnabled) or nameof(MainViewModel.SoundEnabled))
            UpdateState();
    }

    // Someone working in the window sees the status there; a pop-up on top would only get in the way.
    private void OnLinkNotice(string text)
    {
        if (!_window.IsActive)
            _icon.ShowBalloonTip(5000, "Voice Without Borders", text, ToolTipIcon.None);
    }

    private void UpdateState()
    {
        _icon.Icon = !_vm.IsRunning ? _stopped : _vm.Connection switch
        {
            PeerConnection.Connected => _connected,
            PeerConnection.KeyMismatch => _keyMismatch,
            _ => _waiting,
        };
        var text = Tooltip(_vm.IsRunning, _vm.Connection, _vm.MicEnabled, _vm.SoundEnabled);
        _icon.Text = text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;
    }

    /// <summary>Kept under the 63-character tooltip limit for the usual cases.</summary>
    public static string Tooltip(bool running, PeerConnection connection, bool micEnabled, bool soundEnabled)
    {
        if (!running)
            return "Voice Without Borders: stopped";
        var link = connection switch
        {
            PeerConnection.Connected => "connected",
            PeerConnection.KeyMismatch => "key mismatch",
            _ => "waiting",
        };
        return $"Voice Without Borders: {link}, mic {OnOff(micEnabled)}, sound {OnOff(soundEnabled)}";
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    /// <summary>The app icon with a status dot in its bottom-right corner.</summary>
    private static Icon WithDot(Icon icon, Color color)
    {
        using var bitmap = icon.ToBitmap();
        using (var graphics = Graphics.FromImage(bitmap))
        using (var fill = new SolidBrush(color))
        using (var outline = new Pen(Color.FromArgb(0xC0, 0, 0, 0)))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var size = bitmap.Width / 2;
            var dot = new Rectangle(bitmap.Width - size - 1, bitmap.Height - size - 1, size, size);
            graphics.FillEllipse(fill, dot);
            graphics.DrawEllipse(outline, dot);
        }
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone(); // owns its own handle, so the borrowed one can go
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _vm.PropertyChanged -= OnPropertyChanged;
        _vm.LinkNotice -= OnLinkNotice;
        _icon.Visible = false; // otherwise a ghost icon stays until the mouse passes over it
        _icon.Dispose();
        foreach (var icon in new[] { _stopped, _connected, _waiting, _keyMismatch })
            icon.Dispose();
    }
}
