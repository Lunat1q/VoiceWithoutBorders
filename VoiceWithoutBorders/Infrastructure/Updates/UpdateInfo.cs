namespace VoiceWithoutBorders.Infrastructure.Updates;

/// <param name="Version">The released version, parsed from the tag (v0.2.0 → 0.2.0).</param>
/// <param name="DownloadUrl">The installer asset.</param>
/// <param name="ChecksumUrl">SHA-256 of the installer, published beside it; null when the release has none.</param>
public sealed record UpdateInfo(Version Version, Uri DownloadUrl, Uri? ChecksumUrl, long SizeBytes)
{
    public string SizeText => $"{SizeBytes / 1024d / 1024d:N0} MB";
}
