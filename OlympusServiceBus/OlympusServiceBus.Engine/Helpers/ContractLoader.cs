using System.Text.Json;
using System.Text.Json.Serialization;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Helpers;

public sealed class ContractLoader : IContractLoader
{
    private readonly ILogger<ContractLoader> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new SourceFieldJsonConverter(),
            new SinkFieldJsonConverter()
        }
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

        _logger.LogInformation("Scanning forward contracts in {Dir}. JSON files found: {Count}", dir, files.Count);

        var loadedContracts = new List<LoadedContract>();

        foreach (var file in files)
        {
            if (ShouldSkipForwardContractFile(file))
            {
                continue;
            }

            var json = File.ReadAllText(file);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Skipping empty forward contract file: {File}", file);
                continue;
            }

            try
            {
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

                var a2f = JsonSerializer.Deserialize<ApiToFileDocument>(json, JsonOptions);
                if (a2f?.ApiToFile is not null)
                {
                    EnsureContractId(a2f.ApiToFile, file);
                    loadedContracts.Add(new LoadedContract(a2f.ApiToFile, file));
                    continue;
                }

                var f2f = JsonSerializer.Deserialize<FileToFileDocument>(json, JsonOptions);
                if (f2f?.FileToFile is not null)
                {
                    EnsureContractId(f2f.FileToFile, file);
                    loadedContracts.Add(new LoadedContract(f2f.FileToFile, file));
                    continue;
                }

                var p2f = JsonSerializer.Deserialize<PortToFileDocument>(json, JsonOptions);
                if (p2f?.PortToFile is not null)
                {
                    EnsureContractId(p2f.PortToFile, file);
                    loadedContracts.Add(new LoadedContract(p2f.PortToFile, file));
                    continue;
                }

                // ---- Direct shapes ----

                var directA2a = JsonSerializer.Deserialize<ApiToApiContract>(json, JsonOptions);
                if (directA2a is not null && LooksLikeForwardContract(directA2a))
                {
                    EnsureContractId(directA2a, file);
                    loadedContracts.Add(new LoadedContract(directA2a, file));
                    continue;
                }

                var directF2a = JsonSerializer.Deserialize<FileToApiContract>(json, JsonOptions);
                if (directF2a is not null && LooksLikeForwardContract(directF2a))
                {
                    EnsureContractId(directF2a, file);
                    loadedContracts.Add(new LoadedContract(directF2a, file));
                    continue;
                }

                var directP2a = JsonSerializer.Deserialize<PortToApiContract>(json, JsonOptions);
                if (directP2a is not null && LooksLikeForwardContract(directP2a))
                {
                    EnsureContractId(directP2a, file);
                    loadedContracts.Add(new LoadedContract(directP2a, file));
                    continue;
                }

                var directA2f = JsonSerializer.Deserialize<ApiToFileContract>(json, JsonOptions);
                if (directA2f is not null && LooksLikeForwardContract(directA2f))
                {
                    EnsureContractId(directA2f, file);
                    loadedContracts.Add(new LoadedContract(directA2f, file));
                    continue;
                }

                var directF2f = JsonSerializer.Deserialize<FileToFileContract>(json, JsonOptions);
                if (directF2f is not null && LooksLikeForwardContract(directF2f))
                {
                    EnsureContractId(directF2f, file);
                    loadedContracts.Add(new LoadedContract(directF2f, file));
                    continue;
                }

                var directP2f = JsonSerializer.Deserialize<PortToFileContract>(json, JsonOptions);
                if (directP2f is not null && LooksLikeForwardContract(directP2f))
                {
                    EnsureContractId(directP2f, file);
                    loadedContracts.Add(new LoadedContract(directP2f, file));
                    continue;
                }

                _logger.LogWarning("Skipping unrecognized forward contract JSON file: {File}", file);
            }
            catch (JsonException e)
            {
                _logger.LogWarning(e, "Skipping invalid forward contract JSON file: {File}", file);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error while loading forward contract from file: {File}", file);
            }
        }

        var result = SanitizeForwardContracts(loadedContracts)
            .Select(x => x.Contract)
            .ToList();

        _logger.LogInformation("Forward contracts loaded: {Count}", result.Count);
        return result;
    }

    public List<FeedbackContractBase> LoadAllFeedbackContracts(string contractDirectory)
    {
        var dir = ResolveContractsDirectory(contractDirectory);
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Contracts directory not found: {Dir}", dir);
            return new List<FeedbackContractBase>();
        }

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).ToList();

        _logger.LogInformation("Scanning feedback-contracts in {Dir}. JSON files found: {Count}", dir, files.Count);

        var loadedFeedbackContracts = new List<LoadedFeedbackContract>();

        foreach (var file in files)
        {
            if (ShouldSkipFeedbackContractFile(file))
            {
                continue;
            }

            var json = File.ReadAllText(file);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Skipping empty feedback-contract file: {File}", file);
                continue;
            }

            try
            {
                // ---- Wrapper shapes ----

                var apiStatus = JsonSerializer.Deserialize<ApiStatusFeedbackContractDocument>(json, JsonOptions);
                if (apiStatus?.ApiStatusFeedbackContract is not null)
                {
                    EnsureFeedbackContractId(apiStatus.ApiStatusFeedbackContract, file);
                    loadedFeedbackContracts.Add(new LoadedFeedbackContract(apiStatus.ApiStatusFeedbackContract, file));
                    continue;
                }

                // ---- Direct shapes ----

                var directApiStatus = JsonSerializer.Deserialize<ApiStatusFeedbackContract>(json, JsonOptions);
                if (directApiStatus is not null && LooksLikeFeedbackContract(directApiStatus))
                {
                    EnsureFeedbackContractId(directApiStatus, file);
                    loadedFeedbackContracts.Add(new LoadedFeedbackContract(directApiStatus, file));
                    continue;
                }

                _logger.LogWarning("Skipping unrecognized feedback-contract JSON file: {File}", file);
            }
            catch (JsonException e)
            {
                _logger.LogWarning(e, "Skipping invalid feedback-contract JSON file: {File}", file);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error while loading feedback-contract from file: {File}", file);
            }
        }

        var result = SanitizeFeedbackContracts(loadedFeedbackContracts)
            .Select(x => x.Contract)
            .ToList();

        _logger.LogInformation("FeedbackContracts loaded: {Count}", result.Count);
        return result;
    }

    private static void EnsureContractId(ContractBase contract, string filePath)
    {
        if (string.IsNullOrWhiteSpace(contract.ContractId))
            contract.ContractId = Path.GetFileNameWithoutExtension(filePath);
    }

    private static void EnsureFeedbackContractId(FeedbackContractBase contract, string filePath)
    {
        if (string.IsNullOrWhiteSpace(contract.ContractId))
            contract.ContractId = Path.GetFileNameWithoutExtension(filePath);
    }

    private static bool LooksLikeForwardContract(ContractBase? contract)
    {
        return contract is not null &&
               !string.IsNullOrWhiteSpace(contract.Name);
    }

    private static bool LooksLikeFeedbackContract(FeedbackContractBase? contract)
    {
        return contract is not null &&
               !string.IsNullOrWhiteSpace(contract.ContractType);
    }

    private List<LoadedContract> SanitizeForwardContracts(List<LoadedContract> contracts)
    {
        var result = new List<LoadedContract>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in contracts)
        {
            if (string.IsNullOrWhiteSpace(item.Contract.Name))
            {
                _logger.LogWarning(
                    "Skipping forward contract because Name is missing. File: {File}",
                    item.FilePath);
                continue;
            }

            if (item.Contract.BusinessKeyFields is null ||
                item.Contract.BusinessKeyFields.Length == 0 ||
                item.Contract.BusinessKeyFields.All(string.IsNullOrWhiteSpace))
            {
                _logger.LogWarning(
                    "Skipping forward contract '{ContractName}' because BusinessKeyFields is missing or empty. File: {File}",
                    item.Contract.Name,
                    item.FilePath);
                continue;
            }

            var normalizedName = item.Contract.Name.Trim();

            if (!seenNames.Add(normalizedName))
            {
                _logger.LogWarning(
                    "Skipping duplicate forward contract '{ContractName}'. File: {File}",
                    normalizedName,
                    item.FilePath);
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private List<LoadedFeedbackContract> SanitizeFeedbackContracts(List<LoadedFeedbackContract> contracts)
    {
        var result = new List<LoadedFeedbackContract>();
        var seenContractIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in contracts)
        {
            if (string.IsNullOrWhiteSpace(item.Contract.ContractId))
            {
                _logger.LogWarning(
                    "Skipping feedback-contract because ContractId is missing. File: {File}",
                    item.FilePath);
                continue;
            }

            var normalizedContractId = item.Contract.ContractId.Trim();

            if (!seenContractIds.Add(normalizedContractId))
            {
                _logger.LogWarning(
                    "Skipping duplicate feedback-contract '{ContractId}'. File: {File}",
                    normalizedContractId,
                    item.FilePath);
                continue;
            }

            result.Add(item);
        }

        return result;
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

    private sealed record LoadedContract(ContractBase Contract, string FilePath);
    private sealed record LoadedFeedbackContract(FeedbackContractBase Contract, string FilePath);

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

    private sealed class ApiToFileDocument
    {
        public ApiToFileContract? ApiToFile { get; set; }
    }

    private sealed class FileToFileDocument
    {
        public FileToFileContract? FileToFile { get; set; }
    }

    private sealed class PortToFileDocument
    {
        public PortToFileContract? PortToFile { get; set; }
    }

    private sealed class ApiStatusFeedbackContractDocument
    {
        public ApiStatusFeedbackContract? ApiStatusFeedbackContract { get; set; }
    }

    private static bool ShouldSkipForwardContractFile(string filePath)
    {
        return filePath.Contains($"{Path.DirectorySeparatorChar}feedback{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               || filePath.Contains($"{Path.DirectorySeparatorChar}Input{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipFeedbackContractFile(string filePath)
    {
        return !filePath.Contains($"{Path.DirectorySeparatorChar}feedback{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }
}