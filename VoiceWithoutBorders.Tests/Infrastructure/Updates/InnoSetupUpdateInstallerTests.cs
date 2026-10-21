using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VoiceWithoutBorders.Infrastructure.Updates;

namespace VoiceWithoutBorders.Tests.Infrastructure.Updates;

public class InnoSetupUpdateInstallerTests
{
    [Theory]
    [InlineData("https://github.com/Lunat1q/VoiceWithoutBorders/releases/download/v0.2.0/setup.exe")]
    [InlineData("https://release-assets.githubusercontent.com/x/setup.exe")]
    public void EnsureTrusted_GitHubOverHttps_Passes(string url) =>
        FluentActions.Invoking(() => InnoSetupUpdateInstaller.EnsureTrusted(new Uri(url))).Should().NotThrow();

    [Theory]
    [InlineData("http://github.com/setup.exe")]
    [InlineData("https://github.com.evil.example.com/setup.exe")]
    [InlineData("https://example.com/setup.exe")]
    public void EnsureTrusted_AnythingElse_Refused(string url) =>
        FluentActions.Invoking(() => InnoSetupUpdateInstaller.EnsureTrusted(new Uri(url))).Should().Throw<InvalidOperationException>();

    [Theory]
    [InlineData("ABC123", "ABC123")]
    [InlineData("abc123  VoiceWithoutBorders-Setup-0.2.0.exe", "abc123")]
    [InlineData("\r\n abc123\n", "abc123")]
    public void ParseChecksum_BareOrSha256sumLine(string text, string expected) =>
        InnoSetupUpdateInstaller.ParseChecksum(text).Should().Be(expected);

    [Fact]
    public void ParseChecksum_Empty_Refused() =>
        FluentActions.Invoking(() => InnoSetupUpdateInstaller.ParseChecksum(" \n")).Should().Throw<InvalidOperationException>();

    [Fact]
    public async Task DownloadAndInstall_ReleaseWithoutChecksum_RefusedBeforeDownloading()
    {
        using var installer = new InnoSetupUpdateInstaller(NullLogger<InnoSetupUpdateInstaller>.Instance);
        var update = new UpdateInfo(new Version(9, 0, 0), new Uri("https://github.com/x/VoiceWithoutBorders-Setup-9.0.0.exe"), null, 1);

        await installer.Invoking(i => i.DownloadAndInstallAsync(update, resumeStreaming: false))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no checksum*");
    }
}
