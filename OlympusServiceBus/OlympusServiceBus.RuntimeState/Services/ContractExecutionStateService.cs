using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Repositories;

namespace OlympusServiceBus.RuntimeState.Services;

public class ContractExecutionStateService : IContractExecutionStateService
{
    private readonly IContractExecutionStateRepository _repository;

    public ContractExecutionStateService(IContractExecutionStateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ContractExecutionStateEntity?> GetAsync(string contractId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByContractIdAsync(contractId, cancellationToken);
    }

    public async Task SaveAsync(ContractExecutionStateEntity entity, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByContractIdAsync(entity.ContractId, cancellationToken);

        if (existing is null)
        {
            await _repository.AddAsync(entity, cancellationToken);
            return;
        }

        existing.ContractName = entity.ContractName;
        existing.LastRunStartedAt = entity.LastRunStartedAt;
        existing.LastRunCompletedAt = entity.LastRunCompletedAt;
        existing.LastRunStatus = entity.LastRunStatus;
        existing.UpdatedAt = entity.UpdatedAt;

        await _repository.UpdateAsync(existing, cancellationToken);
    }
}