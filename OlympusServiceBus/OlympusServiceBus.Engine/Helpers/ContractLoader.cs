using System.Text.Json;
using System.Text.Json.Serialization;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public class ContractLoader : IContractLoader
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
            return new List<ContractBase>();

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly).ToList();
        var result = new List<ContractBase>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);

                // Supports shape: { "ApiToApi": { ... } }
                var wrapper = JsonSerializer.Deserialize<ApiToApiDocument>(json, JsonOptions);
                if (wrapper?.ApiToApi is not null)
                {
                    wrapper.ApiToApi.ContractId = Path.GetFileNameWithoutExtension(file);
                    result.Add(wrapper.ApiToApi);
                    continue;
                }

                // Optional: support direct ApiToApiContract shape (no wrapper)
                var direct = JsonSerializer.Deserialize<ApiToApiContract>(json, JsonOptions);
                if (direct is not null)
                {
                    direct.ContractId = Path.GetFileNameWithoutExtension(file);
                    result.Add(direct);
                }
            }
            catch(Exception e)
            {
                _logger.LogError($"Couldn't load contract {file} with exception: {e.Message}");
            }
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

    private sealed class ApiToApiDocument
    {
        public ApiToApiContract? ApiToApi { get; set; }
    }
}