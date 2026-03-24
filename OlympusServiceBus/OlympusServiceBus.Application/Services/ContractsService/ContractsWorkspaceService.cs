using System;
using System.IO;
using System.Threading.Tasks;
using OlympusServiceBusApplication.Services.AppSettingsService;

namespace OlympusServiceBusApplication.Services.ContractsService;

public sealed class ContractsWorkspaceService : IContractsWorkspaceService
{
    private const string AppFolderName = "OlympusServiceBus";
    private const string ContractsFolderName = "Contracts";

    private readonly IAppSettingsService _appSettingsService;

    public ContractsWorkspaceService(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    public async Task<string> EnsureContractsDirectoryAsync()
    {
        var settings = await _appSettingsService.LoadAsync();

        var defaultRootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);

        if (string.IsNullOrWhiteSpace(settings.ContractRootDirectory))
        {
            settings.ContractRootDirectory = defaultRootDirectory;
            await _appSettingsService.SaveAsync(settings);
        }

        Directory.CreateDirectory(settings.ContractRootDirectory);

        var contractsDirectory = Path.Combine(
            settings.ContractRootDirectory,
            ContractsFolderName);

        Directory.CreateDirectory(contractsDirectory);

        return contractsDirectory;
    }
}