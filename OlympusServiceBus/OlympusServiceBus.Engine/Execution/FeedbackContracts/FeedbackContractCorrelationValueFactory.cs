using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public static class FeedbackContractCorrelationValueFactory
{
    public static Dictionary<string, string> Create(
        FeedbackContractBase feedbackContract,
        JsonObject? originalPayload,
        JsonObject? transformedPayload,
        JsonObject? responsePayload)
    {
        ArgumentNullException.ThrowIfNull(feedbackContract);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in feedbackContract.CorrelationFields)
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;

            var value =
                ReadJsonValue(transformedPayload, field) ??
                ReadJsonValue(originalPayload, field) ??
                ReadJsonValue(responsePayload, field);

            if (!string.IsNullOrWhiteSpace(value))
            {
                result[field] = value;
            }
        }

        return result;
    }

    private static string? ReadJsonValue(JsonObject? root, string path)
    {
        if (root is null || string.IsNullOrWhiteSpace(path))
            return null;

        JsonNode? current = root;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current?[segment];
            if (current is null)
                return null;
        }

        return current.ToString();
    }
}