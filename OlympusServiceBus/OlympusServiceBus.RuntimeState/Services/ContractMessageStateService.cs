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

    public async Task<ContractMessageStateEntity?> GetAsync(string contractId, string businessKey, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByContractAndBusinessKeyAsync(
            contractId,
            businessKey,
            cancellationToken);    
    }

    public async Task SaveAsync(ContractMessageStateEntity entity, CancellationToken cancellationToken = default)
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
        existing.LastSeenAt = entity.LastSeenAt;
        existing.LastPublishedAt = entity.LastPublishedAt;

        await _repository.UpdateAsync(existing, cancellationToken);    }
}