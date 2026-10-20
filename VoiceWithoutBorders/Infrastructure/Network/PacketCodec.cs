using System.Buffers.Binary;
using System.Security.Cryptography;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Infrastructure.Network;

/// <summary>
/// Frames and encrypts audio packets with AES-256-GCM.
/// Layout: version(1) | kind(1) | session(4) | sequence(4) | nonce(12) | tag(16) | ciphertext.
/// The first 10 bytes are authenticated as associated data.
/// </summary>
public sealed class PacketCodec : IDisposable
{
    public const byte Version = 1;
    public const int MinSharedKeyLength = 16;

    private const int HeaderSize = 10;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    public const int Overhead = HeaderSize + NonceSize + TagSize;

    // ponytail: fixed salt, because both machines must derive the same key from the shared key alone.
    // Upgrade path: a handshake that agrees on per-session keys (also closes cross-session replay).
    private static readonly byte[] Salt = "VoiceWithoutBorders/v1"u8.ToArray();
    private const int Pbkdf2Iterations = 210_000;

    private static readonly int KindCount = Enum.GetValues<StreamKind>().Length;

    private readonly AesGcm _aes;
    private readonly Lock _sendLock = new();
    private readonly uint _sessionId = BinaryPrimitives.ReadUInt32BigEndian(RandomNumberGenerator.GetBytes(4));
    private readonly uint[] _sendSequence = new uint[KindCount];
    private readonly (uint Session, uint Sequence)?[] _lastReceived = new (uint, uint)?[KindCount];

    public PacketCodec(byte[] key) => _aes = new AesGcm(key, TagSize);

    /// <summary>Slow on purpose (PBKDF2); call off the UI thread.</summary>
    public static PacketCodec FromSharedKey(string sharedKey) =>
        new(Rfc2898DeriveBytes.Pbkdf2(sharedKey, Salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32));

    /// <summary>Thread-safe: both capture threads send through the same codec.</summary>
    public byte[] Encode(StreamKind kind, ReadOnlySpan<byte> payload)
    {
        var packet = new byte[Overhead + payload.Length];
        var span = packet.AsSpan();
        span[0] = Version;
        span[1] = (byte)kind;
        BinaryPrimitives.WriteUInt32BigEndian(span[2..], _sessionId);
        // ponytail: random 96-bit nonces are safe for ~2^32 packets per key, about 248 days of continuous streaming.
        RandomNumberGenerator.Fill(span.Slice(HeaderSize, NonceSize));

        lock (_sendLock)
        {
            BinaryPrimitives.WriteUInt32BigEndian(span[6..], ++_sendSequence[(int)kind]);
            _aes.Encrypt(span.Slice(HeaderSize, NonceSize), payload, span[Overhead..], span.Slice(HeaderSize + NonceSize, TagSize), span[..HeaderSize]);
        }
        return packet;
    }

    /// <summary>
    /// Returns false for anything that is not a fresh, authentic packet from the peer: wrong key, tampered,
    /// truncated, duplicate, late, or one of our own packets reflected back. Single-threaded (receive loop).
    /// </summary>
    public bool TryDecode(ReadOnlySpan<byte> packet, out StreamKind kind, out byte[] payload)
    {
        kind = default;
        payload = [];
        if (packet.Length < Overhead || packet[0] != Version || packet[1] >= KindCount)
            return false;

        var session = BinaryPrimitives.ReadUInt32BigEndian(packet[2..]);
        var sequence = BinaryPrimitives.ReadUInt32BigEndian(packet[6..]);
        var index = packet[1];
        if (session == _sessionId)
            return false;
        // ponytail: a new session id resets the window, so a recording of an older session can be replayed.
        if (_lastReceived[index] is { } last && last.Session == session && sequence <= last.Sequence)
            return false;

        var plaintext = new byte[packet.Length - Overhead];
        try
        {
            _aes.Decrypt(packet.Slice(HeaderSize, NonceSize), packet[Overhead..], packet.Slice(HeaderSize + NonceSize, TagSize), plaintext, packet[..HeaderSize]);
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }

        _lastReceived[index] = (session, sequence);
        kind = (StreamKind)index;
        payload = plaintext;
        return true;
    }

    public void Dispose() => _aes.Dispose();
}
