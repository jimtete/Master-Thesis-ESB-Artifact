using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OlympusServiceBus.Engine.Execution.Files;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Engine.Execution.PortToFile;
using OlympusServiceBus.RuntimeState;
using OlympusServiceBus.RuntimeState.Repositories;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils;
using OlympusServiceBus.WebHost.Contracts;
using OlympusServiceBus.WebHost.Endpoints;
using OlympusServiceBus.WebHost.Models;
using OlympusServiceBus.WebHost.OpenApi;
using OlympusServiceBus.WebHost.Validation;
using OlympusServiceBus.WebHost.WebOpenApiSchema;

var builder = WebApplication.CreateBuilder(args);
var runtimeStateDbPath = GetRuntimeStateDbPath();

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "OlympusServiceBus.WebHost", Version = "v1" });
    o.OperationFilter<PortToApiOperationFilter>();
});

// Options + HttpClient
builder.Services.Configure<ContractsOptions>(builder.Configuration.GetSection("Contracts"));

// DI For Runtime State
builder.Services.AddDbContext<RuntimeStateDbContext>(options =>
    options.UseSqlite($"Data Source={runtimeStateDbPath}"));
builder.Services.AddScoped<IContractMessageStateRepository, ContractMessageStateRepository>();
builder.Services.AddScoped<IContractMessageStateService, ContractMessageStateService>();

// DI for extracted services
builder.Services.AddSingleton<IPortToApiContractLoader, PortToApiContractLoader>();
builder.Services.AddSingleton<IPortToFileContractLoader, PortToFileContractLoader>();

builder.Services.AddSingleton<PortToApiSchemaBuilder>();
builder.Services.AddSingleton<PortToApiInboundValidator>();

builder.Services.AddSingleton<IPortToApiEndpointRegistrar, PortToApiEndpointRegistrar>();
builder.Services.AddSingleton<IPortToFileEndpointRegistrar, PortToFileEndpointRegistrar>();

builder.Services.AddSingleton<PortToApiBusinessKeyProvider>();
builder.Services.AddSingleton<PortToApiPayloadHashProvider>();

builder.Services.AddScoped<FileSinkWriter>();
builder.Services.AddScoped<FileSinkService>();

builder.Services.AddHttpClient(Constants.ENGINE_HTTP_CLIENT_NAME);

builder.Services.AddScoped<IPortToApiEngine, PortToApiEngine>();
builder.Services.AddScoped<IPortToFileEngine, PortToFileEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OlympusServiceBus.WebHost v1"));
}

// Contracts root
var contractsOptions = app.Services.GetRequiredService<IOptions<ContractsOptions>>().Value;
var contractsRoot = contractsOptions.RootPath;

if (string.IsNullOrWhiteSpace(contractsRoot))
    app.Logger.LogWarning("Contracts:RootPath is not set. No contracts will be loaded.");
else
    app.Logger.LogInformation("Contracts RootPath: {RootPath}", contractsRoot);

// Load + register
var portToApiLoader = app.Services.GetRequiredService<IPortToApiContractLoader>();
var portToApiContracts = portToApiLoader.Load(contractsRoot);

var portToFileLoader = app.Services.GetRequiredService<IPortToFileContractLoader>();
var portToFileContracts = portToFileLoader.Load(contractsRoot);

app.MapAdminContracts(portToApiContracts);

var portToApiRegistrar = app.Services.GetRequiredService<IPortToApiEndpointRegistrar>();
portToApiRegistrar.Register(app, portToApiContracts);

var portToFileRegistrar = app.Services.GetRequiredService<IPortToFileEndpointRegistrar>();
portToFileRegistrar.Register(app, portToFileContracts);

app.Run();

static string GetRuntimeStateDbPath()
{
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var appFolder = Path.Combine(appDataPath, "OlympusServiceBus");
    return Path.Combine(appFolder, "runtime-state.db");
}