using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.PortToFile;

public interface IPortToFileEngine
{
    Task<EngineResult> ExecuteAsync(
        PortToFileContract portFileContract,
        JsonObject inbound,
        EngineContext context,
        CancellationToken cancellationToken);
}