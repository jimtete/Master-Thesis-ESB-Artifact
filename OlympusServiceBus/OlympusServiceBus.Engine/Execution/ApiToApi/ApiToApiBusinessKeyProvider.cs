using System.Text.Json.Nodes;

namespace OlympusServiceBus.Engine.Execution.ApiToApi;

public class ApiToApiBusinessKeyProvider
{
    public string CreateKey(JsonObject payload, IEnumerable<string> keyFields)
    {
        var parts = new List<string>();

        foreach (var fieldName in keyFields)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                continue;
            }

            if (!payload.TryGetPropertyValue(fieldName, out var value) || value == null)
            {
                continue;
            }
            
            var text = value.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add($"{fieldName}={text}");
            }
        }
        
        return string.Join("|", parts);
    }
}