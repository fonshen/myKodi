using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
            if (ViewModel?.IsPlaying == true && _controlsVisible)
            {
                HideControls();
            }
        };
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        AttachMediaPlayer();
        _controlsVisible = ViewModel?.ShowControls == true;
        UpdateControlsPopupLayout();
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
        UpdateControlsPopupLayout();
        if (!ControlsPopup.IsOpen)
        {
            ControlsPopup.IsOpen = true;
        }

        Dispatcher.BeginInvoke(new Action(UpdateControlsPopupLayout), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateControlsPopupLayout()
    {
        const double horizontalMargin = 0;
        const double bottomMargin = 35;

        var videoWidth = Math.Max(0, VideoGrid.ActualWidth);
        var videoHeight = Math.Max(0, VideoGrid.ActualHeight);

        var panelWidth = videoWidth > horizontalMargin * 2
            ? videoWidth - (horizontalMargin * 2)
            : 800;
        ControlsPanel.Width = panelWidth;

        ControlsPanel.Measure(new Size(panelWidth, double.PositiveInfinity));
        var panelHeight = ControlsPanel.ActualHeight > 0
            ? ControlsPanel.ActualHeight
            : ControlsPanel.DesiredSize.Height;

        ControlsPopup.HorizontalOffset = horizontalMargin;
        ControlsPopup.VerticalOffset = Math.Max(0, videoHeight - panelHeight - bottomMargin);
    }

    private void VideoGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateControlsPopupLayout();
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
        _controlsVisible = true;
        UpdatePlayPauseButton();
        UpdateControlsPopupLayout();

        if (ViewModel != null)
        {
            ViewModel.ShowControls = true;
        }

        EnsureControlsPopupOpen();
    }

    private void HideControls()
    {
        if (ViewModel?.IsPlaying == true)
        {
            ViewModel.ShowControls = false;
            ControlsPopup.IsOpen = false;
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
        PlayPauseButton.Content = ViewModel?.IsPlaying == true
            ? CreatePauseIcon()
            : CreatePlayIcon();
    }

    private static UIElement CreatePauseIcon()
    {
        var icon = new Grid
        {
            Width = 42,
            Height = 50
        };

        icon.ColumnDefinitions.Add(new ColumnDefinition());
        icon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        icon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        icon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        icon.ColumnDefinitions.Add(new ColumnDefinition());

        var leftBar = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(2)
        };
        Grid.SetColumn(leftBar, 1);

        var rightBar = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(2)
        };
        Grid.SetColumn(rightBar, 3);

        icon.Children.Add(leftBar);
        icon.Children.Add(rightBar);
        return icon;
    }

    private static UIElement CreatePlayIcon()
    {
        return new Path
        {
            Width = 48,
            Height = 54,
            Stretch = Stretch.Uniform,
            Fill = Brushes.White,
            Margin = new Thickness(5, 0, 0, 0),
            Data = Geometry.Parse("M 0 0 L 0 54 L 46 27 Z")
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
            ShowControls();
            ResetHideControlsTimer(SeekFeedbackAutoHideDelay);
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
                if (_controlsVisible)
                {
                    EnsureControlsPopupOpen();
                }
                else
                {
                    ControlsPopup.IsOpen = false;
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
