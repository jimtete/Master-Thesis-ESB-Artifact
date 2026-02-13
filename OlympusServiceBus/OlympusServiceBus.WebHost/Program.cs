using Microsoft.Extensions.Options;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.WebHost.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.Configure<ContractsOptions>(
    builder.Configuration.GetSection("Contracts"));

var app = builder.Build();

var contractsOptions = app.Services.GetRequiredService<IOptions<ContractsOptions>>().Value;
var contractsRoot = contractsOptions.RootPath;

if (string.IsNullOrEmpty(contractsRoot))
{
    app.Logger.LogWarning("Contracts: RootPath is not set. No contracts will be loaded");
}
else
{
    app.Logger.LogInformation($"Contracts RootPath: {contractsRoot}");
}

var routes = new Dictionary<string, PortToApiContract>(StringComparer.OrdinalIgnoreCase);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.Run();