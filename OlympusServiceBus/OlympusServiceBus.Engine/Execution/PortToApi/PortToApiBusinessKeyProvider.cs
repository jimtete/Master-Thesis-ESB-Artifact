using System.Text.Json.Nodes;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public class PortToApiBusinessKeyProvider
{
    public string CreateKey(JsonObject payload, IEnumerable<string> keyFields)
    {
        var parts = new List<string>();

        foreach (var fieldName in keyFields)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                continue;

            if (!payload.TryGetPropertyValue(fieldName, out var value) || value is null)
                continue;

            var text = value.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add($"{fieldName}={text}");
        }

        var key = string.Join("|", parts);

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Business key could not be created because none of the configured BusinessKeyFields were present in the payload.");
        }

        return key;
    }
}