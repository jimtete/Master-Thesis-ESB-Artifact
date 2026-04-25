using Microsoft.EntityFrameworkCore;
using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.Engine.Execution.FeedbackContracts;
using OlympusServiceBus.Engine.Execution.ApiToApi;
using OlympusServiceBus.Engine.Execution.ApiToFile;
using OlympusServiceBus.Engine.Execution.Files;
using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Execution.FileToFile;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Engine.Execution.PortToFile;
using OlympusServiceBus.Engine.Execution.Transformation;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Scheduling;
using OlympusServiceBus.Engine.Services;
using OlympusServiceBus.Engine.Workers;
using OlympusServiceBus.RuntimeState;
using OlympusServiceBus.RuntimeState.Repositories;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils;

var builder = Host.CreateApplicationBuilder(args);

var appDataDirectoryPath = GetOlympusAppDataDirectoryPath();
var runtimeStateDbPath = GetRuntimeStateDbPath(appDataDirectoryPath);
var contractsDirectoryPath = GetContractsDirectoryPath(appDataDirectoryPath);

builder.Services.AddHttpClient();
builder.Services.AddHttpClient(Constants.ENGINE_HTTP_CLIENT_NAME);
builder.Services.AddHttpClient<ApiStatusFeedbackContractExecutor>();
builder.Services.Configure<WebHostOptions>(builder.Configuration.GetSection("WebHost"));
builder.Services.AddSingleton<PortToApiReloadClient>();

// OOP pieces
builder.Services.AddScoped<IApiToApiExecutionService, ApiToApiExecutionService>();
builder.Services.AddScoped<IApiToFileExecutionService, ApiToFileExecutionService>();

builder.Services.AddScoped<IContractMessageStateRepository, ContractMessageStateRepository>();
builder.Services.AddScoped<IContractMessageStateService, ContractMessageStateService>();
builder.Services.AddScoped<IContractExecutionStateRepository, ContractExecutionStateRepository>();
builder.Services.AddScoped<IContractExecutionStateService, ContractExecutionStateService>();

builder.Services.AddScoped<IPortToApiEngine, PortToApiEngine>();
builder.Services.AddScoped<IPortToFileEngine, PortToFileEngine>();

builder.Services.AddScoped<FileSinkWriter>();
builder.Services.AddScoped<FileSinkService>();

builder.Services.AddScoped<FileToApiExecutor>();
builder.Services.AddScoped<ApiToFileExecutor>();
builder.Services.AddScoped<FileToFileExecutor>();

// FeedbackContract services
builder.Services.AddScoped<IFeedbackContractExecutor>(sp =>
    sp.GetRequiredService<ApiStatusFeedbackContractExecutor>());
builder.Services.AddScoped<FeedbackContractExecutionService>();
builder.Services.AddScoped<FeedbackContractDispatcher>();

builder.Services.AddSingleton<IContractRegistry, InMemoryContractRegistry>();
builder.Services.AddSingleton<IFeedbackContractRegistry, InMemoryFeedbackContractRegistry>();
builder.Services.AddSingleton<IContractLoader, ContractLoader>();
builder.Services.AddSingleton<ApiToApiExecutor>();

builder.Services.AddSingleton<CsvLoopProcessor>();

builder.Services.AddSingleton<ApiToApiPayloadHashProvider>();
builder.Services.AddSingleton<ApiToApiBusinessKeyProvider>();
builder.Services.AddSingleton<PortToApiBusinessKeyProvider>();
builder.Services.AddSingleton<PortToApiPayloadHashProvider>();

builder.Services.AddHostedService<ApiToApiWorker>();
builder.Services.AddHostedService<ApiToFileWorker>();
builder.Services.AddHostedService<FileToApiWorker>();
builder.Services.AddHostedService<FileToFileWorker>();
builder.Services.AddHostedService<ContractRegistryRefreshService>();
builder.Services.AddHostedService<WebHostReloadOnStartup>();

builder.Services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
builder.Services.AddSingleton<IMappingEngine, MappingEngine>();

builder.Services.AddDbContext<RuntimeStateDbContext>(options =>
    options.UseSqlite($"Data Source={runtimeStateDbPath}"));

var host = builder.Build();

EnsureRuntimeStateDatabase(host, runtimeStateDbPath);
EnsureContractsDirectory(contractsDirectoryPath);

// Load all contracts ONCE at startup
using (var scope = host.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<IContractLoader>();
    var registry = scope.ServiceProvider.GetRequiredService<IContractRegistry>();
    var feedbackContractRegistry = scope.ServiceProvider.GetRequiredService<IFeedbackContractRegistry>();

    var contracts = loader.LoadAllContracts(contractsDirectoryPath);
    ContractSchedulingBootstrapValidator.ValidateAll(contracts);
    registry.SetAllContracts(contracts);

    var feedbackContracts = loader.LoadAllFeedbackContracts(contractsDirectoryPath);
    feedbackContractRegistry.SetAllFeedbackContracts(feedbackContracts);

    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    startupLogger.LogInformation("Contracts directory: {ContractsDirectory}", contractsDirectoryPath);
    startupLogger.LogInformation("Forward contracts loaded at startup: {Count}", contracts.Count);
    startupLogger.LogInformation("FeedbackContracts loaded at startup: {Count}", feedbackContracts.Count);
}

await host.RunAsync();

static string GetOlympusAppDataDirectoryPath()
{
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    return Path.Combine(appDataPath, "OlympusServiceBus");
}

static string GetRuntimeStateDbPath(string appDataDirectoryPath)
{
    return Path.Combine(appDataDirectoryPath, "runtime-state.db");
}

static string GetContractsDirectoryPath(string appDataDirectoryPath)
{
    return Path.Combine(appDataDirectoryPath, "Contracts");
}

static void EnsureRuntimeStateDatabase(IHost host, string dbPath)
{
    var directory = Path.GetDirectoryName(dbPath);

    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeStateDbContext>();
    dbContext.Database.EnsureCreated();
}

static void EnsureContractsDirectory(string contractsDirectoryPath)
{
    Directory.CreateDirectory(contractsDirectoryPath);
}
