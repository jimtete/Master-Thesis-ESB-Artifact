using OlympusServiceBus.Evaluation.DirectScripts;

namespace OlympusServiceBus.Evaluation.DirectScripts.Scripts;

public sealed class EvaluationScriptResult
{
    public string ScriptName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public TimeSpan Duration { get; set; }

    public string? Message { get; set; }

    public Exception? Exception { get; set; }

    public EvaluationMetrics? Metrics { get; set; }
}
