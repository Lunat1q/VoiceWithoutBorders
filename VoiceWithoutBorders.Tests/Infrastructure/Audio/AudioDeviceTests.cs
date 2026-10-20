using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Audio;

namespace VoiceWithoutBorders.Tests.Infrastructure.Audio;

public class AudioDeviceTests
{
    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)", true)]
    [InlineData("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)", true)]
    [InlineData("Line 1 (Virtual Audio Cable)", true)]
    [InlineData("Speakers (Realtek(R) Audio)", false)]
    [InlineData("Headphones (USB Headset)", false)]
    public void IsVirtual_RecognisesCommonVirtualCables(string name, bool expected) =>
        new AudioDevice("id", name).IsVirtual.Should().Be(expected);
}
