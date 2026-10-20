using FluentAssertions;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Tests.Features.Settings;

public class SharedKeyGeneratorTests
{
    [Fact]
    public void Generate_ReturnsFiveGroupsOfUnambiguousCharacters()
    {
        var key = SharedKeyGenerator.Generate();

        key.Should().MatchRegex("^[A-HJKMNP-Z2-9]{4}(-[A-HJKMNP-Z2-9]{4}){4}$");
        (AppSettingsTests.Valid with { SharedKey = key }).Validate().Should().BeEmpty();
    }

    [Fact]
    public void Generate_IsRandom() => SharedKeyGenerator.Generate().Should().NotBe(SharedKeyGenerator.Generate());
}
