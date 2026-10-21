namespace VoiceWithoutBorders.Infrastructure.Updates;

public interface IUpdateInstaller
{
    /// <summary>
    /// Downloads and verifies the installer, starts it silently and closes the app so its exe can be replaced.
    /// The installer reopens the app afterwards; <paramref name="resumeStreaming"/> makes it start streaming again.
    /// </summary>
    Task DownloadAndInstallAsync(UpdateInfo update, bool resumeStreaming, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
