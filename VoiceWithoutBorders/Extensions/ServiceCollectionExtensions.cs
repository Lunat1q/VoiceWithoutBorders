using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Tray;
using VoiceWithoutBorders.Infrastructure.Interop;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Network;
using VoiceWithoutBorders.Features.Updates;
using VoiceWithoutBorders.Infrastructure.Updates;

namespace VoiceWithoutBorders.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVoiceWithoutBorders(this IServiceCollection services) =>
        services.AddSettings().AddAudio().AddNetworking().AddStreaming().AddUpdates().AddUi();

    public static IServiceCollection AddSettings(this IServiceCollection services) =>
        services.AddSingleton<ISettingsStore>(provider =>
            new JsonSettingsStore(JsonSettingsStore.DefaultPath, provider.GetRequiredService<ILogger<JsonSettingsStore>>()));

    public static IServiceCollection AddAudio(this IServiceCollection services) =>
        services
            .AddSingleton<IAudioDeviceService, AudioDeviceService>()
            .AddSingleton<IAudioEndpointFactory, WasapiEndpointFactory>();

    public static IServiceCollection AddNetworking(this IServiceCollection services) =>
        services.AddSingleton<IPeerTransport, UdpPeerTransport>();

    public static IServiceCollection AddStreaming(this IServiceCollection services) =>
        services.AddSingleton<IStreamingService, StreamingService>();

    /// <summary>GitHub Releases is the feed, the Inno Setup installer the payload.</summary>
    public static IServiceCollection AddUpdates(this IServiceCollection services) =>
        services
            .AddSingleton<IUpdateChecker, GitHubUpdateChecker>()
            .AddSingleton<IUpdateInstaller, InnoSetupUpdateInstaller>()
            .AddSingleton<UpdateViewModel>();

    public static IServiceCollection AddUi(this IServiceCollection services) =>
        services
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<OverlayWindow>()
            .AddSingleton<TrayIcon>()
            .AddSingleton<IGlobalHotkeys, GlobalHotkeys>()
            .AddSingleton<IStartupRegistration, RunKeyStartupRegistration>()
            .AddSingleton<HotkeyController>();
}
