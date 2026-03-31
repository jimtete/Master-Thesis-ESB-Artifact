using System.Text.Json;
using System.Text.Json.Serialization;
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

                var doc = JsonSerializer.Deserialize<PortToFileDocument>(json, JsonOpts);
                var contract = doc?.PortToFile;

                if (contract is null)
                {
                    var direct = JsonSerializer.Deserialize<PortToFileContract>(json, JsonOpts);
                    if (direct is null || string.IsNullOrWhiteSpace(direct.Name))
                    {
                        continue;
                    }

                    contract = direct;
                }

                contract.ContractId = string.IsNullOrWhiteSpace(contract.ContractId)
                    ? Path.GetFileNameWithoutExtension(file)
                    : contract.ContractId;

                list.Add(contract);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load PortToFile contract file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded PortToFile contracts: {Count}", list.Count);
        return list;
    }

    private sealed class PortToFileDocument
    {
        public PortToFileContract? PortToFile { get; set; }
    }
}