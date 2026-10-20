namespace VoiceWithoutBorders.Features.Streaming;

/// <summary>Rough feel of a delay in conversation (in line with the usual guidance for voice calls).</summary>
public enum LatencyHealth
{
    Unknown,

    /// <summary>Under 150 ms: feels like talking in the same room.</summary>
    Good,

    /// <summary>150-300 ms: noticeable, people start talking over each other.</summary>
    Fair,

    /// <summary>Over 300 ms: something is wrong (busy Wi-Fi, very high Max latency).</summary>
    Poor,
}
