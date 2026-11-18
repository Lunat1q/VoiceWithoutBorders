using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Updates;

namespace VoiceWithoutBorders.Features.Updates;

/// <summary>
/// Offers a newer release and installs it only when the user says so. The check and its failures are silent.
/// Installing closes the app for a few seconds; when it was streaming, it streams again once reopened.
/// </summary>
public sealed partial class UpdateViewModel(IUpdateChecker checker, IUpdateInstaller installer, IStreamingService streaming) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffered), nameof(Headline))]
    private UpdateInfo? _available;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffered))]
    private bool _isDismissed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffered))]
    private bool _isInstalling;

    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>Answer to a manual check that found nothing; the offer itself answers one that did.</summary>
    [ObservableProperty] private string? _checkResult;

    public bool HasError => Error is not null;

    public bool IsOffered => Available is not null && !IsDismissed && !IsInstalling;

    public string Headline => Available is { } update
        ? $"Voice Without Borders {update.Version} is available ({update.SizeText})."
        : "";

    public async Task CheckInBackgroundAsync()
    {
        try
        {
            Available = await checker.CheckAsync();
        }
        catch (Exception)
        {
            // A malformed release is no reason to bother anyone; the next launch or Check for updates tries again.
        }
    }

    /// <summary>The button's check: brings back a dismissed offer, and says so when there is nothing newer.</summary>
    [RelayCommand]
    private async Task CheckNowAsync()
    {
        CheckResult = null;
        await CheckInBackgroundAsync();
        IsDismissed = false;
        // ponytail: the checker returns null for "up to date" and "GitHub unreachable" alike, hence the wording.
        CheckResult = Available is null ? $"No newer version than {AppVersion.Number} found." : null;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (Available is not { } update)
            return;
        IsInstalling = true;
        Error = null;
        try
        {
            // The app closes from in here.
            await installer.DownloadAndInstallAsync(update, streaming.IsRunning, new Progress<double>(value => DownloadPercent = value * 100));
        }
        catch (Exception ex)
        {
            Error = ex is InvalidOperationException ? ex.Message : $"The update could not be downloaded: {ex.Message}";
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void Dismiss() => IsDismissed = true;
}
