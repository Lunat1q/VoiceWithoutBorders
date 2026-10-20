namespace VoiceWithoutBorders.Infrastructure.Audio;

public sealed record AudioDevice(string Id, string Name)
{
    private static readonly string[] VirtualMarkers = ["cable", "virtual", "voicemeeter"];

    /// <summary>Looks like a virtual audio driver rather than real hardware.</summary>
    // ponytail: name heuristic covering VB-CABLE, VoiceMeeter and "Virtual Audio Cable"-style drivers.
    // Upgrade path: inspect the endpoint's driver/form factor properties if it misses a popular driver.
    public static bool LooksVirtual(string name) =>
        VirtualMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
}

/// <summary>An endpoint as Windows reports it; <see cref="AdapterName"/> is shared by both halves of one device.</summary>
public sealed record AudioEndpoint(string Id, string Name, string AdapterName);
