using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Main;

public sealed partial class MainViewModel : ObservableObject
{
    public static readonly AudioDevice Off = new("", "Off");

    private readonly IStreamingService _streaming;
    private readonly ISettingsStore _store;
    private readonly IAudioDeviceService _devices;

    [ObservableProperty] private string _peerHost = "";
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _sharedKey = "";
    [ObservableProperty] private int _maxLatencyMs;

    // Nullable: a ComboBox writes null back while its item list is being refilled.
    [ObservableProperty] private AudioDevice? _micCapture = Off;
    [ObservableProperty] private AudioDevice? _systemCapture = Off;
    /// <summary>A virtual microphone (named as apps see it); its id is the cable's playback side.</summary>
    [ObservableProperty] private AudioDevice? _micPlayback = Off;
    [ObservableProperty] private AudioDevice? _systemPlayback = Off;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(OverlayVisible))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand), nameof(RefreshDevicesCommand), nameof(NewSharedKeyCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _status = "Stopped";

    private static readonly HashSet<string> PersistedProperties =
    [
        nameof(PeerHost), nameof(Port), nameof(SharedKey), nameof(MaxLatencyMs),
        nameof(MicCapture), nameof(SystemCapture), nameof(MicPlayback), nameof(SystemPlayback),
        nameof(MicEnabled), nameof(SoundEnabled), nameof(ShowOverlay), nameof(OverlayLeft), nameof(OverlayTop),
        nameof(ToggleMicHotkey), nameof(ToggleSoundHotkey), nameof(StartStopHotkey),
    ];

    // True while settings or device lists are being (re)loaded, so those writes are not saved back.
    private bool _loading = true;

    public MainViewModel(IStreamingService streaming, ISettingsStore store, IAudioDeviceService devices)
    {
        _streaming = streaming;
        _store = store;
        _devices = devices;

        var settings = store.Load();
        if (settings.SharedKey.Length == 0)
        {
            // Saved straight away so the key survives a restart while the user types it on the other computer.
            settings = settings with { SharedKey = SharedKeyGenerator.Generate() };
            store.Save(settings);
        }
        PeerHost = settings.PeerHost;
        Port = settings.Port;
        SharedKey = settings.SharedKey;
        MaxLatencyMs = settings.MaxLatencyMs;
        LoadLiveControls(settings);
        LoadDevices(settings);
        _loading = false;
    }

    /// <summary>Every edit is saved immediately, so nothing is lost when the app closes without Start.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading || e.PropertyName is not { } name || !PersistedProperties.Contains(name))
            return;
        try
        {
            _store.Save(ToSettings());
        }
        catch (Exception ex)
        {
            Status = $"Could not save settings: {ex.Message}";
        }
    }

    public ObservableCollection<AudioDevice> InputDevices { get; } = [];

    public ObservableCollection<AudioDevice> OutputDevices { get; } = [];

    public ObservableCollection<AudioDevice> VirtualMicrophones { get; } = [];

    public bool IsIdle => !IsRunning;

    /// <summary>No virtual cable installed, so the other computer's mic cannot show up as a microphone here.</summary>
    public bool NoVirtualMicrophone => VirtualMicrophones.Count <= 1;

    /// <summary>Nothing here can play audio (no sound card, no virtual cable), so receiving is impossible.</summary>
    public bool NoOutputDevices => OutputDevices.Count <= 1;

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task StartAsync()
    {
        var settings = ToSettings();
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            Status = string.Join(Environment.NewLine, errors);
            return;
        }

        Status = $"Connecting to {settings.PeerHost}…";
        try
        {
            var peer = await _streaming.StartAsync(settings);
            Overlay.Configure(settings);
            ResetLink();
            IsRunning = true;
            var address = peer.Address.ToString();
            var target = settings.PeerHost == address ? address : $"{settings.PeerHost} ({address})";
            Status = $"Streaming with {target} on UDP port {settings.Port}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not start: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop()
    {
        _streaming.Stop();
        IsRunning = false;
        Status = "Stopped";
        ResetLink();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void RefreshDevices() => LoadDevices(ToSettings());

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void NewSharedKey() => SharedKey = SharedKeyGenerator.Generate();

    private void LoadDevices(AppSettings selection)
    {
        // Refilling the lists makes bound ComboBoxes write null back; saving that would wipe the stored devices.
        var wasLoading = _loading;
        _loading = true;
        try
        {
            Fill(InputDevices, _devices.GetInputDevices());
            Fill(OutputDevices, _devices.GetOutputDevices());
            Fill(VirtualMicrophones, _devices.GetVirtualMicrophones());
            MicCapture = Find(InputDevices, selection.MicCaptureDeviceId);
            SystemCapture = Find(OutputDevices, selection.SystemCaptureDeviceId);
            MicPlayback = Find(VirtualMicrophones, selection.MicPlaybackDeviceId);
            SystemPlayback = Find(OutputDevices, selection.SystemPlaybackDeviceId);
            OnPropertyChanged(nameof(NoOutputDevices));
            OnPropertyChanged(nameof(NoVirtualMicrophone));
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private AppSettings ToSettings() => new()
    {
        PeerHost = PeerHost.Trim(),
        Port = Port,
        SharedKey = SharedKey,
        MaxLatencyMs = MaxLatencyMs,
        MicCaptureDeviceId = IdOf(MicCapture),
        SystemCaptureDeviceId = IdOf(SystemCapture),
        MicPlaybackDeviceId = IdOf(MicPlayback),
        SystemPlaybackDeviceId = IdOf(SystemPlayback),
        MicEnabled = MicEnabled,
        SoundEnabled = SoundEnabled,
        ShowOverlay = ShowOverlay,
        OverlayLeft = OverlayLeft,
        OverlayTop = OverlayTop,
        ToggleMicHotkey = ToggleMicHotkey,
        ToggleSoundHotkey = ToggleSoundHotkey,
        StartStopHotkey = StartStopHotkey,
    };

    private static void Fill(ObservableCollection<AudioDevice> target, IEnumerable<AudioDevice> devices)
    {
        target.Clear();
        target.Add(Off);
        foreach (var device in devices)
            target.Add(device);
    }

    private static AudioDevice Find(IEnumerable<AudioDevice> devices, string? id) =>
        devices.FirstOrDefault(device => device.Id == id) ?? Off;

    private static string? IdOf(AudioDevice? device) => string.IsNullOrEmpty(device?.Id) ? null : device.Id;
}
