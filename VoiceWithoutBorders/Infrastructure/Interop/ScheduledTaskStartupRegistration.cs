using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace VoiceWithoutBorders.Infrastructure.Interop;

/// <summary>
/// Per-user "at log on" scheduled task: no admin rights (the trigger is limited to this user), and unlike a Run key
/// entry Explorer does not hold it back for its startup delay, so auto-connect begins seconds sooner.
/// </summary>
public sealed class ScheduledTaskStartupRegistration : IStartupRegistration
{
    /// <summary>Passed by the task, so the app knows Windows started it rather than the user.</summary>
    public const string LaunchArgument = "--autostart";

    public const string TaskName = "VoiceWithoutBorders";

    // Versions up to 0.2.3 started from here; moved to the task the next time the app opens.
    private const string LegacyRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled => TaskExists() || LegacyRunEntryExists();

    public void SetEnabled(bool enabled)
    {
        RemoveLegacyRunEntry();
        if (enabled)
            CreateTask();
        else if (TaskExists())
            Schtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    /// <summary>
    /// Task Scheduler defaults would hurt a long-running audio app: below-normal CPU and I/O priority (7), a kill
    /// after 72 hours, and no start or a stop on battery. All overridden here.
    /// </summary>
    public static string BuildTaskXml(string exePath, string userId)
    {
        var user = SecurityElement.Escape(userId);
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Starts Voice Without Borders when you sign in.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(exePath)}</Command><Arguments>{LaunchArgument}</Arguments></Exec></Actions>
            </Task>
            """;
    }

    private static void CreateTask()
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"VoiceWithoutBorders-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, BuildTaskXml(Environment.ProcessPath!, WindowsIdentity.GetCurrent().Name), Encoding.Unicode);
            Schtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F"); // /F rewrites it, so it follows a moved exe
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    private static bool TaskExists() => Run($"/Query /TN \"{TaskName}\"").ExitCode == 0;

    private static void Schtasks(string arguments)
    {
        var (exitCode, error) = Run(arguments);
        if (exitCode != 0)
            throw new InvalidOperationException(error.Trim());
    }

    private static (int ExitCode, string Error) Run(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, error);
    }

    private static bool LegacyRunEntryExists()
    {
        using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey);
        return key?.GetValue(TaskName) is string;
    }

    private static void RemoveLegacyRunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, writable: true);
        key?.DeleteValue(TaskName, throwOnMissingValue: false);
    }
}
