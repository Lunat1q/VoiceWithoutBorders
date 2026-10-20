using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>Plays the incoming wire stream on a device through a latency-capped jitter buffer.</summary>
public sealed class WasapiAudioPlayer : IAudioPlayer
{
    private const int DeviceLatencyMs = 30;

    private readonly MMDevice _device;
    private readonly LatencyCappedBuffer _buffer;
    private readonly WasapiPlayer _player;

    public WasapiAudioPlayer(MMDevice device, TimeSpan maxLatency)
    {
        _device = device;
        _buffer = new LatencyCappedBuffer(WireFormat.Format, maxLatency);
        _player = new WasapiPlayerBuilder().WithDevice(device).WithLatency(DeviceLatencyMs).Build();
        _player.Init(WireFormat.ResampleTo(_buffer, _player.DeviceMixFormat.SampleRate));
        _player.Play();
    }

    public void Write(ArraySegment<byte> pcm) => _buffer.Write(pcm);

    public void Dispose()
    {
        _player.Dispose();
        _device.Dispose();
    }
}
