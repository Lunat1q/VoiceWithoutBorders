using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceWithoutBorders.Extensions;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Tray;
using VoiceWithoutBorders.Infrastructure.Interop;

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
        RegisterHotkeys(services.GetRequiredService<GlobalHotkeys>(), window, services.GetRequiredService<MainViewModel>());
    }

    private static void RegisterHotkeys(GlobalHotkeys hotkeys, Window window, MainViewModel vm)
    {
        hotkeys.Attach(window);
        var taken = new List<string>();
        if (!hotkeys.Register(Hotkeys.Modifiers, Hotkeys.ToggleMicKey, () => vm.MicEnabled = !vm.MicEnabled))
            taken.Add(Hotkeys.ToggleMicText);
        if (!hotkeys.Register(Hotkeys.Modifiers, Hotkeys.ToggleSoundKey, () => vm.SoundEnabled = !vm.SoundEnabled))
            taken.Add(Hotkeys.ToggleSoundText);
        if (taken.Count > 0)
            vm.Status = $"{string.Join(" and ", taken)} already used by another app; use the checkboxes or tray menu instead.";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Disposing the container stops streaming, releases devices and the socket, removes the tray icon and hotkeys.
        _host?.Dispose();
        base.OnExit(e);
    }
}
