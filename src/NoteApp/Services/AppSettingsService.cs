using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteApp.Services;

public sealed class AppSettingsService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsFilePath { get; }
    public AppSettings Current { get; private set; }

    public AppSettingsService()
    {
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());

        SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NoteApp",
            "settings.json");

        Current = Load();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, _serializerOptions));
        Current = settings;
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return AppSettings.Default;

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions) ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }
}
