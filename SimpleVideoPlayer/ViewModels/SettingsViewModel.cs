using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleVideoPlayer.Services;

namespace SimpleVideoPlayer.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly RemoteControlService _remoteService;

    public event Action? OnSettingsClosed;
    public event Action? OnSettingsSaved;

    [ObservableProperty]
    private string _videoRootFolder;

    [ObservableProperty]
    private bool _autoGenerateThumbnails;

    [ObservableProperty]
    private int _thumbnailPosition;

    [ObservableProperty]
    private int _fastForwardSeconds;

    public SettingsViewModel(SettingsService settingsService, RemoteControlService remoteService)
    {
        _settingsService = settingsService;
        _remoteService = remoteService;
        var settings = _settingsService.Settings;
        _videoRootFolder = settings.VideoRootFolder;
        _autoGenerateThumbnails = settings.AutoGenerateThumbnails;
        _thumbnailPosition = settings.ThumbnailPosition;
        _fastForwardSeconds = settings.FastForwardSeconds;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择视频文件夹",
            InitialDirectory = VideoRootFolder
        };

        if (dialog.ShowDialog() == true)
        {
            VideoRootFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.VideoRootFolder = VideoRootFolder;
            settings.AutoGenerateThumbnails = AutoGenerateThumbnails;
            settings.ThumbnailPosition = ThumbnailPosition;
            settings.FastForwardSeconds = FastForwardSeconds;
        });
        OnSettingsSaved?.Invoke();
        OnSettingsClosed?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        OnSettingsClosed?.Invoke();
    }

    public override void OnNavigatedTo()
    {
        _remoteService.SetCallbacks(
            onBack: () => OnSettingsClosed?.Invoke(),
            onEnter: () => SaveSettings(),
            onUp: () => MoveFocus(-1),
            onDown: () => MoveFocus(1)
        );
    }

    public override void OnNavigatedFrom()
    {
        _remoteService.ClearCallbacks();
    }

    private void MoveFocus(int delta)
    {
    }
}