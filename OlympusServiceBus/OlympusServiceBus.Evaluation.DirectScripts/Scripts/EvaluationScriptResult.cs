using OlympusServiceBus.Evaluation.DirectScripts;

namespace OlympusServiceBus.Evaluation.DirectScripts.Scripts;

public sealed class EvaluationScriptResult
{
    public string ScriptName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public TimeSpan Duration { get; set; }

    public DateTimeOffset? StartTimestampUtc { get; set; }

    public DateTimeOffset? EndTimestampUtc { get; set; }

    public string? Message { get; set; }

    public Exception? Exception { get; set; }

    public string? TriggerType { get; set; }

    public string? SourceType { get; set; }

    public string? SinkType { get; set; }

    public int? ProcessedRowsOrMessagesCount { get; set; }

    public EvaluationMetrics? Metrics { get; set; }
}
