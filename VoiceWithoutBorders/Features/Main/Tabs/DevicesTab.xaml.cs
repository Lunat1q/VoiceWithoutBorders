using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace VoiceWithoutBorders.Features.Main.Tabs;

public partial class DevicesTab : UserControl
{
    public DevicesTab() => InitializeComponent();

    private void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
