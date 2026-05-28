using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleVideoPlayer.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public virtual void OnNavigatedTo() { }
    public virtual void OnNavigatedFrom() { }
}