using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState.Services;

public interface IContractMessageStateService
{
    Task<ContractMessageStateEntity?> GetAsync(
        string contractId,
        string businessKey,
        CancellationToken cancellationToken = default);

    Task<List<ContractMessageStateEntity>> GetPendingAsync(
        string contractId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default);
}