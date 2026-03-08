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

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).ToList();

        _logger.LogInformation("Scanning contracts in {Dir}. JSON files found: {Count}", dir, files.Count);

        var loadedContracts = new List<LoadedContract>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);

                // ---- Wrapper shapes ----

                var a2a = JsonSerializer.Deserialize<ApiToApiDocument>(json, JsonOptions);
                if (a2a?.ApiToApi is not null)
                {
                    EnsureContractId(a2a.ApiToApi, file);
                    loadedContracts.Add(new LoadedContract(a2a.ApiToApi, file));
                    continue;
                }

                var f2a = JsonSerializer.Deserialize<FileToApiDocument>(json, JsonOptions);
                if (f2a?.FileToApi is not null)
                {
                    EnsureContractId(f2a.FileToApi, file);
                    loadedContracts.Add(new LoadedContract(f2a.FileToApi, file));
                    continue;
                }

                var p2a = JsonSerializer.Deserialize<PortToApiDocument>(json, JsonOptions);
                if (p2a?.PortToApi is not null)
                {
                    EnsureContractId(p2a.PortToApi, file);
                    loadedContracts.Add(new LoadedContract(p2a.PortToApi, file));
                    continue;
                }

                // ---- Direct shapes ----

                var directA2a = JsonSerializer.Deserialize<ApiToApiContract>(json, JsonOptions);
                if (directA2a is not null)
                {
                    EnsureContractId(directA2a, file);
                    loadedContracts.Add(new LoadedContract(directA2a, file));
                    continue;
                }

                var directF2a = JsonSerializer.Deserialize<FileToApiContract>(json, JsonOptions);
                if (directF2a is not null)
                {
                    EnsureContractId(directF2a, file);
                    loadedContracts.Add(new LoadedContract(directF2a, file));
                    continue;
                }

                var directP2a = JsonSerializer.Deserialize<PortToApiContract>(json, JsonOptions);
                if (directP2a is not null)
                {
                    EnsureContractId(directP2a, file);
                    loadedContracts.Add(new LoadedContract(directP2a, file));
                    continue;
                }

                _logger.LogDebug("Ignoring JSON (no known contract wrapper) in file: {File}", file);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Couldn't load contract from file: {File}", file);
            }
        }

        ValidateContractNames(loadedContracts);
        ValidateBusinessKeyFields(loadedContracts);

        var result = loadedContracts
            .Select(x => x.Contract)
            .ToList();

        _logger.LogInformation("Contracts loaded: {Count}", result.Count);
        return result;
    }

    private static void EnsureContractId(ContractBase contract, string filePath)
    {
        if (string.IsNullOrWhiteSpace(contract.ContractId))
            contract.ContractId = Path.GetFileNameWithoutExtension(filePath);
    }

    private static void ValidateContractNames(List<LoadedContract> contracts)
    {
        var missingNames = contracts
            .Where(x => string.IsNullOrWhiteSpace(x.Contract.Name))
            .Select(x => x.FilePath)
            .ToList();

        if (missingNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"All contracts must define a non-empty Name. Invalid file(s): {string.Join(", ", missingNames)}");
        }

        var duplicateGroups = contracts
            .GroupBy(x => x.Contract.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Count > 0)
        {
            var details = duplicateGroups
                .Select(g => $"{g.Key} => [{string.Join(", ", g.Select(x => x.FilePath))}]");

            throw new InvalidOperationException(
                $"Contract names must be unique. Duplicates found: {string.Join("; ", details)}");
        }
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
    
    private static void ValidateBusinessKeyFields(List<LoadedContract> contracts)
    {
        var invalidContracts = contracts
            .Where(x =>
                x.Contract.BusinessKeyFields is null ||
                x.Contract.BusinessKeyFields.Length == 0 ||
                x.Contract.BusinessKeyFields.All(string.IsNullOrWhiteSpace))
            .Select(x => $"{x.Contract.Name} ({x.FilePath})")
            .ToList();

        if (invalidContracts.Count > 0)
        {
            throw new InvalidOperationException(
                $"All contracts must define at least one non-empty BusinessKeyField. Invalid contract(s): {string.Join(", ", invalidContracts)}");
        }
    }

    private sealed record LoadedContract(ContractBase Contract, string FilePath);

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