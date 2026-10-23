using System.Text.Json;

namespace ClipboardManagerWinForm.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStore(string folder, AppSettings? settings = null)
    {
        Directory.CreateDirectory(folder);
        SettingsPath = Path.Combine(folder, "settings.json");
        Settings = settings ?? Load();
    }

    public string SettingsPath { get; }

    public AppSettings Settings { get; private set; }

    public void Save()
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, SerializerOptions));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return new AppSettings();
    }
}
