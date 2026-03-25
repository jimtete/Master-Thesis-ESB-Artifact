using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OlympusServiceBusApplication.Models;
using OlympusServiceBusApplication.Models.Contracts;

namespace OlympusServiceBusApplication.Services.ContractsService;

public class ContractsExplorerService : IContractsExplorerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<FileExplorerNode> LoadTreeAsync(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path cannot be null or empty.", nameof(rootPath));
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Contracts root path was not found: {rootPath}");
        }

        return await Task.Run(() => BuildDirectoryNode(rootPath));
    }

    public async Task CreateDirectoryAsync(string parentPath, string directoryName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentException("Parent path cannot be null or empty.", nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            throw new ArgumentException("Directory name cannot be null or empty.", nameof(directoryName));
        }

        if (!Directory.Exists(parentPath))
        {
            throw new DirectoryNotFoundException($"Parent path was not found: {parentPath}");
        }

        var targetDirectoryPath = Path.Combine(parentPath, directoryName.Trim());

        await Task.Run(() => Directory.CreateDirectory(targetDirectoryPath));
    }

    public async Task CreateContractFileAsync(string parentPath, CreateContractRequest request, string? existingFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentPath);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Contract name cannot be null or empty.", nameof(request));
        }

        if (!Directory.Exists(parentPath))
        {
            throw new DirectoryNotFoundException($"Parent directory was not found: {parentPath}");
        }

        var normalizedFileName = NormalizeContractFileName(request.Name);
        var targetFilePath = Path.Combine(parentPath, normalizedFileName);

        var json = BuildContractJson(request, targetFilePath);
        await File.WriteAllTextAsync(targetFilePath, json);

        if (!string.IsNullOrWhiteSpace(existingFilePath) &&
            !string.Equals(existingFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(existingFilePath))
        {
            File.Delete(existingFilePath);
        }
    }

    private static string NormalizeContractFileName(string contractName)
    {
        var trimmedName = contractName.Trim();

        return trimmedName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? trimmedName
            : $"{trimmedName}.json";
    }

    private static string BuildContractJson(CreateContractRequest request, string filePath)
    {
        return request.ContractType switch
        {
            "ApiToApi" => BuildApiToApiContractJson(request, filePath),
            _ => throw new NotSupportedException($"Contract type '{request.ContractType}' is not supported yet.")
        };
    }

    private static string BuildApiToApiContractJson(CreateContractRequest request, string filePath)
    {
        var contractId = Path.GetFileNameWithoutExtension(filePath);

        var businessKeyField = string.IsNullOrWhiteSpace(request.BusinessKeyField)
            ? "id"
            : request.BusinessKeyField.Trim();

        var mappings = request.Mappings is { Count: > 0 }
            ? request.Mappings
                .Where(x =>
                    x.SourceFields is { Count: > 0 } &&
                    x.TargetFields is { Count: > 0 })
                .Select(x => new
                {
                    SourceFields = x.SourceFields
                        .Where(static s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToArray(),

                    TargetFields = x.TargetFields
                        .Where(static s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToArray(),

                    Transformation = string.IsNullOrWhiteSpace(x.Transformation)
                        ? "Direct"
                        : x.Transformation.Trim(),

                    Separator = x.Separator ?? " "
                })
                .Where(x => x.SourceFields.Length > 0 && x.TargetFields.Length > 0)
                .ToArray()
            : [];

        var effectiveMappings = mappings.Length > 0
            ? mappings
            : new[]
            {
                new
                {
                    SourceFields = new[] { "id" },
                    TargetFields = new[] { "id" },
                    Transformation = "Direct",
                    Separator = " "
                }
            };

        var sourceEndpoint = string.IsNullOrWhiteSpace(request.SourceEndpoint)
            ? "http://localhost:5001/source"
            : request.SourceEndpoint.Trim();

        var sinkEndpoint = string.IsNullOrWhiteSpace(request.SinkEndpoint)
            ? "http://localhost:5002/sink"
            : request.SinkEndpoint.Trim();

        var sourceMethod = string.IsNullOrWhiteSpace(request.SourceMethod)
            ? "GET"
            : request.SourceMethod.Trim().ToUpperInvariant();

        var sinkMethod = string.IsNullOrWhiteSpace(request.SinkMethod)
            ? "POST"
            : request.SinkMethod.Trim().ToUpperInvariant();

        var document = new
        {
            ApiToApi = new
            {
                ContractId = contractId,
                Name = request.Name.Trim(),
                Enabled = true,
                ContractType = "ApiToApi",
                BusinessKeyFields = new[] { businessKeyField },
                Schedule = new
                {
                    Mode = "AdHoc",
                    RunAt = DateTime.UtcNow
                },
                Source = new
                {
                    Method = sourceMethod,
                    Endpoint = sourceEndpoint
                },
                Sink = new
                {
                    Method = sinkMethod,
                    Endpoint = sinkEndpoint
                },
                Mappings = effectiveMappings
            }
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static FileExplorerNode BuildDirectoryNode(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);

        var node = new FileExplorerNode
        {
            Name = directoryInfo.Name,
            FullPath = directoryInfo.FullName,
            IsDirectory = true,
            IsExpanded = true,
        };

        foreach (var subDirectory in directoryInfo.GetDirectories().OrderBy(d => d.Name))
        {
            node.Children.Add(BuildDirectoryNode(subDirectory.FullName));
        }

        foreach (var jsonFile in directoryInfo.GetFiles("*.json").OrderBy(f => f.Name))
        {
            node.Children.Add(new FileExplorerNode
            {
                Name = Path.GetFileNameWithoutExtension(jsonFile.Name),
                FullPath = jsonFile.FullName,
                IsDirectory = false,
            });
        }

        return node;
    }
}