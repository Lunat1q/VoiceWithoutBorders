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

    /// <summary>WASAPI playback never resamples, so convert when the device runs at another rate.</summary>
    public static IWaveProvider ResampleTo(IWaveProvider source, int sampleRate) =>
        source.WaveFormat.SampleRate == sampleRate
            ? source
            : new WdlResamplingSampleProvider(source.ToSampleProvider(), sampleRate).ToWaveProvider();
}
