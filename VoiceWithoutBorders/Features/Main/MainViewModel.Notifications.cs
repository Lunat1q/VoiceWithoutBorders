using CommunityToolkit.Mvvm.ComponentModel;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Features.Main;

// Notifications tab: which link changes the tray pops up. The link is still tracked when a notice is muted,
// so turning one back on does not replay what happened meanwhile.
public sealed partial class MainViewModel
{
    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _notifyConnected = true;
    [ObservableProperty] private bool _notifyLost = true;
    [ObservableProperty] private bool _notifyKeyMismatch = true;

    private void LoadNotifications(AppSettings settings)
    {
        NotificationsEnabled = settings.NotificationsEnabled;
        NotifyConnected = settings.NotifyConnected;
        NotifyLost = settings.NotifyLost;
        NotifyKeyMismatch = settings.NotifyKeyMismatch;
    }

    private void Notify(bool wanted, string text)
    {
        if (NotificationsEnabled && wanted)
            LinkNotice?.Invoke(text);
    }
}
