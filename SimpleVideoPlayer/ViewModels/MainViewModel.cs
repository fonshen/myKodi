using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleVideoPlayer.Models;
using SimpleVideoPlayer.Services;

namespace SimpleVideoPlayer.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly VideoLibraryService _videoLibrary;
    private readonly SettingsService _settingsService;
    private ViewModelBase? _previousView;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _title = "简易视频播放器";

    [ObservableProperty]
    private ObservableCollection<VideoFolder> _rootFolders = new();

    public FolderBrowserViewModel FolderBrowserVM { get; }
    public VideoPlayerViewModel PlayerVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public MainViewModel(
        VideoLibraryService videoLibrary,
        SettingsService settingsService,
        FolderBrowserViewModel folderBrowserVM,
        VideoPlayerViewModel playerVM,
        SettingsViewModel settingsVM)
    {
        _videoLibrary = videoLibrary;
        _settingsService = settingsService;
        FolderBrowserVM = folderBrowserVM;
        PlayerVM = playerVM;
        SettingsVM = settingsVM;

        FolderBrowserVM.OnVideoSelected += videoFile =>
        {
            RunOnUi(() =>
            {
                NavigateTo(PlayerVM);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                {
                    PlayerVM.LoadVideo(videoFile);
                });
            });
        };

        PlayerVM.OnPlaybackEnded += () =>
        {
            RunOnUi(() =>
            {
                var nextVideo = FolderBrowserVM.SelectNextVideoAfter(PlayerVM.CurrentVideo);
                if (nextVideo != null)
                {
                    PlayerVM.LoadVideo(nextVideo);
                    return;
                }

                NavigateTo(FolderBrowserVM);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    FolderBrowserVM.RequestFocusOnList();
                }));
            });
        };

        PlayerVM.OnBackRequested += () =>
        {
            RunOnUi(() =>
            {
                NavigateTo(FolderBrowserVM);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    FolderBrowserVM.RequestFocusOnList();
                }));
            });
        };

        SettingsVM.OnSettingsSaved += async () =>
        {
            await ReloadVideoLibrary();
        };

        SettingsVM.OnSettingsClosed += () =>
        {
            NavigateTo(FolderBrowserVM);
        };

        FolderBrowserVM.OnSettingsRequested += () =>
        {
            NavigateTo(SettingsVM);
        };

        FolderBrowserVM.OnExitRequested += () =>
        {
            System.Windows.Application.Current?.MainWindow?.Close();
        };

        NavigateTo(FolderBrowserVM);
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private void NavigateTo(ViewModelBase viewModel)
    {
        if (_currentView != null)
        {
            _currentView.OnNavigatedFrom();
        }
        
        _previousView = _currentView;
        CurrentView = viewModel;
        viewModel.OnNavigatedTo();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "正在加载...";

        try
        {
            var settings = _settingsService.Settings;
            RootFolders = await _videoLibrary.ScanFolderAsync(settings.VideoRootFolder);
            FolderBrowserVM.SetFolders(RootFolders);
            StatusMessage = $"已加载 {RootFolders.Count} 个文件夹";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainViewModel.InitializeAsync: Exception: {ex.Message}");
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadVideoLibrary()
    {
        IsLoading = true;
        StatusMessage = "正在重新加载...";

        try
        {
            var settings = _settingsService.Settings;
            RootFolders = await _videoLibrary.ScanFolderAsync(settings.VideoRootFolder);
            FolderBrowserVM.SetFolders(RootFolders);
            StatusMessage = $"已重新加载 {RootFolders.Count} 个文件夹";
        }
        catch (Exception ex)
        {
            StatusMessage = $"重新加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
