using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using SimpleVideoPlayer.Models;
using SimpleVideoPlayer.Services;

namespace SimpleVideoPlayer.ViewModels;

public partial class VideoPlayerViewModel : ViewModelBase, IDisposable
{
    private readonly RemoteControlService _remoteService;
    private readonly SettingsService _settingsService;
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private bool _disposed;

    public event Action? OnPlaybackEnded;
    public event Action? OnBackRequested;
    public event Action? OnSeekFeedbackRequested;

    [ObservableProperty]
    private VideoFile? _currentVideo;

    [ObservableProperty]
    private string _videoTitle = string.Empty;

    [ObservableProperty]
    private TimeSpan _currentPosition;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private string _positionText = "00:00 / 00:00";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _showControls = true;

    [ObservableProperty]
    private double _volume = 100;

    [ObservableProperty]
    private bool _isMuted;

    public LibVLC? LibVLC => _libVLC;
    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public VideoPlayerViewModel(RemoteControlService remoteService, SettingsService settingsService)
    {
        System.Diagnostics.Debug.WriteLine("VideoPlayerViewModel: Constructor called");
        _remoteService = remoteService;
        _settingsService = settingsService;
        InitializeLibVLC();
    }

    private void InitializeLibVLC()
    {
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);

        _mediaPlayer.Volume = (int)(_settingsService.Settings.Volume * 100);
        Volume = _mediaPlayer.Volume;

        _mediaPlayer.Playing += (s, e) =>
        {
            IsPlaying = true;
            Duration = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
            UpdatePositionText();
        };

        _mediaPlayer.Paused += (s, e) =>
        {
            IsPlaying = false;
        };

        _mediaPlayer.Stopped += (s, e) =>
        {
            IsPlaying = false;
        };

        _mediaPlayer.EndReached += (s, e) =>
        {
            IsPlaying = false;
            OnPlaybackEnded?.Invoke();
        };

        _mediaPlayer.PositionChanged += (s, e) =>
        {
            if (_mediaPlayer.IsPlaying)
            {
                CurrentPosition = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
                Progress = e.Position * 100;
                UpdatePositionText();
            }
        };

        SetupRemoteCallbacks();
    }

    private void UpdatePositionText()
    {
        var current = CurrentPosition.TotalHours >= 1
            ? $"{(int)CurrentPosition.TotalHours}:{CurrentPosition.Minutes:D2}:{CurrentPosition.Seconds:D2}"
            : $"{CurrentPosition.Minutes:D2}:{CurrentPosition.Seconds:D2}";

        var total = Duration.TotalHours >= 1
            ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
            : $"{Duration.Minutes:D2}:{Duration.Seconds:D2}";

        PositionText = $"{current} / {total}";
    }

    public void LoadVideo(VideoFile video)
    {
        CurrentVideo = video;
        VideoTitle = video.DisplayName;
        CurrentPosition = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        Progress = 0;
        PositionText = "00:00 / 00:00";
        ShowControls = false;

        if (_mediaPlayer != null && !string.IsNullOrEmpty(video.FilePath))
        {
            _mediaPlayer.Stop();
            using var media = new Media(_libVLC!, video.FilePath, FromType.FromPath);
            _mediaPlayer.Play(media);
        }
    }

    private void SetupRemoteCallbacks()
    {
        System.Diagnostics.Debug.WriteLine("VideoPlayerViewModel: Remote callbacks configured");
        _remoteService.SetCallbacks(
            onUp: () => { },
            onDown: () => { },
            onLeft: () => { SeekRelative(-_settingsService.Settings.FastForwardSeconds); },
            onRight: () => { SeekRelative(_settingsService.Settings.FastForwardSeconds); },
            onEnter: () => { TogglePlayPause(); },
            onBack: () => { System.Diagnostics.Debug.WriteLine("VideoPlayerViewModel: Back - StopAndGoBack"); StopAndGoBack(); },
            onPlayPause: () => { TogglePlayPause(); },
            onStop: () => { StopAndGoBack(); },
            onVolumeUp: () => { AdjustVolume(10); },
            onVolumeDown: () => { AdjustVolume(-10); },
            onMute: () => { ToggleMute(); },
            onFastForward: () => { SeekRelative(_settingsService.Settings.FastForwardSeconds); },
            onRewind: () => { SeekRelative(-_settingsService.Settings.FastForwardSeconds); }
        );
    }

    public void TogglePlayPause()
    {
        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            IsPlaying = false;
            ShowControls = true;
        }
        else
        {
            _mediaPlayer.Play();
            IsPlaying = true;
            ShowControls = false;
        }
    }

    public void SeekRelative(int seconds)
    {
        if (_mediaPlayer == null) return;

        var newTime = _mediaPlayer.Time + (seconds * 1000);
        newTime = Math.Max(0, Math.Min(newTime, _mediaPlayer.Length));
        _mediaPlayer.Time = newTime;
        ShowControls = true;
        OnSeekFeedbackRequested?.Invoke();
    }

    private void AdjustVolume(int delta)
    {
        if (_mediaPlayer == null) return;

        var newVolume = _mediaPlayer.Volume + delta;
        newVolume = Math.Max(0, Math.Min(200, newVolume));
        _mediaPlayer.Volume = newVolume;
        Volume = newVolume;
        _settingsService.UpdateSettings(s => s.Volume = newVolume / 100.0);
    }

    private void ToggleMute()
    {
        if (_mediaPlayer == null) return;

        IsMuted = !IsMuted;
        _mediaPlayer.Mute = IsMuted;
    }

    [RelayCommand]
    private void StopAndGoBack()
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Stop();
        }
        IsPlaying = false;
        OnBackRequested?.Invoke();
    }

    public override void OnNavigatedTo()
    {
        SetupRemoteCallbacks();
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Volume = (int)(_settingsService.Settings.Volume * 100);
        }
    }

    public override void OnNavigatedFrom()
    {
        _remoteService.ClearCallbacks();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _disposed = true;
        }
    }
}
