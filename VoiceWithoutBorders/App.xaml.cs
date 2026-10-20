using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceWithoutBorders.Extensions;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Tray;

namespace VoiceWithoutBorders;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddVoiceWithoutBorders();
        _host = builder.Build();

        var services = _host.Services;
        var window = services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        services.GetRequiredService<OverlayWindow>(); // shows and hides itself with the streaming state
        services.GetRequiredService<TrayIcon>();
        services.GetRequiredService<HotkeyController>().Start(window);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Disposing the container stops streaming, releases devices and the socket, removes the tray icon and hotkeys.
        _host?.Dispose();
        base.OnExit(e);
    }
}
