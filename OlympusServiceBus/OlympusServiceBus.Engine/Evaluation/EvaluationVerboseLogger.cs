using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Evaluation;

public sealed class EvaluationVerboseLogger : IEvaluationVerboseLogger
{
    private const string VerboseEnvironmentVariableName = "OLYMPUS_EVALUATION_VERBOSE";
    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "pwd",
        "secret",
        "token",
        "authorization",
        "cookie",
        "api_key",
        "apikey",
        "clientsecret"
    ];

    private readonly ILogger<EvaluationVerboseLogger> _logger;
    private readonly IOptionsMonitor<EvaluationVerboseLoggingOptions> _optionsMonitor;

    public EvaluationVerboseLogger(
        ILogger<EvaluationVerboseLogger> logger,
        IOptionsMonitor<EvaluationVerboseLoggingOptions> optionsMonitor)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public bool IsEnabled => ResolveEnabled();

    public string CreateCorrelationId(string contractId, string? scope = null)
    {
        var suffix = string.IsNullOrWhiteSpace(scope)
            ? null
            : scope.Trim().Replace(' ', '-');

        return suffix is null
            ? $"{contractId}:{Guid.NewGuid():N}"
            : $"{contractId}:{suffix}:{Guid.NewGuid():N}";
    }

    public void LogExecutionStarted(
        ContractBase contract,
        string triggerType,
        string correlationId,
        DateTimeOffset startedAtUtc)
    {
        if (!IsEnabled)
            return;

        var metadata = EvaluationContractMetadataResolver.Resolve(contract);

        _logger.LogInformation(
            "[EVAL][{ContractType}][ExecutionStart] ContractId={ContractId} ContractName={ContractName} ContractType={ResolvedContractType} ScheduleMode={ScheduleMode} TriggerType={TriggerType} CorrelationId={CorrelationId} StartedAtUtc={StartedAtUtc}",
            metadata.ContractType,
            contract.ContractId,
            contract.Name,
            metadata.ContractType,
            metadata.ScheduleMode,
            triggerType,
            correlationId,
            startedAtUtc);
    }

    public void LogExecutionCompleted(
        ContractBase contract,
        string triggerType,
        string correlationId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        long durationMilliseconds,
        string status,
        string? errorMessage = null)
    {
        if (!IsEnabled)
            return;

        var metadata = EvaluationContractMetadataResolver.Resolve(contract);

        _logger.LogInformation(
            "[EVAL][{ContractType}][ExecutionEnd] ContractId={ContractId} ContractName={ContractName} ContractType={ResolvedContractType} ScheduleMode={ScheduleMode} TriggerType={TriggerType} CorrelationId={CorrelationId} StartedAtUtc={StartedAtUtc} EndedAtUtc={EndedAtUtc} DurationMs={DurationMilliseconds} Status={Status} Error={Error}",
            metadata.ContractType,
            contract.ContractId,
            contract.Name,
            metadata.ContractType,
            metadata.ScheduleMode,
            triggerType,
            correlationId,
            startedAtUtc,
            endedAtUtc,
            durationMilliseconds,
            status,
            errorMessage ?? "<none>");
    }

    public void LogApiSourceRequest(
        ContractBase contract,
        string correlationId,
        string method,
        string url)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][ApiSource] ContractId={ContractId} CorrelationId={CorrelationId} Method={Method} Url={Url}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            method,
            url);
    }

    public void LogApiSourceResponse(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        int statusCode,
        string? responseBody)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][ApiSource] ContractId={ContractId} CorrelationId={CorrelationId} Method={Method} Url={Url} StatusCode={StatusCode} ResponseBody={ResponseBody}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            method,
            url,
            statusCode,
            FormatStringBody(responseBody));
    }

    public void LogApiSourceFailure(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        Exception exception,
        int? statusCode = null,
        string? responseBody = null)
    {
        if (!IsEnabled)
            return;

        _logger.LogError(
            exception,
            "[EVAL][{ContractType}][ApiSource][Failure] ContractId={ContractId} CorrelationId={CorrelationId} Method={Method} Url={Url} StatusCode={StatusCode} ExceptionType={ExceptionType} Error={Error} ResponseBody={ResponseBody}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            method,
            url,
            statusCode?.ToString() ?? "<none>",
            exception.GetType().Name,
            exception.Message,
            FormatStringBody(responseBody));
    }

    public void LogApiSinkRequest(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        JsonObject? payload)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][ApiSink] ContractId={ContractId} CorrelationId={CorrelationId} Method={Method} Url={Url} Payload={Payload}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            method,
            url,
            FormatJsonNode(payload));
    }

    public void LogApiSinkResponse(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        int statusCode,
        string? responseBody,
        JsonObject? payload = null)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][ApiSink] ContractId={ContractId} CorrelationId={CorrelationId} Method={Method} Url={Url} StatusCode={StatusCode} Payload={Payload} ResponseBody={ResponseBody}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            method,
            url,
            statusCode,
            FormatJsonNode(payload),
            FormatStringBody(responseBody));
    }

    public void LogApiSinkFailure(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        JsonObject? payload,
        Exception exception,
        int? statusCode = null,
        string? responseBody = null)
    {
        if (!IsEnabled)
            return;

        _logger.LogError(
            exception,
            "[EVAL][{ContractType}][PublishFailure] ContractId={ContractId} CorrelationId={CorrelationId} Method={Method} Url={Url} StatusCode={StatusCode} ExceptionType={ExceptionType} Error={Error} Payload={Payload} ResponseBody={ResponseBody}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            method,
            url,
            statusCode?.ToString() ?? "<none>",
            exception.GetType().Name,
            exception.Message,
            FormatJsonNode(payload),
            FormatStringBody(responseBody));
    }

    public void LogTransformation(
        ContractBase contract,
        string correlationId,
        IReadOnlyCollection<ApiFieldConfig>? mappings,
        JsonNode? sourcePayload,
        JsonNode? outboundPayload,
        IReadOnlyCollection<string>? mappingErrors = null,
        string? sourceLabel = null)
    {
        if (!IsEnabled)
            return;

        var mappingDescriptions = (mappings ?? Array.Empty<ApiFieldConfig>())
            .Select(DescribeMapping)
            .ToArray();

        _logger.LogInformation(
            "[EVAL][{ContractType}][Mapping] ContractId={ContractId} CorrelationId={CorrelationId} SourceLabel={SourceLabel} SourcePayload={SourcePayload} Mappings={Mappings} MappingErrors={MappingErrors} OutboundPayload={OutboundPayload}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            sourceLabel ?? "payload",
            FormatJsonNode(sourcePayload),
            mappingDescriptions.Length == 0 ? "<none>" : string.Join(" || ", mappingDescriptions),
            mappingErrors is { Count: > 0 } ? string.Join(" | ", mappingErrors) : "<none>",
            FormatJsonNode(outboundPayload));
    }

    public void LogMappingIssue(
        string contractId,
        string? correlationId,
        string transformationType,
        string message,
        IEnumerable<string>? sourceFields = null,
        IEnumerable<string>? sinkFields = null)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][Mapping] ContractId={ContractId} CorrelationId={CorrelationId} TransformationType={TransformationType} SourceFields={SourceFields} SinkFields={SinkFields} Error={Error}",
            contractId,
            correlationId ?? "<none>",
            transformationType,
            JoinFieldNames(sourceFields),
            JoinFieldNames(sinkFields),
            message);
    }

    public void LogRuntimeState(
        ContractBase contract,
        string correlationId,
        ContractMessageStateEntity state,
        bool duplicateSkipped = false,
        string? note = null)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][RuntimeState] ContractId={ContractId} CorrelationId={CorrelationId} BusinessKey={BusinessKey} PayloadHash={PayloadHash} PublishStatus={PublishStatus} PublishAttemptCount={PublishAttemptCount} LastPublishError={LastPublishError} DuplicateSkipped={DuplicateSkipped} Note={Note}",
            contract.ContractId,
            correlationId,
            state.BusinessKey,
            state.PayloadHash,
            state.PublishStatus,
            state.PublishAttemptCount,
            state.LastPublishError ?? "<none>",
            duplicateSkipped,
            note ?? "<none>");
    }

    public void LogFileScan(
        ContractBase contract,
        string correlationId,
        string inputDirectory,
        string searchPattern,
        int discoveredFileCount)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][FileScan] ContractId={ContractId} CorrelationId={CorrelationId} InputDirectory={InputDirectory} SearchPattern={SearchPattern} DiscoveredFiles={DiscoveredFileCount}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            inputDirectory,
            searchPattern,
            discoveredFileCount);
    }

    public void LogFileProcessingStarted(
        ContractBase contract,
        string correlationId,
        string filePath)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][FileProcessing] ContractId={ContractId} CorrelationId={CorrelationId} File={File}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            filePath);
    }

    public void LogFileProcessingCompleted(
        ContractBase contract,
        string correlationId,
        string filePath,
        int totalRows,
        int succeededRows,
        int failedRows,
        string? errorReportPath,
        string? destinationPath)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][FileProcessing] ContractId={ContractId} CorrelationId={CorrelationId} File={File} TotalRows={TotalRows} SuccessfulRows={SucceededRows} FailedRows={FailedRows} ErrorReportPath={ErrorReportPath} DestinationPath={DestinationPath}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            filePath,
            totalRows,
            succeededRows,
            failedRows,
            errorReportPath ?? "<none>",
            destinationPath ?? "<none>");
    }

    public void LogPortRequest(
        ContractBase contract,
        string correlationId,
        string listenerPath,
        JsonObject inboundPayload)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][PortRequest] ContractId={ContractId} CorrelationId={CorrelationId} ListenerPath={ListenerPath} InboundPayload={InboundPayload}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            listenerPath,
            FormatJsonNode(inboundPayload));
    }

    public void LogPortResponse(
        ContractBase contract,
        string correlationId,
        string listenerPath,
        JsonObject? outboundPayload,
        int statusCode,
        object? responseBody)
    {
        if (!IsEnabled)
            return;

        _logger.LogInformation(
            "[EVAL][{ContractType}][PortResponse] ContractId={ContractId} CorrelationId={CorrelationId} ListenerPath={ListenerPath} StatusCode={StatusCode} OutboundPayload={OutboundPayload} ResponseBody={ResponseBody}",
            GetContractType(contract),
            contract.ContractId,
            correlationId,
            listenerPath,
            statusCode,
            FormatJsonNode(outboundPayload),
            FormatObjectBody(responseBody));
    }

    private bool ResolveEnabled()
    {
        var raw = Environment.GetEnvironmentVariable(VerboseEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(raw) && TryParseBoolean(raw, out var enabled))
        {
            return enabled;
        }

        return _optionsMonitor.CurrentValue.Enabled;
    }

    private int GetMaxBodyLength()
    {
        var configured = _optionsMonitor.CurrentValue.MaxBodyLength;
        return configured > 0 ? configured : 4096;
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "on":
                result = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private string FormatObjectBody(object? value)
    {
        if (value is null)
            return "<empty>";

        if (value is string text)
            return FormatStringBody(text);

        try
        {
            var node = JsonSerializer.SerializeToNode(value);
            return FormatJsonNode(node);
        }
        catch
        {
            return Limit(value.ToString() ?? "<empty>");
        }
    }

    private string FormatStringBody(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        try
        {
            var node = JsonNode.Parse(value);
            if (node is not null)
                return FormatJsonNode(node);
        }
        catch
        {
            // Keep raw body when it is not JSON.
        }

        return Limit(value);
    }

    private string FormatJsonNode(JsonNode? node)
    {
        if (node is null)
            return "<empty>";

        var redacted = Redact(node);
        return Limit(redacted?.ToJsonString() ?? "<empty>");
    }

    private JsonNode? Redact(JsonNode? node)
    {
        if (node is null)
            return null;

        return node switch
        {
            JsonObject obj => RedactObject(obj),
            JsonArray array => RedactArray(array),
            _ => node.DeepClone()
        };
    }

    private JsonObject RedactObject(JsonObject source)
    {
        var result = new JsonObject();

        foreach (var pair in source)
        {
            result[pair.Key] = IsSensitiveKey(pair.Key)
                ? "***REDACTED***"
                : Redact(pair.Value);
        }

        return result;
    }

    private JsonArray RedactArray(JsonArray source)
    {
        var result = new JsonArray();

        foreach (var item in source)
        {
            result.Add(Redact(item));
        }

        return result;
    }

    private static bool IsSensitiveKey(string key)
    {
        return SensitiveKeyFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private string Limit(string value)
    {
        var maxBodyLength = GetMaxBodyLength();
        if (value.Length <= maxBodyLength)
            return value;

        return $"{value[..maxBodyLength]}... [truncated {value.Length - maxBodyLength} chars]";
    }

    private static string JoinFieldNames(IEnumerable<string>? fields)
    {
        var values = fields?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return values is { Length: > 0 }
            ? string.Join("|", values)
            : "<none>";
    }

    private static string DescribeMapping(ApiFieldConfig mapping)
    {
        var sourceFields = mapping.SourceFields?
            .Where(x => !x.IsEmpty && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Value!)
            .ToArray();

        var sinkFields = mapping.SinkFields?
            .Where(x => !x.IsEmpty && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Value!)
            .ToArray();

        var directSource = mapping.SourceFieldName.IsEmpty ? null : mapping.SourceFieldName.Value;
        var directSink = mapping.SinkFieldName.IsEmpty ? null : mapping.SinkFieldName.Value;

        return mapping.TransformationType switch
        {
            TransformationType.Direct =>
                $"Type=Direct Source={directSource ?? "<none>"} Sink={directSink ?? "<none>"}",
            TransformationType.Split =>
                $"Type=Split Source={directSource ?? "<none>"} SinkFields={JoinFieldNames(sinkFields)} Separator={mapping.Separator}",
            TransformationType.Join =>
                $"Type=Join SourceFields={JoinFieldNames(sourceFields)} Sink={directSink ?? "<none>"} Separator={mapping.Separator}",
            TransformationType.Expression =>
                $"Type=Expression SourceFields={JoinFieldNames(sourceFields)} SinkFields={JoinFieldNames(sinkFields)} Expression={mapping.Expression ?? "<none>"}",
            _ =>
                $"Type={mapping.TransformationType}"
        };
    }

    private static string GetContractType(ContractBase contract)
    {
        return EvaluationContractMetadataResolver.Resolve(contract).ContractType;
    }
}
