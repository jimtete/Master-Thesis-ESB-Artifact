namespace OlympusServiceBusApplication.Services.ContractsService;

public interface IContractsWorkspaceService
{
    Task<string> EnsureContractsDirectoryAsync();
}