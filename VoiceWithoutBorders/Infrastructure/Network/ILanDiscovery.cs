namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// Finds this app on other computers of the LAN that use the same port and shared key. While not streaming it holds
/// the configured port, answers authentic discovery requests, and sends its own searches from there.
/// </summary>
public interface ILanDiscovery : IDisposable
{
    /// <summary>False when the port could not be taken (another program has it).</summary>
    bool IsListening { get; }

    /// <summary>Takes the port (rebinding only when it changed) and answers requests made with this key.</summary>
    void Listen(int port, string sharedKey);

    /// <summary>Releases the port right away, so streaming can bind it.</summary>
    void StopListening();

    /// <summary>Broadcasts requests for a couple of seconds and returns everyone who answered. Needs <see cref="IsListening"/>.</summary>
    Task<IReadOnlyList<DiscoveredPeer>> SearchAsync(CancellationToken cancellationToken = default);
}
