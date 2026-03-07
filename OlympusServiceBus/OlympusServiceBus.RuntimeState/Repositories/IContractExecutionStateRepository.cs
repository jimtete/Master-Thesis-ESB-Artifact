using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState.Repositories;

public interface IContractExecutionStateRepository
{
    Task<ContractExecutionStateEntity?> GetByContractIdAsync(string contractId, CancellationToken cancellationToken = default);
    Task AddAsync(ContractExecutionStateEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ContractExecutionStateEntity entity, CancellationToken cancellationToken = default);
}