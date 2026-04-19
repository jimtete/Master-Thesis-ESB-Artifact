using System.Text.Json.Nodes;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public sealed class FeedbackContractExecutionContext
{
    /// <summary>
    /// The forward contract that produced this execution outcome.
    /// </summary>
    public string SourceContractId { get; set; } = string.Empty;

    /// <summary>
    /// Optional forward contract name for logging / observability.
    /// </summary>
    public string? SourceContractName { get; set; }

    /// <summary>
    /// A generalized business key derived from the forward flow.
    /// Used for correlation and tracing across systems.
    /// </summary>
    public string BusinessKey { get; set; } = string.Empty;

    /// <summary>
    /// The normalized execution status of the forward flow.
    /// Suggested values: Success, Failed, PartiallySucceeded, Skipped
    /// </summary>
    public string ExecutionStatus { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message when the forward flow fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Machine-readable error code if available.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Original inbound payload received by the forward flow.
    /// </summary>
    public JsonObject? OriginalPayload { get; set; }

    /// <summary>
    /// Final transformed payload used by the forward flow.
    /// </summary>
    public JsonObject? TransformedPayload { get; set; }

    /// <summary>
    /// Optional response payload returned by the sink system.
    /// </summary>
    public JsonObject? ResponsePayload { get; set; }

    /// <summary>
    /// Correlation values collected during execution.
    /// Key = logical name, Value = actual runtime value
    /// </summary>
    public Dictionary<string, string> CorrelationValues { get; set; } = new();

    /// <summary>
    /// UTC time when the forward execution completed.
    /// </summary>
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}