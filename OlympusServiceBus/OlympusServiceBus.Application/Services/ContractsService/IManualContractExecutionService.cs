using OlympusServiceBusApplication.Models.Contracts;

namespace OlympusServiceBusApplication.Services.ContractsService;

public interface IManualContractExecutionService
{
    Task<ManualContractExecutionResult> ExecuteAsync(
        string contractFilePath,
        CancellationToken cancellationToken = default);
}
