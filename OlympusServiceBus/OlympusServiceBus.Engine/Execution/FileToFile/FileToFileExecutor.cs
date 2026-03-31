using System.Text.Json;
using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Engine.Execution.PortToFile;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.FileToFile;

public sealed class FileToFileExecutor(
    ILogger<FileToFileExecutor> logger,
    CsvLoopProcessor csvLoop,
    IPortToFileEngine portToFileEngine)
{
    public async Task ExecuteOnce(FileToFileContract c, CancellationToken ct)
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

        // Adapter only for CSV field typing via Request.Fields
        var portToApiForTypes = ToPortToApiContract(c);

        // Actual execution target
        var portToFile = ToPortToFileContract(c);

        var searchOption = (c.Source?.IncludeSubdirectories ?? false)
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.EnumerateFiles(inputDir, pattern, searchOption)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            return;

        logger.LogInformation(
            "[{Contract}] Found {Count} file(s) matching {Pattern} in {Dir}",
            c.ContractId,
            files.Count,
            pattern,
            inputDir);

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
                    contractForTypes: portToApiForTypes,
                    onRow: async (_, inboundRow, token) =>
                    {
                        return await portToFileEngine.ExecuteAsync(
                            portToFile,
                            inboundRow,
                            new EngineContext(correlationId),
                            token);
                    },
                    ct: ct);

                if (loopResult.FailedRows > 0)
                {
                    logger.LogWarning(
                        "[{Contract}] File {File} processed with failures. Total={Total} OK={OK} Failed={Failed}",
                        c.ContractId,
                        fileName,
                        loopResult.TotalRows,
                        loopResult.SucceededRows,
                        loopResult.FailedRows);

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
                        c.ContractId,
                        fileName,
                        loopResult.TotalRows,
                        loopResult.SucceededRows);

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

    private static PortToApiContract ToPortToApiContract(FileToFileContract c)
    {
        return new PortToApiContract
        {
            ContractId = c.ContractId,
            Name = c.Name,
            Enabled = c.Enabled,
            Request = c.Request,
            Mappings = c.Mappings ?? Array.Empty<ApiFieldConfig>(),
            BusinessKeyFields = c.BusinessKeyFields,
            Sink = new ApiConfig()
        };
    }

    private static PortToFileContract ToPortToFileContract(FileToFileContract c)
    {
        return new PortToFileContract
        {
            ContractId = c.ContractId,
            Name = c.Name,
            Enabled = c.Enabled,
            Sink = c.Sink,
            Request = c.Request,
            Mappings = c.Mappings ?? Array.Empty<ApiFieldConfig>(),
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