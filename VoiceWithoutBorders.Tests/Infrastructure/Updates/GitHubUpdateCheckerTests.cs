using FluentAssertions;
using static VoiceWithoutBorders.Infrastructure.Updates.GitHubUpdateChecker;

namespace VoiceWithoutBorders.Tests.Infrastructure.Updates;

public class GitHubUpdateCheckerTests
{
    private static readonly Version Current = new(0, 1, 0);

    private static GitHubRelease Release(string tag, params string[] assets) => new()
    {
        TagName = tag,
        Assets = assets.Select(name => new GitHubAsset { Name = name, DownloadUrl = $"https://github.com/dl/{name}", Size = 3 * 1024 * 1024 }).ToList(),
    };

    [Theory]
    [InlineData("0.2.0", 0, 2, 0)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("V0.2", 0, 2, 0)]
    public void TryParseVersion_PlainAndPrefixedTags(string tag, int major, int minor, int patch)
    {
        TryParseVersion(tag, out var version).Should().BeTrue();
        version.Should().Be(new Version(major, minor, patch));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v")]
    public void TryParseVersion_RejectsOtherTags(string? tag) => TryParseVersion(tag, out _).Should().BeFalse();

    [Fact]
    public void Pick_NewerRelease_TakesInstallerAndItsChecksum()
    {
        var update = Pick(Release("v0.2.0", "VoiceWithoutBorders-0.2.0-win-x64.zip", "VoiceWithoutBorders-Setup-0.2.0.exe", "VoiceWithoutBorders-Setup-0.2.0.exe.sha256"), Current);

        update.Should().NotBeNull();
        update!.Version.Should().Be(new Version(0, 2, 0));
        update.DownloadUrl.Should().Be(new Uri("https://github.com/dl/VoiceWithoutBorders-Setup-0.2.0.exe"));
        update.ChecksumUrl.Should().Be(new Uri("https://github.com/dl/VoiceWithoutBorders-Setup-0.2.0.exe.sha256"));
        update.SizeText.Should().Be("3 MB");
    }

    [Fact]
    public void Pick_NoChecksum_StillOffered_SoTheInstallerRefusesItWithAReason() =>
        Pick(Release("v0.2.0", "VoiceWithoutBorders-Setup-0.2.0.exe"), Current)!.ChecksumUrl.Should().BeNull();

    [Theory]
    [InlineData("v0.1.0", "0.1.0")] // same as the assembly's 0.1.0.0
    [InlineData("v0.0.9", "0.0.9")]
    public void Pick_NotNewer_Nothing(string tag, string version) =>
        Pick(Release(tag, $"VoiceWithoutBorders-Setup-{version}.exe"), Current).Should().BeNull();

    [Fact]
    public void Pick_OnlyPortableZip_Nothing() =>
        Pick(Release("v0.2.0", "VoiceWithoutBorders-0.2.0-win-x64.zip"), Current).Should().BeNull();

    [Fact]
    public void Pick_DraftOrPrerelease_Nothing()
    {
        var release = Release("v0.2.0", "VoiceWithoutBorders-Setup-0.2.0.exe");

        Pick(release with { Draft = true }, Current).Should().BeNull();
        Pick(release with { Prerelease = true }, Current).Should().BeNull();
    }
}
