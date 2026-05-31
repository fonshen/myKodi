using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleVideoPlayer.Models;
using SimpleVideoPlayer.Services;

namespace SimpleVideoPlayer.ViewModels;

public partial class FolderBrowserViewModel : ViewModelBase
    {
        private readonly VideoLibraryService _videoLibrary;
        private readonly SettingsService _settingsService;
        private readonly RemoteControlService _remoteService;
        private CancellationTokenSource? _thumbnailCts;
        private VideoFile? _lastSelectedVideo;
        private ObservableCollection<VideoFolder> _allFolders = new();
        private bool _isFocusOnCategories = true;

        public bool IsFocusOnCategories => _isFocusOnCategories;

        private void SetFocusOnCategories(bool value)
        {
            if (_isFocusOnCategories == value) return;

            _isFocusOnCategories = value;
            OnPropertyChanged(nameof(IsFocusOnCategories));
        }

        public event Action<VideoFile>? OnVideoSelected;
        public event Action? OnSettingsRequested;
        public event Action? OnExitRequested;
        internal event Action? OnFocusRequested;
        internal event Action? OnFocusSettingsRequested;
        internal event Action? OnFocusListRequested;
        internal event Action? OnFocusCategoriesRequested;

        public void RequestFocus() => OnFocusRequested?.Invoke();
        public void RequestFocusOnList() => OnFocusListRequested?.Invoke();

        [ObservableProperty]
        private ObservableCollection<NavigationItem> _items = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private int _selectedCategoryIndex;

        [ObservableProperty]
        private int _focusedCategoryIndex;

        [ObservableProperty]
        private int _selectedIndex;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private double _cardWidth = 340;

        [ObservableProperty]
        private double _cardHeight = 225;

        public double TitleMaxWidth => CardWidth - 40;

        [ObservableProperty]
        private string _pageTitle = "视频";

    private ObservableCollection<VideoFolder> _rootFolders = new();

    [RelayCommand]
    private void SelectItem(NavigationItem item)
    {
        if (item == null) return;
        
        if (item.Type == ItemType.Video && item.Video != null)
        {
            _lastSelectedVideo = item.Video;
            SelectedIndex = Items.IndexOf(item);
            OnVideoSelected?.Invoke(item.Video);
        }
    }

    public FolderBrowserViewModel(
        VideoLibraryService videoLibrary,
        SettingsService settingsService,
        RemoteControlService remoteService)
    {
        System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Constructor called");
        _videoLibrary = videoLibrary;
        _settingsService = settingsService;
        _remoteService = remoteService;
        UpdateCardSize();
    }

    public void UpdateCardSize()
    {
        var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
        var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
        int maxCardsPerRow;
        double itemMargin = 12; // each card Button has Margin=12
        double wrapPanelMargin = 20; // WrapPanel has Margin="20" (left+right total 40)
        double containerPadding = 100; // original reserved space (side panels / spacing)
        double listItemBorder = 4; // CardListBoxItem border thickness

        // 横屏优先显示 4 列卡片
        if (screenWidth > screenHeight)
        {
            maxCardsPerRow = 4;
        }
        else
        {
            if (screenWidth >= 1600)
                maxCardsPerRow = 4;
            else if (screenWidth >= 1280)
                maxCardsPerRow = 3;
            else if (screenWidth >= 800)
                maxCardsPerRow = 2;
            else
                maxCardsPerRow = 1;
        }

        // 计算可用宽度，考虑容器预留、WrapPanel 外边距，以及每个卡片的边框宽度和卡片间距
        double availableWidth = screenWidth - containerPadding - (wrapPanelMargin * 2);
        double gapBetweenItems = itemMargin * 2; // 因为每个按钮左右都有 margin
        double totalGaps = gapBetweenItems * (maxCardsPerRow - 1);
        double totalBorders = (listItemBorder * 2) * maxCardsPerRow; // 每个 ListBoxItem 的 BorderThickness 左右各占用
        double cardWidth = (availableWidth - totalGaps - totalBorders) / maxCardsPerRow;
        double cardHeight = cardWidth * 0.66;
        
        CardWidth = Math.Max(150, Math.Min(400, cardWidth));
        CardHeight = Math.Max(100, Math.Min(270, cardHeight));
    }

    public void SetFolders(ObservableCollection<VideoFolder> folders)
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts = new CancellationTokenSource();
        
        _allFolders = folders;
        Categories.Clear();

        var rootPath = _settingsService.Settings.VideoRootFolder;
        if (Directory.Exists(rootPath))
        {
            var dirInfo = new DirectoryInfo(rootPath);
            foreach (var subDir in dirInfo.GetDirectories())
            {
                var subFolders = new ObservableCollection<VideoFolder>();
                ScanFolderRecursive(subDir.FullName, subFolders);
                var categoryFolder = subFolders.FirstOrDefault();
                if (categoryFolder != null && categoryFolder.Videos.Count > 0)
                {
                    Categories.Add(categoryFolder.FolderName);
                }
            }
        }

        if (Categories.Count == 0)
        {
            foreach (var folder in folders)
            {
                if (folder.Videos.Count > 0)
                {
                    Categories.Add(folder.FolderName);
                }
                foreach (var sub in folder.SubFolders)
                {
                    if (sub.Videos.Count > 0)
                    {
                        Categories.Add(sub.FolderName);
                    }
                }
            }
        }

        SelectedCategoryIndex = Categories.Count > 0 ? 0 : -1;
        FocusedCategoryIndex = Categories.Count > 0 ? 0 : -1;
        LoadCategoryVideos();

        System.Diagnostics.Debug.WriteLine($"SetFolders: Categories.Count={Categories.Count}, Items.Count={Items.Count}");

        SelectedIndex = -1;
        OnFocusRequested?.Invoke();

        // 同步已有的缩略图和时长到当前显示的导航项，确保首个卡片显示
        foreach (var item in Items)
        {
            if (item.Video != null)
            {
                if (!string.IsNullOrEmpty(item.Video.ThumbnailPath) && string.IsNullOrEmpty(item.ThumbnailPath))
                {
                    item.ThumbnailPath = item.Video.ThumbnailPath;
                }
                var durText = item.Video.DurationText;
                if (!string.IsNullOrEmpty(durText) && item.Subtitle != durText)
                {
                    item.Subtitle = durText;
                }
            }
        }

        // 传递当前卡片宽度以便生成匹配尺寸的缩略图并异步更新时长
        _ = GenerateThumbnailsInBackgroundAsync(_thumbnailCts.Token, (int)CardWidth);
    }

    private void ScanFolderRecursive(string path, ObservableCollection<VideoFolder> folders)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            var folder = new VideoFolder
            {
                FolderPath = path,
                FolderName = dirInfo.Name
            };

            foreach (var file in dirInfo.GetFiles())
            {
                var ext = file.Extension.ToLowerInvariant();
                if (ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov" ||
                    ext == ".wmv" || ext == ".flv" || ext == ".webm" || ext == ".m4v")
                {
                    var videoFile = new VideoFile
                    {
                        FilePath = file.FullName,
                        FileName = file.Name,
                        FolderPath = file.DirectoryName ?? string.Empty,
                        FileSize = file.Length,
                        LastModified = file.LastWriteTime
                    };
                    folder.Videos.Add(videoFile);
                }
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                var subFolders = new ObservableCollection<VideoFolder>();
                ScanFolderRecursive(subDir.FullName, subFolders);
                foreach (var sub in subFolders)
                {
                    folder.SubFolders.Add(sub);
                }
            }

            folder.VideoCount = folder.Videos.Count + folder.SubFolders.Sum(f => f.VideoCount);

            if (folder.VideoCount > 0)
            {
                folders.Add(folder);
            }
        }
        catch { }
    }

    public VideoFile? SelectNextVideoAfter(VideoFile? currentVideo)
    {
        if (currentVideo == null || Items.Count == 0)
        {
            return null;
        }

        var currentIndex = Items.ToList().FindIndex(i => i.Video == currentVideo);
        if (currentIndex < 0)
        {
            currentIndex = SelectedIndex;
        }

        for (var index = currentIndex + 1; index < Items.Count; index++)
        {
            var nextVideo = Items[index].Video;
            if (Items[index].Type == ItemType.Video && nextVideo != null)
            {
                _lastSelectedVideo = nextVideo;
                SelectedIndex = index;
                SetFocusOnCategories(false);
                return nextVideo;
            }
        }

        _lastSelectedVideo = currentVideo;
        if (currentIndex >= 0 && currentIndex < Items.Count)
        {
            SelectedIndex = currentIndex;
        }

        return null;
    }

    private void LoadCategoryVideos()
    {
        Items.Clear();

        if (SelectedCategoryIndex < 0 || SelectedCategoryIndex >= Categories.Count)
            return;

        var categoryName = Categories[SelectedCategoryIndex];
        var folder = FindFolderByName(_allFolders, categoryName);

        if (folder != null)
        {
            CurrentPath = folder.FolderPath;
            PageTitle = folder.FolderName;

            foreach (var video in folder.Videos)
            {
                Items.Add(new NavigationItem
                {
                    Type = ItemType.Video,
                    Title = video.DisplayName,
                    Video = video,
                    ThumbnailPath = video.ThumbnailPath,
                    Subtitle = video.DurationText
                });
            }
        }
    }

    private VideoFolder? FindFolderByName(IEnumerable<VideoFolder> folders, string name)
    {
        foreach (var folder in folders)
        {
            if (folder.FolderName == name)
                return folder;
            var found = FindFolderByName(folder.SubFolders, name);
            if (found != null)
                return found;
        }
        return null;
    }

    [RelayCommand]
    private void SelectCategory(int index)
    {
        if (index >= 0 && index < Categories.Count)
        {
            SelectedCategoryIndex = index;
            FocusedCategoryIndex = index;
            LoadCategoryVideos();
            SetFocusOnCategories(true);
            OnFocusRequested?.Invoke();
        }
    }

    private async Task GenerateThumbnailsInBackgroundAsync(CancellationToken cancellationToken, int thumbnailWidth)
    {
        await Task.Delay(500, cancellationToken);
        
        var allVideos = _allFolders.SelectMany(f => GetAllVideos(f)).ToList();
        
        foreach (var video in allVideos)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            if (string.IsNullOrEmpty(video.ThumbnailPath))
            {
                var thumbnail = await _videoLibrary.GenerateThumbnailAsync(video.FilePath, cancellationToken, thumbnailWidth);
                if (!string.IsNullOrEmpty(thumbnail) && File.Exists(thumbnail))
                {
                    video.ThumbnailPath = thumbnail;
                    UpdateNavigationItemThumbnail(video);
                }
            }
            // 异步获取时长，如果还没有时长则尝试获取并更新导航项的 Subtitle
            if (video.Duration == TimeSpan.Zero)
            {
                var durationStr = await _videoLibrary.GetVideoDurationAsync(video.FilePath);
                if (!string.IsNullOrEmpty(durationStr))
                {
                    if (TimeSpan.TryParse(durationStr, out var ts))
                    {
                        video.Duration = ts;
                        UpdateNavigationItemDuration(video);
                    }
                    else
                    {
                        var parts = durationStr.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var s))
                        {
                            video.Duration = new TimeSpan(0,0,m,s);
                            UpdateNavigationItemDuration(video);
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<VideoFile> GetAllVideos(VideoFolder folder)
    {
        foreach (var video in folder.Videos)
            yield return video;
        
        foreach (var subFolder in folder.SubFolders)
        {
            foreach (var video in GetAllVideos(subFolder))
                yield return video;
        }
    }

    private void UpdateNavigationItemThumbnail(VideoFile video)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var item = Items.FirstOrDefault(i => i.Video == video);
            if (item != null)
            {
                item.ThumbnailPath = video.ThumbnailPath;
            }
        });
    }

    private void UpdateNavigationItemDuration(VideoFile video)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var item = Items.FirstOrDefault(i => i.Video == video);
            if (item != null)
            {
                item.Subtitle = video.DurationText;
            }
        });
    }

    private int GetItemsPerRow()
    {
        double screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
        double cardWidth = CardWidth + 24;
        return Math.Max(1, (int)((screenWidth - 100) / cardWidth));
    }

    [RelayCommand]
    private void MoveSelection(int delta)
    {
        if (Items.Count == 0) return;

        int newIndex = SelectedIndex + delta;
        if (newIndex < 0) newIndex = 0;
        if (newIndex >= Items.Count) newIndex = Items.Count - 1;
        SelectedIndex = newIndex;
    }

    [RelayCommand]
    private void MoveSelectionVertical(int delta)
    {
        if (Items.Count == 0) return;

        int itemsPerRow = GetItemsPerRow();
        int newIndex = SelectedIndex + (delta * itemsPerRow);

        if (delta < 0 && newIndex < 0)
        {
            SetFocusOnCategories(true);
            SelectedIndex = -1;
            OnFocusCategoriesRequested?.Invoke();
            return;
        }

        if (delta > 0 && newIndex >= Items.Count)
        {
            return;
        }

        if (newIndex < 0) newIndex = 0;
        if (newIndex >= Items.Count) newIndex = Items.Count - 1;
        SelectedIndex = newIndex;
    }

    private void EnterSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count) return;

        var item = Items[SelectedIndex];
        if (item.Type == ItemType.Video && item.Video != null)
        {
            // 记录最后选中的视频，以便返回时恢复列表焦点
            _lastSelectedVideo = item.Video;
            OnVideoSelected?.Invoke(item.Video);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        OnSettingsRequested?.Invoke();
    }

    public override void OnNavigatedTo()
    {
        System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: OnNavigatedTo - Setting up remote callbacks");
        // 如果之前有选中的视频，优先将焦点恢复到列表并选中该项；否则默认聚焦到分类
        if (_lastSelectedVideo != null)
        {
            var idx = Items.ToList().FindIndex(i => i.Video == _lastSelectedVideo);
            if (idx >= 0)
            {
                SelectedIndex = idx;
                SetFocusOnCategories(false);
                // 请求视图将焦点设置到列表项
                OnFocusListRequested?.Invoke();
            }
            else
            {
                SetFocusOnCategories(true);
            }
        }
        else
        {
            SetFocusOnCategories(true);
        }
        _remoteService.SetCallbacks(
            onUp: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Up"); HandleUp(); },
            onDown: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Down"); HandleDown(); },
            onLeft: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Left"); HandleLeft(); },
            onRight: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Right"); HandleRight(); },
            onEnter: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Enter"); HandleEnter(); },
            onBack: () => {
                System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Back");
                if (_isFocusOnCategories)
                {
                    System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Back - Ignored (focus on categories)");
                }
                else
                {
                    SetFocusOnCategories(true);
                    OnFocusCategoriesRequested?.Invoke();
                }
            },
            onFastForward: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: FastForward"); if (!_isFocusOnCategories) MoveSelectionCommand.Execute(5); },
            onRewind: () => { System.Diagnostics.Debug.WriteLine("FolderBrowserViewModel: Rewind"); if (!_isFocusOnCategories) MoveSelectionCommand.Execute(-5); }
        );
    }

    private void HandleUp()
    {
        if (_isFocusOnCategories)
        {
            OnFocusSettingsRequested?.Invoke();
        }
        else
        {
            MoveSelectionVerticalCommand.Execute(-1);
        }
    }

    private void HandleDown()
    {
        if (_isFocusOnCategories)
        {
            if (Items.Count > 0)
            {
                SetFocusOnCategories(false);
                SelectedIndex = 0;
                OnFocusListRequested?.Invoke();
            }
        }
        else
        {
            MoveSelectionVerticalCommand.Execute(1);
        }
    }

    private void HandleLeft()
    {
        if (_isFocusOnCategories)
        {
            PreviousCategory();
        }
        else
        {
            MoveSelectionCommand.Execute(-1);
        }
    }

    private void HandleRight()
    {
        if (_isFocusOnCategories)
        {
            NextCategory();
        }
        else
        {
            MoveSelectionCommand.Execute(1);
        }
    }

    private void HandleEnter()
    {
        if (_isFocusOnCategories)
        {
            if (Items.Count > 0)
            {
                SelectedCategoryIndex = FocusedCategoryIndex;
                LoadCategoryVideos();
                SetFocusOnCategories(false);
                SelectedIndex = 0;
                OnFocusListRequested?.Invoke();
            }
        }
        else
        {
            EnterSelected();
        }
    }

    private void PreviousCategory()
    {
        if (Categories.Count == 0) return;
        int newIndex = FocusedCategoryIndex - 1;
        if (newIndex < 0) newIndex = Categories.Count - 1;
        FocusedCategoryIndex = newIndex;
        SelectedCategoryIndex = newIndex;
        LoadCategoryVideos();
        OnFocusRequested?.Invoke();
    }

    private void NextCategory()
    {
        if (Categories.Count == 0) return;
        int newIndex = FocusedCategoryIndex + 1;
        if (newIndex >= Categories.Count) newIndex = 0;
        FocusedCategoryIndex = newIndex;
        SelectedCategoryIndex = newIndex;
        LoadCategoryVideos();
        OnFocusRequested?.Invoke();
    }

    public override void OnNavigatedFrom()
    {
        _remoteService.ClearCallbacks();
    }
}

public enum ItemType
{
    Folder,
    Video,
    Back,
    Root
}

public class NavigationItem : INotifyPropertyChanged
{
    private string? _thumbnailPath;

    public ItemType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    
    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set
        {
            if (_thumbnailPath != value)
            {
                _thumbnailPath = value;
                OnPropertyChanged(nameof(ThumbnailPath));
            }
        }
    }
    
    public VideoFolder? Folder { get; set; }
    public VideoFile? Video { get; set; }
    public string? FolderPath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
