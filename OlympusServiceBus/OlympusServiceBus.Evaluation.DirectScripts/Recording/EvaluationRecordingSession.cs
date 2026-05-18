namespace OlympusServiceBus.Evaluation.DirectScripts.Recording;

public sealed class EvaluationRecordingSession
{
    public string SessionId { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? StoppedAtUtc { get; set; }
}
