using System.ComponentModel;
using System.Windows;
using VoiceWithoutBorders.Features.Main;
using VoiceWithoutBorders.Infrastructure.Interop;

namespace VoiceWithoutBorders.Features.Hotkeys;

/// <summary>
/// Keeps the system-wide shortcuts in sync with the user's bindings and reports, in plain words, any that
/// could not be registered.
/// </summary>
public sealed class HotkeyController(IGlobalHotkeys hotkeys, MainViewModel vm) : IDisposable
{
    private static readonly HashSet<string> BindingProperties =
        [nameof(MainViewModel.ToggleMicHotkey), nameof(MainViewModel.ToggleSoundHotkey), nameof(MainViewModel.StartStopHotkey)];

    private bool _paused;

    public void Start(Window window)
    {
        hotkeys.Attach(window);
        vm.PropertyChanged += OnPropertyChanged;
        HotkeyBox.CapturingChanged += OnCapturingChanged;
        RegisterAll();
    }

    public void RegisterAll()
    {
        hotkeys.UnregisterAll();
        if (_paused)
            return;

        var problems = new List<string>();
        var taken = new Dictionary<HotkeyBinding, string>();
        Register(vm.ToggleMicHotkey, "Microphone on/off", () => vm.MicEnabled = !vm.MicEnabled);
        Register(vm.ToggleSoundHotkey, "Sound on/off", () => vm.SoundEnabled = !vm.SoundEnabled);
        Register(vm.StartStopHotkey, "Start/stop streaming", () => vm.ToggleStreamingCommand.Execute(null));
        vm.HotkeyStatus = problems.Count == 0 ? "Shortcuts work from any app." : string.Join(Environment.NewLine, problems);

        void Register(HotkeyBinding? binding, string name, Action action)
        {
            if (binding is null)
                return;
            if (taken.TryGetValue(binding, out var other))
                problems.Add($"{binding} is set for both \"{other}\" and \"{name}\"; only \"{other}\" works.");
            else if (!hotkeys.Register(binding.Modifiers, binding.Key, action))
                problems.Add($"{binding} (\"{name}\") is already used by another app. Pick another combination.");
            else
                taken[binding] = name;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is { } name && BindingProperties.Contains(name))
            RegisterAll();
    }

    private void OnCapturingChanged(bool capturing)
    {
        _paused = capturing;
        RegisterAll();
    }

    public void Dispose()
    {
        vm.PropertyChanged -= OnPropertyChanged;
        HotkeyBox.CapturingChanged -= OnCapturingChanged;
        hotkeys.Dispose();
    }
}
