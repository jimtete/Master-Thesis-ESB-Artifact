using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution;

public sealed class PortToApiEndpointRegistrar
{
    private readonly ILogger<PortToApiEndpointRegistrar> _logger;
    private readonly IContractRegistry _registry;

    public PortToApiEndpointRegistrar(
        ILogger<PortToApiEndpointRegistrar> logger,
        IContractRegistry registry
        )
    {
        _logger = logger;
        _registry = registry;
    }
    
    public void MapEndpoints(WebApplication app)
    {
        var contracts = _registry.Get<PortToApiContract>();

        _logger.LogInformation("Registering PortToApi endpoints. Contracts found: {Count}", contracts.Count);

        foreach (var c in contracts)
        {
            if (!c.Enabled)
            {
                _logger.LogInformation("[{Contract}] Skipping (disabled).", c.ContractId);
                continue;
            }

            var path = NormalizePath(c.Listener?.Path);
            var method = NormalizeMethod(c.Listener?.Method);

            // IMPORTANT: endpoint names must be globally unique
            var endpointName = $"PortToApi_{c.ContractId}";

            _logger.LogInformation("[{Contract}] Mapping {Method} {Path} -> {Sink}",
                c.ContractId, method, path, c.Sink?.Endpoint);

            app.MapMethods(path, new[] { method }, async (HttpContext ctx) =>
                {
                    // PoC: just confirm it exists. Next step: read body, transform, forward to Sink.
                    return Results.Ok(new
                    {
                        contractId = c.ContractId,
                        inbound = new { method, path },
                        sink = c.Sink?.Endpoint,
                        mapped = true
                    });
                })
                .WithName(endpointName);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }
        
        return path.StartsWith("/") ? path : "/" + path;
    }

    private static string NormalizeMethod(string? method)
    {
        return string.IsNullOrEmpty(method) ? "POST" : method.Trim().ToUpperInvariant();
    }
}