using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Workers;
using OlympusServiceBus.Utils;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHttpClient(Constants.ENGINE_HTTP_CLIENT_NAME);

// OOP pieces
builder.Services.AddSingleton<IContractRegistry, InMemoryContractRegistry>();
builder.Services.AddSingleton<IContractLoader, ContractLoader>();
builder.Services.AddSingleton<ApiToApiExecutor>();
builder.Services.AddSingleton<IPortToApiEngine, PortToApiEngine>();

builder.Services.AddSingleton<CsvLoopProcessor>();
builder.Services.AddSingleton<FileToApiExecutor>();

builder.Services.AddHostedService<ApiToApiWorker>();
builder.Services.AddHostedService<FileToApiWorker>();
builder.Services.AddHostedService<WebHostReloadOnStartup>();

var host = builder.Build();

// Load all contracts ONCE at startup (as you wanted)
using (var scope = host.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<IContractLoader>();
    var registry = scope.ServiceProvider.GetRequiredService<IContractRegistry>();

    var contracts = loader.LoadAllContracts("Configuration");
    registry.SetAllContracts(contracts);
}

await host.RunAsync();