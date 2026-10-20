using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Records a microphone (or an output device in loopback) straight into the wire format.</summary>
public sealed class WasapiAudioCapture : IAudioCapture
{
    private readonly MMDevice _device;
    private readonly WasapiRecorder _recorder;
    private readonly byte[] _chunk = new byte[WireFormat.ChunkBytes];

    public event Action<ArraySegment<byte>>? ChunkReady;

    public WasapiAudioCapture(MMDevice device, bool loopback)
    {
        _device = device;
        // Shared mode with AutoConvertPcm: the Windows audio engine converts rate, depth and channels for us.
        var builder = new WasapiRecorderBuilder().WithDevice(device).WithFormat(WireFormat.Format).WithBufferLength(20);
        _recorder = (loopback ? builder.WithLoopbackCapture() : builder).Build();
        _recorder.DataAvailable += OnDataAvailable;
    }

    public void Start() => _recorder.StartRecording();

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        if (ChunkReady is { } emit)
            WireFormat.ForEachChunk(buffer, flags.HasFlag(AudioClientBufferFlags.Silent), _chunk, emit);
    }

    public void Dispose()
    {
        _recorder.DataAvailable -= OnDataAvailable;
        _recorder.Dispose();
        _device.Dispose();
    }
}
