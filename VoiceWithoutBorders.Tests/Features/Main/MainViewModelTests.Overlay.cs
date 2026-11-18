using FluentAssertions;
using Moq;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Tests.Features.Main;

// Overlay look: layout, size, background opacity, hide when quiet.
public partial class MainViewModelTests
{
    [Fact]
    public void Constructor_PassesSavedOverlayLookToTheOverlay()
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings
        {
            SharedKey = "0123456789abcdef", OverlayVertical = true, OverlayScale = 1.5, OverlayOpacity = 0.3, OverlayHideWhenQuiet = true,
        });

        var vm = Create();

        vm.Overlay.IsVertical.Should().BeTrue();
        vm.Overlay.Scale.Should().Be(1.5);
        vm.Overlay.Opacity.Should().Be(0.3);
        vm.Overlay.HideWhenQuiet.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.1, 5, AppSettings.MinOverlayScale, 1)]
    [InlineData(9, -1, AppSettings.MaxOverlayScale, 0)]
    public void Constructor_HandEditedOverlayLookOutOfRange_IsClamped(double scale, double opacity, double expectedScale, double expectedOpacity)
    {
        _store.Setup(s => s.Load()).Returns(new AppSettings { SharedKey = "0123456789abcdef", OverlayScale = scale, OverlayOpacity = opacity });

        var vm = Create();

        vm.OverlayScale.Should().Be(expectedScale);
        vm.OverlayOpacity.Should().Be(expectedOpacity);
    }

    [Fact]
    public void ChangingOverlayLook_UpdatesTheOverlayAndIsSaved()
    {
        var vm = Create();

        vm.OverlayVertical = true;
        vm.OverlayScale = 1.25;
        vm.OverlayOpacity = 0.5;
        vm.OverlayHideWhenQuiet = true;

        vm.Overlay.IsVertical.Should().BeTrue();
        vm.Overlay.Scale.Should().Be(1.25);
        vm.Overlay.Opacity.Should().Be(0.5);
        vm.Overlay.HideWhenQuiet.Should().BeTrue();
        _store.Verify(s => s.Save(It.Is<AppSettings>(saved =>
            saved.OverlayVertical && saved.OverlayScale == 1.25 && saved.OverlayOpacity == 0.5 && saved.OverlayHideWhenQuiet)));
    }
}
