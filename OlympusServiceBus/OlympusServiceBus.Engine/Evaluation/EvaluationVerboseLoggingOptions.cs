namespace OlympusServiceBus.Engine.Evaluation;

public sealed class EvaluationVerboseLoggingOptions
{
    public const string SectionName = "Evaluation:VerboseLogging";

    public bool Enabled { get; set; }

    public int MaxBodyLength { get; set; } = 4096;
}
