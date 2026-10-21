using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Infrastructure.Interop;

namespace VoiceWithoutBorders.Features.Overlay;

/// <summary>
/// Always-on-top activity pill. Locked (the default) it is click-through and never focused, so it cannot get in the
/// way of games; unlocked it takes the mouse and can be dragged anywhere, and the spot is remembered.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double TopMargin = 8;

    // Another topmost window that comes to the front (a borderless game, a video player, the taskbar) lands above
    // this one, and Topmost="True" alone never puts it back.
    private static readonly TimeSpan RaiseInterval = TimeSpan.FromSeconds(1);
    private static readonly Brush UnlockedBorder = new SolidColorBrush(Color.FromRgb(0x26, 0xC6, 0xDA));
    private static readonly Brush LockedBorder = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    private readonly MainViewModel _main;
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastFrame;
    private TimeSpan _lastRaise;

    public OverlayWindow(MainViewModel main)
    {
        InitializeComponent();
        _main = main;
        DataContext = main.Overlay;
        SourceInitialized += (_, _) => ApplyLock();
        SizeChanged += (_, _) => Place();
        MouseLeftButtonDown += OnMouseDown;
        main.PropertyChanged += OnMainPropertyChanged;
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.OverlayVisible):
                UpdateVisibility();
                break;
            case nameof(MainViewModel.OverlayUnlocked):
                ApplyLock();
                break;
            case nameof(MainViewModel.OverlayLeft) or nameof(MainViewModel.OverlayTop):
                Place();
                break;
        }
    }

    private void UpdateVisibility()
    {
        if (_main.OverlayVisible && !IsVisible)
        {
            Show();
            Place();
            _clock.Restart();
            _lastFrame = _lastRaise = TimeSpan.Zero;
            RaiseToTop();
            CompositionTarget.Rendering += OnFrame;
        }
        else if (!_main.OverlayVisible && IsVisible)
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
        if (now - _lastRaise >= RaiseInterval)
        {
            _lastRaise = now;
            RaiseToTop();
        }
    }

    // ponytail: polled once a second; a foreground-change hook (SetWinEventHook) would react instantly.
    // Exclusive-fullscreen games cannot be drawn over by any window.
    private void RaiseToTop() =>
        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, NativeMethods.HwndTopmost, 0, 0, 0, 0, NativeMethods.SwpNoSizeMoveActivate);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_main.OverlayUnlocked)
            return;
        DragMove(); // returns when the button is released
        _main.MoveOverlay(Left, Top);
    }

    /// <summary>Saved spot if it is still on a connected screen, otherwise top centre of the primary screen.</summary>
    private void Place()
    {
        var screens = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (_main.OverlayLeft is { } left && _main.OverlayTop is { } top && screens.Contains(new Point(left + ActualWidth / 2, top)))
        {
            Left = left;
            Top = top;
            return;
        }
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + TopMargin;
    }

    private void ApplyLock()
    {
        Pill.BorderBrush = _main.OverlayUnlocked ? UnlockedBorder : LockedBorder;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
        style = _main.OverlayUnlocked ? style & ~NativeMethods.WsExTransparent : style | NativeMethods.WsExTransparent;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style));
    }
}
