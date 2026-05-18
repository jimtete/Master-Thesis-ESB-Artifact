using System.Text.Json.Nodes;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Evaluation;

public interface IEvaluationVerboseLogger
{
    bool IsEnabled { get; }

    string CreateCorrelationId(string contractId, string? scope = null);

    void LogExecutionStarted(
        ContractBase contract,
        string triggerType,
        string correlationId,
        DateTimeOffset startedAtUtc);

    void LogExecutionCompleted(
        ContractBase contract,
        string triggerType,
        string correlationId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        long durationMilliseconds,
        string status,
        string? errorMessage = null);

    void LogApiSourceRequest(
        ContractBase contract,
        string correlationId,
        string method,
        string url);

    void LogApiSourceResponse(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        int statusCode,
        string? responseBody);

    void LogApiSourceFailure(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        Exception exception,
        int? statusCode = null,
        string? responseBody = null);

    void LogApiSinkRequest(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        JsonObject? payload);

    void LogApiSinkResponse(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        int statusCode,
        string? responseBody,
        JsonObject? payload = null);

    void LogApiSinkFailure(
        ContractBase contract,
        string correlationId,
        string method,
        string url,
        JsonObject? payload,
        Exception exception,
        int? statusCode = null,
        string? responseBody = null);

    void LogTransformation(
        ContractBase contract,
        string correlationId,
        IReadOnlyCollection<ApiFieldConfig>? mappings,
        JsonNode? sourcePayload,
        JsonNode? outboundPayload,
        IReadOnlyCollection<string>? mappingErrors = null,
        string? sourceLabel = null);

    void LogMappingIssue(
        string contractId,
        string? correlationId,
        string transformationType,
        string message,
        IEnumerable<string>? sourceFields = null,
        IEnumerable<string>? sinkFields = null);

    void LogRuntimeState(
        ContractBase contract,
        string correlationId,
        ContractMessageStateEntity state,
        bool duplicateSkipped = false,
        string? note = null);

    void LogFileScan(
        ContractBase contract,
        string correlationId,
        string inputDirectory,
        string searchPattern,
        int discoveredFileCount);

    void LogFileProcessingStarted(
        ContractBase contract,
        string correlationId,
        string filePath);

    void LogFileProcessingCompleted(
        ContractBase contract,
        string correlationId,
        string filePath,
        int totalRows,
        int succeededRows,
        int failedRows,
        string? errorReportPath,
        string? destinationPath);

    void LogPortRequest(
        ContractBase contract,
        string correlationId,
        string listenerPath,
        JsonObject inboundPayload);

    void LogPortResponse(
        ContractBase contract,
        string correlationId,
        string listenerPath,
        JsonObject? outboundPayload,
        int statusCode,
        object? responseBody);
}
