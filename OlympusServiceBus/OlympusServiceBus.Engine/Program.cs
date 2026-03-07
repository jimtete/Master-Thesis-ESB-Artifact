using Microsoft.EntityFrameworkCore;
using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Services;
using OlympusServiceBus.Engine.Workers;
using OlympusServiceBus.RuntimeState;
using OlympusServiceBus.RuntimeState.Repositories;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils;

var builder = Host.CreateApplicationBuilder(args);

var runtimeStateDbPath = GetRuntimeStateDbPath();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient(Constants.ENGINE_HTTP_CLIENT_NAME);

// OOP pieces
builder.Services.AddScoped<IApiToApiExecutionService, ApiToApiExecutionService>();

builder.Services.AddScoped<IContractMessageStateRepository, ContractMessageStateRepository>();
builder.Services.AddScoped<IContractMessageStateService, ContractMessageStateService>();
builder.Services.AddScoped<IContractExecutionStateRepository, ContractExecutionStateRepository>();
builder.Services.AddScoped<IContractExecutionStateService, ContractExecutionStateService>();

builder.Services.AddSingleton<IContractRegistry, InMemoryContractRegistry>();
builder.Services.AddSingleton<IContractLoader, ContractLoader>();
builder.Services.AddSingleton<ApiToApiExecutor>();
builder.Services.AddSingleton<IPortToApiEngine, PortToApiEngine>();

builder.Services.AddSingleton<CsvLoopProcessor>();
builder.Services.AddSingleton<FileToApiExecutor>();

builder.Services.AddHostedService<ApiToApiWorker>();
builder.Services.AddHostedService<FileToApiWorker>();
builder.Services.AddHostedService<WebHostReloadOnStartup>();

builder.Services.AddDbContext<RuntimeStateDbContext>(options =>
    options.UseSqlite($"Data Source={runtimeStateDbPath}"));

var host = builder.Build();

EnsureRuntimeStateDatabase(host, runtimeStateDbPath);

// Load all contracts ONCE at startup
using (var scope = host.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<IContractLoader>();
    var registry = scope.ServiceProvider.GetRequiredService<IContractRegistry>();

    var contracts = loader.LoadAllContracts("Configuration");
    registry.SetAllContracts(contracts);
}

await host.RunAsync();

static string GetRuntimeStateDbPath()
{
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var appFolder = Path.Combine(appDataPath, "OlympusServiceBus");
    return Path.Combine(appFolder, "runtime-state.db");
}

static void EnsureRuntimeStateDatabase(IHost host, string dbPath)
{
    var directory = Path.GetDirectoryName(dbPath);

    if (!string.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);

    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeStateDbContext>();
    dbContext.Database.EnsureCreated();
}