using System.Text.Json;
using System.Text.Json.Serialization;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public sealed class ContractLoader : IContractLoader
{
    private readonly ILogger<ContractLoader> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ContractLoader(ILogger<ContractLoader> logger)
    {
        _logger = logger;
    }

    public List<ContractBase> LoadAllContracts(string contractDirectory)
    {
        var dir = ResolveContractsDirectory(contractDirectory);
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Contracts directory not found: {Dir}", dir);
            return new List<ContractBase>();
        }

        // IMPORTANT: recursive scan (your FileToApi contract is in a subfolder)
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).ToList();

        _logger.LogInformation("Scanning contracts in {Dir}. JSON files found: {Count}", dir, files.Count);

        var result = new List<ContractBase>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);

                // ---- Wrapper shapes (recommended) ----

                // { "ApiToApi": { ... } }
                var a2a = JsonSerializer.Deserialize<ApiToApiDocument>(json, JsonOptions);
                if (a2a?.ApiToApi is not null)
                {
                    EnsureContractId(a2a.ApiToApi, file);
                    result.Add(a2a.ApiToApi);
                    continue;
                }

                // { "FileToApi": { ... } }
                var f2a = JsonSerializer.Deserialize<FileToApiDocument>(json, JsonOptions);
                if (f2a?.FileToApi is not null)
                {
                    EnsureContractId(f2a.FileToApi, file);
                    result.Add(f2a.FileToApi);
                    continue;
                }

                // Optional if you also load PortToApi in Engine:
                // { "PortToApi": { ... } }
                var p2a = JsonSerializer.Deserialize<PortToApiDocument>(json, JsonOptions);
                if (p2a?.PortToApi is not null)
                {
                    EnsureContractId(p2a.PortToApi, file);
                    result.Add(p2a.PortToApi);
                    continue;
                }

                // ---- Direct shapes (optional fallback) ----

                var directA2a = JsonSerializer.Deserialize<ApiToApiContract>(json, JsonOptions);
                if (directA2a is not null)
                {
                    EnsureContractId(directA2a, file);
                    result.Add(directA2a);
                    continue;
                }

                var directF2a = JsonSerializer.Deserialize<FileToApiContract>(json, JsonOptions);
                if (directF2a is not null)
                {
                    EnsureContractId(directF2a, file);
                    result.Add(directF2a);
                    continue;
                }

                var directP2a = JsonSerializer.Deserialize<PortToApiContract>(json, JsonOptions);
                if (directP2a is not null)
                {
                    EnsureContractId(directP2a, file);
                    result.Add(directP2a);
                    continue;
                }

                _logger.LogDebug("Ignoring JSON (no known contract wrapper) in file: {File}", file);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Couldn't load contract from file: {File}", file);
            }
        }

        _logger.LogInformation("Contracts loaded: {Count}", result.Count);
        return result;
    }

    private static void EnsureContractId(ContractBase c, string filePath)
    {
        if (string.IsNullOrWhiteSpace(c.ContractId))
            c.ContractId = Path.GetFileNameWithoutExtension(filePath);
    }

    private static string ResolveContractsDirectory(string folderName)
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), folderName),
            Path.Combine(AppContext.BaseDirectory, folderName),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[1];
    }

    private sealed class ApiToApiDocument
    {
        public ApiToApiContract? ApiToApi { get; set; }
    }

    private sealed class FileToApiDocument
    {
        public FileToApiContract? FileToApi { get; set; }
    }

    private sealed class PortToApiDocument
    {
        public PortToApiContract? PortToApi { get; set; }
    }
}