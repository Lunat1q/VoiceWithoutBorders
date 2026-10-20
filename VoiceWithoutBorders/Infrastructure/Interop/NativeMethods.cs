using System.Runtime.InteropServices;

namespace VoiceWithoutBorders.Infrastructure.Interop;

internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const uint ModNoRepeat = 0x4000;

    public const int GwlExStyle = -20;
    public const long WsExTransparent = 0x20;
    public const long WsExToolWindow = 0x80;
    public const long WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
}
