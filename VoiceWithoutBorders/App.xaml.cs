using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceWithoutBorders.Extensions;
using VoiceWithoutBorders.Features.Main;

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
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Disposing the container stops streaming and releases devices and the socket.
        _host?.Dispose();
        base.OnExit(e);
    }
}
