using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Records a microphone (or an output device in loopback) straight into the wire format.</summary>
public sealed class WasapiAudioCapture : IAudioCapture
{
    /// <summary>Capture buffer; also the capture part of the end-to-end latency estimate.</summary>
    public const int BufferMilliseconds = 20;

    private readonly MMDevice _device;
    private readonly WasapiRecorder _recorder;
    private readonly byte[] _chunk = new byte[WireFormat.ChunkBytes];
    private volatile bool _disposing;
    private volatile bool _faulted;

    public event Action<ArraySegment<byte>>? ChunkReady;

    public WasapiAudioCapture(MMDevice device, bool loopback)
    {
        _device = device;
        // Shared mode with AutoConvertPcm: the Windows audio engine converts rate, depth and channels for us.
        var builder = new WasapiRecorderBuilder().WithDevice(device).WithFormat(WireFormat.Format).WithBufferLength(BufferMilliseconds);
        _recorder = (loopback ? builder.WithLoopbackCapture() : builder).Build();
        _recorder.DataAvailable += OnDataAvailable;
        _recorder.RecordingStopped += (_, _) => _faulted |= !_disposing; // only Dispose stops it on purpose
    }

    public bool Faulted => _faulted;

    public void Start() => _recorder.StartRecording();

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        if (ChunkReady is { } emit)
            WireFormat.ForEachChunk(buffer, flags.HasFlag(AudioClientBufferFlags.Silent), _chunk, emit);
    }

    public void Dispose()
    {
        _disposing = true;
        _recorder.DataAvailable -= OnDataAvailable;
        _recorder.Dispose();
        _device.Dispose();
    }
}
