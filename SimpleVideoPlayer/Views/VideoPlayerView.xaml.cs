using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimpleVideoPlayer.ViewModels;

namespace SimpleVideoPlayer.Views;

public partial class VideoPlayerView : UserControl
{
    private VideoPlayerViewModel? ViewModel => DataContext as VideoPlayerViewModel;
    private bool _controlsVisible = true;
    private System.Windows.Threading.DispatcherTimer? _hideControlsTimer;

    public VideoPlayerView()
    {
        InitializeComponent();
        SetupHideControlsTimer();
        DataContextChanged += VideoPlayerView_DataContextChanged;
    }

    private void SetupHideControlsTimer()
    {
        _hideControlsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _hideControlsTimer.Tick += (s, e) =>
        {
            if (_controlsVisible && ViewModel?.IsPlaying == true)
            {
                HideControls();
            }
        };
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        AttachMediaPlayer();
        ShowControls();
        _hideControlsTimer?.Start();
    }

    private void AttachMediaPlayer()
    {
        if (ViewModel?.MediaPlayer != null)
        {
            if (VideoView.MediaPlayer != null && !ReferenceEquals(VideoView.MediaPlayer, ViewModel.MediaPlayer))
            {
                VideoView.MediaPlayer = null;
            }

            VideoView.MediaPlayer = ViewModel.MediaPlayer;
        }
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _hideControlsTimer?.Stop();
        ControlsPopup.IsOpen = false;
        VideoView.MediaPlayer = null;

        if (ViewModel != null)
        {
            ViewModel.OnSeekFeedbackRequested -= ViewModel_OnSeekFeedbackRequested;
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void ShowControls()
    {
        if (ViewModel != null)
        {
            ViewModel.ShowControls = true;
        }

        ControlsOverlay.Visibility = Visibility.Visible;
        _controlsVisible = true;
        UpdatePlayPauseButton();
    }

    private void HideControls()
    {
        if (ViewModel?.IsPlaying == true)
        {
            ControlsOverlay.Visibility = Visibility.Collapsed;
            ViewModel.ShowControls = false;
            _controlsVisible = false;
        }
    }

    private void ToggleControls()
    {
        if (_controlsVisible)
            HideControls();
        else
            ShowControls();
    }

    private void UpdatePlayPauseButton()
    {
        PlayPauseButton.Content = new TextBlock { Text = "||", FontSize = 64 };
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.TogglePlayPause();
        UpdatePlayPauseButton();
        if (ViewModel?.IsPlaying == true)
        {
            HideControls();
        }
    }

    private void FastForwardButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.SeekRelative(30);
    }

    private void RewindButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.SeekRelative(-30);
    }

    private void ResetHideControlsTimer(TimeSpan? delay = null)
    {
        _hideControlsTimer?.Stop();
        if (delay.HasValue && _hideControlsTimer != null)
        {
            _hideControlsTimer.Interval = delay.Value;
        }

        if (ViewModel?.IsPlaying == true)
        {
            _hideControlsTimer?.Start();
        }
    }

    private void ClickCapture_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        if (!_controlsVisible)
        {
            ShowControls();
        }
        else
        {
            ViewModel?.TogglePlayPause();
            UpdatePlayPauseButton();
            if (ViewModel?.IsPlaying == true)
            {
                HideControls();
            }
        }
    }

    private void VideoPlayerView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is VideoPlayerViewModel oldViewModel)
        {
            oldViewModel.OnSeekFeedbackRequested -= ViewModel_OnSeekFeedbackRequested;
            oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is VideoPlayerViewModel newViewModel)
        {
            newViewModel.OnSeekFeedbackRequested += ViewModel_OnSeekFeedbackRequested;
            newViewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (IsLoaded)
            {
                AttachMediaPlayer();
            }
        }
    }

    private void ViewModel_OnSeekFeedbackRequested()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ShowControls();
            ResetHideControlsTimer(TimeSpan.FromSeconds(1));
        });
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPlayerViewModel.IsPlaying))
        {
            Dispatcher.BeginInvoke(UpdatePlayPauseButton);
        }
        else if (e.PropertyName == nameof(VideoPlayerViewModel.ShowControls))
        {
            Dispatcher.BeginInvoke(() =>
            {
                _controlsVisible = ViewModel?.ShowControls == true;
            });
        }
    }

    private void ClickCapture_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        ViewModel?.StopAndGoBackCommand?.Execute(null);
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void EnterFullScreen()
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Maximized;
            window.Topmost = true;
            window.Cursor = Cursors.None;
        }
    }

    private void ExitFullScreen()
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.WindowState = WindowState.Normal;
            window.Topmost = false;
            window.Cursor = Cursors.Arrow;
        }
    }

    public void ClosePlayer()
    {
        ExitFullScreen();
    }
}
