using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Features.Updates;
using VoiceWithoutBorders.Infrastructure.Updates;

namespace VoiceWithoutBorders.Tests.Features.Updates;

public class UpdateViewModelTests
{
    private static readonly UpdateInfo Update = new(new Version(9, 9, 9), new Uri("https://github.com/x/setup.exe"), null, 1024 * 1024);

    private readonly Mock<IUpdateChecker> _checker = new();
    private readonly Mock<IUpdateInstaller> _installer = new();
    private readonly Mock<IStreamingService> _streaming = new();

    private UpdateViewModel Create() => new(_checker.Object, _installer.Object, _streaming.Object);

    private async Task<UpdateViewModel> CreateOffered()
    {
        _checker.Setup(c => c.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Update);
        var vm = Create();
        await vm.CheckInBackgroundAsync();
        return vm;
    }

    [Fact]
    public async Task Check_NewerRelease_IsOffered()
    {
        Create().IsOffered.Should().BeFalse("nothing before the check");

        var vm = await CreateOffered();

        vm.IsOffered.Should().BeTrue();
        vm.Headline.Should().Contain("9.9.9").And.Contain("1 MB");
    }

    [Fact]
    public async Task Check_Current_NothingOffered()
    {
        var vm = Create();

        await vm.CheckInBackgroundAsync();

        vm.IsOffered.Should().BeFalse();
        vm.Headline.Should().BeEmpty();
    }

    [Fact]
    public async Task Check_Throws_IsSilent()
    {
        _checker.Setup(c => c.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new UriFormatException());
        var vm = Create();

        await vm.CheckInBackgroundAsync();

        vm.IsOffered.Should().BeFalse();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Dismiss_HidesOffer()
    {
        var vm = await CreateOffered();

        vm.DismissCommand.Execute(null);

        vm.IsOffered.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Install_ResumesStreamingOnlyIfItWasStreaming(bool streaming)
    {
        _streaming.Setup(s => s.IsRunning).Returns(streaming);
        var vm = await CreateOffered();

        await vm.InstallCommand.ExecuteAsync(null);

        _installer.Verify(i => i.DownloadAndInstallAsync(Update, streaming, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Install_Refused_ShowsReasonAndOffersAgain()
    {
        _installer.Setup(i => i.DownloadAndInstallAsync(It.IsAny<UpdateInfo>(), It.IsAny<bool>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("does not match its published checksum"));
        var vm = await CreateOffered();

        await vm.InstallCommand.ExecuteAsync(null);

        vm.Error.Should().Contain("checksum");
        vm.HasError.Should().BeTrue();
        vm.IsInstalling.Should().BeFalse();
        vm.IsOffered.Should().BeTrue();
    }
}
