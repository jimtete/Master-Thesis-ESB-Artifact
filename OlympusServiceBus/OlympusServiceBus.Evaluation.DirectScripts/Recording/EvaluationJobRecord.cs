namespace OlympusServiceBus.Evaluation.DirectScripts.Recording;

public sealed class EvaluationJobRecord
{
    public string RecordingSessionId { get; set; } = string.Empty;

    public string ContractId { get; set; } = string.Empty;

    public string ContractName { get; set; } = string.Empty;

    public string ContractType { get; set; } = string.Empty;

    public string? ScheduleMode { get; set; }

    public string? TriggerType { get; set; }

    public string? SourceType { get; set; }

    public string? SinkType { get; set; }

    public DateTimeOffset StartTimestampUtc { get; set; }

    public DateTimeOffset EndTimestampUtc { get; set; }

    public long DurationMilliseconds { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public int? ProcessedRowsOrMessagesCount { get; set; }
}
