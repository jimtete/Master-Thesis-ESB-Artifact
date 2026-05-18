namespace OlympusServiceBus.Evaluation.DirectScripts.Metrics;

public sealed class EvaluationOperationTracker
{
    private readonly List<TimeSpan> _operationDurations = [];

    public int MessagesProcessed { get; private set; }

    public int SuccessfulOperations { get; private set; }

    public int FailedOperations { get; private set; }

    public long BytesProcessed { get; private set; }

    public void RecordSuccess(
        TimeSpan duration,
        int messagesProcessed = 1,
        long bytesProcessed = 0)
    {
        _operationDurations.Add(duration);
        MessagesProcessed += messagesProcessed;
        SuccessfulOperations += 1;
        BytesProcessed += bytesProcessed;
    }

    public void RecordFailure(
        TimeSpan duration,
        int messagesProcessed = 1,
        long bytesProcessed = 0)
    {
        _operationDurations.Add(duration);
        MessagesProcessed += messagesProcessed;
        FailedOperations += 1;
        BytesProcessed += bytesProcessed;
    }

    public EvaluationMetrics CreateMetrics(TimeSpan? totalDuration = null)
    {
        return new EvaluationMetrics
        {
            MessagesProcessed = MessagesProcessed,
            SuccessfulOperations = SuccessfulOperations,
            FailedOperations = FailedOperations,
            BytesProcessed = BytesProcessed,
            TotalDuration = totalDuration,
            AverageOperationDuration = _operationDurations.Count == 0
                ? null
                : TimeSpan.FromTicks((long)_operationDurations.Average(duration => duration.Ticks))
        };
    }
}
