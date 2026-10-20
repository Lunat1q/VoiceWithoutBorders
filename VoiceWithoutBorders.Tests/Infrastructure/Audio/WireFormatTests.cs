using FluentAssertions;
using NAudio.Wave;
using VoiceWithoutBorders.Infrastructure.Audio;

namespace VoiceWithoutBorders.Tests.Infrastructure.Audio;

public class WireFormatTests
{
    [Fact]
    public void ForEachChunk_SplitsIntoWireSizedChunksWithRemainder()
    {
        var pcm = Enumerable.Range(0, 2000).Select(i => (byte)i).ToArray();
        var chunks = new List<byte[]>();

        WireFormat.ForEachChunk(pcm, silent: false, new byte[WireFormat.ChunkBytes], chunk => chunks.Add(chunk.ToArray()));

        chunks.Select(c => c.Length).Should().Equal(960, 960, 80);
        chunks.SelectMany(c => c).Should().Equal(pcm);
    }

    [Fact]
    public void ForEachChunk_SilentBufferSendsZeros()
    {
        var pcm = Enumerable.Repeat((byte)0xAB, 100).ToArray();
        var scratch = Enumerable.Repeat((byte)0xFF, WireFormat.ChunkBytes).ToArray();
        byte[]? sent = null;

        WireFormat.ForEachChunk(pcm, silent: true, scratch, chunk => sent = chunk.ToArray());

        sent.Should().HaveCount(100).And.OnlyContain(b => b == 0);
    }

    [Fact]
    public void ResampleTo_SameRate_ReturnsSource()
    {
        var source = new BufferedWaveProvider(WireFormat.Format);

        WireFormat.ResampleTo(source, 48000).Should().BeSameAs(source);
    }

    [Fact]
    public void ResampleTo_OtherRate_ConvertsSampleRateKeepingDuration()
    {
        var source = new BufferedWaveProvider(WireFormat.Format, TimeSpan.FromSeconds(2)) { ReadFully = false };
        source.AddSamples(new byte[WireFormat.Format.AverageBytesPerSecond], 0, WireFormat.Format.AverageBytesPerSecond);

        var resampled = WireFormat.ResampleTo(source, 44100);
        var total = 0;
        var buffer = new byte[4096];
        int read;
        while ((read = resampled.Read(buffer)) > 0)
            total += read;

        resampled.WaveFormat.SampleRate.Should().Be(44100);
        resampled.WaveFormat.Channels.Should().Be(2);
        total.Should().BeCloseTo(resampled.WaveFormat.AverageBytesPerSecond, (uint)(resampled.WaveFormat.AverageBytesPerSecond / 20));
    }
}
