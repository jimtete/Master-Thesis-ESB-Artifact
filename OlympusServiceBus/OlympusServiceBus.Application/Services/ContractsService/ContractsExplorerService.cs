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
            "PortToApi" => BuildPortToApiContractJson(request, filePath),
            "FileToApi" => BuildFileToApiContractJson(request, filePath),
            _ => throw new NotSupportedException($"Contract type '{request.ContractType}' is not supported yet.")
        };
    }

    private static string BuildApiToApiContractJson(CreateContractRequest request, string filePath)
    {
        var contractId = Path.GetFileNameWithoutExtension(filePath);

        var businessKeyFields = BuildBusinessKeyFields(request.BusinessKeyField);
        var effectiveMappings = BuildEffectiveMappings(request.Mappings);

        var sourceEndpoint = string.IsNullOrWhiteSpace(request.SourceEndpoint)
            ? "http://localhost:5001/source"
            : request.SourceEndpoint.Trim();

        var sinkEndpoint = string.IsNullOrWhiteSpace(request.SinkEndpoint)
            ? "http://localhost:5002/sink"
            : request.SinkEndpoint.Trim();

        var sourceMethod = NormalizeMethod(request.SourceMethod, "GET");
        var sinkMethod = NormalizeMethod(request.SinkMethod, "POST");

        var document = new
        {
            ApiToApi = new
            {
                ContractId = contractId,
                Name = request.Name.Trim(),
                Enabled = true,
                ContractType = "ApiToApi",
                BusinessKeyFields = businessKeyFields,
                Schedule = BuildScheduleObject(request.Schedule),
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

    private static string BuildPortToApiContractJson(CreateContractRequest request, string filePath)
    {
        var contractId = Path.GetFileNameWithoutExtension(filePath);
        var effectiveMappings = BuildEffectiveMappings(request.Mappings);
        var businessKeyFields = BuildBusinessKeyFields(request.BusinessKeyField);

        var listenerPath = string.IsNullOrWhiteSpace(request.ListenerPath)
            ? "/incoming"
            : request.ListenerPath.Trim();

        if (!listenerPath.StartsWith('/'))
        {
            listenerPath = "/" + listenerPath;
        }

        var listenerMethod = NormalizeMethod(request.ListenerMethod, "POST");
        var sinkEndpoint = string.IsNullOrWhiteSpace(request.SinkEndpoint)
            ? "http://localhost:5002/sink"
            : request.SinkEndpoint.Trim();
        var sinkMethod = NormalizeMethod(request.SinkMethod, "POST");

        var document = new
        {
            PortToApi = new
            {
                ContractId = contractId,
                Name = request.Name.Trim(),
                Enabled = true,
                ContractType = "PortToApi",
                BusinessKeyFields = businessKeyFields,
                Listener = new
                {
                    Path = listenerPath,
                    Method = listenerMethod
                },
                Sink = new
                {
                    Method = sinkMethod,
                    Endpoint = sinkEndpoint
                },
                Request = (object?)null,
                Mappings = effectiveMappings
            }
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string BuildFileToApiContractJson(CreateContractRequest request, string filePath)
    {
        var contractId = Path.GetFileNameWithoutExtension(filePath);
        var effectiveMappings = BuildEffectiveMappings(request.Mappings);

        var directory = string.IsNullOrWhiteSpace(request.FilePath)
            ? @"C:\Temp"
            : request.FilePath.Trim();

        var searchPattern = NormalizeSearchPattern(request.FileType);
        var sinkEndpoint = string.IsNullOrWhiteSpace(request.SinkEndpoint)
            ? "http://localhost:5002/sink"
            : request.SinkEndpoint.Trim();
        var sinkMethod = NormalizeMethod(request.SinkMethod, "POST");

        var csvSourceFields = request.Mappings
            .SelectMany(m => m.SourceFields ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var bindings = csvSourceFields.Select(field => new
        {
            Column = field,
            Field = field,
            Required = false,
            DefaultValue = (string?)null
        }).ToArray();

        var document = new
        {
            FileToApi = new
            {
                ContractId = contractId,
                Name = request.Name.Trim(),
                Enabled = true,
                ContractType = "FileToApi",
                Schedule = BuildScheduleObject(request.Schedule),
                Source = new
                {
                    Directory = directory,
                    SearchPattern = searchPattern,
                    IncludeSubdirectories = false,
                    ProcessedDirectory = Path.Combine(directory, "processed"),
                    ErrorDirectory = Path.Combine(directory, "error")
                },
                Sink = new
                {
                    Method = sinkMethod,
                    Endpoint = sinkEndpoint
                },
                Mappings = effectiveMappings,
                Rules = new
                {
                    LoopCSV = new
                    {
                        Delimiter = ",",
                        HasHeader = true,
                        RequiredColumns = csvSourceFields,
                        Bindings = bindings
                    }
                },
                Request = (object?)null
            }
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string[] BuildBusinessKeyFields(string businessKeyField)
    {
        var businessKeyFields = string.IsNullOrWhiteSpace(businessKeyField)
            ? new[] { "id" }
            : businessKeyField
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

        if (businessKeyFields.Length == 0)
        {
            businessKeyFields = new[] { "id" };
        }

        return businessKeyFields;
    }

    private static object[] BuildEffectiveMappings(List<ContractFieldMappingModel>? mappings)
    {
        var builtMappings = mappings is { Count: > 0 }
            ? mappings
                .Select(BuildApiFieldMapping)
                .Where(static x => x is not null)
                .Cast<object>()
                .ToArray()
            : Array.Empty<object>();

        if (builtMappings.Length > 0)
        {
            return builtMappings;
        }

        return
        [
            new
            {
                SourceFieldName = "id",
                SinkFieldName = "id",
                TransformationType = "Direct",
                Separator = " "
            }
        ];
    }

    private static object BuildScheduleObject(ScheduleEditorRequest? schedule)
    {
        if (schedule is null)
        {
            return new
            {
                Mode = "Manual"
            };
        }

        var mode = NormalizeScheduleMode(schedule.Mode);

        return mode switch
        {
            "Manual" => new
            {
                Mode = "Manual"
            },

            "AdHoc" => new
            {
                Mode = "AdHoc",
                RunAt = schedule.RunAt,
                TimeZone = string.IsNullOrWhiteSpace(schedule.TimeZone)
                    ? null
                    : schedule.TimeZone.Trim()
            },

            "Interval" => new
            {
                Mode = "Interval",
                Every = new
                {
                    Value = schedule.IntervalValue <= 0 ? 1 : schedule.IntervalValue,
                    Unit = NormalizeIntervalUnit(schedule.IntervalUnit)
                }
            },

            "Recurring" => new
            {
                Mode = "Recurring",
                CronExpression = string.IsNullOrWhiteSpace(schedule.CronExpression)
                    ? null
                    : schedule.CronExpression.Trim(),
                TimeZone = string.IsNullOrWhiteSpace(schedule.TimeZone)
                    ? null
                    : schedule.TimeZone.Trim()
            },

            _ => new
            {
                Mode = "Manual"
            }
        };
    }

    private static string NormalizeScheduleMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "Manual";
        }

        return mode.Trim() switch
        {
            "Manual" => "Manual",
            "AdHoc" => "AdHoc",
            "Interval" => "Interval",
            "Recurring" => "Recurring",
            _ => "Manual"
        };
    }

    private static string NormalizeIntervalUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return "Minutes";
        }

        return unit.Trim() switch
        {
            "Seconds" => "Seconds",
            "Minutes" => "Minutes",
            "Hours" => "Hours",
            "Days" => "Days",
            _ => "Minutes"
        };
    }

    private static string NormalizeMethod(string? method, string fallback)
    {
        return string.IsNullOrWhiteSpace(method)
            ? fallback
            : method.Trim().ToUpperInvariant();
    }

    private static string NormalizeSearchPattern(string? fileType)
    {
        if (string.IsNullOrWhiteSpace(fileType))
        {
            return "*.csv";
        }

        var trimmed = fileType.Trim();

        if (trimmed.Contains('*'))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('.'))
        {
            return $"*{trimmed}";
        }

        return $"*.{trimmed.ToLowerInvariant()}";
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
            var fileNode = new FileExplorerNode
            {
                Name = Path.GetFileNameWithoutExtension(jsonFile.Name),
                FullPath = jsonFile.FullName,
                IsDirectory = false,
            };

            ApplyContractMetadata(fileNode);
            node.Children.Add(fileNode);
        }

        return node;
    }

    private static void ApplyContractMetadata(FileExplorerNode node)
    {
        if (node.IsDirectory || !File.Exists(node.FullPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(node.FullPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            string? contractType = null;
            JsonElement contractElement = default;

            if (root.TryGetProperty("ApiToApi", out var apiToApiElement))
            {
                contractType = "ApiToApi";
                contractElement = apiToApiElement;
            }
            else if (root.TryGetProperty("PortToApi", out var portToApiElement))
            {
                contractType = "PortToApi";
                contractElement = portToApiElement;
            }
            else if (root.TryGetProperty("FileToApi", out var fileToApiElement))
            {
                contractType = "FileToApi";
                contractElement = fileToApiElement;
            }

            if (contractType is null)
            {
                return;
            }

            node.ContractType = contractType;

            var scheduleMode = "None";

            if (contractElement.ValueKind == JsonValueKind.Object &&
                contractElement.TryGetProperty("Schedule", out var scheduleElement) &&
                scheduleElement.ValueKind == JsonValueKind.Object)
            {
                scheduleMode = GetStringProperty(scheduleElement, "Mode", "None");
            }

            node.ScheduleMode = scheduleMode;
            node.CanExecuteManually =
                (string.Equals(contractType, "ApiToApi", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(contractType, "FileToApi", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(scheduleMode, "Manual", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            node.ContractType = null;
            node.ScheduleMode = null;
            node.CanExecuteManually = false;
        }
    }

    private static object? BuildApiFieldMapping(ContractFieldMappingModel mapping)
    {
        var transformationType = string.IsNullOrWhiteSpace(mapping.Transformation)
            ? "Direct"
            : mapping.Transformation.Trim();

        var sourceFields = (mapping.SourceFields ?? [])
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .ToArray();

        var targetFields = (mapping.TargetFields ?? [])
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(static x => x.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var separator = string.IsNullOrWhiteSpace(mapping.Separator)
            ? " "
            : mapping.Separator;

        var expression = string.IsNullOrWhiteSpace(mapping.Expression)
            ? null
            : mapping.Expression.Trim();

        return transformationType switch
        {
            "Direct" when sourceFields.Length > 0 && targetFields.Length > 0 => new
            {
                SourceFieldName = sourceFields[0],
                SinkFieldName = targetFields[0],
                TransformationType = "Direct",
                Separator = separator
            },

            "Split" when sourceFields.Length > 0 && targetFields.Length > 0 => new
            {
                SourceFieldName = sourceFields[0],
                SinkFields = targetFields,
                TransformationType = "Split",
                Separator = separator
            },

            "Join" when sourceFields.Length > 0 && targetFields.Length > 0 => new
            {
                SourceFields = sourceFields,
                SinkFieldName = targetFields[0],
                TransformationType = "Join",
                Separator = separator
            },

            "Expression" when sourceFields.Length > 0 && targetFields.Length > 0 && !string.IsNullOrWhiteSpace(expression) => new
            {
                SourceFields = sourceFields,
                SinkFields = targetFields,
                TransformationType = "Expression",
                Separator = separator,
                Expression = expression
            },

            _ => null
        };
    }

    private static string GetStringProperty(JsonElement element, string propertyName, string fallback = "")
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement))
        {
            return fallback;
        }

        return propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString() ?? fallback
            : fallback;
    }
}