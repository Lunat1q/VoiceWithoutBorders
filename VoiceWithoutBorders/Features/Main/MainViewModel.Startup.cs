using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Features.Main;

// Starting with Windows, and connecting by itself after such a start.
public sealed partial class MainViewModel
{
    /// <summary>At sign-in the network or a device may need a while; with the delays below this covers about a minute.</summary>
    public const int AutoConnectAttempts = 14;

    /// <summary>Quick first retries (the network is often up a moment later), then the last one repeats.</summary>
    public static readonly IReadOnlyList<TimeSpan> AutoConnectRetryDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    /// <summary>Lives in Task Scheduler, not in the settings file, so it never disagrees with what Windows does.</summary>
    [ObservableProperty] private bool _startWithWindows;

    [ObservableProperty] private bool _autoConnect;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_loading)
            ApplyStartWithWindows(value);
    }

    /// <summary>Called once when Windows launched the app at sign-in.</summary>
    public async Task OnStartedWithWindowsAsync(IReadOnlyList<TimeSpan> retryDelays)
    {
        if (AutoConnect)
            await ConnectWithRetriesAsync(retryDelays);
    }

    /// <summary>After sign-in, or reopened by an update that interrupted streaming.</summary>
    public async Task ConnectWithRetriesAsync(IReadOnlyList<TimeSpan> retryDelays)
    {
        for (var attempt = 1; ; attempt++)
        {
            await StartAsync();
            if (IsRunning || attempt == AutoConnectAttempts || ToSettings().Validate().Count > 0)
                return;
            var retryDelay = RetryDelay(retryDelays, attempt);
            Status += $"{Environment.NewLine}Trying again in {retryDelay.TotalSeconds:0} s…";
            await Task.Delay(retryDelay);
            if (IsRunning) // started by hand meanwhile
                return;
        }
    }

    /// <summary>Delay after the given failed attempt (1-based); past the list, its last entry repeats.</summary>
    public static TimeSpan RetryDelay(IReadOnlyList<TimeSpan> retryDelays, int attempt) =>
        retryDelays[Math.Min(attempt, retryDelays.Count) - 1];

    private void LoadStartup(AppSettings settings)
    {
        AutoConnect = settings.AutoConnect;
        try
        {
            StartWithWindows = _startup.IsEnabled;
        }
        catch (Exception ex)
        {
            Status = $"Could not read the Windows startup setting: {ex.Message}";
        }
        if (StartWithWindows)
            ApplyStartWithWindows(true); // the exe may have moved since it was registered
    }

    private void ApplyStartWithWindows(bool enabled)
    {
        try
        {
            _startup.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            Status = $"Could not change the Windows startup setting: {ex.Message}";
        }
    }
}
