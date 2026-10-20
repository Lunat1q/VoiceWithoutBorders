using System.Windows.Input;
using FluentAssertions;
using VoiceWithoutBorders.Features.Hotkeys;
using Microsoft.Extensions.Logging.Abstractions;
using VoiceWithoutBorders.Features.Settings;

namespace VoiceWithoutBorders.Tests.Features.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vwb-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _path = Path.Combine(_directory, "settings.json");
        _store = new JsonSettingsStore(_path, NullLogger<JsonSettingsStore>.Instance);
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults() => _store.Load().Should().Be(new AppSettings());

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var settings = AppSettingsTests.Valid with { SystemPlaybackDeviceId = "headset", MaxLatencyMs = 80 };

        _store.Save(settings);

        _store.Load().Should().Be(settings);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsHotkeysAndOverlayPosition_WithReadableEnums()
    {
        var settings = AppSettingsTests.Valid with
        {
            ToggleSoundHotkey = null,
            StartStopHotkey = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, Key.F8),
            OverlayLeft = 1650.5,
            OverlayTop = 12,
        };

        _store.Save(settings);

        _store.Load().Should().Be(settings);
        File.ReadAllText(_path).Should().Contain("\"F8\"").And.Contain("Control, Shift");
    }

    [Fact]
    public void Save_DoesNotWriteSharedKeyInPlainText()
    {
        _store.Save(AppSettingsTests.Valid);

        File.ReadAllText(_path).Should().NotContain(AppSettingsTests.Valid.SharedKey);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not json");

        _store.Load().Should().Be(new AppSettings());
    }

    [Fact]
    public void Load_UndecryptableKey_KeepsOtherSettings()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """{ "PeerHost": "OFFICE-PC", "SharedKey": "bm90LWRwYXBp" }""");

        var loaded = _store.Load();

        loaded.PeerHost.Should().Be("OFFICE-PC");
        loaded.SharedKey.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
