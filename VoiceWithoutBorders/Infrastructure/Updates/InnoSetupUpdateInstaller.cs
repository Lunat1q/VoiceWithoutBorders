using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace VoiceWithoutBorders.Infrastructure.Updates;

/// <summary>
/// Downloads something and runs it, so deliberately unforgiving: HTTPS, GitHub hosts only, and the SHA-256 published
/// with the release must match. A release without a checksum is refused, not trusted: the release workflow always
/// publishes one, so its absence means something is wrong.
/// </summary>
public sealed class InnoSetupUpdateInstaller : IUpdateInstaller, IDisposable
{
    /// <summary>Passed back to the app by the installer when it was streaming before the update.</summary>
    public const string ResumeArgument = "--resume";

    private static readonly string[] AllowedHosts =
        ["github.com", "api.github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly ILogger<InnoSetupUpdateInstaller> _logger;

    public InnoSetupUpdateInstaller(ILogger<InnoSetupUpdateInstaller> logger)
    {
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("VoiceWithoutBorders-Updater");
    }

    public async Task DownloadAndInstallAsync(UpdateInfo update, bool resumeStreaming, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureTrusted(update.DownloadUrl);
        var checksumUrl = update.ChecksumUrl
            ?? throw new InvalidOperationException("This release publishes no checksum, so the installer cannot be verified. Update skipped.");
        EnsureTrusted(checksumUrl);

        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VoiceWithoutBorders-update")).FullName;
        var path = Path.Combine(directory, $"VoiceWithoutBorders-Setup-{update.Version}.exe");
        await DownloadAsync(update.DownloadUrl, path, progress, cancellationToken);

        var expected = ParseChecksum(await _http.GetStringAsync(checksumUrl, cancellationToken));
        string actual;
        await using (var file = File.OpenRead(path))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidOperationException($"The downloaded installer does not match its published checksum and was discarded (expected {expected}, got {actual}).");
        }

        _logger.LogInformation("Update {Version} verified; starting the installer", update.Version);
        // The installer reopens the app when done ([Run] in the .iss) and hands it these arguments.
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" + (resumeStreaming ? $" /launchargs={ResumeArgument}" : ""),
            UseShellExecute = true,
        });
        // A normal shutdown, so devices, the socket and the tray icon are released before the exe is replaced.
        _ = Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
    }

    /// <summary>An http:// URL or an unknown host means an installer we did not publish: refuse before downloading.</summary>
    public static void EnsureTrusted(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to download an update from {uri}.");
    }

    /// <summary>A bare hash, or the "hash  filename" line sha256sum writes.</summary>
    public static string ParseChecksum(string text) =>
        text.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        ?? throw new InvalidOperationException("The published checksum is empty. Update skipped.");

    private async Task DownloadAsync(Uri uri, string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(path);
        var buffer = new byte[128 * 1024];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            if (total > 0)
                progress?.Report((double)copied / total);
        }
    }

    public void Dispose() => _http.Dispose();
}
