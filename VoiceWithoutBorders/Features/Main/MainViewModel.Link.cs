using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Main;

// Link health while streaming: is the peer answering, is audio moving, how late does it play.
public sealed partial class MainViewModel
{
    /// <summary>How many one-second samples the skip warning stays up after audio was skipped.</summary>
    private const int SkipWarningSeconds = 10;

    /// <summary>
    /// One-second samples without the peer before a lost link is announced. Covers the first samples after a restart,
    /// when the reopened transport has not heard the peer yet.
    /// </summary>
    private const int LostNoticeSamples = 3;

    private readonly LinkMonitor _link = new();
    private int _lastSkips;
    private int _skipWarningLeft;

    // After the peer went quiet, or this computer slept, devices may have been reset and the peer's address changed.
    // Streaming reopens everything once the peer answers again, rather than playing into a dead device.
    private bool _wasConnected;
    private bool _restartWhenConnected;
    private bool _restarting;

    // What the last notice said, so each change is announced once. Kept across restarts, cleared by Stop.
    private bool _announcedConnected;
    private bool _announcedBefore;
    private int _samplesWithoutPeer;

    /// <summary>Raised when the link comes up, drops or comes back; the tray shows it when the window is not in use.</summary>
    public event Action<string>? LinkNotice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    private PeerConnection _connection;

    /// <summary>Audio left / arrived during the last second.</summary>
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _isReceiving;

    [ObservableProperty] private string _latencyText = "";
    [ObservableProperty] private LatencyHealth _latencyHealth;

    /// <summary>The jitter buffer recently dropped audio to catch up: the network is uneven.</summary>
    [ObservableProperty] private bool _audioSkipping;

    public string ConnectionText => Connection switch
    {
        PeerConnection.Connected => "Connected: the other computer is answering.",
        PeerConnection.KeyMismatch => $"{PeerHost} answers, but with a different shared key (or app version).",
        _ => $"Waiting for {PeerHost} to answer. Start the app there with the same port and key, and allow it through Windows Firewall.",
    };

    /// <summary>Called once a second by the view.</summary>
    public void UpdateLinkStatus()
    {
        if (!IsRunning || _restarting)
            return;
        if (_streaming.DeviceFailed)
        {
            _ = RestartAsync();
            return;
        }
        var stats = _streaming.Stats;
        var link = _link.Update(stats);
        Connection = Overlay.Connection = link.Connection;
        Announce(link.Connection);
        if (link.Connection != PeerConnection.Connected)
            _restartWhenConnected |= _wasConnected;
        else if (_restartWhenConnected)
        {
            _ = RestartAsync();
            return;
        }
        else
            _wasConnected = true;
        IsSending = link.Sending;
        IsReceiving = link.Receiving;

        var streams = _streaming.GetLatency();
        LatencyText = DescribeLatency(stats.RoundTrip, streams);
        LatencyHealth = streams.Count == 0
            ? (stats.RoundTrip is null ? LatencyHealth.Unknown : LatencyHealth.Good)
            : streams.Max(s => s.Health);

        var skips = streams.Sum(s => s.Skips);
        if (skips > _lastSkips)
            _skipWarningLeft = SkipWarningSeconds;
        else if (_skipWarningLeft > 0)
            _skipWarningLeft--;
        _lastSkips = skips;
        AudioSkipping = _skipWarningLeft > 0;
    }

    /// <summary>Called when Windows resumes from sleep or hibernation.</summary>
    public void OnSystemResumed() => _restartWhenConnected |= IsRunning;

    /// <summary>Waits between restart attempts; tests shorten it.</summary>
    public IReadOnlyList<TimeSpan> RestartRetryDelays { get; set; } = AutoConnectRetryDelays;

    private void Announce(PeerConnection connection)
    {
        if (connection == PeerConnection.Connected)
        {
            _samplesWithoutPeer = 0;
            if (_announcedConnected)
                return;
            _announcedConnected = true;
            Notify(NotifyConnected, _announcedBefore ? $"Connected to {PeerHost} again." : $"Connected to {PeerHost}.");
            _announcedBefore = true;
        }
        else if (_announcedConnected && ++_samplesWithoutPeer == LostNoticeSamples)
        {
            _announcedConnected = false;
            if (connection == PeerConnection.KeyMismatch)
                Notify(NotifyKeyMismatch, $"{PeerHost} answers with a different shared key.");
            else
                Notify(NotifyLost, $"Lost the connection to {PeerHost}. Waiting for it to answer again…");
        }
    }

    private void ForgetAnnouncements()
    {
        _announcedConnected = _announcedBefore = false;
        _samplesWithoutPeer = 0;
    }

    // Devices are often still resetting right after resume ("audio device has been disconnected"), so a failed
    // reopen retries rather than leaving streaming down.
    // ponytail: retries on the auto-connect schedule (about a minute), then stops with the reason in Status.
    // A device plugged back in later needs Start by hand; watch device arrivals (MMNotificationClient) if that annoys.
    private async Task RestartAsync()
    {
        _restarting = true;
        try
        {
            _streaming.Stop();
            IsRunning = false;
            await ConnectWithRetriesAsync(RestartRetryDelays);
        }
        finally
        {
            _restarting = false;
        }
    }

    private void ResetLink()
    {
        _link.Reset();
        _wasConnected = _restartWhenConnected = false;
        _lastSkips = 0;
        _skipWarningLeft = 0;
        Connection = PeerConnection.Waiting;
        Overlay.Connection = PeerConnection.Connected; // no warning until the first sample, nor on the stopped preview
        IsSending = IsReceiving = AudioSkipping = false;
        LatencyText = "Measuring latency…";
        LatencyHealth = LatencyHealth.Unknown;
    }

    private static string DescribeLatency(TimeSpan? roundTrip, IReadOnlyList<StreamLatency> streams)
    {
        if (roundTrip is not { } rtt)
            return "Measuring latency…";
        var parts = new List<string> { $"Network round trip {Ms(rtt)}" };
        foreach (var stream in streams)
        {
            var what = stream.Kind == StreamKind.Microphone ? "their mic" : "their sound";
            parts.Add($"{what} → here ≈ {Ms(stream.EndToEnd!.Value)}");
        }
        return string.Join(" · ", parts);
    }

    private static string Ms(TimeSpan time) => $"{Math.Round(time.TotalMilliseconds):0} ms";
}
