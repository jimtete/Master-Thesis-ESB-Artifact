using OlympusServiceBus.Engine;
using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.Engine.Helpers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();

// OOP pieces
builder.Services.AddSingleton<IContractRegistry, InMemoryContractRegistry>();
builder.Services.AddSingleton<IContractLoader, ContractLoader>();
builder.Services.AddSingleton<ApiToApiExecutor>();

builder.Services.AddHostedService<ApiToApiWorker>();
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