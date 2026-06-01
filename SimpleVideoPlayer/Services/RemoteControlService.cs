using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SimpleVideoPlayer.Services;

public class RemoteControlService
{
    private readonly DispatcherTimer _keyRepeatTimer;
    private readonly HashSet<Key> _pressedKeys = new();
    private Key _currentKey;
    private bool _isProcessing;
    private const int KeyRepeatDelay = 500;
    private const int KeyRepeatInterval = 100;

    public event Action? OnUp;
    public event Action? OnDown;
    public event Action? OnLeft;
    public event Action? OnRight;
    public event Action? OnEnter;
    public event Action? OnBack;
    public event Action? OnPlayPause;
    public event Action? OnStop;
    public event Action? OnVolumeUp;
    public event Action? OnVolumeDown;
    public event Action? OnMute;
    public event Action? OnFastForward;
    public event Action? OnRewind;

    public RemoteControlService()
    {
        _keyRepeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(KeyRepeatInterval)
        };
        _keyRepeatTimer.Tick += KeyRepeatTimer_Tick;
        SetupDefaultMappings();
    }

    private void SetupDefaultMappings()
    {
        OnUp += () => { };
        OnDown += () => { };
        OnLeft += () => { };
        OnRight += () => { };
        OnEnter += () => { };
        OnBack += () => { };
        OnPlayPause += () => { };
        OnStop += () => { };
        OnVolumeUp += () => { };
        OnVolumeDown += () => { };
        OnMute += () => { };
        OnFastForward += () => { };
        OnRewind += () => { };
    }

    public void Start()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.PreviewKeyDown += MainWindow_PreviewKeyDown;
                Application.Current.MainWindow.PreviewKeyUp += MainWindow_PreviewKeyUp;
                Application.Current.MainWindow.KeyDown += MainWindow_KeyDown;
            }
        });
    }

    public void Stop()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.PreviewKeyDown -= MainWindow_PreviewKeyDown;
                Application.Current.MainWindow.PreviewKeyUp -= MainWindow_PreviewKeyUp;
                Application.Current.MainWindow.KeyDown -= MainWindow_KeyDown;
            }
        });
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ProcessKeyDown(e.Key, e);
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        ProcessKeyUp(e.Key);
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        ProcessKeyDown(e.Key, e);
    }

    private void ProcessKeyDown(Key key, KeyEventArgs e)
    {
        if (IsSystemVolumeKey(key))
        {
            return;
        }

        if (_pressedKeys.Contains(key))
        {
            return;
        }

        _pressedKeys.Add(key);
        _currentKey = key;
        _isProcessing = false;

        ExecuteKeyAction(key);

        if (!_isProcessing)
        {
            _keyRepeatTimer.Stop();
            _keyRepeatTimer.Interval = TimeSpan.FromMilliseconds(KeyRepeatDelay);
            _keyRepeatTimer.Start();
        }

        e.Handled = true;
    }

    private void ProcessKeyUp(Key key)
    {
        if (IsSystemVolumeKey(key))
        {
            return;
        }

        _pressedKeys.Remove(key);
        _keyRepeatTimer.Stop();

        if (_currentKey == key)
        {
            _isProcessing = false;
        }
    }

    private void KeyRepeatTimer_Tick(object? sender, EventArgs e)
    {
        if (_pressedKeys.Contains(_currentKey))
        {
            _keyRepeatTimer.Interval = TimeSpan.FromMilliseconds(KeyRepeatInterval);
            _isProcessing = true;
            ExecuteKeyAction(_currentKey);
        }
    }

    private static bool IsSystemVolumeKey(Key key)
    {
        return key is Key.VolumeUp or Key.VolumeDown or Key.VolumeMute;
    }

    private void ExecuteKeyAction(Key key)
    {
        // key handling
        Action? action = key switch
        {
            Key.Up or Key.W => OnUp,
            Key.Down or Key.S => OnDown,
            Key.Left or Key.A => OnLeft,
            Key.Right or Key.D => OnRight,
            Key.Enter or Key.Space => OnEnter,
            Key.Escape or Key.Back or Key.BrowserBack => OnBack,
            Key.MediaPlayPause => OnPlayPause,
            Key.MediaStop => OnStop,
            Key.VolumeUp => OnVolumeUp,
            Key.VolumeDown => OnVolumeDown,
            Key.VolumeMute => OnMute,
            Key.Add or Key.OemPlus => OnFastForward,
            Key.Subtract or Key.OemMinus => OnRewind,
            Key.PageUp => OnUp,
            Key.PageDown => OnDown,
            Key.Home => OnBack,
            _ => null
        };

        action?.Invoke();
    }

    public void SetCallbacks(
        Action? onUp = null,
        Action? onDown = null,
        Action? onLeft = null,
        Action? onRight = null,
        Action? onEnter = null,
        Action? onBack = null,
        Action? onPlayPause = null,
        Action? onStop = null,
        Action? onVolumeUp = null,
        Action? onVolumeDown = null,
        Action? onMute = null,
        Action? onFastForward = null,
        Action? onRewind = null)
    {
        ResetKeyState();

        if (onUp != null) OnUp = onUp;
        if (onDown != null) OnDown = onDown;
        if (onLeft != null) OnLeft = onLeft;
        if (onRight != null) OnRight = onRight;
        if (onEnter != null) OnEnter = onEnter;
        if (onBack != null) OnBack = onBack;
        if (onPlayPause != null) OnPlayPause = onPlayPause;
        if (onStop != null) OnStop = onStop;
        if (onVolumeUp != null) OnVolumeUp = onVolumeUp;
        if (onVolumeDown != null) OnVolumeDown = onVolumeDown;
        if (onMute != null) OnMute = onMute;
        if (onFastForward != null) OnFastForward = onFastForward;
        if (onRewind != null) OnRewind = onRewind;
    }

    public void ClearCallbacks()
    {
        ResetKeyState();
        SetupDefaultMappings();
    }

    private void ResetKeyState()
    {
        _keyRepeatTimer.Stop();
        _pressedKeys.Clear();
        _isProcessing = false;
        _currentKey = Key.None;
    }
}