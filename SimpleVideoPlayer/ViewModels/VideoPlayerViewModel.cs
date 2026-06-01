using System.Windows;
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
    private const int FixedPlayerVolume = 100;

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _isStartingPlayback;
    private bool _showControlsOnNextPause;
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
    private bool _showControls;

    [ObservableProperty]
    private double _volume = FixedPlayerVolume;

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

        SetFixedPlayerVolume();

        _mediaPlayer.Playing += (s, e) =>
        {
            RunOnUi(() =>
            {
                _isStartingPlayback = false;
                _showControlsOnNextPause = false;
                IsPlaying = true;
                UpdatePlaybackPositionFromPlayer();
                ShowControls = false;
            });
        };

        _mediaPlayer.Paused += (s, e) =>
        {
            RunOnUi(() =>
            {
                IsPlaying = false;
                var shouldShowControls = _showControlsOnNextPause && !_isStartingPlayback;
                _showControlsOnNextPause = false;
                UpdatePlaybackPositionFromPlayer();
                ShowControls = shouldShowControls;
            });
        };

        _mediaPlayer.Stopped += (s, e) =>
        {
            RunOnUi(() =>
            {
                IsPlaying = false;
            });
        };

        _mediaPlayer.EndReached += (s, e) =>
        {
            RunOnUi(() =>
            {
                if (_isStartingPlayback)
                {
                    IsPlaying = false;
                    ShowControls = false;
                    return;
                }

                _isStartingPlayback = false;
                _showControlsOnNextPause = false;
                IsPlaying = false;
                OnPlaybackEnded?.Invoke();
            });
        };

        _mediaPlayer.PositionChanged += (s, e) =>
        {
            RunOnUi(() => UpdatePlaybackPositionFromPlayer(e.Position));
        };

        SetupRemoteCallbacks();
    }

    private void SetFixedPlayerVolume()
    {
        if (_mediaPlayer == null)
        {
            return;
        }

        _mediaPlayer.Mute = false;
        _mediaPlayer.Volume = FixedPlayerVolume;
        IsMuted = false;
        Volume = FixedPlayerVolume;
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private void UpdatePlaybackPositionFromPlayer(float? reportedPosition = null)
    {
        if (_mediaPlayer == null)
        {
            return;
        }

        UpdatePlaybackPosition(_mediaPlayer.Time, _mediaPlayer.Length, reportedPosition);
    }

    private void UpdatePlaybackPosition(long playerTime, long playerLength, float? reportedPosition = null)
    {
        var length = Math.Max(0, playerLength);
        var time = Math.Max(0, playerTime);

        if (length > 0)
        {
            time = Math.Min(time, length);
        }

        CurrentPosition = TimeSpan.FromMilliseconds(time);
        Duration = TimeSpan.FromMilliseconds(length);

        if (length > 0)
        {
            Progress = Math.Max(0, Math.Min(100, (reportedPosition ?? (float)((double)time / length)) * 100));
        }
        else
        {
            Progress = 0;
        }

        UpdatePositionText();
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
        _isStartingPlayback = true;
        _showControlsOnNextPause = false;
        ShowControls = false;

        if (_mediaPlayer != null && !string.IsNullOrEmpty(video.FilePath))
        {
            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _currentMedia = new Media(_libVLC!, video.FilePath, FromType.FromPath);
            _mediaPlayer.Play(_currentMedia);
            SetFixedPlayerVolume();
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
            onFastForward: () => { SeekRelative(_settingsService.Settings.FastForwardSeconds); },
            onRewind: () => { SeekRelative(-_settingsService.Settings.FastForwardSeconds); }
        );
    }

    public void TogglePlayPause()
    {
        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
        {
            _isStartingPlayback = false;
            _showControlsOnNextPause = true;
            _mediaPlayer.Pause();
            IsPlaying = false;
            UpdatePlaybackPositionFromPlayer();
            ShowControls = true;
        }
        else
        {
            _isStartingPlayback = false;
            _showControlsOnNextPause = false;
            SetFixedPlayerVolume();
            _mediaPlayer.Play();
            IsPlaying = true;
            ShowControls = false;
        }
    }

    public void SeekRelative(int seconds)
    {
        if (_mediaPlayer == null) return;

        _isStartingPlayback = false;
        var length = Math.Max(0, _mediaPlayer.Length);
        var currentTime = Math.Max(0, _mediaPlayer.Time);
        var newTime = currentTime + (seconds * 1000L);

        if (length > 0)
        {
            newTime = Math.Min(newTime, length);
        }

        newTime = Math.Max(0, newTime);
        _mediaPlayer.Time = newTime;
        UpdatePlaybackPosition(newTime, length);
        ShowControls = true;
        OnSeekFeedbackRequested?.Invoke();
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
        SetFixedPlayerVolume();
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
            _currentMedia?.Dispose();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _disposed = true;
        }
    }
}
