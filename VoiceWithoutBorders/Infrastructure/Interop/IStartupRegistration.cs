namespace VoiceWithoutBorders.Infrastructure.Interop;

/// <summary>Whether Windows starts the app when the user signs in.</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    /// <summary>Enabling again rewrites the entry, so it follows the exe when a portable copy moves.</summary>
    void SetEnabled(bool enabled);
}
