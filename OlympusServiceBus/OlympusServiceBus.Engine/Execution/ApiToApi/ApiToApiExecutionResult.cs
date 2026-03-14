using System.Text.Json.Nodes;

namespace OlympusServiceBus.Engine.Execution.ApiToApi;

public class ApiToApiExecutionResult
{
    public JsonObject? SourcePayload { get; set; }
    public JsonObject? SinkPayload { get; set; }
    public JsonObject? SinkResponsePayload { get; set; }
}