using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.ContractsService;

public interface IContractsExplorerService
{
    Task<FileExplorerNode> LoadTreeAsync(string rootPath);
    Task CreateDirectoryAsync(string parentPath, string directoryName);
    Task CreateContractFileAsync(string parentPath, string contractName);
}