using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Utils;
using OlympusServiceBus.WebHost.Contracts;
using OlympusServiceBus.WebHost.Endpoints;
using OlympusServiceBus.WebHost.Models;
using OlympusServiceBus.WebHost.OpenApi;
using OlympusServiceBus.WebHost.Validation;
using OlympusServiceBus.WebHost.WebOpenApiSchema;

var builder = WebApplication.CreateBuilder(args);

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "OlympusServiceBus.WebHost", Version = "v1" });
    o.OperationFilter<PortToApiOperationFilter>();
});

// Options + HttpClient
builder.Services.Configure<ContractsOptions>(builder.Configuration.GetSection("Contracts"));

// DI for extracted services
builder.Services.AddSingleton<IPortToApiContractLoader, PortToApiContractLoader>();
builder.Services.AddSingleton<PortToApiSchemaBuilder>();
builder.Services.AddSingleton<PortToApiInboundValidator>();
builder.Services.AddSingleton<IPortToApiEndpointRegistrar, PortToApiEndpointRegistrar>();
builder.Services.AddHttpClient(Constants.ENGINE_HTTP_CLIENT_NAME);
builder.Services.AddSingleton<IPortToApiEngine, PortToApiEngine>();

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
var loader = app.Services.GetRequiredService<IPortToApiContractLoader>();
var contracts = loader.Load(contractsRoot);

app.MapAdminContracts(contracts);

var registrar = app.Services.GetRequiredService<IPortToApiEndpointRegistrar>();
registrar.Register(app, contracts);

app.Run();