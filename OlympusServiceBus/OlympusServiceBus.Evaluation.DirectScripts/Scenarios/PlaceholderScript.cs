using OlympusServiceBus.Evaluation.DirectScripts.Metrics;
using OlympusServiceBus.Evaluation.DirectScripts.Recording;
using OlympusServiceBus.Evaluation.DirectScripts.Scripts;

namespace OlympusServiceBus.Evaluation.DirectScripts.Scenarios;

public sealed class PlaceholderScript : IEvaluationScript
{
    public const string ScriptName = "PlaceholderScript";

    public string Name => ScriptName;

    public string Description => "Baseline placeholder script that demonstrates the direct-script execution flow.";

    public async Task<EvaluationScriptResult> RunAsync(
        EvaluationScriptContext context,
        CancellationToken cancellationToken)
    {
        var tracker = new EvaluationOperationTracker();
        var generatedAt = DateTimeOffset.UtcNow;
        var outputFileName = $"placeholder-script-output-{generatedAt:yyyyMMddHHmmss}.txt";
        var outputFilePath = Path.Combine(context.OutputDirectory, outputFileName);
        var writeTimer = SimpleExecutionTimer.StartNew();

        var fileContents = string.Join(
            Environment.NewLine,
            [
                "OlympusServiceBus direct-script evaluation placeholder",
                $"GeneratedUtc: {generatedAt:O}",
                $"BaseDirectory: {context.BaseDirectory}",
                $"ActiveRecordingSession: {context.ActiveRecordingSession?.SessionId ?? "<none>"}",
                "Purpose: Future hardcoded integration logic will be implemented here."
            ]);

        await File.WriteAllTextAsync(outputFilePath, fileContents, cancellationToken);
        var bytesProcessed = new FileInfo(outputFilePath).Length;
        tracker.RecordSuccess(writeTimer.Elapsed, bytesProcessed: bytesProcessed);

        return new EvaluationScriptResult
        {
            ScriptName = Name,
            Success = true,
            TriggerType = EvaluationRecordingDefaults.ManualTriggerType,
            SourceType = "PlaceholderSource",
            SinkType = "PlaceholderSink",
            ProcessedRowsOrMessagesCount = 1,
            Message = $"Placeholder execution completed. Output written to '{outputFilePath}'.",
            Metrics = tracker.CreateMetrics()
        };
    }
}
