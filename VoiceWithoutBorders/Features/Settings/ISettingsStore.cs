namespace VoiceWithoutBorders.Features.Settings;

public interface ISettingsStore
{
    /// <summary>Returns defaults when nothing is saved yet or the file is unreadable.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
