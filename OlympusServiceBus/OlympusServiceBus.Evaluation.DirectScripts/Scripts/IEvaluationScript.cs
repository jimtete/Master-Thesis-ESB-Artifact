namespace OlympusServiceBus.Evaluation.DirectScripts.Scripts;

public interface IEvaluationScript
{
    string Name { get; }

    string Description { get; }

    Task<EvaluationScriptResult> RunAsync(
        EvaluationScriptContext context,
        CancellationToken cancellationToken);
}
