using System.Windows;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OlympusServiceBus.Engine.Execution.AntiContracts;
using OlympusServiceBus.Engine.Execution.ApiToApi;
using OlympusServiceBus.Engine.Execution.ApiToFile;
using OlympusServiceBus.Engine.Execution.Files;
using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Execution.FileToFile;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Engine.Execution.PortToFile;
using OlympusServiceBus.Engine.Execution.Transformation;
using OlympusServiceBus.Engine.Services;
using OlympusServiceBus.RuntimeState;
using OlympusServiceBus.RuntimeState.Repositories;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils;
using OlympusServiceBusApplication.Services.AppSettingsService;
using OlympusServiceBusApplication.Services.ContractsService;
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

        EnsureRuntimeStateDatabase(_serviceProvider);

        var configuratorWindow = _serviceProvider.GetRequiredService<ConfiguratorWindow>();
        configuratorWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var appDataDirectoryPath = GetOlympusAppDataDirectoryPath();
        var runtimeStateDbPath = Path.Combine(appDataDirectoryPath, "runtime-state.db");

        services.AddLogging();
        services.AddHttpClient();
        services.AddHttpClient(Constants.ENGINE_HTTP_CLIENT_NAME);
        services.AddHttpClient<ApiStatusAntiContractExecutor>();

        services.AddDbContext<RuntimeStateDbContext>(options =>
            options.UseSqlite($"Data Source={runtimeStateDbPath}"));

        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<MainWindow>();
        
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<IFolderPickerService, FolderPickerService>();

        services.AddTransient<IContractsWorkspaceService, ContractsWorkspaceService>();
        services.AddTransient<IContractsExplorerService, ContractsExplorerService>();
        services.AddTransient<IManualContractExecutionService, ManualContractExecutionService>();
        services.AddTransient<ConfiguratorViewModel>();
        services.AddTransient<ConfiguratorWindow>();

        services.AddScoped<IApiToApiExecutionService, ApiToApiExecutionService>();
        services.AddScoped<IApiToFileExecutionService, ApiToFileExecutionService>();
        services.AddScoped<FileToApiExecutor>();
        services.AddScoped<FileToFileExecutor>();
        services.AddScoped<ApiToFileExecutor>();
        services.AddSingleton<ApiToApiExecutor>();

        services.AddScoped<FileSinkWriter>();
        services.AddScoped<FileSinkService>();
        services.AddScoped<IPortToApiEngine, PortToApiEngine>();
        services.AddScoped<IPortToFileEngine, PortToFileEngine>();

        services.AddScoped<IContractMessageStateRepository, ContractMessageStateRepository>();
        services.AddScoped<IContractMessageStateService, ContractMessageStateService>();
        services.AddScoped<IContractExecutionStateRepository, ContractExecutionStateRepository>();
        services.AddScoped<IContractExecutionStateService, ContractExecutionStateService>();

        services.AddSingleton<ApiToApiPayloadHashProvider>();
        services.AddSingleton<ApiToApiBusinessKeyProvider>();
        services.AddSingleton<PortToApiBusinessKeyProvider>();
        services.AddSingleton<PortToApiPayloadHashProvider>();
        services.AddSingleton<CsvLoopProcessor>();
        services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
        services.AddSingleton<IMappingEngine, MappingEngine>();

        services.AddSingleton<IAntiContractRegistry, InMemoryAntiContractRegistry>();
        services.AddScoped<IAntiContractExecutor>(sp =>
            sp.GetRequiredService<ApiStatusAntiContractExecutor>());
        services.AddScoped<AntiContractExecutionService>();
        services.AddScoped<AntiContractDispatcher>();
    }

    private static string GetOlympusAppDataDirectoryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OlympusServiceBus");
    }

    private static void EnsureRuntimeStateDatabase(IServiceProvider serviceProvider)
    {
        var appDataDirectoryPath = GetOlympusAppDataDirectoryPath();
        Directory.CreateDirectory(appDataDirectoryPath);

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeStateDbContext>();
        dbContext.Database.EnsureCreated();
    }
}
