using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Infrastructure.Network;

public readonly record struct TransportStats(long Sent, long Received, long Rejected);

/// <summary>Encrypted, connectionless audio link to one peer machine.</summary>
public interface IPeerTransport : IDisposable
{
    /// <summary>Raised on the receive thread for every authentic packet.</summary>
    event Action<StreamKind, ArraySegment<byte>>? AudioReceived;

    TransportStats Stats { get; }

    /// <summary>Resolves the peer, derives the key and starts listening on <paramref name="port"/>.</summary>
    Task OpenAsync(string peerHost, int port, string sharedKey, CancellationToken cancellationToken = default);

    void Send(StreamKind kind, ArraySegment<byte> pcm);

    void Close();
}
