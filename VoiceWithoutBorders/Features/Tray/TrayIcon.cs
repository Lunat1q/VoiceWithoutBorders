using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VoiceWithoutBorders.Features.Main;

namespace VoiceWithoutBorders.Features.Tray;

/// <summary>
/// Notification-area icon: double-click restores the window, right-click offers the everyday actions.
/// Menu state is refreshed each time it opens, so it never drifts from the view model.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int MaxTooltipLength = 63;

    private readonly NotifyIcon _icon;
    private readonly MainViewModel _vm;

    public TrayIcon(MainViewModel vm, MainWindow window)
    {
        _vm = vm;
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

        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => window.Restore();
        vm.PropertyChanged += OnPropertyChanged;
        UpdateTooltip();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.MicEnabled) or nameof(MainViewModel.SoundEnabled))
            UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var text = _vm.IsRunning
            ? $"Voice Without Borders: streaming (mic {OnOff(_vm.MicEnabled)}, sound {OnOff(_vm.SoundEnabled)})"
            : "Voice Without Borders: stopped";
        _icon.Text = text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    public void Dispose()
    {
        _vm.PropertyChanged -= OnPropertyChanged;
        _icon.Visible = false; // otherwise a ghost icon stays until the mouse passes over it
        _icon.Dispose();
    }
}
