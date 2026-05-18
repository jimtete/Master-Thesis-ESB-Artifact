using System.Globalization;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Evaluation;
using OlympusServiceBus.Utils.Configuration;

namespace OlympusServiceBus.Engine.Execution.Transformation;

public sealed class MappingEngine : IMappingEngine
{
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<MappingEngine> _logger;
    private readonly IEvaluationVerboseLogger _evaluationVerboseLogger;

    public MappingEngine(
        IExpressionEvaluator expressionEvaluator,
        ILogger<MappingEngine> logger,
        IEvaluationVerboseLogger evaluationVerboseLogger)
    {
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        _evaluationVerboseLogger = evaluationVerboseLogger;
    }

    public JsonObject BuildSinkPayload(
        JsonObject sourcePayload,
        IReadOnlyCollection<ApiFieldConfig>? mappings,
        string? contractId = null,
        string? correlationId = null)
    {
        var sinkPayload = new JsonObject();

        foreach (var mapping in mappings ?? Array.Empty<ApiFieldConfig>())
        {
            switch (mapping.TransformationType)
            {
                case TransformationType.Direct:
                    ApplyDirect(sourcePayload, sinkPayload, mapping, contractId, correlationId);
                    break;

                case TransformationType.Split:
                    ApplySplit(sourcePayload, sinkPayload, mapping, contractId, correlationId);
                    break;

                case TransformationType.Join:
                    ApplyJoin(sourcePayload, sinkPayload, mapping, contractId, correlationId);
                    break;

                case TransformationType.Expression:
                    ApplyExpression(sourcePayload, sinkPayload, mapping, contractId, correlationId);
                    break;
            }
        }

        return sinkPayload;
    }

    private void ApplyDirect(
        JsonObject sourcePayload,
        JsonObject sinkPayload,
        ApiFieldConfig mapping,
        string? contractId,
        string? correlationId)
    {
        if (mapping.SourceFieldName.IsEmpty || mapping.SinkFieldName.IsEmpty)
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Direct,
                "Direct mapping skipped because SourceFieldName or SinkFieldName is empty.",
                WrapField(mapping.SourceFieldName.Value),
                WrapField(mapping.SinkFieldName.Value));
            return;
        }

        if (!TryGetValueCaseInsensitive(sourcePayload, mapping.SourceFieldName.Value!, out var value) || value is null)
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Direct,
                $"Source field '{mapping.SourceFieldName.Value}' was not found.",
                WrapField(mapping.SourceFieldName.Value),
                WrapField(mapping.SinkFieldName.Value));
            return;
        }

        sinkPayload[mapping.SinkFieldName.Value!] = value.DeepClone();
    }

    private void ApplySplit(
        JsonObject sourcePayload,
        JsonObject sinkPayload,
        ApiFieldConfig mapping,
        string? contractId,
        string? correlationId)
    {
        if (mapping.SourceFieldName.IsEmpty ||
            mapping.SinkFields is null || mapping.SinkFields.Length == 0 ||
            mapping.SinkFields.All(x => x.IsEmpty))
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Split,
                "Split mapping skipped because SourceFieldName or SinkFields are missing.",
                WrapField(mapping.SourceFieldName.Value),
                mapping.SinkFields?.Where(x => !x.IsEmpty).Select(x => x.Value!));
            return;
        }

        if (!TryGetValueCaseInsensitive(sourcePayload, mapping.SourceFieldName.Value!, out var node) || node is null)
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Split,
                $"Source field '{mapping.SourceFieldName.Value}' was not found.",
                WrapField(mapping.SourceFieldName.Value),
                mapping.SinkFields.Where(x => !x.IsEmpty).Select(x => x.Value!));
            return;
        }

        var input = node.ToString();
        if (string.IsNullOrWhiteSpace(input))
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Split,
                $"Source field '{mapping.SourceFieldName.Value}' was empty.",
                WrapField(mapping.SourceFieldName.Value),
                mapping.SinkFields.Where(x => !x.IsEmpty).Select(x => x.Value!));
            return;
        }

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

    private void ApplyJoin(
        JsonObject sourcePayload,
        JsonObject sinkPayload,
        ApiFieldConfig mapping,
        string? contractId,
        string? correlationId)
    {
        if (mapping.SinkFieldName.IsEmpty ||
            mapping.SourceFields is null || mapping.SourceFields.Length == 0 ||
            mapping.SourceFields.All(x => x.IsEmpty))
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Join,
                "Join mapping skipped because SinkFieldName or SourceFields are missing.",
                mapping.SourceFields?.Where(x => !x.IsEmpty).Select(x => x.Value!),
                WrapField(mapping.SinkFieldName.Value));
            return;
        }

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
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Join,
                "Join mapping skipped because none of the source fields contained a value.",
                mapping.SourceFields.Where(x => !x.IsEmpty).Select(x => x.Value!),
                WrapField(mapping.SinkFieldName.Value));
            return;
        }

        sinkPayload[mapping.SinkFieldName.Value!] = string.Join(separator, values);
    }

    private void ApplyExpression(
        JsonObject sourcePayload,
        JsonObject sinkPayload,
        ApiFieldConfig mapping,
        string? contractId,
        string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(mapping.Expression) ||
            mapping.SourceFields is null || mapping.SourceFields.Length == 0 ||
            mapping.SinkFields is null || mapping.SinkFields.Length == 0)
        {
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Expression,
                "Expression mapping skipped because Expression, SourceFields, or SinkFields are missing.",
                mapping.SourceFields?.Where(x => !x.IsEmpty).Select(x => x.Value!),
                mapping.SinkFields?.Where(x => !x.IsEmpty).Select(x => x.Value!));
            return;
        }

        var inputs = new decimal[mapping.SourceFields.Length];

        for (var i = 0; i < mapping.SourceFields.Length; i++)
        {
            var sourceField = mapping.SourceFields[i];

            if (sourceField.IsEmpty)
            {
                _logger.LogWarning("Expression mapping skipped because SourceFields[{Index}] is empty.", i);
                LogMappingIssue(
                    contractId,
                    correlationId,
                    TransformationType.Expression,
                    $"SourceFields[{i}] is empty.",
                    mapping.SourceFields.Where(x => !x.IsEmpty).Select(x => x.Value!),
                    mapping.SinkFields.Where(x => !x.IsEmpty).Select(x => x.Value!));
                return;
            }

            if (!TryGetValueCaseInsensitive(sourcePayload, sourceField.Value!, out var value) || value is null)
            {
                _logger.LogWarning(
                    "Expression mapping skipped because input field '{Field}' was not found.",
                    sourceField.Value);
                LogMappingIssue(
                    contractId,
                    correlationId,
                    TransformationType.Expression,
                    $"Input field '{sourceField.Value}' was not found.",
                    mapping.SourceFields.Where(x => !x.IsEmpty).Select(x => x.Value!),
                    mapping.SinkFields.Where(x => !x.IsEmpty).Select(x => x.Value!));
                return;
            }

            if (!TryConvertNodeToDecimal(value, out var numericValue))
            {
                _logger.LogWarning(
                    "Expression mapping skipped because input field '{Field}' is not numeric.",
                    sourceField.Value);
                LogMappingIssue(
                    contractId,
                    correlationId,
                    TransformationType.Expression,
                    $"Input field '{sourceField.Value}' is not numeric.",
                    mapping.SourceFields.Where(x => !x.IsEmpty).Select(x => x.Value!),
                    mapping.SinkFields.Where(x => !x.IsEmpty).Select(x => x.Value!));
                return;
            }

            inputs[i] = numericValue;
        }

        if (!_expressionEvaluator.TryEvaluateAssignments(mapping.Expression!, inputs, out var outputs))
        {
            _logger.LogWarning(
                "Expression mapping skipped because expression evaluation failed. Expression: {Expression}",
                mapping.Expression);
            LogMappingIssue(
                contractId,
                correlationId,
                TransformationType.Expression,
                $"Expression evaluation failed. Expression: {mapping.Expression}",
                mapping.SourceFields.Where(x => !x.IsEmpty).Select(x => x.Value!),
                mapping.SinkFields.Where(x => !x.IsEmpty).Select(x => x.Value!));
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

    private void LogMappingIssue(
        string? contractId,
        string? correlationId,
        TransformationType transformationType,
        string message,
        IEnumerable<string?>? sourceFields,
        IEnumerable<string?>? sinkFields)
    {
        if (string.IsNullOrWhiteSpace(contractId))
            return;

        _evaluationVerboseLogger.LogMappingIssue(
            contractId,
            correlationId,
            transformationType.ToString(),
            message,
            sourceFields is null ? null : sourceFields.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!),
            sinkFields is null ? null : sinkFields.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!));
    }

    private static IEnumerable<string?>? WrapField(string? field)
    {
        return string.IsNullOrWhiteSpace(field) ? null : [field];
    }
}
