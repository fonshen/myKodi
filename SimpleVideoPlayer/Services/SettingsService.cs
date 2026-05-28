using SimpleVideoPlayer.Models;

namespace SimpleVideoPlayer.Services;

public class SettingsService
{
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        _settings = AppSettings.Load();
    }

    public void Save()
    {
        _settings.Save();
    }

    public void UpdateSettings(Action<AppSettings> updateAction)
    {
        updateAction(_settings);
        Save();
    }
}