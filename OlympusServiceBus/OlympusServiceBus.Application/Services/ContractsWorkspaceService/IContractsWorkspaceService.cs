namespace OlympusServiceBusApplication.Services.ContractsWorkspaceService;

public interface IContractsWorkspaceService
{
    Task<string> EnsureContractsDirectoryAsync();
}