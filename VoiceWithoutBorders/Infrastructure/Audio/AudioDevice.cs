namespace VoiceWithoutBorders.Infrastructure.Audio;

public sealed record AudioDevice(string Id, string Name)
{
    private static readonly string[] VirtualMarkers = ["cable", "virtual", "voicemeeter"];

    /// <summary>
    /// Looks like a virtual cable, i.e. audio played here can be recorded by apps instead of heard in the room.
    /// </summary>
    // ponytail: name heuristic covering VB-CABLE, VoiceMeeter and "Virtual Audio Cable"-style drivers; it only drives a
    // warning. Upgrade path: inspect the endpoint's driver/form factor properties if false hits become a problem.
    public bool IsVirtual => VirtualMarkers.Any(marker => Name.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
