using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Main;

// Link health while streaming: is the peer answering, is audio moving, how late does it play.
public sealed partial class MainViewModel
{
    /// <summary>How many one-second samples the skip warning stays up after audio was skipped.</summary>
    private const int SkipWarningSeconds = 10;

    private readonly LinkMonitor _link = new();
    private int _lastSkips;
    private int _skipWarningLeft;

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
        if (!IsRunning)
            return;
        var stats = _streaming.Stats;
        var link = _link.Update(stats);
        Connection = link.Connection;
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

    private void ResetLink()
    {
        _link.Reset();
        _lastSkips = 0;
        _skipWarningLeft = 0;
        Connection = PeerConnection.Waiting;
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
