using System.IO;
using OlympusServiceBusApplication.Services.AppSettingsService;

namespace OlympusServiceBusApplication.Services.ContractsWorkspaceService;

public class ContractsWorkspaceService : IContractsWorkspaceService
{
    private readonly IAppSettingsService _appSettingsService;

    public ContractsWorkspaceService(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    public async Task<string> EnsureContractsDirectoryAsync()
    {
        var settings = await _appSettingsService.LoadAsync();

        if (string.IsNullOrWhiteSpace(settings.ContractRootDirectory))
        {
            throw new InvalidOperationException("Contract root directory is not configured");
        }

        var contractsDirectoryPath = Path.Combine(settings.ContractRootDirectory, "Contracts");

        if (!Directory.Exists(contractsDirectoryPath))
        {
            Directory.CreateDirectory(contractsDirectoryPath);
        }
        
        return contractsDirectoryPath;
    }
}