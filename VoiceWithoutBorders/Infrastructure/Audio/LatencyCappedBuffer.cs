using NAudio.Wave;

namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>
/// Jitter buffer between the network and the sound card. Underruns play silence; overruns drop audio
/// so latency never grows past the configured cap, and audio that sits unused for a second is trimmed.
/// </summary>
public sealed class LatencyCappedBuffer : IWaveProvider
{
    /// <summary>Writes per trim window: one second of 5 ms chunks.</summary>
    private const int TrimWindowWrites = 200;

    /// <summary>Audio kept above the lowest level seen in a window, to ride out the next one's jitter.</summary>
    private const int SpareMilliseconds = 15;

    private readonly BufferedWaveProvider _buffer;
    private readonly int _maxBytes;
    private readonly int _targetBytes;
    private readonly int _spareBytes;
    private readonly byte[] _scratch;
    private int _windowWrites;
    private int _windowMinBytes = int.MaxValue;

    public LatencyCappedBuffer(WaveFormat format, TimeSpan maxLatency)
    {
        _buffer = new BufferedWaveProvider(format, maxLatency + TimeSpan.FromSeconds(1)) { DiscardOnBufferOverflow = true };
        _maxBytes = format.ConvertLatencyToByteSize((int)maxLatency.TotalMilliseconds);
        _targetBytes = format.ConvertLatencyToByteSize((int)maxLatency.TotalMilliseconds / 2);
        _spareBytes = format.ConvertLatencyToByteSize(SpareMilliseconds);
        _scratch = new byte[_maxBytes];
    }

    public WaveFormat WaveFormat => _buffer.WaveFormat;

    public TimeSpan BufferedDuration => _buffer.BufferedDuration;

    /// <summary>How often audio was dropped to stay under the cap: a steady rise means the network is uneven.</summary>
    public int Skips => Volatile.Read(ref _skips);

    private int _skips;

    public int Read(Span<byte> buffer) => _buffer.Read(buffer);

    public void Write(ReadOnlySpan<byte> pcm)
    {
        // ponytail: sender and receiver sound cards drift apart, so latency creeps up over time. On overflow we
        // drop back to half the cap (a short audible skip). Upgrade path: adaptive resampling to track the drift.
        if (_buffer.BufferedBytes > _maxBytes)
        {
            Discard(_buffer.BufferedBytes - _targetBytes);
            Interlocked.Increment(ref _skips);
        }
        TrimUnusedAudio();
        _buffer.AddSamples(pcm);
    }

    /// <summary>
    /// After an underrun the late audio stays queued, adding delay for good. Whatever never drained in a whole
    /// window was not needed to absorb jitter, so drop it (keeping a small spare) instead of waiting for the cap.
    /// </summary>
    private void TrimUnusedAudio()
    {
        _windowMinBytes = Math.Min(_windowMinBytes, _buffer.BufferedBytes);
        if (++_windowWrites < TrimWindowWrites)
            return;
        if (_windowMinBytes > _spareBytes)
            Discard(_windowMinBytes - _spareBytes);
        _windowWrites = 0;
        _windowMinBytes = int.MaxValue;
    }

    private void Discard(int bytes)
    {
        bytes -= bytes % WaveFormat.BlockAlign;
        _buffer.Read(_scratch.AsSpan(0, Math.Min(bytes, _scratch.Length)));
    }
}
