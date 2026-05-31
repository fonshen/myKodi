using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimpleVideoPlayer.ViewModels;

namespace SimpleVideoPlayer.Views;

public partial class VideoPlayerView : UserControl
{
    private static readonly TimeSpan DefaultAutoHideDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SeekFeedbackAutoHideDelay = TimeSpan.FromSeconds(3);

    private VideoPlayerViewModel? ViewModel => DataContext as VideoPlayerViewModel;
    private bool _controlsVisible;
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
            Interval = DefaultAutoHideDelay
        };
        _hideControlsTimer.Tick += (s, e) =>
        {
            if (ViewModel?.IsPlaying == true && (_controlsVisible || ViewModel.ShowProgressBar))
            {
                HideControls();
            }
        };
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        AttachMediaPlayer();
        _controlsVisible = ViewModel?.ShowControls == true;
        UpdatePopupSize();
        ResetHideControlsTimer();
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

    private void EnsureControlsPopupOpen()
    {
        UpdatePopupSize();
        if (!ControlsPopup.IsOpen)
        {
            ControlsPopup.IsOpen = true;
        }
    }

    private void UpdatePopupSize()
    {
        if (VideoGrid.ActualWidth > 0)
        {
            PopupRoot.Width = VideoGrid.ActualWidth;
        }

        if (VideoGrid.ActualHeight > 0)
        {
            PopupRoot.Height = VideoGrid.ActualHeight;
        }
    }

    private void VideoGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePopupSize();
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
            ViewModel.ShowProgressBar = false;
        }

        _controlsVisible = true;
        EnsureControlsPopupOpen();
        UpdatePlayPauseButton();
    }

    private void HideControls()
    {
        if (ViewModel?.IsPlaying == true)
        {
            ViewModel.ShowControls = false;
            ViewModel.ShowProgressBar = false;
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

    private void ShowProgressFeedback()
    {
        if (ViewModel != null)
        {
            ViewModel.ShowControls = true;
            ViewModel.ShowProgressBar = false;
        }

        _controlsVisible = true;
        EnsureControlsPopupOpen();
    }

    private void UpdatePlayPauseButton()
    {
        PlayPauseButton.Content = new TextBlock
        {
            Text = ViewModel?.IsPlaying == true ? "||" : "▶",
            FontSize = 64
        };
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
        if (_hideControlsTimer != null)
        {
            _hideControlsTimer.Interval = delay ?? DefaultAutoHideDelay;
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

    private void VideoGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != VideoGrid && e.OriginalSource != VideoView)
        {
            return;
        }

        ShowControls();
        ResetHideControlsTimer();
    }

    private void VideoGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != VideoGrid && e.OriginalSource != VideoView)
        {
            return;
        }

        ViewModel?.StopAndGoBackCommand?.Execute(null);
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
            ShowProgressFeedback();
            ResetHideControlsTimer(SeekFeedbackAutoHideDelay);
        });
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPlayerViewModel.IsPlaying))
        {
            Dispatcher.BeginInvoke(UpdatePlayPauseButton);
        }
        else if (e.PropertyName == nameof(VideoPlayerViewModel.ShowControls) ||
                 e.PropertyName == nameof(VideoPlayerViewModel.ShowProgressBar))
        {
            Dispatcher.BeginInvoke(() =>
            {
                var viewModel = ViewModel;
                _controlsVisible = viewModel?.ShowControls == true;
                if (viewModel?.ShowControls == true || viewModel?.ShowProgressBar == true)
                {
                    EnsureControlsPopupOpen();
                }
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
