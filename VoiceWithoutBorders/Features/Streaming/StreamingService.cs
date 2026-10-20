using System.Net;
using Microsoft.Extensions.Logging;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Network;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Streaming;

public sealed class StreamingService(
    IPeerTransport transport,
    IAudioEndpointFactory endpoints,
    ILogger<StreamingService> logger) : IStreamingService, IDisposable
{
    private readonly List<IDisposable> _opened = [];

    // Replaced as a whole, never mutated, because the receive thread reads it while the UI thread starts/stops.
    private IReadOnlyDictionary<StreamKind, IAudioPlayer> _players = new Dictionary<StreamKind, IAudioPlayer>();

    public bool IsRunning { get; private set; }

    public TransportStats Stats => transport.Stats;

    public async Task<IPEndPoint> StartAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Stop();
        try
        {
            var peer = await transport.OpenAsync(settings.PeerHost, settings.Port, settings.SharedKey, cancellationToken);

            var maxLatency = TimeSpan.FromMilliseconds(settings.MaxLatencyMs);
            var players = new Dictionary<StreamKind, IAudioPlayer>();
            AddPlayer(players, StreamKind.Microphone, settings.MicPlaybackDeviceId, maxLatency);
            AddPlayer(players, StreamKind.SystemAudio, settings.SystemPlaybackDeviceId, maxLatency);
            _players = players;
            transport.AudioReceived += OnAudioReceived;

            StartCapture(StreamKind.Microphone, settings.MicCaptureDeviceId, loopback: false);
            StartCapture(StreamKind.SystemAudio, settings.SystemCaptureDeviceId, loopback: true);
            IsRunning = true;
            return peer;
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        transport.AudioReceived -= OnAudioReceived;
        // Reverse order: captures stop feeding the transport before anything else goes away.
        for (var i = _opened.Count - 1; i >= 0; i--)
        {
            try
            {
                _opened[i].Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Closing an audio device failed");
            }
        }
        _opened.Clear();
        _players = new Dictionary<StreamKind, IAudioPlayer>();
        transport.Close();
        IsRunning = false;
    }

    private void AddPlayer(Dictionary<StreamKind, IAudioPlayer> players, StreamKind kind, string? deviceId, TimeSpan maxLatency)
    {
        if (string.IsNullOrEmpty(deviceId))
            return;
        var player = endpoints.CreatePlayer(deviceId, maxLatency);
        _opened.Add(player);
        players[kind] = player;
    }

    private void StartCapture(StreamKind kind, string? deviceId, bool loopback)
    {
        if (string.IsNullOrEmpty(deviceId))
            return;
        var capture = endpoints.CreateCapture(deviceId, loopback);
        _opened.Add(capture);
        capture.ChunkReady += chunk => transport.Send(kind, chunk);
        capture.Start();
    }

    private void OnAudioReceived(StreamKind kind, ArraySegment<byte> pcm)
    {
        if (_players.TryGetValue(kind, out var player))
            player.Write(pcm);
    }

    public void Dispose() => Stop();
}
