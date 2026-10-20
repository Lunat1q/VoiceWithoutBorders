using System.Net;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Features.Streaming;

/// <summary>Wires the chosen devices to the peer link and tears it all down again.</summary>
public interface IStreamingService
{
    bool IsRunning { get; }

    TransportStats Stats { get; }

    /// <summary>Restarts if already running. On failure everything opened so far is closed and the error rethrown.</summary>
    /// <returns>The peer address audio is sent to.</returns>
    Task<IPEndPoint> StartAsync(AppSettings settings, CancellationToken cancellationToken = default);

    void Stop();
}
