using System.Globalization;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.Utils.Configuration.File;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.FileToApi;

public class CsvLoopProcessor
{
    public sealed record RowFailure(int RowNumber, List<string> Errors);
    public sealed record Result(int TotalRows, int SucceededRows, int FailedRows, List<RowFailure> Failures);

    /// <summary>
    /// Reads a CSV stream, validates required columns, and invokes onRow for each row
    /// </summary>
    public async Task<Result> ProcessAsync(
        Stream csvStream,
        LoopCsvRule rule,
        PortToApiContract contractForTypes,
        Func<int, JsonObject, CancellationToken, Task<EngineResult>> onRow,
        CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream);

        var headerLine = rule.HasHeader ? await reader.ReadLineAsync(ct) : null;
        if (rule.HasHeader && string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException("CSV has no header line.");
        }

        string[] headers;
        Dictionary<string, int> headerIndex;

        if (rule.HasHeader)
        {
            headers = CsvLineParser.Parse(headerLine!, rule.Delimiter).ToArray();
            headerIndex = BuildHeaderIndex(headers);

            // Validate required columns exist
            var missing = rule.RequiredColumns
                .Where(c => !headerIndex.ContainsKey(c))
                .ToArray();

            if (missing.Length > 0)
                throw new InvalidOperationException($"CSV missing required columns: {string.Join(", ", missing)}");
        }
        else
        {
            // If no header, you must bind by index instead (not implemented yet)
            throw new NotSupportedException("LoopCSV without header is not supported yet.");
        }

        // Build a lookup from FieldName -> RequestField type info
        var fieldTypeMap = (contractForTypes.Request?.Fields ?? Array.Empty<PortToApiRequestField>())
            .Where(f => !string.IsNullOrWhiteSpace(f.FieldName))
            .ToDictionary(f => f.FieldName, f => f, StringComparer.OrdinalIgnoreCase);

        var failures = new List<RowFailure>();
        var total = 0;
        var ok = 0;
        var fail = 0;

        // Data rows
        var rowNumber = 1; // header is line 1
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            rowNumber++;

            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            total++;

            var cells = CsvLineParser.Parse(line, rule.Delimiter);
            var rowErrors = new List<string>();

            // Build inbound JsonObject for this row
            var inbound = new JsonObject();

            foreach (var b in rule.Bindings)
            {
                if (string.IsNullOrWhiteSpace(b.Column) || string.IsNullOrWhiteSpace(b.Field))
                    continue;

                if (!headerIndex.TryGetValue(b.Column, out var idx))
                {
                    rowErrors.Add($"Binding references missing column '{b.Column}'");
                    continue;
                }

                var raw = idx < cells.Count ? cells[idx] : "";
                raw = raw?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(raw))
                    raw = b.DefaultValue ?? "";

                if (b.Required && string.IsNullOrWhiteSpace(raw))
                {
                    rowErrors.Add($"Row missing required value for column '{b.Column}' (field '{b.Field}')");
                    continue;
                }

                // Convert to correct JsonNode based on contract Request.Fields types (if present)
                if (fieldTypeMap.TryGetValue(b.Field, out var fieldInfo))
                {
                    if (!TryConvert(raw, fieldInfo, out var node, out var err))
                        rowErrors.Add($"Field '{b.Field}' conversion error: {err}");
                    else
                        inbound[b.Field] = node;
                }
                else
                {
                    // If field not in Request.Fields, default to string
                    inbound[b.Field] = raw;
                }
            }

            if (rowErrors.Count > 0)
            {
                fail++;
                failures.Add(new RowFailure(rowNumber, rowErrors));
                continue;
            }

            var result = await onRow(rowNumber, inbound, ct);
            if (result.Success) ok++;
            else
            {
                fail++;
                failures.Add(new RowFailure(rowNumber, new List<string>
                {
                    result.Error ?? "Row execution failed",
                    $"StatusCode={result.StatusCode}"
                }));
            }
        }

        return new Result(total, ok, fail, failures);
    }

    private static Dictionary<string, int> BuildHeaderIndex(string[] headers)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var h = (headers[i] ?? "").Trim();
            if (h.Length == 0) continue;
            if (!dict.ContainsKey(h))
            {
                dict[h] = i;
            }
        }
        return dict;
    }

    private static bool TryConvert(string raw, PortToApiRequestField field, out JsonNode? node, out string error)
    {
        node = null;
        error = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            node = null;
            return true;
        }

        try
        {
            switch (field.Type)
            {
                case JsonFieldType.String:
                    node = raw;
                    return true;
                
                case JsonFieldType.Integer:
                    if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    {
                        node = l;
                        return true;
                    }
                    error = $"Expected integer, got '{raw}'";
                    return false;
                
                case JsonFieldType.Number:
                    if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    {
                        node = d;
                        return true;
                    }
                    error = $"Expected number, got '{raw}'";
                    return false;
                
                case JsonFieldType.Boolean:
                    if (bool.TryParse(raw, out var b))
                    {
                        node = b;
                        return true;
                    }
                    error = $"Expected boolean, got '{raw}'";
                    return false;
                
                default:
                    node = raw;
                    return true;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}