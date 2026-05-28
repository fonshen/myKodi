using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleVideoPlayer.Services;
using SimpleVideoPlayer.ViewModels;
using SimpleVideoPlayer.Views;

namespace SimpleVideoPlayer;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("App: Application_Startup started");
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        ServiceProvider = _serviceProvider;

        System.Diagnostics.Debug.WriteLine("App: Getting MainWindow from DI");
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        System.Diagnostics.Debug.WriteLine("App: Showing MainWindow");
        mainWindow.Show();

        var remoteService = _serviceProvider.GetRequiredService<RemoteControlService>();
        remoteService.Start();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<VideoLibraryService>();
        services.AddSingleton<RemoteControlService>();
        services.AddSingleton<SettingsService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<FolderBrowserViewModel>();
        services.AddTransient<VideoPlayerViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<MainWindow>();
        services.AddTransient<FolderBrowserView>();
        services.AddTransient<VideoPlayerView>();
        services.AddTransient<SettingsView>();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _serviceProvider?.GetService<RemoteControlService>()?.Stop();
        _serviceProvider?.Dispose();
    }
}
