using System.Globalization;
using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration;

namespace OlympusServiceBus.Engine.Execution.Transformation;

public sealed class MappingEngine : IMappingEngine
{
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<MappingEngine> _logger;

    public MappingEngine(
        IExpressionEvaluator expressionEvaluator,
        ILogger<MappingEngine> logger)
    {
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
    }

    public JsonObject BuildSinkPayload(
        JsonObject sourcePayload,
        IReadOnlyCollection<ApiFieldConfig>? mappings)
    {
        var sinkPayload = new JsonObject();

        foreach (var mapping in mappings ?? Array.Empty<ApiFieldConfig>())
        {
            switch (mapping.TransformationType)
            {
                case TransformationType.Direct:
                    ApplyDirect(sourcePayload, sinkPayload, mapping);
                    break;

                case TransformationType.Split:
                    ApplySplit(sourcePayload, sinkPayload, mapping);
                    break;

                case TransformationType.Join:
                    ApplyJoin(sourcePayload, sinkPayload, mapping);
                    break;

                case TransformationType.Expression:
                    ApplyExpression(sourcePayload, sinkPayload, mapping);
                    break;
            }
        }

        return sinkPayload;
    }

    private void ApplyDirect(JsonObject sourcePayload, JsonObject sinkPayload, ApiFieldConfig mapping)
    {
        if (mapping.SourceFieldName.IsEmpty || mapping.SinkFieldName.IsEmpty)
            return;

        if (!TryGetValueCaseInsensitive(sourcePayload, mapping.SourceFieldName.Value!, out var value) || value is null)
            return;

        sinkPayload[mapping.SinkFieldName.Value!] = value.DeepClone();
    }

    private void ApplySplit(JsonObject sourcePayload, JsonObject sinkPayload, ApiFieldConfig mapping)
    {
        if (mapping.SourceFieldName.IsEmpty ||
            mapping.SinkFields is null || mapping.SinkFields.Length == 0 ||
            mapping.SinkFields.All(x => x.IsEmpty))
            return;

        if (!TryGetValueCaseInsensitive(sourcePayload, mapping.SourceFieldName.Value!, out var node) || node is null)
            return;

        var input = node.ToString();
        if (string.IsNullOrWhiteSpace(input))
            return;

        var separator = string.IsNullOrEmpty(mapping.Separator) ? " " : mapping.Separator;

        var parts = separator == " "
            ? input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : input.Split(separator, StringSplitOptions.None).Select(x => x.Trim()).ToArray();

        for (var i = 0; i < mapping.SinkFields.Length; i++)
        {
            var sinkField = mapping.SinkFields[i];
            if (sinkField.IsEmpty)
                continue;

            if (i >= parts.Length)
                break;

            sinkPayload[sinkField.Value!] = parts[i];
        }
    }

    private void ApplyJoin(JsonObject sourcePayload, JsonObject sinkPayload, ApiFieldConfig mapping)
    {
        if (mapping.SinkFieldName.IsEmpty ||
            mapping.SourceFields is null || mapping.SourceFields.Length == 0 ||
            mapping.SourceFields.All(x => x.IsEmpty))
            return;

        var separator = string.IsNullOrEmpty(mapping.Separator) ? " " : mapping.Separator;
        var values = new List<string>();

        foreach (var sourceField in mapping.SourceFields)
        {
            if (sourceField.IsEmpty)
                continue;

            if (!TryGetValueCaseInsensitive(sourcePayload, sourceField.Value!, out var value) || value is null)
                continue;

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                values.Add(text.Trim());
        }

        if (values.Count == 0)
            return;

        sinkPayload[mapping.SinkFieldName.Value!] = string.Join(separator, values);
    }

    private void ApplyExpression(JsonObject sourcePayload, JsonObject sinkPayload, ApiFieldConfig mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.Expression) ||
            mapping.SourceFields is null || mapping.SourceFields.Length == 0 ||
            mapping.SinkFields is null || mapping.SinkFields.Length == 0)
        {
            return;
        }

        var inputs = new decimal[mapping.SourceFields.Length];

        for (var i = 0; i < mapping.SourceFields.Length; i++)
        {
            var sourceField = mapping.SourceFields[i];

            if (sourceField.IsEmpty)
            {
                _logger.LogWarning("Expression mapping skipped because SourceFields[{Index}] is empty.", i);
                return;
            }

            if (!TryGetValueCaseInsensitive(sourcePayload, sourceField.Value!, out var value) || value is null)
            {
                _logger.LogWarning(
                    "Expression mapping skipped because input field '{Field}' was not found.",
                    sourceField.Value);
                return;
            }

            if (!TryConvertNodeToDecimal(value, out var numericValue))
            {
                _logger.LogWarning(
                    "Expression mapping skipped because input field '{Field}' is not numeric.",
                    sourceField.Value);
                return;
            }

            inputs[i] = numericValue;
        }

        if (!_expressionEvaluator.TryEvaluateAssignments(mapping.Expression!, inputs, out var outputs))
        {
            _logger.LogWarning(
                "Expression mapping skipped because expression evaluation failed. Expression: {Expression}",
                mapping.Expression);
            return;
        }

        for (var i = 0; i < mapping.SinkFields.Length; i++)
        {
            var sinkField = mapping.SinkFields[i];
            if (sinkField.IsEmpty)
                continue;

            if (!outputs.TryGetValue(i, out var outputValue))
                continue;

            sinkPayload[sinkField.Value!] = outputValue;
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

    private static bool TryGetValueCaseInsensitive(JsonObject obj, string key, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(key, out value))
            return true;

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