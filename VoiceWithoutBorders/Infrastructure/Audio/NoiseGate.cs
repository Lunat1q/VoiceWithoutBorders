namespace VoiceWithoutBorders.Infrastructure.Audio;

/// <summary>
/// Silences microphone chunks while the level stays under a threshold (keyboard, fan, room noise). Stays open for
/// <see cref="HoldMilliseconds"/> after the last loud chunk so word endings and short pauses are not cut.
/// Silence is sent rather than nothing, so the far side's jitter buffer stays primed. One per capture thread.
/// </summary>
// ponytail: hard open/close with no ramp; a quiet click is possible at the edges. Add a short fade if anyone hears it.
public sealed class NoiseGate
{
    public const int HoldMilliseconds = 300;

    private static readonly int HoldBytes = WireFormat.Format.ConvertLatencyToByteSize(HoldMilliseconds);

    private int _holdLeft;

    /// <param name="threshold">16-bit peak a chunk must reach to open the gate; 0 or less turns the gate off.</param>
    public void Process(Span<byte> chunk, int threshold)
    {
        if (threshold <= 0 || WireFormat.Peak(chunk) >= threshold)
        {
            _holdLeft = HoldBytes;
            return;
        }
        if (_holdLeft > 0)
            _holdLeft -= chunk.Length;
        else
            chunk.Clear();
    }
}
