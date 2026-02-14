using System.Text.Json;
using System.Text.Json.Serialization;
using OlympusServiceBus.Utils;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Contracts;

public sealed class PortToApiContractLoader : IPortToApiContractLoader
{
    private readonly ILogger<PortToApiContractLoader> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PortToApiContractLoader(ILogger<PortToApiContractLoader> logger)
    {
        _logger = logger;
    }

    public List<PortToApiContract> Load(string? rootPath)
    {
        var list = new List<PortToApiContract>();

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

                var doc = JsonSerializer.Deserialize<PortToApiDocument>(json, JsonOpts);
                var c = doc?.PortToApi;
                if (c is null) continue;

                c.ContractId = string.IsNullOrWhiteSpace(c.ContractId)
                    ? Path.GetFileNameWithoutExtension(file)
                    : c.ContractId;

                list.Add(c);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load contract file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded PortToApi contracts: {Count}", list.Count);
        return list;
    }
}