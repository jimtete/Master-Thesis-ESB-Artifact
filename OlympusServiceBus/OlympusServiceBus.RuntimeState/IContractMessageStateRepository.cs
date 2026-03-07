using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState;

public interface IContractMessageStateRepository
{
    Task<ContractMessageStateEntity?> GetByContractAndBusinessKeyASync(
        string contractId,
        string businessKey,
        CancellationToken cancellationToken = default);
    
    Task AddAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default
    );
}