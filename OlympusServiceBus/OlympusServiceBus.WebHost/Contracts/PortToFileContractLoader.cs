using System.Text.Json;
using System.Text.Json.Serialization;
using OlympusServiceBus.Utils;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Contracts;

public sealed class PortToFileContractLoader : IPortToFileContractLoader
{
    private readonly ILogger<PortToFileContractLoader> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new SourceFieldJsonConverter(),
            new SinkFieldJsonConverter()
        }
    };

    public PortToFileContractLoader(ILogger<PortToFileContractLoader> logger)
    {
        _logger = logger;
    }

    public List<PortToFileContract> Load(string? rootPath)
    {
        var list = new List<PortToFileContract>();

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.LogWarning("Contracts folder not found: {RootPath}", rootPath);
            return list;
        }

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);

                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Wrapper shape: { "PortToFile": { ... } }
                if (root.ValueKind == JsonValueKind.Object &&
                    TryGetPropertyCaseInsensitive(root, "PortToFile", out _))
                {
                    var wrapped = JsonSerializer.Deserialize<PortToFileDocument>(json, JsonOpts);
                    var wrappedContract = wrapped?.PortToFile;

                    if (wrappedContract is not null && !string.IsNullOrWhiteSpace(wrappedContract.Name))
                    {
                        wrappedContract.ContractId = string.IsNullOrWhiteSpace(wrappedContract.ContractId)
                            ? Path.GetFileNameWithoutExtension(file)
                            : wrappedContract.ContractId;

                        list.Add(wrappedContract);
                    }

                    continue;
                }

                // Direct shape
                if (root.ValueKind == JsonValueKind.Object &&
                    LooksLikeDirectPortToFile(root))
                {
                    var direct = JsonSerializer.Deserialize<PortToFileContract>(json, JsonOpts);

                    if (direct is not null && !string.IsNullOrWhiteSpace(direct.Name))
                    {
                        direct.ContractId = string.IsNullOrWhiteSpace(direct.ContractId)
                            ? Path.GetFileNameWithoutExtension(file)
                            : direct.ContractId;

                        list.Add(direct);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse PortToFile contract file: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load PortToFile contract file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded PortToFile contracts: {Count}", list.Count);
        return list;
    }

    private static bool LooksLikeDirectPortToFile(JsonElement root)
    {
        return TryGetPropertyCaseInsensitive(root, "Name", out _) &&
               TryGetPropertyCaseInsensitive(root, "Listener", out _) &&
               TryGetPropertyCaseInsensitive(root, "Sink", out _);
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed class PortToFileDocument
    {
        public PortToFileContract? PortToFile { get; set; }
    }
}