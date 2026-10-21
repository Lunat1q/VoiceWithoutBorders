using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Features.Main;

// Starting with Windows, and connecting by itself after such a start.
public sealed partial class MainViewModel
{
    /// <summary>At sign-in the network or a device may need a while; this covers about a minute.</summary>
    public const int AutoConnectAttempts = 12;

    public static readonly TimeSpan AutoConnectRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>Lives in the Run key, not in the settings file, so it never disagrees with what Windows does.</summary>
    [ObservableProperty] private bool _startWithWindows;

    [ObservableProperty] private bool _autoConnect;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_loading)
            ApplyStartWithWindows(value);
    }

    /// <summary>Called once when Windows launched the app at sign-in.</summary>
    public async Task OnStartedWithWindowsAsync(TimeSpan retryDelay)
    {
        if (AutoConnect)
            await ConnectWithRetriesAsync(retryDelay);
    }

    /// <summary>After sign-in, or reopened by an update that interrupted streaming.</summary>
    public async Task ConnectWithRetriesAsync(TimeSpan retryDelay)
    {
        for (var attempt = 1; ; attempt++)
        {
            await StartAsync();
            if (IsRunning || attempt == AutoConnectAttempts || ToSettings().Validate().Count > 0)
                return;
            Status += $"{Environment.NewLine}Trying again in {retryDelay.TotalSeconds:0} s…";
            await Task.Delay(retryDelay);
            if (IsRunning) // started by hand meanwhile
                return;
        }
    }

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
