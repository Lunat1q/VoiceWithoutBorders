using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using VoiceWithoutBorders.Extensions;
using VoiceWithoutBorders.Features.Hotkeys;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Features.Overlay;
using VoiceWithoutBorders.Features.Tray;
using VoiceWithoutBorders.Features.Updates;
using VoiceWithoutBorders.Infrastructure.Interop;
using VoiceWithoutBorders.Infrastructure.Updates;

namespace VoiceWithoutBorders;

public partial class App : Application
{
    // Per sign-in session. Owning it makes this the only instance; a second launch signals it and exits.
    private const string ShowRequestName = @"Local\VoiceWithoutBorders.Show";

    private IHost? _host;
    private EventWaitHandle? _showRequest;
    private RegisteredWaitHandle? _showWait;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestName, out var firstInstance);
        if (!firstInstance)
        {
            _showRequest.Set(); // brings the running instance's window up, even from the tray
            Shutdown();
            return;
        }

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddVoiceWithoutBorders();
        _host = builder.Build();

        var services = _host.Services;
        var window = services.GetRequiredService<MainWindow>();
        MainWindow = window;
        var startedWithWindows = e.Args.Contains(RunKeyStartupRegistration.LaunchArgument);
        if (!startedWithWindows)
            window.Show(); // at sign-in it stays in the tray

        services.GetRequiredService<OverlayWindow>(); // shows and hides itself with the streaming state
        services.GetRequiredService<TrayIcon>();
        services.GetRequiredService<HotkeyController>().Start(window);

        _showWait = ThreadPool.RegisterWaitForSingleObject(_showRequest, (_, _) => Dispatcher.BeginInvoke(window.Restore), null, Timeout.Infinite, executeOnlyOnce: false);
        _viewModel = services.GetRequiredService<MainViewModel>();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        if (e.Args.Contains(InnoSetupUpdateInstaller.ResumeArgument))
            _ = _viewModel.ConnectWithRetriesAsync(MainViewModel.AutoConnectRetryDelay);
        else if (startedWithWindows)
            _ = _viewModel.OnStartedWithWindowsAsync(MainViewModel.AutoConnectRetryDelay);
        _ = services.GetRequiredService<UpdateViewModel>().CheckInBackgroundAsync();
    }

    // Raised on a system events thread.
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume && _viewModel is { } vm)
            Dispatcher.BeginInvoke(vm.OnSystemResumed);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _showWait?.Unregister(null);
        _showRequest?.Dispose();
        // Disposing the container stops streaming, releases devices and the socket, removes the tray icon and hotkeys.
        _host?.Dispose();
        base.OnExit(e);
    }
}
