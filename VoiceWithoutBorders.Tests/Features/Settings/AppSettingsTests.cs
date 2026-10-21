using FluentAssertions;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Tests.Features.Settings;

public class AppSettingsTests
{
    internal static readonly AppSettings Valid = new()
    {
        PeerHost = "OFFICE-PC",
        SharedKey = "0123456789abcdef",
        MicCaptureDeviceId = "mic",
    };

    [Fact]
    public void Validate_ValidSettings_NoErrors() => Valid.Validate().Should().BeEmpty();

    [Fact]
    public void Defaults_LatencyCapSurvivesWiFi_AndNoAutoConnect()
    {
        var settings = new AppSettings();

        settings.MaxLatencyMs.Should().Be(200);
        settings.AutoConnect.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "16-char-key-ok!!", 15300, 120, "computer's name")]
    [InlineData("pc", "too-short", 15300, 120, "at least 16")]
    [InlineData("pc", "16-char-key-ok!!", 0, 120, "Port")]
    [InlineData("pc", "16-char-key-ok!!", 65536, 120, "Port")]
    [InlineData("pc", "16-char-key-ok!!", 15300, 10, "latency")]
    [InlineData("pc", "16-char-key-ok!!", 15300, 5000, "latency")]
    public void Validate_ReportsBadField(string host, string key, int port, int latency, string expected)
    {
        var settings = Valid with { PeerHost = host, SharedKey = key, Port = port, MaxLatencyMs = latency };

        settings.Validate().Should().ContainSingle().Which.Should().Contain(expected);
    }

    [Fact]
    public void Validate_NoDevices_ReportsError()
    {
        var settings = Valid with { MicCaptureDeviceId = null };

        settings.Validate().Should().ContainSingle().Which.Should().Contain("at least one device");
    }
}
