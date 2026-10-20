using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Main;

// Things that work while streaming: stream toggles, the overlay and the global shortcuts.
public sealed partial class MainViewModel
{
    [ObservableProperty] private bool _micEnabled = true;
    [ObservableProperty] private bool _soundEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverlayVisible))]
    private bool _showOverlay = true;

    /// <summary>While unlocked the overlay is shown (even when stopped), takes the mouse and can be dragged.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverlayVisible))]
    private bool _overlayUnlocked;

    [ObservableProperty] private double? _overlayLeft;
    [ObservableProperty] private double? _overlayTop;

    [ObservableProperty] private HotkeyBinding? _toggleMicHotkey;
    [ObservableProperty] private HotkeyBinding? _toggleSoundHotkey;
    [ObservableProperty] private HotkeyBinding? _startStopHotkey;

    /// <summary>Set by the hotkey controller: which shortcuts work, or why one does not.</summary>
    [ObservableProperty] private string _hotkeyStatus = "";

    public OverlayViewModel Overlay { get; } = new();

    public bool OverlayVisible => (IsRunning && ShowOverlay) || OverlayUnlocked;

    partial void OnMicEnabledChanged(bool value) => _streaming.SetEnabled(StreamKind.Microphone, value);

    partial void OnSoundEnabledChanged(bool value) => _streaming.SetEnabled(StreamKind.SystemAudio, value);

    partial void OnIsRunningChanged(bool value)
    {
        if (!value && OverlayUnlocked)
            Overlay.Configure(ToSettings()); // keep a preview to drag around
    }

    partial void OnOverlayUnlockedChanged(bool value)
    {
        Overlay.IsUnlocked = value;
        if (value && !IsRunning)
            Overlay.Configure(ToSettings());
    }

    /// <summary>Called by the overlay on every animation frame.</summary>
    public void TickOverlay(double elapsedSeconds) =>
        Overlay.Tick(_streaming.ReadLevels(), MicEnabled, SoundEnabled, elapsedSeconds);

    /// <summary>Called once a drag of the unlocked overlay ends.</summary>
    public void MoveOverlay(double left, double top)
    {
        OverlayLeft = left;
        OverlayTop = top;
    }

    [RelayCommand]
    private void ResetOverlayPosition()
    {
        OverlayLeft = null;
        OverlayTop = null;
    }

    [RelayCommand]
    private async Task ToggleStreamingAsync()
    {
        if (IsRunning)
            Stop();
        else if (StartCommand.CanExecute(null))
            await StartCommand.ExecuteAsync(null);
    }

    private void LoadLiveControls(AppSettings settings)
    {
        MicEnabled = settings.MicEnabled;
        SoundEnabled = settings.SoundEnabled;
        ShowOverlay = settings.ShowOverlay;
        OverlayLeft = settings.OverlayLeft;
        OverlayTop = settings.OverlayTop;
        ToggleMicHotkey = settings.ToggleMicHotkey;
        ToggleSoundHotkey = settings.ToggleSoundHotkey;
        StartStopHotkey = settings.StartStopHotkey;
    }
}
