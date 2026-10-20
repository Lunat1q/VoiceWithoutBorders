using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VoiceWithoutBorders.Features.Settings;

/// <summary>
/// JSON file in the user's AppData. The shared key is encrypted with DPAPI, so only this Windows user
/// on this machine can read it back.
/// </summary>
public sealed class JsonSettingsStore(string path, ILogger<JsonSettingsStore> logger) : ISettingsStore
{
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceWithoutBorders", "settings.json");

    private static readonly byte[] Entropy = "VoiceWithoutBorders.SharedKey"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public AppSettings Load()
    {
        if (!File.Exists(path))
            return new AppSettings();
        try
        {
            var stored = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
            return stored with { SharedKey = Unprotect(stored.SharedKey) };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Settings file {Path} is unreadable, using defaults", path);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings with { SharedKey = Protect(settings.SharedKey) }, JsonOptions));
    }

    private static string Protect(string value) =>
        value.Length == 0
            ? ""
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));

    private string Unprotect(string value)
    {
        if (value.Length == 0)
            return "";
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Copied from another user or machine: keep the rest of the settings, ask for the key again.
            logger.LogWarning("Stored shared key could not be decrypted; it must be entered again");
            return "";
        }
    }
}
