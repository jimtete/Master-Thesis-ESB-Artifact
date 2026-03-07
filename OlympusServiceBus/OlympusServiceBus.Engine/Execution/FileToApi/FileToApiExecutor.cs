using System.Text.Json;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.FileToApi;

public sealed class FileToApiExecutor(
    ILogger<FileToApiExecutor> logger,
    CsvLoopProcessor csvLoop,
    IPortToApiEngine portToApiEngine)
{
    public async Task ExecuteOnce(FileToApiContract c, CancellationToken ct)
    {
        var inputDir = c.Source?.Directory;
        var pattern = string.IsNullOrWhiteSpace(c.Source?.SearchPattern) ? "*.csv" : c.Source!.SearchPattern;

        if (string.IsNullOrWhiteSpace(inputDir) || !Directory.Exists(inputDir))
        {
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
            logger.LogWarning("[{Contract}] Missing Rules.LoopCSV configuration.", c.ContractId);
            return;
        }

        // Adapter contract so we can reuse PortToApiEngine + (optionally) CSV typing from Request.Fields
        var portToApi = ToPortToApiContract(c);

        var searchOption = (c.Source?.IncludeSubdirectories ?? false)
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.EnumerateFiles(inputDir, pattern, searchOption)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            return;

        logger.LogInformation("[{Contract}] Found {Count} file(s) matching {Pattern} in {Dir}",
            c.ContractId, files.Count, pattern, inputDir);

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            var correlationId = $"{c.ContractId}:{fileName}:{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

            try
            {
                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var loopResult = await csvLoop.ProcessAsync(
                    csvStream: fs,
                    rule: rule,
                    contractForTypes: portToApi, // if your processor uses Request.Fields for type conversion
                    onRow: async (rowNumber, inboundRow, token) =>
                    {
                        // One call per row (LoopCSV)
                        return await portToApiEngine.ExecuteAsync(
                            portToApi,
                            inboundRow,
                            new EngineContext(correlationId),
                            token);
                    },
                    ct: ct);

                if (loopResult.FailedRows > 0)
                {
                    logger.LogWarning(
                        "[{Contract}] File {File} processed with failures. Total={Total} OK={OK} Failed={Failed}",
                        c.ContractId, fileName, loopResult.TotalRows, loopResult.SucceededRows, loopResult.FailedRows);

                    var reportPath = Path.Combine(errorDir, $"{fileName}.errors.json");
                    var reportJson = JsonSerializer.Serialize(new
                    {
                        contractId = c.ContractId,
                        file = fileName,
                        loopResult.TotalRows,
                        loopResult.SucceededRows,
                        loopResult.FailedRows,
                        loopResult.Failures
                    });
                    await File.WriteAllTextAsync(reportPath, reportJson, ct);

                    MoveTo(filePath, Path.Combine(errorDir, fileName));
                }
                else
                {
                    logger.LogInformation(
                        "[{Contract}] File {File} processed OK. Total={Total} OK={OK}",
                        c.ContractId, fileName, loopResult.TotalRows, loopResult.SucceededRows);

                    MoveTo(filePath, Path.Combine(processedDir, fileName));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{Contract}] Failed processing file {File}", c.ContractId, fileName);
                MoveTo(filePath, Path.Combine(errorDir, fileName));
            }
        }
    }

    private static PortToApiContract ToPortToApiContract(FileToApiContract c)
    {
        // Minimal adapter: PortToApiEngine uses Sink + Mappings (+ optional Request)
        return new PortToApiContract
        {
            ContractId = c.ContractId,
            Enabled = c.Enabled,
            Sink = c.Sink,
            Mappings = c.Mappings ?? [],
            Request = c.Request,
            Name =  c.Name,
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