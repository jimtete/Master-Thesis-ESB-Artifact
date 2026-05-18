using OlympusServiceBus.Evaluation.DirectScripts;
using OlympusServiceBus.Evaluation.DirectScripts.Scripts;

using var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

var baseDirectory = AppContext.BaseDirectory;
var outputDirectory = Path.Combine(baseDirectory, "output");

Directory.CreateDirectory(outputDirectory);

using var httpClient = new HttpClient();

var context = new EvaluationScriptContext(
    baseDirectory,
    outputDirectory,
    httpClient);

var registry = new ScriptRegistry();
var selectedScriptName = args.Length > 0
    ? args[0]
    : ScriptRegistry.DefaultScriptName;

PrintAvailableScripts(registry);
Console.WriteLine();
Console.WriteLine($"Selected script: {selectedScriptName}");

if (!registry.TryGetByName(selectedScriptName, out var script))
{
    Console.Error.WriteLine($"Script '{selectedScriptName}' was not found.");
    return 1;
}

var result = await ExecuteScriptAsync(script, context, cancellationTokenSource.Token);

PrintResult(result);

return result.Success ? 0 : 1;

static async Task<EvaluationScriptResult> ExecuteScriptAsync(
    IEvaluationScript script,
    EvaluationScriptContext context,
    CancellationToken cancellationToken)
{
    var timer = SimpleExecutionTimer.StartNew();

    try
    {
        var result = await script.RunAsync(context, cancellationToken);
        result.ScriptName = string.IsNullOrWhiteSpace(result.ScriptName) ? script.Name : result.ScriptName;
        result.Duration = timer.Elapsed;
        result.Metrics ??= new EvaluationMetrics();
        result.Metrics.TotalDuration ??= result.Duration;

        return result;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return new EvaluationScriptResult
        {
            ScriptName = script.Name,
            Success = false,
            Duration = timer.Elapsed,
            Message = "Execution was cancelled.",
            Metrics = new EvaluationMetrics
            {
                TotalDuration = timer.Elapsed
            }
        };
    }
    catch (Exception exception)
    {
        return new EvaluationScriptResult
        {
            ScriptName = script.Name,
            Success = false,
            Duration = timer.Elapsed,
            Message = "Execution failed with an unhandled exception.",
            Exception = exception,
            Metrics = new EvaluationMetrics
            {
                FailedOperations = 1,
                TotalDuration = timer.Elapsed
            }
        };
    }
}

static void PrintAvailableScripts(ScriptRegistry registry)
{
    Console.WriteLine("Available evaluation scripts:");

    foreach (var availableScript in registry.AvailableScripts)
    {
        Console.WriteLine($"- {availableScript.Name}: {availableScript.Description}");
    }
}

static void PrintResult(EvaluationScriptResult result)
{
    Console.WriteLine();
    Console.WriteLine("Execution summary");
    Console.WriteLine("-----------------");
    Console.WriteLine($"Script: {result.ScriptName}");
    Console.WriteLine($"Success: {result.Success}");
    Console.WriteLine($"Duration: {result.Duration}");
    Console.WriteLine($"Message: {result.Message ?? "No message provided."}");

    if (result.Exception is not null)
    {
        Console.WriteLine($"Exception: {result.Exception.GetType().Name}: {result.Exception.Message}");
    }

    if (result.Metrics is not null)
    {
        Console.WriteLine("Metrics:");
        Console.WriteLine($"  MessagesProcessed: {result.Metrics.MessagesProcessed}");
        Console.WriteLine($"  SuccessfulOperations: {result.Metrics.SuccessfulOperations}");
        Console.WriteLine($"  FailedOperations: {result.Metrics.FailedOperations}");
        Console.WriteLine($"  BytesProcessed: {result.Metrics.BytesProcessed}");
        Console.WriteLine($"  TotalDuration: {result.Metrics.TotalDuration?.ToString() ?? "n/a"}");
        Console.WriteLine($"  AverageOperationDuration: {result.Metrics.AverageOperationDuration?.ToString() ?? "n/a"}");
    }
}
