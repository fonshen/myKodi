using System.IO;
using System.Text.Json;

namespace SimpleVideoPlayer.Models;

public class AppSettings
{
    public string VideoRootFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    public bool AutoGenerateThumbnails { get; set; } = true;
    public int ThumbnailPosition { get; set; } = 10;
    public double Volume { get; set; } = 1.0;
    public bool RememberPosition { get; set; } = true;
    public int FastForwardSeconds { get; set; } = 30;
    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimpleVideoPlayer",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}