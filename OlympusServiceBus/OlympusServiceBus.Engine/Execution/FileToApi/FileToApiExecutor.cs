using System.Diagnostics;
using System.Text.Json;
using OlympusServiceBus.Engine.Evaluation;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.FileToApi;

public sealed class FileToApiExecutor(
    ILogger<FileToApiExecutor> logger,
    CsvLoopProcessor csvLoop,
    IPortToApiEngine portToApiEngine,
    IEvaluationRecordingService evaluationRecordingService,
    IEvaluationVerboseLogger evaluationVerboseLogger)
{
    public async Task ExecuteOnce(FileToApiContract c, string triggerType, CancellationToken ct)
    {
        var activeSession = await evaluationRecordingService.GetActiveSessionAsync(ct);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var executionCorrelationId = evaluationVerboseLogger.CreateCorrelationId(c.ContractId, "file-to-api");
        var status = "NoFiles";
        var errorMessages = new List<string>();
        var processedCount = 0;
        var hadFailures = false;

        evaluationVerboseLogger.LogExecutionStarted(c, triggerType, executionCorrelationId, startedAtUtc);

        try
        {
            var inputDir = c.Source?.Directory;
            var pattern = string.IsNullOrWhiteSpace(c.Source?.SearchPattern) ? "*.csv" : c.Source!.SearchPattern;

            if (string.IsNullOrWhiteSpace(inputDir) || !Directory.Exists(inputDir))
            {
                status = "Failed";
                errorMessages.Add($"Input directory missing or not found: {inputDir}");
                logger.LogWarning("[{Contract}] Input directory missing/not found: {Dir}", c.ContractId, inputDir);
                return;
            }

            var processedDir = c.Source?.ProcessedDirectory;
            if (string.IsNullOrWhiteSpace(processedDir))
                processedDir = Path.Combine(inputDir, "Processed");

            var errorDir = c.Source?.ErrorDirectory;
            if (string.IsNullOrWhiteSpace(errorDir))
                errorDir = Path.Combine(inputDir, "Error");

            Directory.CreateDirectory(processedDir);
            Directory.CreateDirectory(errorDir);

            var rule = c.Rules?.LoopCSV;
            if (rule is null)
            {
                status = "Failed";
                errorMessages.Add("Missing Rules.LoopCSV configuration.");
                logger.LogWarning("[{Contract}] Missing Rules.LoopCSV configuration.", c.ContractId);
                return;
            }

            var portToApi = ToPortToApiContract(c);
            var searchOption = (c.Source?.IncludeSubdirectories ?? false)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.EnumerateFiles(inputDir, pattern, searchOption)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                evaluationVerboseLogger.LogFileScan(c, executionCorrelationId, inputDir, pattern, 0);
                return;
            }

            evaluationVerboseLogger.LogFileScan(c, executionCorrelationId, inputDir, pattern, files.Count);

            logger.LogInformation(
                "[{Contract}] Found {Count} file(s) matching {Pattern} in {Dir}",
                c.ContractId,
                files.Count,
                pattern,
                inputDir);

            status = "Success";

            foreach (var filePath in files)
            {
                ct.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(filePath);
                var correlationId = $"{c.ContractId}:{fileName}:{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
                evaluationVerboseLogger.LogFileProcessingStarted(c, correlationId, filePath);
                string? errorReportPath = null;
                string? destinationPath = null;

                try
                {
                    await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    var loopResult = await csvLoop.ProcessAsync(
                        csvStream: fs,
                        rule: rule,
                        contractForTypes: portToApi,
                        onRow: async (_, inboundRow, token) =>
                        {
                            return await portToApiEngine.ExecuteAsync(
                                portToApi,
                                inboundRow,
                                new EngineContext(correlationId),
                                token);
                        },
                        ct: ct);

                    processedCount += loopResult.TotalRows;

                    if (loopResult.FailedRows > 0)
                    {
                        hadFailures = true;
                        errorMessages.Add($"File '{fileName}' had {loopResult.FailedRows} failed row(s).");

                        logger.LogWarning(
                            "[{Contract}] File {File} processed with failures. Total={Total} OK={OK} Failed={Failed}",
                            c.ContractId,
                            fileName,
                            loopResult.TotalRows,
                            loopResult.SucceededRows,
                            loopResult.FailedRows);

                        errorReportPath = Path.Combine(errorDir, $"{fileName}.errors.json");
                        var reportJson = JsonSerializer.Serialize(new
                        {
                            contractId = c.ContractId,
                            file = fileName,
                            loopResult.TotalRows,
                            loopResult.SucceededRows,
                            loopResult.FailedRows,
                            loopResult.Failures
                        });
                        await File.WriteAllTextAsync(errorReportPath, reportJson, ct);

                        destinationPath = Path.Combine(errorDir, fileName);
                        MoveTo(filePath, destinationPath);
                    }
                    else
                    {
                        logger.LogInformation(
                            "[{Contract}] File {File} processed OK. Total={Total} OK={OK}",
                            c.ContractId,
                            fileName,
                            loopResult.TotalRows,
                            loopResult.SucceededRows);

                        destinationPath = Path.Combine(processedDir, fileName);
                        MoveTo(filePath, destinationPath);
                    }

                    evaluationVerboseLogger.LogFileProcessingCompleted(
                        c,
                        correlationId,
                        filePath,
                        loopResult.TotalRows,
                        loopResult.SucceededRows,
                        loopResult.FailedRows,
                        errorReportPath,
                        destinationPath);
                }
                catch (Exception ex)
                {
                    hadFailures = true;
                    errorMessages.Add($"File '{fileName}' failed: {ex.Message}");
                    logger.LogError(ex, "[{Contract}] Failed processing file {File}", c.ContractId, fileName);
                    destinationPath = Path.Combine(errorDir, fileName);
                    MoveTo(filePath, destinationPath);
                    evaluationVerboseLogger.LogFileProcessingCompleted(
                        c,
                        correlationId,
                        filePath,
                        0,
                        0,
                        1,
                        errorReportPath,
                        destinationPath);
                }
            }

            if (hadFailures)
            {
                status = "Failed";
            }
        }
        finally
        {
            stopwatch.Stop();
            evaluationVerboseLogger.LogExecutionCompleted(
                c,
                triggerType,
                executionCorrelationId,
                startedAtUtc,
                DateTimeOffset.UtcNow,
                stopwatch.ElapsedMilliseconds,
                status,
                errorMessages.Count > 0 ? string.Join(" | ", errorMessages) : null);

            if (activeSession is not null)
            {
                var metadata = EvaluationContractMetadataResolver.Resolve(c);
                await evaluationRecordingService.RecordJobAsync(new EvaluationJobRecord
                {
                    RecordingSessionId = activeSession.SessionId,
                    ContractId = c.ContractId,
                    ContractName = c.Name,
                    ContractType = metadata.ContractType,
                    ScheduleMode = metadata.ScheduleMode,
                    TriggerType = triggerType,
                    SourceType = metadata.SourceType,
                    SinkType = metadata.SinkType,
                    StartTimestampUtc = startedAtUtc,
                    EndTimestampUtc = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    Status = status,
                    ErrorMessage = errorMessages.Count > 0 ? string.Join(" | ", errorMessages) : null,
                    ProcessedRowsOrMessagesCount = processedCount
                }, ct);
            }
        }
    }

    private static PortToApiContract ToPortToApiContract(FileToApiContract c)
    {
        return new PortToApiContract
        {
            ContractId = c.ContractId,
            Enabled = c.Enabled,
            Sink = c.Sink,
            Mappings = c.Mappings ?? [],
            Request = c.Request,
            Name = c.Name,
            BusinessKeyFields = c.BusinessKeyFields
        };
    }

    private static void MoveTo(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);

        if (File.Exists(to))
        {
            var dir = Path.GetDirectoryName(to)!;
            var name = Path.GetFileNameWithoutExtension(to);
            var ext = Path.GetExtension(to);
            to = Path.Combine(dir, $"{name}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}");
        }

        File.Move(from, to);
    }
}
