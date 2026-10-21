using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VoiceWithoutBorders.Infrastructure.Updates;

/// <summary>GitHub Releases is the feed: the release workflow publishes the installer and its checksum there.</summary>
public sealed class GitHubUpdateChecker : IUpdateChecker, IDisposable
{
    private const string LatestRelease = "https://api.github.com/repos/Lunat1q/VoiceWithoutBorders/releases/latest";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ILogger<GitHubUpdateChecker> _logger;

    public GitHubUpdateChecker(ILogger<GitHubUpdateChecker> logger)
    {
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("VoiceWithoutBorders-Updater");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(LatestRelease, cancellationToken);
            var update = release is null ? null : Pick(release, AppVersion.Number);
            if (update is not null)
                _logger.LogInformation("Update available: {Version} (running {Current})", update.Version, AppVersion.Number);
            return update;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or System.Text.Json.JsonException)
        {
            _logger.LogDebug(ex, "Update check failed");
            return null;
        }
    }

    /// <summary>The release's installer when it is newer than <paramref name="current"/>; drafts and pre-releases never.</summary>
    public static UpdateInfo? Pick(GitHubRelease release, Version current)
    {
        if (release.Draft || release.Prerelease || !TryParseVersion(release.TagName, out var version) || version <= current)
            return null;
        // Named by installer/VoiceWithoutBorders.iss.
        var installer = release.Assets.FirstOrDefault(a => a.Name.Equals($"VoiceWithoutBorders-Setup-{version}.exe", StringComparison.OrdinalIgnoreCase));
        if (installer is null)
            return null;
        var checksum = release.Assets.FirstOrDefault(a => a.Name.Equals(installer.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
        return new UpdateInfo(version, new Uri(installer.DownloadUrl), checksum is null ? null : new Uri(checksum.DownloadUrl), installer.Size);
    }

    /// <summary>"1.2.0" or "v1.2.0", compared as three parts so 0.1.0 equals the assembly's 0.1.0.0.</summary>
    public static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (!Version.TryParse(tag?.Trim().TrimStart('v', 'V'), out var parsed))
            return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }

    public void Dispose() => _http.Dispose();

    public sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("assets")] public IReadOnlyList<GitHubAsset> Assets { get; init; } = [];
    }

    public sealed record GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; init; } = "";
        [JsonPropertyName("size")] public long Size { get; init; }
    }
}
