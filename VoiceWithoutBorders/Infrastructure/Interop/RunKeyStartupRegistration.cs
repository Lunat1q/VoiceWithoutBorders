using Microsoft.Win32;

namespace VoiceWithoutBorders.Infrastructure.Interop;

/// <summary>Per-user Run key entry: no admin rights, and it shows up in Task Manager's Startup apps.</summary>
public sealed class RunKeyStartupRegistration : IStartupRegistration
{
    /// <summary>Passed by the Run entry, so the app knows Windows started it rather than the user.</summary>
    public const string LaunchArgument = "--autostart";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VoiceWithoutBorders";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {LaunchArgument}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
