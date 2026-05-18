using OlympusServiceBus.Evaluation.DirectScripts;
using OlympusServiceBus.Evaluation.DirectScripts.Metrics;
using OlympusServiceBus.Evaluation.DirectScripts.Recording;
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
var evaluationRecordingService = new FileEvaluationRecordingService();

if (args.Length > 0 && IsManagementCommand(args[0]))
{
    return await HandleManagementCommandAsync(
        args,
        evaluationRecordingService,
        outputDirectory,
        cancellationTokenSource.Token);
}

var activeRecordingSession = await evaluationRecordingService.GetActiveSessionAsync(cancellationTokenSource.Token);

var context = new EvaluationScriptContext(
    baseDirectory,
    outputDirectory,
    httpClient,
    evaluationRecordingService,
    activeRecordingSession);

var registry = new ScriptRegistry();
var selectedScriptName = args.Length > 0
    ? args[0]
    : ScriptRegistry.DefaultScriptName;

PrintAvailableScripts(registry);
Console.WriteLine();
Console.WriteLine($"Selected script: {selectedScriptName}");
PrintRecordingStatus(evaluationRecordingService, activeRecordingSession);

if (!registry.TryGetByName(selectedScriptName, out var script))
{
    Console.Error.WriteLine($"Script '{selectedScriptName}' was not found.");
    return 1;
}

var result = await ExecuteScriptAsync(script, context, cancellationTokenSource.Token);

PrintResult(result);

if (activeRecordingSession is not null)
{
    await evaluationRecordingService.RecordJobAsync(
        CreateJobRecord(script, result, activeRecordingSession),
        cancellationTokenSource.Token);
}

return result.Success ? 0 : 1;

static async Task<EvaluationScriptResult> ExecuteScriptAsync(
    IEvaluationScript script,
    EvaluationScriptContext context,
    CancellationToken cancellationToken)
{
    var startedAtUtc = DateTimeOffset.UtcNow;
    var timer = SimpleExecutionTimer.StartNew();

    try
    {
        var result = await script.RunAsync(context, cancellationToken);
        var endedAtUtc = DateTimeOffset.UtcNow;
        result.ScriptName = string.IsNullOrWhiteSpace(result.ScriptName) ? script.Name : result.ScriptName;
        result.Duration = timer.Elapsed;
        result.StartTimestampUtc ??= startedAtUtc;
        result.EndTimestampUtc ??= endedAtUtc;
        result.Metrics ??= new EvaluationMetrics();
        result.Metrics.TotalDuration ??= result.Duration;

        return result;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        var endedAtUtc = DateTimeOffset.UtcNow;
        return new EvaluationScriptResult
        {
            ScriptName = script.Name,
            Success = false,
            Duration = timer.Elapsed,
            StartTimestampUtc = startedAtUtc,
            EndTimestampUtc = endedAtUtc,
            Message = "Execution was cancelled.",
            TriggerType = EvaluationRecordingDefaults.ManualTriggerType,
            Metrics = new EvaluationMetrics
            {
                TotalDuration = timer.Elapsed
            }
        };
    }
    catch (Exception exception)
    {
        var endedAtUtc = DateTimeOffset.UtcNow;
        return new EvaluationScriptResult
        {
            ScriptName = script.Name,
            Success = false,
            Duration = timer.Elapsed,
            StartTimestampUtc = startedAtUtc,
            EndTimestampUtc = endedAtUtc,
            Message = "Execution failed with an unhandled exception.",
            Exception = exception,
            TriggerType = EvaluationRecordingDefaults.ManualTriggerType,
            Metrics = new EvaluationMetrics
            {
                FailedOperations = 1,
                TotalDuration = timer.Elapsed
            }
        };
    }
}

static async Task<int> HandleManagementCommandAsync(
    string[] args,
    IEvaluationRecordingService evaluationRecordingService,
    string outputDirectory,
    CancellationToken cancellationToken)
{
    switch (args[0])
    {
        case "--recording-status":
        {
            var activeSession = await evaluationRecordingService.GetActiveSessionAsync(cancellationToken);
            PrintRecordingStatus(evaluationRecordingService, activeSession);
            return 0;
        }
        case "--start-recording":
        {
            var session = await evaluationRecordingService.StartSessionAsync(cancellationToken);
            Console.WriteLine($"Recording session started: {session.SessionId}");
            Console.WriteLine($"Storage root: {evaluationRecordingService.StorageRootPath}");
            return 0;
        }
        case "--stop-recording":
        {
            var sessionId = args.Length > 1
                ? args[1]
                : (await evaluationRecordingService.GetActiveSessionAsync(cancellationToken))?.SessionId;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Console.Error.WriteLine("No active recording session was found.");
                return 1;
            }

            var stoppedSession = await evaluationRecordingService.StopSessionAsync(sessionId, cancellationToken);
            if (stoppedSession is null)
            {
                Console.Error.WriteLine($"Recording session '{sessionId}' was not found.");
                return 1;
            }

            Console.WriteLine($"Recording session stopped: {stoppedSession.SessionId}");
            return 0;
        }
        case "--export-recording":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: --export-recording <sessionId> [destinationFilePath]");
                return 1;
            }

            var sessionId = args[1];
            var destinationFilePath = args.Length > 2
                ? args[2]
                : Path.Combine(outputDirectory, $"evaluation-recording-{sessionId}.csv");

            await evaluationRecordingService.ExportSessionToCsvAsync(
                sessionId,
                destinationFilePath,
                cancellationToken);

            Console.WriteLine($"Recording session exported: {destinationFilePath}");
            return 0;
        }
        default:
            Console.Error.WriteLine($"Unknown management command: {args[0]}");
            Console.Error.WriteLine("Supported commands: --recording-status, --start-recording, --stop-recording, --export-recording");
            return 1;
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

static void PrintRecordingStatus(
    IEvaluationRecordingService evaluationRecordingService,
    EvaluationRecordingSession? activeRecordingSession)
{
    Console.WriteLine($"Recording storage: {evaluationRecordingService.StorageRootPath}");

    if (activeRecordingSession is null)
    {
        Console.WriteLine("Recording session: none active");
        return;
    }

    Console.WriteLine($"Recording session: {activeRecordingSession.SessionId}");
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

static EvaluationJobRecord CreateJobRecord(
    IEvaluationScript script,
    EvaluationScriptResult result,
    EvaluationRecordingSession activeRecordingSession)
{
    var status = result.Success ? EvaluationRecordingDefaults.SuccessStatus : EvaluationRecordingDefaults.FailedStatus;
    var errorMessage = result.Success
        ? null
        : result.Exception?.Message ?? result.Message;

    return new EvaluationJobRecord
    {
        RecordingSessionId = activeRecordingSession.SessionId,
        ContractId = script.Name,
        ContractName = script.Name,
        ContractType = EvaluationRecordingDefaults.DirectScriptContractType,
        ScheduleMode = EvaluationRecordingDefaults.ManualScheduleMode,
        TriggerType = result.TriggerType ?? EvaluationRecordingDefaults.ManualTriggerType,
        SourceType = result.SourceType ?? EvaluationRecordingDefaults.UnknownSourceType,
        SinkType = result.SinkType ?? EvaluationRecordingDefaults.UnknownSinkType,
        StartTimestampUtc = result.StartTimestampUtc ?? DateTimeOffset.UtcNow.Subtract(result.Duration),
        EndTimestampUtc = result.EndTimestampUtc ?? DateTimeOffset.UtcNow,
        DurationMilliseconds = (long)result.Duration.TotalMilliseconds,
        Status = status,
        ErrorMessage = errorMessage,
        ProcessedRowsOrMessagesCount = result.ProcessedRowsOrMessagesCount ?? result.Metrics?.MessagesProcessed
    };
}

static bool IsManagementCommand(string arg)
{
    return string.Equals(arg, "--recording-status", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--start-recording", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--stop-recording", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--export-recording", StringComparison.OrdinalIgnoreCase);
}
