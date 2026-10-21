namespace VoiceWithoutBorders.Infrastructure.Updates;

/// <summary>The version this copy is: shown in the title bar, so "did the update install?" has an answer.</summary>
public static class AppVersion
{
    /// <summary>From the csproj's &lt;Version&gt;, as major.minor.patch. This assembly, not the entry one (a test host).</summary>
    public static Version Number { get; } = typeof(AppVersion).Assembly.GetName().Version is { } v
        ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0))
        : new Version(0, 0, 0);
}
