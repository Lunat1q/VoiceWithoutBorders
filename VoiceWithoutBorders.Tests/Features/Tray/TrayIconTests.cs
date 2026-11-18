using FluentAssertions;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Features.Tray;

namespace VoiceWithoutBorders.Tests.Features.Tray;

public class TrayIconTests
{
    [Theory]
    [InlineData(false, PeerConnection.Connected, true, true, "Voice Without Borders: stopped")]
    [InlineData(true, PeerConnection.Connected, true, false, "Voice Without Borders: connected, mic on, sound off")]
    [InlineData(true, PeerConnection.Waiting, false, true, "Voice Without Borders: waiting, mic off, sound on")]
    [InlineData(true, PeerConnection.KeyMismatch, false, false, "Voice Without Borders: key mismatch, mic off, sound off")]
    public void Tooltip_ShowsTheLinkAndToggles_WithinTheTooltipLimit(bool running, PeerConnection connection, bool mic, bool sound, string expected)
    {
        var text = TrayIcon.Tooltip(running, connection, mic, sound);

        text.Should().Be(expected);
        text.Length.Should().BeLessThanOrEqualTo(63, "Windows cuts tray tooltips at 63 characters");
    }
}
