using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public interface IPortToApiEngine
{
    Task<EngineResult> ExecuteAsync(
        PortToApiContract portToApiContract,
        JsonObject inbound,
        EngineContext context,
        CancellationToken cancellationToken);
}

public sealed record EngineContext(string CorrelationId);

public sealed record EngineResult(
    bool Success,
    int StatusCode,
    object? Body,
    string? Error = null
);