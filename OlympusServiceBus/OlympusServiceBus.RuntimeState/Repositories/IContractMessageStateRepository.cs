using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState.Repositories;

public interface IContractMessageStateRepository
{
    Task<ContractMessageStateEntity?> GetByContractAndBusinessKeyAsync(
        string contractId,
        string businessKey,
        CancellationToken cancellationToken = default);

    Task<List<ContractMessageStateEntity>> GetPendingByContractAsync(
        string contractId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default);
}