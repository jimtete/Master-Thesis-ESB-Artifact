using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OlympusServiceBus.Utils;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.WebHost.Models;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI (what AddOpenApi gives you)
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bind options
builder.Services.Configure<ContractsOptions>(
    builder.Configuration.GetSection("Contracts"));

// Needed for forwarding
builder.Services.AddHttpClient();

var app = builder.Build();

// OpenAPI endpoints in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// (Optional) keep if you want https redirect; if it causes issues in dev, remove it
// app.UseHttpsRedirection();

// ---- Runtime state ----
var contractsOptions = app.Services.GetRequiredService<IOptions<ContractsOptions>>().Value;
var contractsRoot = contractsOptions.RootPath;

if (string.IsNullOrWhiteSpace(contractsRoot))
{
    app.Logger.LogWarning("Contracts:RootPath is not set. No contracts will be loaded.");
}
else
{
    app.Logger.LogInformation("Contracts RootPath: {RootPath}", contractsRoot);
}

var routes = new Dictionary<string, PortToApiContract>(StringComparer.OrdinalIgnoreCase);

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};

// ---- Load once on startup ----
ReloadContracts();

// ---- Admin reload ----
app.MapPost("/admin/reload", () =>
{
    ReloadContracts();
    return Results.Ok(new { reloaded = true, count = routes.Count });
})
.WithName("AdminReload");

// ---- Catch-all ingress (PoC stub for now) ----
app.Map("/{**path}", async (HttpContext ctx) =>
{
    var key = $"{ctx.Request.Method} {ctx.Request.Path.Value}";
    if (!routes.TryGetValue(key, out var contract) || !contract.Enabled)
    {
        return Results.NotFound(new { error = "No contract for this route", key });
    }

    // TODO: read body -> transform -> forward to contract.Sink
    return Results.Ok(new
    {
        contractId = contract.ContractId,
        matched = true,
        sink = contract.Sink?.Endpoint
    });
})
.WithName("DynamicIngress");

app.Run();


// ---------------- Local helpers ----------------

void ReloadContracts()
{
    routes.Clear();

    if (string.IsNullOrWhiteSpace(contractsRoot) || !Directory.Exists(contractsRoot))
    {
        app.Logger.LogWarning("Contracts folder not found: {RootPath}", contractsRoot);
        return;
    }

    var files = Directory.EnumerateFiles(contractsRoot, "*.json", SearchOption.AllDirectories);

    foreach (var file in files)
    {
        try
        {
            var json = File.ReadAllText(file);

            var doc = JsonSerializer.Deserialize<PortToApiDocument>(json, jsonOpts);
            var c = doc?.PortToApi;
            if (c is null) continue;

            // If you inherit ContractBase, ensure ID exists (fallback to filename)
            c.ContractId = string.IsNullOrWhiteSpace(c.ContractId)
                ? Path.GetFileNameWithoutExtension(file)
                : c.ContractId;

            var method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant();
            var path = NormalizePath(c.Listener?.Path);

            var key = $"{method} {path}";
            routes[key] = c;

            app.Logger.LogInformation("[PortToApi:{ContractId}] Mapped {Method} {Path} -> {Sink}",
                c.ContractId, method, path, c.Sink?.Endpoint);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to load contract file: {File}", file);
        }
    }

    app.Logger.LogInformation("Reload complete. Active PortToApi routes: {Count}", routes.Count);
}

static string NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
        return "/";

    return path.StartsWith('/') ? path : "/" + path;
}
