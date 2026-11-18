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
    private volatile bool _disposing;
    private volatile bool _faulted;

    public WasapiAudioPlayer(MMDevice device, TimeSpan maxLatency)
    {
        _device = device;
        _buffer = new LatencyCappedBuffer(WireFormat.Format, maxLatency);
        _player = new WasapiPlayerBuilder().WithDevice(device).WithLatency(DeviceLatencyMs).Build();
        // The jitter buffer never ends, so only Dispose or a device error stops playback.
        _player.PlaybackStopped += (_, _) => _faulted |= !_disposing;
        _player.Init(WireFormat.ResampleTo(_buffer, _player.DeviceMixFormat.SampleRate));
        _player.Play();
    }

    public bool Faulted => _faulted;

    public PlaybackStats Stats => new(_buffer.BufferedDuration, TimeSpan.FromMilliseconds(_player.LatencyMilliseconds), _buffer.Skips);

    public void Write(ArraySegment<byte> pcm) => _buffer.Write(pcm);

    public void Dispose()
    {
        _disposing = true;
        _player.Dispose();
        _device.Dispose();
    }
}
