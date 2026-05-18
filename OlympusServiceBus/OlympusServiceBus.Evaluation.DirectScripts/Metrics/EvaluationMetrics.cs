namespace OlympusServiceBus.Evaluation.DirectScripts;

public sealed class EvaluationMetrics
{
    public int MessagesProcessed { get; set; }

    public int SuccessfulOperations { get; set; }

    public int FailedOperations { get; set; }

    public long BytesProcessed { get; set; }

    public TimeSpan? TotalDuration { get; set; }

    public TimeSpan? AverageOperationDuration { get; set; }
}
