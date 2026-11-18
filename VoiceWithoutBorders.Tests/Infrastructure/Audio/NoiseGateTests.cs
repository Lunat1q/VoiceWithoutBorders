using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Audio;

namespace VoiceWithoutBorders.Tests.Infrastructure.Audio;

public class NoiseGateTests
{
    private const int Threshold = 1000;

    // 5 ms chunks: the 300 ms hold lasts 60 of them.
    private static readonly int HoldChunks = NoiseGate.HoldMilliseconds / 5;

    [Fact]
    public void LoudChunk_PassesUntouched()
    {
        var chunk = Chunk(2000);

        new NoiseGate().Process(chunk, Threshold);

        chunk.Should().Equal(Chunk(2000));
    }

    [Fact]
    public void QuietChunk_IsSilenced_OnlyAfterTheHoldRunsOut()
    {
        var gate = new NoiseGate();
        gate.Process(Chunk(2000), Threshold);

        for (var i = 0; i < HoldChunks; i++)
        {
            var held = Chunk(500);
            gate.Process(held, Threshold);
            held.Should().Equal(Chunk(500), "a short pause inside speech is kept");
        }

        var after = Chunk(500);
        gate.Process(after, Threshold);
        after.Should().OnlyContain(b => b == 0);
    }

    [Fact]
    public void ThresholdZero_TurnsTheGateOff()
    {
        var gate = new NoiseGate();
        for (var i = 0; i < HoldChunks * 2; i++)
        {
            var chunk = Chunk(10);
            gate.Process(chunk, threshold: 0);
            chunk.Should().Equal(Chunk(10));
        }
    }

    private static byte[] Chunk(short level)
    {
        var chunk = new byte[WireFormat.ChunkBytes];
        for (var i = 0; i < chunk.Length; i += 2)
            BitConverter.TryWriteBytes(chunk.AsSpan(i), (short)(i % 4 == 0 ? level : -level));
        return chunk;
    }
}
