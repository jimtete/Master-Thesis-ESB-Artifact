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

    public Task<ContractExecutionStateEntity?> GetAsync(
        string contractId,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByContractIdAsync(contractId, cancellationToken);
    }

    public async Task SaveAsync(
        ContractExecutionStateEntity entity,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByContractIdAsync(entity.ContractId, cancellationToken);

        if (existing is null)
        {
            await _repository.AddAsync(entity, cancellationToken);
            return;
        }

        existing.ContractName = entity.ContractName;

        if (entity.LastRunStartedAt.HasValue)
            existing.LastRunStartedAt = entity.LastRunStartedAt;

        if (entity.LastRunCompletedAt.HasValue)
            existing.LastRunCompletedAt = entity.LastRunCompletedAt;

        if (!string.IsNullOrWhiteSpace(entity.LastRunStatus))
            existing.LastRunStatus = entity.LastRunStatus;

        existing.UpdatedAt = entity.UpdatedAt;

        await _repository.UpdateAsync(existing, cancellationToken);
    }
}