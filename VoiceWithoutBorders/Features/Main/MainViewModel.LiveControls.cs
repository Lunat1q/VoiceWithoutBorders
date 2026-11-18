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

    [ObservableProperty] private bool _overlayVertical;
    [ObservableProperty] private double _overlayScale = 1;
    [ObservableProperty] private double _overlayOpacity = 0.85;
    [ObservableProperty] private bool _overlayHideWhenQuiet;

    [ObservableProperty] private HotkeyBinding? _toggleMicHotkey;
    [ObservableProperty] private HotkeyBinding? _toggleSoundHotkey;
    [ObservableProperty] private HotkeyBinding? _startStopHotkey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicOpen))]
    private HotkeyBinding? _pushToTalkHotkey;

    /// <summary>The push-to-talk shortcut is held right now. Set by the hotkey controller.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicOpen))]
    private bool _isTalking;

    /// <summary>This computer's mic may be sent: no push to talk, or its shortcut is held.</summary>
    public bool MicOpen => PushToTalkHotkey is null || IsTalking;

    /// <summary>Set by the hotkey controller: which shortcuts work, or why one does not.</summary>
    [ObservableProperty] private string _hotkeyStatus = "";

    public OverlayViewModel Overlay { get; } = new();

    public bool OverlayVisible => (IsRunning && ShowOverlay) || OverlayUnlocked;

    partial void OnMicEnabledChanged(bool value) => _streaming.SetEnabled(StreamKind.Microphone, value);

    partial void OnSoundEnabledChanged(bool value) => _streaming.SetEnabled(StreamKind.SystemAudio, value);

    partial void OnPushToTalkHotkeyChanged(HotkeyBinding? value) => _streaming.SetMicOpen(MicOpen);

    partial void OnIsTalkingChanged(bool value) => _streaming.SetMicOpen(MicOpen);

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

    partial void OnOverlayVerticalChanged(bool value) => Overlay.IsVertical = value;

    partial void OnOverlayScaleChanged(double value) => Overlay.Scale = value;

    partial void OnOverlayOpacityChanged(double value) => Overlay.Opacity = value;

    partial void OnOverlayHideWhenQuietChanged(bool value) => Overlay.HideWhenQuiet = value;

    /// <summary>Called by the overlay on every animation frame.</summary>
    public void TickOverlay(double elapsedSeconds) =>
        Overlay.Tick(_streaming.ReadLevels(), MicEnabled, SoundEnabled, elapsedSeconds, MicOpen);

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
        OverlayVertical = settings.OverlayVertical;
        // Clamped, not rejected: a hand-edited settings file must not keep streaming from starting.
        OverlayScale = Math.Clamp(settings.OverlayScale, AppSettings.MinOverlayScale, AppSettings.MaxOverlayScale);
        OverlayOpacity = Math.Clamp(settings.OverlayOpacity, 0, 1);
        OverlayHideWhenQuiet = settings.OverlayHideWhenQuiet;
        ToggleMicHotkey = settings.ToggleMicHotkey;
        ToggleSoundHotkey = settings.ToggleSoundHotkey;
        StartStopHotkey = settings.StartStopHotkey;
        PushToTalkHotkey = settings.PushToTalkHotkey;
    }
}
