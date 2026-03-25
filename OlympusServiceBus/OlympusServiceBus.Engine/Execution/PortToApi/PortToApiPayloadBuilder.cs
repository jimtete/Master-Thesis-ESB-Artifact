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
        if (m.SourceFieldName.IsEmpty || m.SinkFieldName.IsEmpty)
        {
            errors.Add("Direct requires SourceFieldName and SinkFieldName.");
            return;
        }

        if (!TryGetCaseInsensitive(inbound, m.SourceFieldName, out var value) || value is null)
        {
            errors.Add($"Direct missing source field: {m.SourceFieldName.Value}");
            return;
        }

        outbound[m.SinkFieldName.Value!] = value.DeepClone();
    }

    private static void ApplySplit(JsonObject inbound, JsonObject outbound, ApiFieldConfig m, List<string> errors)
    {
        if (m.SourceFieldName.IsEmpty || m.SinkFields is null || m.SinkFields.Length == 0)
        {
            errors.Add("Split requires SourceFieldName and SinkFields.");
            return;
        }

        var sep = string.IsNullOrWhiteSpace(m.Separator) ? " " : m.Separator;

        if (!TryGetCaseInsensitive(inbound, m.SourceFieldName, out var value) || value is null)
        {
            errors.Add($"Split missing source field: {m.SourceFieldName.Value}");
            return;
        }

        var str = value.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(str))
        {
            errors.Add($"Split source is empty/not string: {m.SourceFieldName.Value}");
            return;
        }

        var parts = str.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < m.SinkFields.Length; i++)
        {
            var sinkField = m.SinkFields[i];
            if (sinkField.IsEmpty) continue;

            if (i < parts.Length)
            {
                if (i == m.SinkFields.Length - 1 && parts.Length > m.SinkFields.Length)
                    outbound[sinkField.Value!] = string.Join(sep, parts.Skip(i));
                else
                    outbound[sinkField.Value!] = parts[i];
            }
            else
            {
                outbound[sinkField.Value!] = null;
            }
        }
    }

    private static bool TryGetCaseInsensitive(JsonObject obj, SourceField key, out JsonNode? value)
    {
        value = null;

        if (key.IsEmpty || key.Value is null)
            return false;

        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, key.Value, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        return false;
    }
}