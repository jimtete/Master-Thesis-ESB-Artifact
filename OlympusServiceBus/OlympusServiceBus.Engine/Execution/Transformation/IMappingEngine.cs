using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration;

namespace OlympusServiceBus.Engine.Execution.Transformation;

public interface IMappingEngine
{
    JsonObject BuildSinkPayload(
        JsonObject sourcePayload,
        IReadOnlyCollection<ApiFieldConfig>? mappings,
        string? contractId = null,
        string? correlationId = null);
}
