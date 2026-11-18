using System.Net;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Infrastructure.Network;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Streaming;

/// <summary>Loudest level (0 to 1) of each stream since the previous read.</summary>
public readonly record struct StreamLevels(float MicSent, float SoundSent, float MicReceived, float SoundReceived);

/// <summary>Wires the chosen devices to the peer link and tears it all down again.</summary>
public interface IStreamingService
{
    bool IsRunning { get; }

    TransportStats Stats { get; }

    /// <summary>Restarts if already running. On failure everything opened so far is closed and the error rethrown.</summary>
    /// <returns>The peer address audio is sent to.</returns>
    Task<IPEndPoint> StartAsync(AppSettings settings, CancellationToken cancellationToken = default);

    void Stop();

    /// <summary>A disabled stream is neither sent nor played; takes effect immediately.</summary>
    void SetEnabled(StreamKind kind, bool enabled);

    /// <summary>Gain for what the peer sends on this stream, played here (1 = as sent); takes effect immediately.</summary>
    void SetVolume(StreamKind kind, double gain);

    /// <summary>Mic chunks quieter than this many dB below full scale are sent as silence; null turns the gate off.</summary>
    void SetNoiseGate(double? thresholdDb);

    /// <summary>Returns and resets the peak levels, for activity meters.</summary>
    StreamLevels ReadLevels();

    /// <summary>Latency estimate for every stream this computer plays, ordered microphone first.</summary>
    IReadOnlyList<StreamLatency> GetLatency();
}
