using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.ContractsService;

public interface IContractsExplorerService
{
    Task<FileExplorerNode> LoadTreeAsync(string rootPath);
}