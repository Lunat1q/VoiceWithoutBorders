using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VoiceWithoutBorders.Extensions;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Interop;
using VoiceWithoutBorders.Infrastructure.Updates;

namespace VoiceWithoutBorders.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVoiceWithoutBorders_ResolvesServices()
    {
        using var provider = new ServiceCollection().AddLogging().AddVoiceWithoutBorders()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        provider.GetRequiredService<IStreamingService>().Should().BeOfType<StreamingService>();
        provider.GetRequiredService<ISettingsStore>().Should().BeOfType<JsonSettingsStore>();
        provider.GetRequiredService<IAudioDeviceService>().Should().BeOfType<AudioDeviceService>();
        provider.GetRequiredService<IStartupRegistration>().Should().BeOfType<RunKeyStartupRegistration>();
        provider.GetRequiredService<IUpdateChecker>().Should().BeOfType<GitHubUpdateChecker>();
        provider.GetRequiredService<IUpdateInstaller>().Should().BeOfType<InnoSetupUpdateInstaller>();
    }
}
