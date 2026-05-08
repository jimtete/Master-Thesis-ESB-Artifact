using System.Globalization;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.Transformation;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public static class PortToApiPayloadBuilder
{
    private static readonly ExpressionEvaluator ExpressionEvaluator = new();

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

                case TransformationType.Join:
                    ApplyJoin(inbound, outbound, m, errors);
                    break;

                case TransformationType.Expression:
                    ApplyExpression(inbound, outbound, m, errors);
                    break;

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

    private static void ApplyJoin(JsonObject inbound, JsonObject outbound, ApiFieldConfig m, List<string> errors)
    {
        if (m.SinkFieldName.IsEmpty || m.SourceFields is null || m.SourceFields.Length == 0)
        {
            errors.Add("Join requires SinkFieldName and SourceFields.");
            return;
        }

        var sep = string.IsNullOrWhiteSpace(m.Separator) ? " " : m.Separator;
        var values = new List<string>();

        foreach (var sourceField in m.SourceFields)
        {
            if (sourceField.IsEmpty)
                continue;

            if (!TryGetCaseInsensitive(inbound, sourceField, out var value) || value is null)
            {
                errors.Add($"Join missing source field: {sourceField.Value}");
                return;
            }

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                values.Add(text.Trim());
        }

        if (values.Count == 0)
        {
            errors.Add($"Join source fields are empty/not string for sink field: {m.SinkFieldName.Value}");
            return;
        }

        outbound[m.SinkFieldName.Value!] = string.Join(sep, values);
    }

    private static void ApplyExpression(JsonObject inbound, JsonObject outbound, ApiFieldConfig m, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(m.Expression) || m.SourceFields is null || m.SourceFields.Length == 0 ||
            m.SinkFields is null || m.SinkFields.Length == 0)
        {
            errors.Add("Expression requires Expression, SourceFields, and SinkFields.");
            return;
        }

        var inputs = new decimal[m.SourceFields.Length];

        for (var i = 0; i < m.SourceFields.Length; i++)
        {
            var sourceField = m.SourceFields[i];

            if (sourceField.IsEmpty)
            {
                errors.Add($"Expression SourceFields[{i}] is empty.");
                return;
            }

            if (!TryGetCaseInsensitive(inbound, sourceField, out var value) || value is null)
            {
                errors.Add($"Expression missing source field: {sourceField.Value}");
                return;
            }

            if (!TryConvertNodeToDecimal(value, out var numericValue))
            {
                errors.Add($"Expression source field '{sourceField.Value}' is not numeric.");
                return;
            }

            inputs[i] = numericValue;
        }

        if (!ExpressionEvaluator.TryEvaluateAssignments(m.Expression, inputs, out var outputs))
        {
            errors.Add($"Expression evaluation failed: {m.Expression}");
            return;
        }

        for (var i = 0; i < m.SinkFields.Length; i++)
        {
            var sinkField = m.SinkFields[i];
            if (sinkField.IsEmpty)
                continue;

            if (!outputs.TryGetValue(i, out var outputValue))
                continue;

            outbound[sinkField.Value!] = outputValue;
        }
    }

    private static bool TryConvertNodeToDecimal(JsonNode node, out decimal value)
    {
        value = 0m;

        var text = node.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return decimal.TryParse(
            text,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
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
