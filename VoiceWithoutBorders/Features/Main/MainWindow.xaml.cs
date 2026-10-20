using System.Windows;
using System.Windows.Threading;

namespace VoiceWithoutBorders.Features.Main;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // PasswordBox.Password is deliberately not bindable, so sync it by hand.
        SharedKeyBox.Password = viewModel.SharedKey;
        SharedKeyBox.PasswordChanged += (_, _) => viewModel.SharedKey = SharedKeyBox.Password;

        var statsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.UpdateStats(), Dispatcher);
        Closed += (_, _) => statsTimer.Stop();
    }
}
