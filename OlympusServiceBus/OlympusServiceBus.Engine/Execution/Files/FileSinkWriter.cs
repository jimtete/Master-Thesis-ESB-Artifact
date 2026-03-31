using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration.File;

namespace OlympusServiceBus.Engine.Execution.Files;

public sealed class FileSinkWriter
{
    private readonly ILogger<FileSinkWriter> _logger;

    public FileSinkWriter(ILogger<FileSinkWriter> logger)
    {
        _logger = logger;
    }
    
    public async Task<FileWriteResult> AppendAsync(
        string contractName,
        FileSinkConfig sink,
        JsonObject payload,
        IReadOnlyList<string> orderedColumns,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contractName))
            throw new InvalidOperationException("Contract name is required for file sink writing.");

        if (sink is null)
            throw new InvalidOperationException("File sink configuration is missing.");

        if (string.IsNullOrWhiteSpace(sink.Directory))
            throw new InvalidOperationException("File sink directory is missing.");

        var extension = NormalizeExtension(sink.FileExtension);
        var safeContractName = SanitizeFileName(contractName);
        var fileName = $"{safeContractName}-{DateTime.UtcNow:yyyyMMdd}.{extension}";
        var fullPath = Path.Combine(sink.Directory, fileName);

        Directory.CreateDirectory(sink.Directory);

        var fileExists = File.Exists(fullPath);
        var columns = orderedColumns
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (columns.Length == 0)
        {
            columns = payload
                .Select(x => x.Key)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);

        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        if (!fileExists || stream.Length == 0)
        {
            var header = string.Join(",", columns.Select(EscapeCsv));
            await writer.WriteLineAsync(header);
        }

        var row = string.Join(",", columns.Select(column =>
        {
            payload.TryGetPropertyValue(column, out var value);
            return EscapeCsv(ToFlatString(value));
        }));

        await writer.WriteLineAsync(row);
        await writer.FlushAsync();

        _logger.LogInformation(
            "Appended payload for contract {ContractName} to file {FilePath}",
            contractName,
            fullPath);

        return new FileWriteResult(fullPath, fileName);
    }
    
    private static string NormalizeExtension(string? extension)
    {
        var value = string.IsNullOrWhiteSpace(extension) ? "csv" : extension.Trim();
        return value.TrimStart('.');
    }
    
    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "contract" : sanitized;
    }
    
    private static string ToFlatString(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is not JsonValue value) return node.ToJsonString();
        if (value.TryGetValue<string>(out var s))
        {
            return s ?? string.Empty;
        }

        if (value.TryGetValue<bool>(out var b))
        {
            return b ? "true" : "false";
        }

        if (value.TryGetValue<int>(out var i))
        {
            return i.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<long>(out var l))
        {
            return l.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<decimal>(out var d))
        {
            return d.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<double>(out var db))
        {
            return db.ToString(CultureInfo.InvariantCulture);
        }

        return node.ToJsonString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes =
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r');

        if (needsQuotes)
        {
            return value;
        }
        
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

public sealed record FileWriteResult(string FullPath, string FileName);