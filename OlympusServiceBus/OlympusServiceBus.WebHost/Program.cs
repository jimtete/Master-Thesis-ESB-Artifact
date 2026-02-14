using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OlympusServiceBus.Utils;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.WebHost.Models;
using OlympusServiceBus.WebHost.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "OlympusServiceBus.WebHost", Version = "v1" });

    // Reads PortToApiOpenApiMetadata from endpoints and injects RequestBody schema
    o.OperationFilter<PortToApiOperationFilter>();
});

// Options + HttpClient
builder.Services.Configure<ContractsOptions>(builder.Configuration.GetSection("Contracts"));
builder.Services.AddHttpClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OlympusServiceBus.WebHost v1");
    });
}

// ---- Contracts root ----
var contractsOptions = app.Services.GetRequiredService<IOptions<ContractsOptions>>().Value;
var contractsRoot = contractsOptions.RootPath;

if (string.IsNullOrWhiteSpace(contractsRoot))
    app.Logger.LogWarning("Contracts:RootPath is not set. No contracts will be loaded.");
else
    app.Logger.LogInformation("Contracts RootPath: {RootPath}", contractsRoot);

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};

// ---- Load & map contracts ON STARTUP ----
var contracts = LoadPortToApiContracts(contractsRoot);

app.MapGet("/admin/contracts", () =>
{
    return Results.Ok(contracts.Select(c => new
    {
        c.ContractId,
        enabled = c.Enabled,
        method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant(),
        path = NormalizePath(c.Listener?.Path),
        sink = c.Sink?.Endpoint
    }));
})
.WithName("AdminContracts");

MapContracts(app, contracts);

app.Run();


// ---------------- Helpers ----------------

List<PortToApiContract> LoadPortToApiContracts(string? rootPath)
{
    var list = new List<PortToApiContract>();

    if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
    {
        app.Logger.LogWarning("Contracts folder not found: {RootPath}", rootPath);
        return list;
    }

    foreach (var file in Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories))
    {
        try
        {
            var json = File.ReadAllText(file);

            var doc = JsonSerializer.Deserialize<PortToApiDocument>(json, jsonOpts);
            var c = doc?.PortToApi;
            if (c is null) continue;

            c.ContractId = string.IsNullOrWhiteSpace(c.ContractId)
                ? Path.GetFileNameWithoutExtension(file)
                : c.ContractId;

            list.Add(c);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to load contract file: {File}", file);
        }
    }

    app.Logger.LogInformation("Loaded PortToApi contracts: {Count}", list.Count);
    return list;
}

void MapContracts(WebApplication webApp, List<PortToApiContract> loaded)
{
    var usedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var c in loaded)
    {
        if (!c.Enabled) continue;

        var method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant();
        var path = NormalizePath(c.Listener?.Path);

        var routeKey = $"{method} {path}";
        if (!usedRoutes.Add(routeKey))
        {
            app.Logger.LogWarning("[{Contract}] Duplicate route ignored: {Route}", c.ContractId, routeKey);
            continue;
        }

        // IMPORTANT: endpoint names must be globally unique
        var endpointName = $"PortToApi_{c.ContractId}";

        var requestSchema = BuildSchemaFromContract(c);

        app.Logger.LogInformation("[{Contract}] Mapping {Method} {Path} -> {Sink}",
            c.ContractId, method, path, c.Sink?.Endpoint);

        webApp.MapMethods(path, new[] { method }, async (HttpContext ctx) =>
            {
                // Read JSON body (works for any method; will be empty for GET, etc.)
                JsonObject inboundObj;

                try
                {
                    if (ctx.Request.ContentLength is > 0)
                    {
                        var node = await JsonNode.ParseAsync(ctx.Request.Body);
                        inboundObj = node as JsonObject
                                    ?? throw new InvalidOperationException("Request body must be a JSON object.");
                    }
                    else
                    {
                        inboundObj = new JsonObject();
                    }
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = "Invalid JSON body", detail = ex.Message });
                }

                var errors = ValidateInbound(inboundObj, c);
                if (errors.Count > 0)
                    return Results.BadRequest(new { error = "Payload validation failed", errors });

                // TODO: transform (Type1/Type2) + forward to sink
                return Results.Ok(new { contractId = c.ContractId, accepted = true });
            })
            .WithName(endpointName)
            // This is what your PortToApiOperationFilter should read to set the OpenAPI RequestBody schema:
            .WithMetadata(new PortToApiOpenApiMetadata(c.ContractId, requestSchema));
    }
}

static OpenApiSchema BuildSchemaFromContract(PortToApiContract c)
{
    var fields = c.Request?.Fields ?? Array.Empty<PortToApiRequestField>();

    var props = new Dictionary<string, IOpenApiSchema>(StringComparer.OrdinalIgnoreCase);
    var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var f in fields)
    {
        var name = f.FieldName;
        if (string.IsNullOrWhiteSpace(name)) continue;

        props[name] = new OpenApiSchema
        {
            Type = f.Type switch
            {
                JsonFieldType.String  => JsonSchemaType.String,
                JsonFieldType.Integer => JsonSchemaType.Integer,
                JsonFieldType.Number  => JsonSchemaType.Number,
                JsonFieldType.Boolean => JsonSchemaType.Boolean,
                JsonFieldType.Object  => JsonSchemaType.Object,
                JsonFieldType.Array   => JsonSchemaType.Array,
                _                     => JsonSchemaType.String
            },
            Format = string.IsNullOrWhiteSpace(f.Format) ? null : f.Format
        };

        if (f.Required) required.Add(name);
    }

    return new OpenApiSchema
    {
        Type = JsonSchemaType.Object,
        Properties = props,
        Required = required
    };
}


static OpenApiSchema BuildSchemaFromFieldNames(IEnumerable<string> names)
{
    var props = new Dictionary<string, IOpenApiSchema>(StringComparer.OrdinalIgnoreCase);
    var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var name in names)
    {
        if (string.IsNullOrWhiteSpace(name)) continue;

        props[name] = new OpenApiSchema { Type = JsonSchemaType.String };
        required.Add(name);
    }

    return new OpenApiSchema
    {
        Type = JsonSchemaType.Object,
        Properties = props,
        Required = required
    };
}

static IEnumerable<string> InferInboundFieldsFromMappings(PortToApiContract c)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // If your PortToApiContract uses the same mapping type as ApiToApi (ApiFieldConfig),
    // this will work. If you have a different mapping type, adjust accordingly.
    var mappings = c.Mappings ?? Array.Empty<ApiFieldConfig>();

    foreach (var m in mappings)
    {
        switch (m.TransformationType)
        {
            case TransformationType.Direct:
            case TransformationType.Split:
                if (!string.IsNullOrWhiteSpace(m.SourceFieldName))
                    set.Add(m.SourceFieldName);
                break;

            case TransformationType.Join:
                if (m.SourceFields is { Length: > 0 })
                    foreach (var sf in m.SourceFields)
                        if (!string.IsNullOrWhiteSpace(sf))
                            set.Add(sf);
                break;
        }
    }

    return set;
}

static string NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return "/";
    return path.StartsWith('/') ? path : "/" + path;
}

static List<string> ValidateInbound(JsonObject inbound, PortToApiContract c)
{
    var errors = new List<string>();

    // Prefer explicit Request.Fields if present
    var fields = c.Request?.Fields ?? Array.Empty<PortToApiRequestField>();

    if (fields.Length == 0)
    {
        // Fallback: infer required fields from mappings (PoC behavior)
        var inferred = InferInboundFieldsFromMappings(c);
        foreach (var name in inferred)
        {
            var exists = inbound.Any(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                errors.Add($"Missing required field: {name}");
        }

        return errors;
    }

    foreach (var f in fields)
    {
        var name = f.FieldName;
        if (string.IsNullOrWhiteSpace(name)) continue;

        var exists = inbound.Any(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));
        if (!exists && f.Required)
            errors.Add($"Missing required field: {name}");
    }

    return errors;
}
