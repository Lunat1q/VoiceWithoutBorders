using System.Xml.Linq;
using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Interop;

namespace VoiceWithoutBorders.Tests.Infrastructure.Interop;

public class ScheduledTaskStartupRegistrationTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private static XElement Build(string exePath = @"C:\Apps\VoiceWithoutBorders.exe", string user = @"PC\alice") =>
        XDocument.Parse(ScheduledTaskStartupRegistration.BuildTaskXml(exePath, user)).Root!;

    [Fact]
    public void BuildTaskXml_StartsTheExeWithTheAutostartArgument()
    {
        var exec = Build().Descendants(Ns + "Exec").Single();

        exec.Element(Ns + "Command")!.Value.Should().Be(@"C:\Apps\VoiceWithoutBorders.exe");
        exec.Element(Ns + "Arguments")!.Value.Should().Be(ScheduledTaskStartupRegistration.LaunchArgument);
    }

    [Fact]
    public void BuildTaskXml_TriggersOnlyAtThisUsersLogon()
    {
        var root = Build();

        // A standard user may only register a logon trigger limited to themselves.
        root.Descendants(Ns + "LogonTrigger").Single().Element(Ns + "UserId")!.Value.Should().Be(@"PC\alice");
        root.Descendants(Ns + "RunLevel").Single().Value.Should().Be("LeastPrivilege");
    }

    [Fact]
    public void BuildTaskXml_OverridesDefaultsThatHurtAudio()
    {
        var settings = Build().Element(Ns + "Settings")!;

        settings.Element(Ns + "Priority")!.Value.Should().Be("4"); // default 7 is below-normal CPU and I/O
        settings.Element(Ns + "ExecutionTimeLimit")!.Value.Should().Be("PT0S"); // default kills it after 72 h
        settings.Element(Ns + "DisallowStartIfOnBatteries")!.Value.Should().Be("false");
        settings.Element(Ns + "StopIfGoingOnBatteries")!.Value.Should().Be("false");
    }

    [Fact]
    public void BuildTaskXml_EscapesPathsAndNames()
    {
        var root = Build(@"C:\R&D <x>\VoiceWithoutBorders.exe", @"PC\a&b");

        root.Descendants(Ns + "Command").Single().Value.Should().Be(@"C:\R&D <x>\VoiceWithoutBorders.exe");
        root.Descendants(Ns + "UserId").First().Value.Should().Be(@"PC\a&b");
    }
}
