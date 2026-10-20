using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Infrastructure.Interop;

namespace VoiceWithoutBorders.Features.Overlay;

/// <summary>
/// Always-on-top pill at the top of the primary screen while streaming. Click-through and never focused, so it
/// cannot get in the way of games or full-screen apps.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double TopMargin = 8;

    private readonly MainViewModel _main;
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastFrame;

    public OverlayWindow(MainViewModel main)
    {
        InitializeComponent();
        _main = main;
        DataContext = main.Overlay;
        SourceInitialized += (_, _) => MakeClickThrough();
        SizeChanged += (_, _) => PlaceAtTopCenter();
        main.PropertyChanged += OnMainPropertyChanged;
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.OverlayVisible))
            return;
        if (_main.OverlayVisible)
        {
            Show();
            PlaceAtTopCenter();
            _clock.Restart();
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnFrame;
        }
        else
        {
            CompositionTarget.Rendering -= OnFrame;
            Hide();
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        _main.TickOverlay((now - _lastFrame).TotalSeconds);
        _lastFrame = now;
    }

    private void PlaceAtTopCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + TopMargin;
    }

    private void MakeClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExTransparent | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style));
    }
}
