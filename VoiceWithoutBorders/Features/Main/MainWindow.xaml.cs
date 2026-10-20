using System.Windows;
using System.Windows.Threading;

namespace VoiceWithoutBorders.Features.Main;

/// <summary>Shell for the four tabs (Features/Main/Tabs); minimizing hides it to the tray.</summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        var statsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.UpdateLinkStatus(), Dispatcher);
        Closed += (_, _) => statsTimer.Stop();

        // Minimize goes to the tray; the tray icon brings it back.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                Hide();
        };
    }

    public void Restore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
