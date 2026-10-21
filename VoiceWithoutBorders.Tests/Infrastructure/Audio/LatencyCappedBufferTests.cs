using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Audio;

namespace VoiceWithoutBorders.Tests.Infrastructure.Audio;

public class LatencyCappedBufferTests
{
    private static readonly byte[] Chunk = new byte[WireFormat.ChunkBytes]; // 5 ms

    [Fact]
    public void Write_UnderCap_KeepsEverything()
    {
        var buffer = new LatencyCappedBuffer(WireFormat.Format, TimeSpan.FromMilliseconds(100));

        WriteMilliseconds(buffer, 50);

        buffer.BufferedDuration.Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Write_OverCap_DropsBackBelowCap()
    {
        var buffer = new LatencyCappedBuffer(WireFormat.Format, TimeSpan.FromMilliseconds(100));

        WriteMilliseconds(buffer, 300);

        buffer.BufferedDuration.Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(105));
        buffer.Skips.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Write_UnderCap_CountsNoSkips()
    {
        var buffer = new LatencyCappedBuffer(WireFormat.Format, TimeSpan.FromMilliseconds(100));

        WriteMilliseconds(buffer, 90);

        buffer.Skips.Should().Be(0);
    }

    [Fact]
    public void Read_WhenEmpty_PlaysSilence()
    {
        var buffer = new LatencyCappedBuffer(WireFormat.Format, TimeSpan.FromMilliseconds(100));
        var output = Enumerable.Repeat((byte)0xFF, 64).ToArray();

        buffer.Read(output).Should().Be(64);
        output.Should().OnlyContain(b => b == 0);
    }

    [Fact]
    public void Write_AfterLatePacketsPileUp_TrimsBackWithinTwoSeconds()
    {
        // Regression: a burst of late packets used to stay queued until the cap, adding delay for good.
        var buffer = new LatencyCappedBuffer(WireFormat.Format, TimeSpan.FromMilliseconds(200));
        WriteMilliseconds(buffer, 100);

        PlaySteadily(buffer, seconds: 2); // the window holding the underrun has nothing spare; the next one trims

        buffer.BufferedDuration.Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(20));
        buffer.Skips.Should().Be(0);
    }

    [Fact]
    public void Write_WithOnlyASmallSpare_KeepsIt()
    {
        var buffer = new LatencyCappedBuffer(WireFormat.Format, TimeSpan.FromMilliseconds(200));
        WriteMilliseconds(buffer, 10);

        PlaySteadily(buffer, seconds: 2);

        buffer.BufferedDuration.Should().Be(TimeSpan.FromMilliseconds(10));
    }

    private static void PlaySteadily(LatencyCappedBuffer buffer, int seconds)
    {
        var output = new byte[WireFormat.ChunkBytes];
        for (var i = 0; i < seconds * 200; i++)
        {
            buffer.Write(Chunk);
            buffer.Read(output);
        }
    }

    private static void WriteMilliseconds(LatencyCappedBuffer buffer, int milliseconds)
    {
        for (var i = 0; i < milliseconds / 5; i++)
            buffer.Write(Chunk);
    }
}
