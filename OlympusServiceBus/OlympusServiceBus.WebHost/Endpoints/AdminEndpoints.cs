using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.WebHost.Services;

namespace OlympusServiceBus.WebHost.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminContracts(
        this IEndpointRouteBuilder app,
        List<PortToApiContract> portToApiContracts,
        List<PortToFileContract> portToFileContracts)
    {
        app.MapGet("/admin/contracts", () =>
                Results.Ok(
                    portToApiContracts
                        .Select(c => new
                        {
                            c.ContractId,
                            c.Name,
                            contractType = "PortToApi",
                            enabled = c.Enabled,
                            method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant(),
                            path = RouteHelpers.NormalizePath(c.Listener?.Path),
                            sink = c.Sink?.Endpoint
                        })
                        .Concat(
                            portToFileContracts.Select(c => new
                            {
                                c.ContractId,
                                c.Name,
                                contractType = "PortToFile",
                                enabled = c.Enabled,
                                method = (c.Listener?.Method ?? "POST").Trim().ToUpperInvariant(),
                                path = RouteHelpers.NormalizePath(c.Listener?.Path),
                                sink = c.Sink?.Directory
                            }))
                ))
            .WithName("AdminContracts");

        app.MapPost("/admin/reload", (WebHostRestartService restartService) =>
            {
                if (!restartService.TryScheduleRestart(out var error))
                {
                    return Results.Problem(
                        detail: error,
                        title: "WebHost restart could not be scheduled.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                return Results.Accepted("/admin/reload", new { restarting = true });
            })
            .WithName("AdminReload");

        return app;
    }
}
