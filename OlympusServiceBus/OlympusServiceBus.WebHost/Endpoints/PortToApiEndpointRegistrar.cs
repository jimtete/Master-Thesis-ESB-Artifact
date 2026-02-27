using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.WebHost.Models;
using OlympusServiceBus.WebHost.Validation;
using OlympusServiceBus.WebHost.WebOpenApiSchema;

namespace OlympusServiceBus.WebHost.Endpoints;

public sealed class PortToApiEndpointRegistrar : IPortToApiEndpointRegistrar
{
    private readonly ILogger<PortToApiEndpointRegistrar> _logger;
    private readonly PortToApiSchemaBuilder _schemaBuilder;
    private readonly PortToApiInboundValidator _validator;
    private readonly IPortToApiEngine _engine;

    public PortToApiEndpointRegistrar(
        ILogger<PortToApiEndpointRegistrar> logger,
        PortToApiSchemaBuilder schemaBuilder,
        PortToApiInboundValidator validator,
        IPortToApiEngine engine)
    {
        _logger = logger;
        _schemaBuilder = schemaBuilder;
        _validator = validator;
        _engine = engine;
    }

    public void Register(WebApplication app, List<PortToApiContract> contracts)
    {
        var usedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in contracts)
        {
            if (!c.Enabled) continue;

            var method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant();
            var path = RouteHelpers.NormalizePath(c.Listener?.Path);

            var routeKey = $"{method} {path}";
            if (!usedRoutes.Add(routeKey))
            {
                _logger.LogWarning("[{Contract}] Duplicate route ignored: {Route}", c.ContractId, routeKey);
                continue;
            }

            var endpointName = $"PortToApi_{c.ContractId}";
            var requestSchema = _schemaBuilder.BuildFromContract(c);

            _logger.LogInformation("[{Contract}] Mapping {Method} {Path} -> {Sink}",
                c.ContractId, method, path, c.Sink?.Endpoint);

            app.MapMethods(path, new[] { method }, async (HttpContext ctx) =>
                {
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

                    var errors = _validator.Validate(inboundObj, c);
                    if (errors.Count > 0)
                        return Results.BadRequest(new { error = "Payload validation failed", errors });

                    var correlationId =
                        ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var cid) && !string.IsNullOrWhiteSpace(cid)
                            ? cid.ToString()
                            : Guid.NewGuid().ToString("N");

                    var result = await _engine.ExecuteAsync(
                        c,
                        inboundObj,
                        new EngineContext(correlationId),   // adjust ctor args to match your EngineContext
                        ctx.RequestAborted);

                    return ToHttpResult(result);
                })
                .WithName(endpointName)
                .WithMetadata(new PortToApiOpenApiMetadata(c.ContractId, requestSchema));
        }
    }

    private static IResult ToHttpResult(EngineResult r)
    {
        if (r.Success)
        {
            return Results.Json(r.Body, statusCode: r.StatusCode);
        }

        return Results.Json(new
        {
            error = r.Error ?? "EngineError",
            details = r.Body
        }, statusCode: r.StatusCode);
    }
}
