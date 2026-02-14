using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminContracts(this IEndpointRouteBuilder app, List<PortToApiContract> contracts)
    {
        app.MapGet("/admin/contracts", () =>
                Results.Ok(contracts.Select(c => new
                {
                    c.ContractId,
                    enabled = c.Enabled,
                    method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant(),
                    path = RouteHelpers.NormalizePath(c.Listener?.Path),
                    sink = c.Sink?.Endpoint
                })))
            .WithName("AdminContracts");

        return app;
    }
}