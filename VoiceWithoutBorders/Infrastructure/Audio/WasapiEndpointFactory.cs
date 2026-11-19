using NAudio.CoreAudioApi;

namespace VoiceWithoutBorders.Infrastructure.Audio;

public sealed class WasapiEndpointFactory : IAudioEndpointFactory
{
    /// <summary>HRESULT_FROM_WIN32(ERROR_NOT_FOUND), "Core Audio error 0x80070490".</summary>
    public const int ElementNotFound = unchecked((int)0x80070490);

    public IAudioCapture CreateCapture(string deviceId, bool loopback) => new WasapiAudioCapture(GetDevice(deviceId), loopback);

    public IAudioPlayer CreatePlayer(string deviceId, TimeSpan maxLatency) => new WasapiAudioPlayer(GetDevice(deviceId), maxLatency);

    private static MMDevice GetDevice(string deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        return Resolve(deviceId, enumerator.GetDevice, () => enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active), d => d.ID);
    }

    /// <summary>
    /// Looks the device up by id; when Windows says it does not know that id, finds it among the active endpoints
    /// instead. Seen at sign-in with SteelSeries Sonar: it re-creates its endpoints after the app started, and lookup by
    /// id then kept failing in this process (until a restart) while enumeration still listed them.
    /// </summary>
    public static T Resolve<T>(string id, Func<string, T> getById, Func<IEnumerable<T>> listActive, Func<T, string> idOf)
        where T : class, IDisposable
    {
        try
        {
            return getById(id);
        }
        catch (Exception ex) when (ex.HResult == ElementNotFound)
        {
            T? match = null;
            foreach (var device in listActive())
            {
                if (match is null && string.Equals(idOf(device), id, StringComparison.OrdinalIgnoreCase))
                    match = device;
                else
                    device.Dispose();
            }
            return match ?? throw new InvalidOperationException(
                "An audio device chosen on the Devices tab is not available (unplugged, disabled, or its driver restarted). " +
                "Pick it again, or Refresh devices.", ex);
        }
    }
}
