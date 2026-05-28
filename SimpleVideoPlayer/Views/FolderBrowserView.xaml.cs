using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimpleVideoPlayer.ViewModels;

namespace SimpleVideoPlayer.Views;

public partial class FolderBrowserView : UserControl
{
    public FolderBrowserView()
    {
        InitializeComponent();
        DataContextChanged += FolderBrowserView_DataContextChanged;
    }

    private void VideoListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 按键处理由 RemoteControlService 统一处理
    }

    private void VideoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VideoListBox.SelectedItem != null)
        {
            VideoListBox.ScrollIntoView(VideoListBox.SelectedItem);

            if (VideoListBox.IsKeyboardFocusWithin)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    FocusSelectedListBoxItem();
                });
            }
        }
    }

    private void VideoListBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        VideoListBox.Focus();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FolderBrowserViewModel vm)
        {
            vm.OpenSettingsCommand.Execute(null);
        }
    }

    private void FolderBrowserView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is FolderBrowserViewModel vm)
        {
            vm.OnFocusRequested += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    FocusCategory();
                });
            };

            vm.OnFocusSettingsRequested += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    VideoListBox.SelectedIndex = -1;
                    SettingsButton.Focus();
                });
            };

            vm.OnFocusListRequested += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    FocusAndSelectItem(VideoListBox.SelectedIndex >= 0 ? VideoListBox.SelectedIndex : 0);
                });
            };

            vm.OnFocusCategoriesRequested += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    VideoListBox.SelectedIndex = -1;
                    FocusCategory();
                });
            };
        }
    }

    private void FocusCategory()
    {

        if (DataContext is FolderBrowserViewModel vm && CategoryItemsControl.Items.Count > 0)
        {
            int index = vm.FocusedCategoryIndex >= 0 ? vm.FocusedCategoryIndex : 0;
            var container = CategoryItemsControl.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
            if (container != null)
            {
                var button = FindVisualChild<System.Windows.Controls.Button>(container);
                    if (button != null)
                    {
                        button.Focus();
                        return;
                    }
            }
            CategoryItemsControl.Focus();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private void SettingsButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is FolderBrowserViewModel vm)
        {
            switch (e.Key)
            {
                case Key.Down:
                case Key.Enter:
                case Key.Space:
                    System.Diagnostics.Debug.WriteLine("FolderBrowserView: SettingsButton - Down/Enter/Space pressed, moving to first item");
                    if (VideoListBox.Items.Count > 0)
                    {
                        FocusAndSelectItem(0);
                        vm.SelectedIndex = 0;
                    }
                    e.Handled = true;
                    break;
                case Key.Up:
                    System.Diagnostics.Debug.WriteLine("FolderBrowserView: SettingsButton - Up pressed, moving to last item");
                    if (VideoListBox.Items.Count > 0)
                    {
                        FocusAndSelectItem(VideoListBox.Items.Count - 1);
                        vm.SelectedIndex = VideoListBox.Items.Count - 1;
                    }
                    e.Handled = true;
                    break;
            }
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void FocusAndSelectItem(int index)
    {
        if (VideoListBox.Items.Count == 0)
        {
            SettingsButton.Focus();
            return;
        }

        index = Math.Max(0, Math.Min(index, VideoListBox.Items.Count - 1));

        // 清理之前选中项的残留样式（如果存在），以防止边框/效果未还原
        int previousIndex = VideoListBox.SelectedIndex;

        VideoListBox.SelectedIndex = index;
        VideoListBox.UpdateLayout();

        if (previousIndex >= 0 && previousIndex != index)
        {
            var prevContainer = VideoListBox.ItemContainerGenerator.ContainerFromIndex(previousIndex) as ListBoxItem;
            if (prevContainer != null)
            {
                // 查找模板内命名的 ItemBorder 并清除其样式影响
                var border = FindVisualChildByName<System.Windows.Controls.Border>(prevContainer, "ItemBorder");
                if (border != null)
                {
                    border.ClearValue(System.Windows.Controls.Border.BorderBrushProperty);
                    border.ClearValue(System.Windows.Controls.Border.EffectProperty);
                }
            }
        }

        var container = VideoListBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
        if (container != null)
        {
            container.Focus();
        }
        else
        {
            VideoListBox.Focus();
        }
    }

    private void FocusSelectedListBoxItem()
    {
        if (VideoListBox.SelectedIndex < 0)
        {
            return;
        }

        VideoListBox.UpdateLayout();
        var container = VideoListBox.ItemContainerGenerator.ContainerFromIndex(VideoListBox.SelectedIndex) as ListBoxItem;
        container?.Focus();
    }

    private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name && child is T t)
                return t;
            var result = FindVisualChildByName<T>(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
}
