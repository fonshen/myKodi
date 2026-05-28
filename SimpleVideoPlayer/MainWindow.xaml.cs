using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleVideoPlayer.ViewModels;

namespace SimpleVideoPlayer;

public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _isCursorHidden;
        private System.Windows.Threading.DispatcherTimer? _cursorHideTimer;

        public MainWindow(MainViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("MainWindow: Constructor started");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("MainWindow: InitializeComponent completed");
            _viewModel = viewModel;
            DataContext = _viewModel;
            Loaded += MainWindow_Loaded;
            Focusable = true;
            MouseMove += MainWindow_MouseMove;
            System.Diagnostics.Debug.WriteLine("MainWindow: Constructor completed, Cursor=None");
            // 初始化为隐藏光标
            this.Cursor = Cursors.None;
            Mouse.OverrideCursor = Cursors.None;

            // 设置用于在一段时间无鼠标活动后重新隐藏光标的定时器
            _cursorHideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _cursorHideTimer.Tick += (s, e) =>
            {
                // 超时后隐藏光标
                Mouse.OverrideCursor = Cursors.None;
                this.Cursor = Cursors.None;
                _isCursorHidden = true;
                _cursorHideTimer?.Stop();
            };
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
            this.Activate();
            this.Focus();
            System.Windows.Input.Keyboard.Focus(this);
            // 窗口加载时默认隐藏光标，直到检测到鼠标移动
            _isCursorHidden = true;
            Mouse.OverrideCursor = Cursors.None;
            this.Cursor = Cursors.None;
            await _viewModel.InitializeAsync();
        }

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow_MouseMove: _isCursorHidden={_isCursorHidden}");
            // 鼠标移动时显示光标并启动/重置隐藏定时器
            if (_isCursorHidden)
            {
                Mouse.OverrideCursor = null;
                Cursor = Cursors.Arrow;
                _isCursorHidden = false;
                System.Diagnostics.Debug.WriteLine("MainWindow_MouseMove: Cursor shown");
            }

            if (_cursorHideTimer != null)
            {
                _cursorHideTimer.Stop();
                _cursorHideTimer.Start();
            }
        }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
    }

    private void ExitFullScreen()
    {
        WindowStyle = WindowStyle.SingleBorderWindow;
        WindowState = WindowState.Normal;
        Topmost = false;
        Mouse.OverrideCursor = null;
        Cursor = Cursors.Arrow;
        _isCursorHidden = false;
    }
}
