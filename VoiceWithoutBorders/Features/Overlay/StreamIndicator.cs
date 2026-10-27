using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Features.Overlay;

/// <summary>One stream in the overlay: icon, direction and four bouncing level bars.</summary>
public sealed partial class StreamIndicator(StreamKind kind, bool sending) : ObservableObject
{
    public const double MinBar = 3;
    public const double MaxBar = 18;

    // Segoe Fluent Icons / MDL2 Assets code points.
    private const string MicGlyph = "";
    private const string MicOffGlyph = "";
    private const string VolumeGlyph = "";
    private const string MuteGlyph = "";

    private static readonly double[] BarWeights = [0.75, 1.0, 0.9, 0.65];

    public StreamKind Kind { get; } = kind;

    public bool Sending { get; } = sending;

    /// <summary>Teal for audio leaving this computer, indigo for audio arriving.</summary>
    public string Accent => Sending ? "#26C6DA" : "#9FA8DA";

    public string DirectionGlyph => Sending ? "" : ""; // up / down arrow

    public string Glyph => Kind == StreamKind.Microphone
        ? (IsMuted ? MicOffGlyph : MicGlyph)
        : (IsMuted ? MuteGlyph : VolumeGlyph);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    private bool _isMuted;

    /// <summary>Smoothed level, 0 to 1: jumps up with the audio, falls back gently.</summary>
    [ObservableProperty] private double _level;

    [ObservableProperty] private double _bar0 = MinBar;
    [ObservableProperty] private double _bar1 = MinBar;
    [ObservableProperty] private double _bar2 = MinBar;
    [ObservableProperty] private double _bar3 = MinBar;

    private const double FallPerSecond = 2.5;

    /// <summary>Peaks this far below full scale and quieter show flat bars (room noise, dither).</summary>
    private const double FloorDb = 50;

    /// <param name="peak">Loudest sample since the last tick, 0 to 1.</param>
    /// <param name="time">Running clock in seconds, drives the wobble so bars do not move in lockstep.</param>
    public void Update(float peak, bool muted, double elapsedSeconds, double time)
    {
        IsMuted = muted;
        var target = muted ? 0 : ToLevel(peak);
        Level = Math.Max(target, Level - FallPerSecond * elapsedSeconds);

        Bar0 = BarHeight(0, time);
        Bar1 = BarHeight(1, time);
        Bar2 = BarHeight(2, time);
        Bar3 = BarHeight(3, time);
    }

    /// <summary>
    /// Maps a peak to 0-1 on a decibel scale, the way ears hear it: quiet speech (peaks around 0.03-0.1, -30 to -20 dB)
    /// already fills about half the bar instead of a sliver.
    /// </summary>
    public static double ToLevel(float peak) =>
        peak <= 0 ? 0 : Math.Clamp((20 * Math.Log10(Math.Min(peak, 1)) + FloorDb) / FloorDb, 0, 1);

    private double BarHeight(int index, double time)
    {
        var wobble = 0.85 + 0.15 * Math.Sin(time * 9 + index * 1.7);
        return MinBar + (MaxBar - MinBar) * Level * BarWeights[index] * wobble;
    }
}
