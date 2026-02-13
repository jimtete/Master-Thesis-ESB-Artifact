using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OlympusServiceBus.Utils;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.WebHost.Models;

var builder = WebApplication.CreateBuilder(args);

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bind options
builder.Services.Configure<ContractsOptions>(
    builder.Configuration.GetSection("Contracts"));

// Needed later for forwarding to Sink APIs
builder.Services.AddHttpClient();

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Optional (only keep if you run https)
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

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};

// ---- Load & map contracts ON STARTUP ----
var contracts = LoadPortToApiContracts(contractsRoot);

// Optional: show what was loaded
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
}).WithOpenApi();

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

    var files = Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories);

    foreach (var file in files)
    {
        try
        {
            var json = File.ReadAllText(file);

            var doc = JsonSerializer.Deserialize<PortToApiDocument>(json, jsonOpts);
            var c = doc?.PortToApi;
            if (c is null) continue;

            // Ensure ContractId exists (fallback to filename)
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
    var usedEndpointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var usedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var c in loaded)
    {
        if (!c.Enabled)
        {
            app.Logger.LogInformation("[{Contract}] Skipping (disabled).", c.ContractId);
            continue;
        }

        var method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant();
        var path = NormalizePath(c.Listener?.Path);

        var routeKey = $"{method} {path}";
        if (!usedRoutes.Add(routeKey))
        {
            app.Logger.LogWarning("[{Contract}] Duplicate route ignored: {Route}", c.ContractId, routeKey);
            continue;
        }

        // Endpoint names MUST be globally unique
        var endpointName = $"PortToApi_{c.ContractId}";
        if (!usedEndpointNames.Add(endpointName))
        {
            endpointName = $"PortToApi_{c.ContractId}_{Guid.NewGuid():N}";
        }

        app.Logger.LogInformation("[{Contract}] Mapping {Method} {Path} -> {Sink}",
            c.ContractId, method, path, c.Sink?.Endpoint);

        webApp.MapMethods(path, new[] { method }, async (HttpContext ctx, IHttpClientFactory httpFactory) =>
        {
            // 1) Read inbound JSON
            var inbound = await ctx.Request.ReadFromJsonAsync<JsonObject>(cancellationToken: ctx.RequestAborted);
            if (inbound is null)
                return Results.BadRequest(new { error = "Request body must be JSON object." });

            // 2) Validate that inbound contains the expected fields (derived from mappings)
            var expected = GetExpectedInboundFields(c);
            var missing = expected.Where(f => !HasPropertyCaseInsensitive(inbound, f)).ToList();

            if (missing.Count > 0)
                return Results.BadRequest(new { error = "Missing required fields", missing });

            // 3) Transform inbound -> sink payload (Type 1 + Type 2)
            var sinkPayload = new JsonObject();

            foreach (var m in c.Mappings ?? Array.Empty<ApiFieldConfig>())
            {
                switch (m.TransformationType)
                {
                    case TransformationType.Direct:
                    {
                        if (string.IsNullOrWhiteSpace(m.SourceFieldName) || string.IsNullOrWhiteSpace(m.SinkFieldName))
                            continue;

                        if (!TryGetValueCaseInsensitive(inbound, m.SourceFieldName, out var v) || v is null)
                            continue;

                        sinkPayload[m.SinkFieldName] = v.DeepClone();
                        break;
                    }

                    case TransformationType.Split:
                    {
                        if (string.IsNullOrWhiteSpace(m.SourceFieldName) ||
                            m.SinkFields is null || m.SinkFields.Length == 0)
                            continue;

                        if (!TryGetValueCaseInsensitive(inbound, m.SourceFieldName, out var v) || v is null)
                            continue;

                        var input = v.ToString();
                        var parts = (m.Separator == " ")
                            ? input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            : input.Split(m.Separator, StringSplitOptions.None).Select(p => p.Trim()).ToArray();

                        for (var i = 0; i < m.SinkFields.Length && i < parts.Length; i++)
                        {
                            var sinkField = m.SinkFields[i];
                            if (!string.IsNullOrWhiteSpace(sinkField))
                                sinkPayload[sinkField] = parts[i];
                        }
                        break;
                    }

                    case TransformationType.Join:
                    {
                        if (string.IsNullOrWhiteSpace(m.SinkFieldName) ||
                            m.SourceFields is null || m.SourceFields.Length == 0)
                            continue;

                        var joinParts = new List<string>();
                        foreach (var sf in m.SourceFields)
                        {
                            if (string.IsNullOrWhiteSpace(sf)) continue;
                            if (!TryGetValueCaseInsensitive(inbound, sf, out var node) || node is null) continue;

                            var s = node.ToString();
                            if (!string.IsNullOrWhiteSpace(s)) joinParts.Add(s.Trim());
                        }

                        if (joinParts.Count > 0)
                            sinkPayload[m.SinkFieldName] = string.Join(m.Separator ?? " ", joinParts);

                        break;
                    }
                }
            }

            // 4) Forward to sink
            var client = httpFactory.CreateClient();

            var sinkMethod = new HttpMethod((c.Sink?.Method ?? "POST").Trim().ToUpperInvariant());
            using var req = new HttpRequestMessage(sinkMethod, c.Sink!.Endpoint!)
            {
                Content = JsonContent.Create(sinkPayload)
            };

            using var resp = await client.SendAsync(req, ctx.RequestAborted);

            // PoC: return sink status + payload we sent
            return Results.Ok(new
            {
                contractId = c.ContractId,
                forwardedTo = c.Sink.Endpoint,
                sinkStatus = (int)resp.StatusCode,
                payload = sinkPayload
            });
        })
        .Accepts<JsonObject>("application/json")
        .Produces(StatusCodes.Status200OK)
        .WithOpenApi(op =>
        {
            op.Summary = $"PortToApi contract: {c.ContractId}";
            op.RequestBody = BuildRequestBodyFromContract(c);
            return op;
        })
        .WithName(endpointName);
    }
}

static string NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
        return "/";

    return path.StartsWith('/') ? path : "/" + path;
}

static OpenApiRequestBody BuildRequestBodyFromContract(PortToApiContract c)
{
    var fields = GetExpectedInboundFields(c) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // OpenApiSchema.Properties expects IDictionary<string, IOpenApiSchema>
    var props = new Dictionary<string, IOpenApiSchema>(StringComparer.OrdinalIgnoreCase);
    foreach (var f in fields)
    {
        if (string.IsNullOrWhiteSpace(f)) continue;
        props[f] = new OpenApiSchema { Type = JsonSchemaType.String }; // PoC: assume strings
    }

    var schema = new OpenApiSchema
    {
        Type = JsonSchemaType.Object,
        Properties = props,
        Required = new HashSet<string>(fields.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase)
    };

    return new OpenApiRequestBody
    {
        Required = true,
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/json"] = new OpenApiMediaType
            {
                Schema = schema
            }
        }
    };
}



static HashSet<string> GetExpectedInboundFields(PortToApiContract c)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var m in c.Mappings ?? Array.Empty<ApiFieldConfig>())
    {
        switch (m.TransformationType)
        {
            case TransformationType.Direct:
            case TransformationType.Split:
                if (!string.IsNullOrWhiteSpace(m.SourceFieldName))
                    set.Add(m.SourceFieldName);
                break;

            case TransformationType.Join:
                if (m.SourceFields is not null)
                    foreach (var f in m.SourceFields)
                        if (!string.IsNullOrWhiteSpace(f))
                            set.Add(f);
                break;
        }
    }

    return set;
}

static bool HasPropertyCaseInsensitive(JsonObject obj, string key)
{
    return obj.Any(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
}

static bool TryGetValueCaseInsensitive(JsonObject obj, string key, out JsonNode? value)
{
    if (obj.TryGetPropertyValue(key, out value)) return true;

    foreach (var kv in obj)
        if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            value = kv.Value;
            return true;
        }

    value = null;
    return false;
}

