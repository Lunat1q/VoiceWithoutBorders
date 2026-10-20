using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Audio;

namespace VoiceWithoutBorders.Tests.Infrastructure.Audio;

public class AudioDeviceTests
{
    private const string VbCable = "VB-Audio Virtual Cable";

    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)", true)]
    [InlineData("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)", true)]
    [InlineData("Line 1 (Virtual Audio Cable)", true)]
    [InlineData("Speakers (Realtek(R) Audio)", false)]
    [InlineData("Headphones (USB Headset)", false)]
    public void LooksVirtual_RecognisesCommonVirtualCables(string name, bool expected) =>
        AudioDevice.LooksVirtual(name).Should().Be(expected);

    // Endpoint names as VB-CABLE reports them on Windows 11, plus real hardware that must not be offered.
    [Fact]
    public void PairVirtualMicrophones_NamesTheMicAndPlaysIntoTheCableInput()
    {
        AudioEndpoint[] renders =
        [
            new("lg", "2 - LG ULTRAGEAR (AMD High Definition Audio Device)", "AMD High Definition Audio Device"),
            new("cable-16ch", "CABLE In 16ch (VB-Audio Virtual Cable)", VbCable),
            new("cable-in", "CABLE Input (VB-Audio Virtual Cable)", VbCable),
            new("realtek-out", "Speakers (Realtek(R) Audio)", "Realtek(R) Audio"),
        ];
        AudioEndpoint[] captures =
        [
            new("cable-out", "CABLE Output (VB-Audio Virtual Cable)", VbCable),
            new("realtek-mic", "Microphone (Realtek(R) Audio)", "Realtek(R) Audio"),
        ];

        AudioDeviceService.PairVirtualMicrophones(renders, captures)
            .Should().Equal(new AudioDevice("cable-in", "CABLE Output (VB-Audio Virtual Cable)"));
    }

    [Fact]
    public void PairVirtualMicrophones_PrefersPlaybackEndpointNamedLikeTheMic()
    {
        const string sonar = "SteelSeries Sonar Virtual Audio Device";
        AudioEndpoint[] renders =
        [
            new("chat", "SteelSeries Sonar - Chat (SteelSeries Sonar Virtual Audio Device)", sonar),
            new("gaming", "SteelSeries Sonar - Gaming (SteelSeries Sonar Virtual Audio Device)", sonar),
            new("mic-in", "SteelSeries Sonar - Microphone (SteelSeries Sonar Virtual Audio Device)", sonar),
        ];
        AudioEndpoint[] captures = [new("mic", "SteelSeries Sonar - Microphone (SteelSeries Sonar Virtual Audio Device)", sonar)];

        AudioDeviceService.PairVirtualMicrophones(renders, captures).Single().Id.Should().Be("mic-in");
    }

    [Fact]
    public void PairVirtualMicrophones_VirtualMicWithoutPlaybackSide_IsSkipped()
    {
        AudioEndpoint[] captures = [new("mic", "Microphone (VIVE Virtual Audio Device)", "VIVE Virtual Audio Device")];

        AudioDeviceService.PairVirtualMicrophones([], captures).Should().BeEmpty();
    }
}
