using NAudio.Wave;

namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>
/// Jitter buffer between the network and the sound card. Underruns play silence; overruns drop audio
/// so latency never grows past the configured cap.
/// </summary>
public sealed class LatencyCappedBuffer : IWaveProvider
{
    private readonly BufferedWaveProvider _buffer;
    private readonly int _maxBytes;
    private readonly int _targetBytes;
    private readonly byte[] _scratch;

    public LatencyCappedBuffer(WaveFormat format, TimeSpan maxLatency)
    {
        _buffer = new BufferedWaveProvider(format, maxLatency + TimeSpan.FromSeconds(1)) { DiscardOnBufferOverflow = true };
        _maxBytes = format.ConvertLatencyToByteSize((int)maxLatency.TotalMilliseconds);
        _targetBytes = format.ConvertLatencyToByteSize((int)maxLatency.TotalMilliseconds / 2);
        _scratch = new byte[_maxBytes];
    }

    public WaveFormat WaveFormat => _buffer.WaveFormat;

    public TimeSpan BufferedDuration => _buffer.BufferedDuration;

    public int Read(Span<byte> buffer) => _buffer.Read(buffer);

    public void Write(ReadOnlySpan<byte> pcm)
    {
        // ponytail: sender and receiver sound cards drift apart, so latency creeps up over time. On overflow we
        // drop back to half the cap (a short audible skip). Upgrade path: adaptive resampling to track the drift.
        if (_buffer.BufferedBytes > _maxBytes)
            _buffer.Read(_scratch.AsSpan(0, Math.Min(_buffer.BufferedBytes - _targetBytes, _scratch.Length)));
        _buffer.AddSamples(pcm);
    }
}
