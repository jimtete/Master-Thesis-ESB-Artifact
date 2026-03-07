using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState.Services;

public interface IContractExecutionStateService
{
    Task<ContractExecutionStateEntity?> GetAsync(string contractId, CancellationToken cancellationToken = default);
    Task SaveAsync(ContractExecutionStateEntity entity, CancellationToken cancellationToken = default);
}