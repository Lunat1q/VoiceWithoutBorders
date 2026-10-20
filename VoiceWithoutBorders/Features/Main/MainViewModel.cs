using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;

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
    [ObservableProperty] private AudioDevice? _micPlayback = Off;
    [ObservableProperty] private AudioDevice? _systemPlayback = Off;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand), nameof(RefreshDevicesCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _status = "Stopped";
    [ObservableProperty] private string _stats = "";

    public MainViewModel(IStreamingService streaming, ISettingsStore store, IAudioDeviceService devices)
    {
        _streaming = streaming;
        _store = store;
        _devices = devices;

        var settings = store.Load();
        PeerHost = settings.PeerHost;
        Port = settings.Port;
        SharedKey = settings.SharedKey;
        MaxLatencyMs = settings.MaxLatencyMs;
        LoadDevices(settings);
    }

    public ObservableCollection<AudioDevice> InputDevices { get; } = [];

    public ObservableCollection<AudioDevice> OutputDevices { get; } = [];

    public bool IsIdle => !IsRunning;

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
            _store.Save(settings);
            await _streaming.StartAsync(settings);
            IsRunning = true;
            Status = $"Streaming with {settings.PeerHost} on UDP port {settings.Port}.";
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
        Stats = "";
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void RefreshDevices() => LoadDevices(ToSettings());

    /// <summary>Called once a second by the view.</summary>
    public void UpdateStats()
    {
        if (!IsRunning)
            return;
        var stats = _streaming.Stats;
        Stats = $"Packets sent {stats.Sent:N0} · received {stats.Received:N0} · rejected {stats.Rejected:N0}"
            + (stats.Rejected > 0 && stats.Received == 0 ? " (do both computers use the same shared key?)" : "");
    }

    private void LoadDevices(AppSettings selection)
    {
        Fill(InputDevices, _devices.GetInputDevices());
        Fill(OutputDevices, _devices.GetOutputDevices());
        MicCapture = Find(InputDevices, selection.MicCaptureDeviceId);
        SystemCapture = Find(OutputDevices, selection.SystemCaptureDeviceId);
        MicPlayback = Find(OutputDevices, selection.MicPlaybackDeviceId);
        SystemPlayback = Find(OutputDevices, selection.SystemPlaybackDeviceId);
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
