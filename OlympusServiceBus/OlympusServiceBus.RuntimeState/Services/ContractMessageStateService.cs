using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Repositories;

namespace OlympusServiceBus.RuntimeState.Services;

public class ContractMessageStateService : IContractMessageStateService
{
    private readonly IContractMessageStateRepository _repository;

    public ContractMessageStateService(IContractMessageStateRepository repository)
    {
        _repository = repository;
    }

    public Task<ContractMessageStateEntity?> GetAsync(
        string contractId,
        string businessKey,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByContractAndBusinessKeyAsync(
            contractId,
            businessKey,
            cancellationToken);
    }

    public Task<List<ContractMessageStateEntity>> GetPendingAsync(
        string contractId,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetPendingByContractAsync(
            contractId,
            cancellationToken);
    }

    public async Task SaveAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByContractAndBusinessKeyAsync(
            entity.ContractId,
            entity.BusinessKey,
            cancellationToken);

        if (existing is null)
        {
            await _repository.AddAsync(entity, cancellationToken);
            return;
        }

        existing.ContractName = entity.ContractName;
        existing.PayloadHash = entity.PayloadHash;
        existing.CanonicalSnapshot = entity.CanonicalSnapshot;

        if (entity.FirstSeenAt != default)
            existing.FirstSeenAt = existing.FirstSeenAt == default ? entity.FirstSeenAt : existing.FirstSeenAt;

        if (entity.LastSeenAt != default)
            existing.LastSeenAt = entity.LastSeenAt;

        if (entity.LastPublishedAt.HasValue)
            existing.LastPublishedAt = entity.LastPublishedAt;

        existing.PublishStatus = entity.PublishStatus;
        existing.PublishAttemptCount = entity.PublishAttemptCount;

        if (entity.LastPublishAttemptAt.HasValue)
            existing.LastPublishAttemptAt = entity.LastPublishAttemptAt;

        existing.LastPublishError = entity.LastPublishError;

        await _repository.UpdateAsync(existing, cancellationToken);
    }
}