using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public static class PortToApiPayloadBuilder
{
    public static (JsonObject Outbound, List<string> Errors) BuildOutbound(PortToApiContract c, JsonObject inbound)
    {
        var outbound = new JsonObject();
        var errors = new List<string>();

        var mappings = c.Mappings ?? Array.Empty<ApiFieldConfig>();

        foreach (var m in mappings)
        {
            switch (m.TransformationType)
            {
                case TransformationType.Direct:
                    ApplyDirect(inbound, outbound, m, errors);
                    break;

                case TransformationType.Split:
                    ApplySplit(inbound, outbound, m, errors);
                    break;

                // You can add Join later if you already have it for ApiToApi
                default:
                    errors.Add($"Unsupported TransformationType: {m.TransformationType}");
                    break;
            }
        }

        return (outbound, errors);
    }

    private static void ApplyDirect(JsonObject inbound, JsonObject outbound, ApiFieldConfig m, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(m.SourceFieldName) || string.IsNullOrWhiteSpace(m.SinkFieldName))
        {
            errors.Add("Direct requires SourceFieldName and SinkFieldName.");
            return;
        }

        if (!TryGetCaseInsensitive(inbound, m.SourceFieldName, out var value) || value is null)
        {
            errors.Add($"Direct missing source field: {m.SourceFieldName}");
            return;
        }

        outbound[m.SinkFieldName] = value.DeepClone();
    }

    private static void ApplySplit(JsonObject inbound, JsonObject outbound, ApiFieldConfig m, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(m.SourceFieldName) || m.SinkFields is null || m.SinkFields.Length == 0)
        {
            errors.Add("Split requires SourceFieldName and SinkFields.");
            return;
        }

        var sep = string.IsNullOrWhiteSpace(m.Separator) ? " " : m.Separator;

        if (!TryGetCaseInsensitive(inbound, m.SourceFieldName, out var value) || value is null)
        {
            errors.Add($"Split missing source field: {m.SourceFieldName}");
            return;
        }

        var str = value.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(str))
        {
            errors.Add($"Split source is empty/not string: {m.SourceFieldName}");
            return;
        }

        var parts = str.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < m.SinkFields.Length; i++)
        {
            var sinkField = m.SinkFields[i];
            if (string.IsNullOrWhiteSpace(sinkField)) continue;

            if (i < parts.Length)
            {
                // If there are extra parts, put the remainder into the last sink field.
                if (i == m.SinkFields.Length - 1 && parts.Length > m.SinkFields.Length)
                    outbound[sinkField] = string.Join(sep, parts.Skip(i));
                else
                    outbound[sinkField] = parts[i];
            }
            else
            {
                outbound[sinkField] = null;
            }
        }
    }

    private static bool TryGetCaseInsensitive(JsonObject obj, string key, out JsonNode? value)
    {
        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}