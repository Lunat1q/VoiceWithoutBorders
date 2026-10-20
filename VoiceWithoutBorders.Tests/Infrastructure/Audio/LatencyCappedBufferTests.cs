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

    private static void WriteMilliseconds(LatencyCappedBuffer buffer, int milliseconds)
    {
        for (var i = 0; i < milliseconds / 5; i++)
            buffer.Write(Chunk);
    }
}
