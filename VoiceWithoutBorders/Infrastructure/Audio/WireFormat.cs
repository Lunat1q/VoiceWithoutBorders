using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>The one PCM format every stream uses on the wire, plus helpers to get in and out of it.</summary>
public static class WireFormat
{
    public static readonly WaveFormat Format = new(48000, 16, 2);

    /// <summary>5 ms of audio. Keeps each UDP packet well under a 1500-byte MTU.</summary>
    public const int ChunkBytes = 960;

    /// <summary>
    /// Splits captured PCM into wire-sized chunks. <paramref name="scratch"/> is reused for every chunk,
    /// so <paramref name="emit"/> must consume the segment before returning.
    /// </summary>
    public static void ForEachChunk(ReadOnlySpan<byte> pcm, bool silent, byte[] scratch, Action<ArraySegment<byte>> emit)
    {
        for (var offset = 0; offset < pcm.Length; offset += scratch.Length)
        {
            var count = Math.Min(scratch.Length, pcm.Length - offset);
            if (silent)
                Array.Clear(scratch, 0, count);
            else
                pcm.Slice(offset, count).CopyTo(scratch);
            emit(new ArraySegment<byte>(scratch, 0, count));
        }
    }

    /// <summary>Loudest 16-bit sample in the chunk, 0 to 32768.</summary>
    public static int Peak(ReadOnlySpan<byte> pcm)
    {
        var peak = 0;
        foreach (var sample in MemoryMarshal.Cast<byte, short>(pcm))
            peak = Math.Max(peak, Math.Abs((int)sample));
        return peak;
    }

    /// <summary>Scales 16-bit samples in place, clipping at full scale. 1 leaves the audio untouched.</summary>
    public static void ApplyGain(Span<byte> pcm, float gain)
    {
        if (gain == 1)
            return;
        foreach (ref var sample in MemoryMarshal.Cast<byte, short>(pcm))
            sample = (short)Math.Clamp(sample * gain, short.MinValue, short.MaxValue);
    }

    /// <summary>16-bit peak for a level in decibels below full scale (0 dB = 32768).</summary>
    public static int PeakFromDb(double db) => (int)Math.Round(32768 * Math.Pow(10, db / 20));

    /// <summary>WASAPI playback never resamples, so convert when the device runs at another rate.</summary>
    public static IWaveProvider ResampleTo(IWaveProvider source, int sampleRate) =>
        source.WaveFormat.SampleRate == sampleRate
            ? source
            : new WdlResamplingSampleProvider(source.ToSampleProvider(), sampleRate).ToWaveProvider();
}
