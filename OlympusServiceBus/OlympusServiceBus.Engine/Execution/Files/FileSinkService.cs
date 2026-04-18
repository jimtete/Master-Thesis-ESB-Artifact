using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Configuration.File;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.Files;

public sealed class FileSinkService
{
    private readonly FileSinkWriter _writer;

    public FileSinkService(FileSinkWriter writer)
    {
        _writer = writer;
    }
    
    public Task<FileWriteResult> WriteAsync(
        ContractBase contract,
        FileSinkConfig sink,
        JsonObject payload,
        ApiFieldConfig[]? mappings,
        CancellationToken cancellationToken)
    {
        var orderedColumns = BuildOrderedColumns(mappings, payload);

        return _writer.AppendAsync(
            contractName: contract.Name,
            sink: sink,
            payload: payload,
            orderedColumns: orderedColumns,
            cancellationToken: cancellationToken);
    }
    
    private static IReadOnlyList<string> BuildOrderedColumns(
        ApiFieldConfig[]? mappings,
        JsonObject payload)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings ?? Array.Empty<ApiFieldConfig>())
        {
            switch (mapping.TransformationType)
            {
                case TransformationType.Direct:
                case TransformationType.Join:
                    AddIfValid(mapping.SinkFieldName.Value);
                    break;

                case TransformationType.Split:
                case TransformationType.Expression:
                    foreach (var sinkField in mapping.SinkFields ?? Array.Empty<SinkField>())
                    {
                        AddIfValid(sinkField.Value);
                    }
                    break;
            }
        }

        if (ordered.Count == 0)
        {
            foreach (var key in payload.Select(x => x.Key))
            {
                AddIfValid(key);
            }
        }

        return ordered;

        void AddIfValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (seen.Add(value))
                ordered.Add(value);
        }
    }
}
