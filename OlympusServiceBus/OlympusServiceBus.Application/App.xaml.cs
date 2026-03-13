using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OlympusServiceBusApplication.Services.AppSettingsService;
using OlympusServiceBusApplication.Services.ContractsWorkspaceService;
using OlympusServiceBusApplication.Services.FolderPickerService;
using OlympusServiceBusApplication.ViewModels;
using OlympusServiceBusApplication.Views;
using Application = System.Windows.Application;

namespace OlympusServiceBusApplication;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var services = new ServiceCollection();
        
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<MainWindow>();
        
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<IFolderPickerService, FolderPickerService>();

        services.AddTransient<IContractsWorkspaceService, ContractsWorkspaceService>();
        services.AddTransient<ConfiguratorViewModel>();
        services.AddTransient<ConfiguratorWindow>();
    }
}