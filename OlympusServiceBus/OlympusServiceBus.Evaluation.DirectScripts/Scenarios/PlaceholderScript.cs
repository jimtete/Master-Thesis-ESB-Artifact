using OlympusServiceBus.Evaluation.DirectScripts;
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
        var generatedAt = DateTimeOffset.UtcNow;
        var outputFileName = $"placeholder-script-output-{generatedAt:yyyyMMddHHmmss}.txt";
        var outputFilePath = Path.Combine(context.OutputDirectory, outputFileName);

        var fileContents = string.Join(
            Environment.NewLine,
            [
                "OlympusServiceBus direct-script evaluation placeholder",
                $"GeneratedUtc: {generatedAt:O}",
                $"BaseDirectory: {context.BaseDirectory}",
                "Purpose: Future hardcoded integration logic will be implemented here."
            ]);

        await File.WriteAllTextAsync(outputFilePath, fileContents, cancellationToken);

        return new EvaluationScriptResult
        {
            ScriptName = Name,
            Success = true,
            Message = $"Placeholder execution completed. Output written to '{outputFilePath}'.",
            Metrics = new EvaluationMetrics
            {
                MessagesProcessed = 1,
                SuccessfulOperations = 1,
                FailedOperations = 0,
                BytesProcessed = new FileInfo(outputFilePath).Length
            }
        };
    }
}
